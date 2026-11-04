using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The shared battle (the original's SB): what goes over the link while a linked game is on.
    /// Every phone sends its own pilot fifteen times a second; the host sends where every enemy and AI teammate is ten
    /// times a second, and tells everyone when one dies and who gets the kill. Shots at a puppet are sent to whoever owns
    /// the real thing. Explosions and tracers are sent so everyone sees them.</summary>
    public class NetSync : MonoBehaviour
    {
        public static NetSync Instance { get; private set; }
        /// <summary>True while a message from the link is being applied: damage then lands instead of being forwarded.</summary>
        public static bool Applying;
        public int Remaining;   // on a client: the host's enemy count for the HUD

        // opcodes (>= 32): 32 player state · 33 enemy snapshot · 34 hit on a puppet · 35 damage to a player · 36 explosion ·
        // 37 tracer · 38 enemy died · 39 player died/respawned · 40 wave banner · 41 mode line
        const byte P = 32, W = 33, H = 34, D = 35, X = 36, F = 37, K = 38, R = 39;

        static readonly string[] Kinds = { "inf", "egun", "medic", "vest", "ejet", "grey", "ape", "gob", "raptor", "trex", "stalk", "eninja", "eshade", "esling", "clown", "colossus", "beast", "kaiju", "jeep", "tank", "aa", "sam", "bunker", "eplane", "eheli", "ufo", "dragon", "dummy" };
        static int KindCode(string k) { int i = System.Array.IndexOf(Kinds, k); return i < 0 ? 0 : i; }

        NetSession net; MaterialLibrary lib; float sendT, snapT;
        readonly Dictionary<ulong, NetGhost> players = new Dictionary<ulong, NetGhost>();   // other phones' pilots
        readonly Dictionary<int, NetGhost> enemies = new Dictionary<int, NetGhost>();       // (client) the host's enemies
        readonly Dictionary<int, Health> tracked = new Dictionary<int, Health>();           // (host) real things by id
        int nextId = 1;

        public static NetSync Create(Transform parent, MaterialLibrary lib)
        {
            var go = new GameObject("NetSync"); go.transform.SetParent(parent, false);
            var s = go.AddComponent<NetSync>(); Instance = s; s.lib = lib; s.net = NetSession.Instance;
            s.net.OnGameMessage = s.OnMessage;
            return s;
        }
        void OnDestroy() { if (Instance == this) Instance = null; if (net != null) net.OnGameMessage = null; }
        public bool IsHost { get { return net != null && net.IsHost; } }

        // ------------------------------------------------------------------ each frame
        void Update()
        {
            if (net == null || !net.Active) return;
            float dt = Time.deltaTime;
            sendT -= dt; if (sendT <= 0f) { sendT = 1f / 15f; SendMe(); }
            if (IsHost) { snapT -= dt; if (snapT <= 0f) { snapT = 0.1f; SendSnapshot(); } }
            foreach (var g in players.Values) g.Tick(dt);
            foreach (var g in enemies.Values) g.Tick(dt);
            net.Stat(GameState.Kills, GameState.Deaths);
        }

        // ------------------------------------------------------------------ my pilot → everyone
        void SendMe()
        {
            var pm = PlayerManager.Instance; if (pm == null) return;
            Transform t; string craft; Health h; byte flags = 0; float speed = 0f, pitch = 0f;
            if (pm.OnFoot && pm.foot != null) { t = pm.foot.transform; craft = Progress.IsFoot(Progress.Craft) ? Progress.Craft : (Progress.Crew ?? "rifleman"); h = pm.foot.health; flags |= 1; if (pm.foot.Crouching) flags |= 2; speed = pm.foot.MoveSpeed; pitch = pm.rig != null ? pm.rig.pitch : 0f; }
            else if (pm.drive != null && pm.drive.Active) { t = pm.drive.transform; craft = pm.drive.ProgressId; h = pm.drive.health; }
            else if (pm.plane != null && pm.plane.Active) { t = pm.plane.transform; craft = Progress.Craft; h = pm.plane.health; speed = pm.plane.speedU; }
            else return;
            if (h != null && h.alive) flags |= 4;
            if (InputBridge.Instance != null && InputBridge.Instance.Fire) flags |= 8;
            var w = new FastBufferWriter(128, Allocator.Temp);
            w.WriteValueSafe(P); w.WriteValueSafe(net.MyId); w.WriteValueSafe(craft ?? "plane"); w.WriteValueSafe(flags);
            Quaternion q = flags != 0 && (flags & 1) != 0 && pm.rig != null ? pm.rig.FlatRotation : t.rotation;
            w.WriteValueSafe(t.position); w.WriteValueSafe(q); w.WriteValueSafe(speed); w.WriteValueSafe(pitch); w.WriteValueSafe(h != null ? h.current / Mathf.Max(1f, h.max) : 1f);
            if (IsHost) net.SendToOthers(net.MyId, w, false); else net.SendToHost(w, false);
        }

        // ------------------------------------------------------------------ host: enemies and AI mates → everyone
        void SendSnapshot()
        {
            var w = new FastBufferWriter(16000, Allocator.Temp, 65000);
            w.WriteValueSafe(W); w.WriteValueSafe(GameState.Wave); w.WriteValueSafe(GameState.Spawner != null ? GameState.Spawner.Remaining : 0);
            int n = 0; var countPos = w.Position; w.WriteValueSafe(0);
            foreach (var h in Health.All)
            {
                if (h == null || !h.alive || !h.gameObject.activeInHierarchy || n >= 180) continue;
                if (h.GetComponent<NetGhost>() != null) continue;   // puppets are not re-sent
                var pm = PlayerManager.Instance;
                if (pm != null && ((pm.foot != null && h == pm.foot.health) || (pm.plane != null && h == pm.plane.health) || (pm.drive != null && h == pm.drive.health))) continue;
                if (pm != null && (h.GetComponent<PlaneController>() != null || h.GetComponent<GroundVehicle>() != null || h.GetComponent<FootController>() != null)) continue;   // the host's own craft
                string kind; float yaw; Transform t = h.transform; byte flags = 0;
                var es = h.GetComponent<EnemySoldier>(); var ev = h.GetComponent<EnemyVehicle>(); var ep = h.GetComponent<EnemyPlane>(); var dr = h.GetComponent<Dragon>();
                if (es != null) { kind = es.grey ? "grey" : es.kind; yaw = t.eulerAngles.y; if (es.model != null && es.model.IsDead) continue; }
                else if (ev != null) { kind = ev.kind == EnemyVehicle.Kind.Jeep ? "jeep" : ev.kind == EnemyVehicle.Kind.Tank ? "tank" : ev.kind == EnemyVehicle.Kind.AA ? "aa" : ev.kind == EnemyVehicle.Kind.Sam ? "sam" : "bunker"; yaw = t.eulerAngles.y; }
                else if (ep != null) { kind = ep.saucer ? "ufo" : ep.heli ? "eheli" : "eplane"; yaw = t.eulerAngles.y; flags |= 16; }
                else if (dr != null) { kind = "dragon"; yaw = t.eulerAngles.y; flags |= 16; }
                else continue;
                if (h.netId == 0) { h.netId = nextId++; tracked[h.netId] = h; int id = h.netId; h.OnDied += info => OnTrackedDied(id, info); }
                w.WriteValueSafe(h.netId); w.WriteValueSafe((byte)KindCode(kind)); w.WriteValueSafe((byte)h.team); w.WriteValueSafe(flags);
                w.WriteValueSafe(t.position); w.WriteValueSafe(flags != 0 ? t.rotation : Quaternion.Euler(0f, yaw, 0f)); w.WriteValueSafe((ushort)Mathf.Clamp(h.current / Mathf.Max(1f, h.max) * 1000f, 0f, 1000f));
                n++;
            }
            int end = w.Position; w.Seek(countPos); w.WriteValueSafe(n); w.Seek(end);
            net.SendToOthers(net.MyId, w, true);
        }

        void OnTrackedDied(int id, DamageInfo info)
        {
            Health h; if (!tracked.TryGetValue(id, out h)) return;
            // who gets the kill: the puppet that fired, or the host
            ulong killer = net.MyId; var g = info.source != null ? info.source.GetComponent<NetGhost>() : null; if (g != null && g.isPlayer) killer = g.owner;
            int score = 10; var es = h.GetComponent<EnemySoldier>(); if (es != null) score = es.score; else if (h.GetComponent<EnemyVehicle>() != null) score = 80; else if (h.GetComponent<EnemyPlane>() != null) score = 400; else if (h.GetComponent<Dragon>() != null) score = 6000;
            if (h.team != 0 && killer != net.MyId) { GameState.Kills--; GameState.Credits -= score; Progress.Bank -= score; if (info.headshot) GameState.Headshots--; }   // the host's own tally already counted it: give it back
            var w = new FastBufferWriter(64, Allocator.Temp);
            w.WriteValueSafe(K); w.WriteValueSafe(id); w.WriteValueSafe(killer); w.WriteValueSafe(info.direction); w.WriteValueSafe(info.headshot); w.WriteValueSafe(info.explosive); w.WriteValueSafe(score); w.WriteValueSafe(info.amount);
            net.SendToOthers(net.MyId, w, true);
            tracked.Remove(id);
        }

        // ------------------------------------------------------------------ damage through the link
        /// <summary>Health asks here before taking damage: a puppet's damage goes to whoever owns the real one. Returns true when forwarded.</summary>
        public bool Intercept(Health h, DamageInfo info)
        {
            if (Applying || net == null || !net.Active) return false;
            var g = h.GetComponent<NetGhost>(); if (g == null) return false;
            var w = new FastBufferWriter(64, Allocator.Temp);
            if (g.isPlayer)
            {
                w.WriteValueSafe(D); w.WriteValueSafe(g.owner); w.WriteValueSafe(net.MyId); w.WriteValueSafe(info.amount); w.WriteValueSafe(info.point); w.WriteValueSafe(info.direction); w.WriteValueSafe(info.headshot); w.WriteValueSafe(info.explosive);
                if (IsHost) net.SendTo(g.owner, w, true); else net.SendToHost(w, true);
            }
            else
            {
                w.WriteValueSafe(H); w.WriteValueSafe(g.netId); w.WriteValueSafe(net.MyId); w.WriteValueSafe(info.amount); w.WriteValueSafe(info.point); w.WriteValueSafe(info.direction); w.WriteValueSafe(info.headshot); w.WriteValueSafe(info.foot); w.WriteValueSafe(info.explosive);
                net.SendToHost(w, true);
            }
            return true;
        }

        /// <summary>Projectiles tells everyone about a blast so they see the flash and the crater (no damage on the far side).</summary>
        public void Explosion(Vector3 at, float coreR, float dmgR, float dmg)
        {
            if (Applying || net == null || !net.Active) return;
            var w = new FastBufferWriter(32, Allocator.Temp); w.WriteValueSafe(X); w.WriteValueSafe(at); w.WriteValueSafe(coreR); w.WriteValueSafe(dmgR); w.WriteValueSafe(dmg);
            if (IsHost) net.SendToOthers(net.MyId, w, false); else net.SendToHost(w, false);
        }
        float tracerT;
        public void Tracer(Vector3 from, Vector3 dir, float speed, Color c)
        {
            if (Applying || net == null || !net.Active) return;
            if (Time.time - tracerT < 0.08f) return; tracerT = Time.time;
            var w = new FastBufferWriter(48, Allocator.Temp); w.WriteValueSafe(F); w.WriteValueSafe(from); w.WriteValueSafe(dir); w.WriteValueSafe(speed); w.WriteValueSafe((Color32)c);
            if (IsHost) net.SendToOthers(net.MyId, w, false); else net.SendToHost(w, false);
        }

        // ------------------------------------------------------------------ incoming
        void OnMessage(ulong from, byte op, FastBufferReader r)
        {
            switch (op)
            {
                case P:
                    {
                        ulong id; string craft; byte flags; Vector3 pos; Quaternion rot; float speed, pitch, hp;
                        r.ReadValueSafe(out id); r.ReadValueSafe(out craft); r.ReadValueSafe(out flags); r.ReadValueSafe(out pos); r.ReadValueSafe(out rot); r.ReadValueSafe(out speed); r.ReadValueSafe(out pitch); r.ReadValueSafe(out hp);
                        if (id == net.MyId) return;
                        NetGhost g; if (!players.TryGetValue(id, out g)) { g = NetGhost.Player(transform, lib, id, craft); players[id] = g; }
                        g.Apply(craft, flags, pos, rot, speed, pitch, hp);
                        if (IsHost) Relay(from, (w) => { w.WriteValueSafe(P); w.WriteValueSafe(id); w.WriteValueSafe(craft); w.WriteValueSafe(flags); w.WriteValueSafe(pos); w.WriteValueSafe(rot); w.WriteValueSafe(speed); w.WriteValueSafe(pitch); w.WriteValueSafe(hp); }, false);
                        break;
                    }
                case W:
                    {
                        if (IsHost) return;
                        int wave, rem, n; r.ReadValueSafe(out wave); r.ReadValueSafe(out rem); r.ReadValueSafe(out n);
                        if (wave != GameState.Wave) { GameState.Wave = wave; if (GameState.Hud != null && wave > 0) { GameState.Hud.Announce("Wave " + wave); GameState.Hud.Sting(); } }
                        Remaining = rem;
                        var seen = new HashSet<int>();
                        for (int i = 0; i < n; i++)
                        {
                            int id; byte kc, team, flags; Vector3 pos; Quaternion rot; ushort hp;
                            r.ReadValueSafe(out id); r.ReadValueSafe(out kc); r.ReadValueSafe(out team); r.ReadValueSafe(out flags); r.ReadValueSafe(out pos); r.ReadValueSafe(out rot); r.ReadValueSafe(out hp);
                            seen.Add(id);
                            NetGhost g; if (!enemies.TryGetValue(id, out g)) { g = NetGhost.Enemy(transform, lib, id, Kinds[Mathf.Min(kc, Kinds.Length - 1)], team, pos); if (g == null) continue; enemies[id] = g; }
                            g.ApplyEnemy(pos, rot, hp / 1000f, (flags & 16) != 0);
                        }
                        // gone from the list without a death message: it was removed quietly
                        var drop = new List<int>(); foreach (var kv in enemies) if (!seen.Contains(kv.Key) && kv.Value.alive && kv.Value.age > 1f) drop.Add(kv.Key);
                        foreach (var id in drop) { enemies[id].Vanish(); enemies.Remove(id); }
                        GameState.Touch();
                        break;
                    }
                case H:
                    {
                        if (!IsHost) return;
                        int id; ulong who; float amount; Vector3 point, dir; bool head, foot, expl;
                        r.ReadValueSafe(out id); r.ReadValueSafe(out who); r.ReadValueSafe(out amount); r.ReadValueSafe(out point); r.ReadValueSafe(out dir); r.ReadValueSafe(out head); r.ReadValueSafe(out foot); r.ReadValueSafe(out expl);
                        Health h; if (!tracked.TryGetValue(id, out h) || h == null) return;
                        NetGhost src; players.TryGetValue(who, out src);
                        Applying = true; h.TakeDamage(new DamageInfo { amount = amount, point = point, direction = dir, headshot = head, foot = foot, explosive = expl, source = src != null ? src.gameObject : gameObject }); Applying = false;
                        break;
                    }
                case D:
                    {
                        ulong target, who; float amount; Vector3 point, dir; bool head, expl;
                        r.ReadValueSafe(out target); r.ReadValueSafe(out who); r.ReadValueSafe(out amount); r.ReadValueSafe(out point); r.ReadValueSafe(out dir); r.ReadValueSafe(out head); r.ReadValueSafe(out expl);
                        if (target != net.MyId) { if (IsHost) { var w = new FastBufferWriter(64, Allocator.Temp); w.WriteValueSafe(D); w.WriteValueSafe(target); w.WriteValueSafe(who); w.WriteValueSafe(amount); w.WriteValueSafe(point); w.WriteValueSafe(dir); w.WriteValueSafe(head); w.WriteValueSafe(expl); net.SendTo(target, w, true); } return; }
                        var pm = PlayerManager.Instance; if (pm == null) return;
                        Health mine = pm.OnFoot && pm.foot != null ? pm.foot.health : pm.drive != null && pm.drive.Active ? pm.drive.health : pm.plane != null ? pm.plane.health : null;
                        if (mine == null) return;
                        Applying = true; mine.TakeDamage(new DamageInfo { amount = amount, point = point, direction = dir, headshot = head, explosive = expl, source = gameObject }); Applying = false;
                        if (GameState.Hud != null) GameState.Hud.Damaged(amount);
                        break;
                    }
                case X:
                    {
                        Vector3 at; float coreR, dmgR, dmg; r.ReadValueSafe(out at); r.ReadValueSafe(out coreR); r.ReadValueSafe(out dmgR); r.ReadValueSafe(out dmg);
                        if (Projectiles.Instance != null) { Applying = true; Projectiles.Instance.Explode(at, coreR, dmgR, dmg, gameObject, 2, true); Applying = false; }
                        if (IsHost) Relay(from, w => { w.WriteValueSafe(X); w.WriteValueSafe(at); w.WriteValueSafe(coreR); w.WriteValueSafe(dmgR); w.WriteValueSafe(dmg); }, false);
                        break;
                    }
                case F:
                    {
                        Vector3 fromP, dir; float speed; Color32 c; r.ReadValueSafe(out fromP); r.ReadValueSafe(out dir); r.ReadValueSafe(out speed); r.ReadValueSafe(out c);
                        if (Projectiles.Instance != null) { Applying = true; Projectiles.Instance.FireRound(fromP, dir, speed, 0f, 300f, 2, gameObject, c); Applying = false; }
                        if (IsHost) Relay(from, w => { w.WriteValueSafe(F); w.WriteValueSafe(fromP); w.WriteValueSafe(dir); w.WriteValueSafe(speed); w.WriteValueSafe(c); }, false);
                        break;
                    }
                case K:
                    {
                        if (IsHost) return;
                        int id; ulong killer; Vector3 dir; bool head, expl; int score; float amount;
                        r.ReadValueSafe(out id); r.ReadValueSafe(out killer); r.ReadValueSafe(out dir); r.ReadValueSafe(out head); r.ReadValueSafe(out expl); r.ReadValueSafe(out score); r.ReadValueSafe(out amount);
                        NetGhost g; if (enemies.TryGetValue(id, out g)) { g.Die(dir, head, expl, amount, killer == net.MyId); enemies.Remove(id); }
                        else if (killer == net.MyId) GameState.EnemyKilled(head, score);
                        break;
                    }
            }
        }

        void Relay(ulong from, System.Action<FastBufferWriter> write, bool reliable)
        {
            var w = new FastBufferWriter(256, Allocator.Temp); write(w); net.SendToOthers(from, w, reliable);
        }

        public void PeerLeft(ulong id) { NetGhost g; if (players.TryGetValue(id, out g)) { Destroy(g.gameObject); players.Remove(id); } if (GameState.Hud != null) GameState.Hud.Comms(net.NameOf(id) + " left"); }
        public void HostGone() { if (GameState.Hud != null) GameState.Hud.Announce("The host left the game", 4f); foreach (var g in enemies.Values) if (g != null) g.Vanish(); enemies.Clear(); }
    }

    /// <summary>A puppet: another phone's pilot, or (on a client) one of the host's enemies. Built from the real classes with
    /// their brains switched off, moved by the link with a little smoothing.</summary>
    public class NetGhost : MonoBehaviour
    {
        public bool isPlayer; public ulong owner; public int netId; public string kind; public int team; public bool alive = true; public float age;
        Health health; SoldierModelBase model; Transform body; Vector3 targetPos, vel; Quaternion targetRot = Quaternion.identity; float speed, pitch; bool crouch, air, flying; string craft; MaterialLibrary lib; Text3 label;

        public static NetGhost Player(Transform parent, MaterialLibrary lib, ulong id, string craft)
        {
            var go = new GameObject("Buddy " + id); go.transform.SetParent(parent, false); go.layer = 8;
            var g = go.AddComponent<NetGhost>(); g.isPlayer = true; g.owner = id; g.lib = lib; g.team = 0;
            g.health = go.AddComponent<Health>(); g.health.team = 0; g.health.max = 100f; g.health.current = 100f; g.health.aimHeight = 1.2f;
            g.Rebuild(craft);
            return g;
        }

        void Rebuild(string c)
        {
            craft = c; if (body != null) Destroy(body.gameObject); model = null;
            body = new GameObject("Body").transform; body.SetParent(transform, false);
            foreach (var col in GetComponents<Collider>()) Destroy(col);
            var d = Progress.Def(c);
            if (d == null || d.foot) { model = SoldierModelBase.Create(body, lib, 0, 8); var cc = gameObject.AddComponent<CapsuleCollider>(); cc.center = new Vector3(0f, 0.95f, 0f); cc.height = 1.8f; cc.radius = 0.32f; health.isAir = false; }
            else if (!d.ground)
            {
                Showroom.BuildCraftModel(body, c, lib, 8);
                var sc = gameObject.AddComponent<SphereCollider>(); sc.radius = c == "b52" || c == "ac130" ? 4f : 1.6f; health.isAir = true;
            }
            else { Showroom.BuildCraftModel(body, c, lib, 8); var bc = gameObject.AddComponent<BoxCollider>(); bc.center = new Vector3(0f, 0.8f, 0f); bc.size = new Vector3(2.2f, 1.6f, 4f); health.isAir = false; }
            foreach (var r in body.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(0.6f, 0.75f, 1f, 1f));   // buddies are blue, as the original coloured them
            if (label == null) { try { label = Text3.Create(transform, NetSession.Instance != null ? NetSession.Instance.NameOf(owner) : "Buddy", 2.3f); } catch (System.Exception) { label = null; } }   // headless builds have no font material
        }

        public void Apply(string c, byte flags, Vector3 pos, Quaternion rot, float spd, float pch, float hp)
        {
            if (c != craft) Rebuild(c);
            bool wasAlive = alive; alive = (flags & 4) != 0;
            if (age == 0f) { transform.position = pos; transform.rotation = rot; }
            targetPos = pos; targetRot = rot; speed = spd; pitch = pch; crouch = (flags & 2) != 0; flying = (flags & 1) == 0;
            health.current = hp * health.max; health.alive = alive;
            foreach (var col in GetComponents<Collider>()) col.enabled = alive;
            if (model != null) model.SetVisible(alive);
            if (body != null && model == null) foreach (var r in body.GetComponentsInChildren<Renderer>()) r.enabled = alive;
            if (label != null) label.SetText(NetSession.Instance != null ? NetSession.Instance.NameOf(owner) : "Buddy");
        }

        public static NetGhost Enemy(Transform parent, MaterialLibrary lib, int id, string kind, int team, Vector3 at)
        {
            GameObject go; Health hp; SoldierModelBase model = null; bool air = false;
            var spawner = GameState.Spawner;
            if (kind == "jeep" || kind == "tank" || kind == "aa" || kind == "sam" || kind == "bunker")
            {
                var k = kind == "jeep" ? EnemyVehicle.Kind.Jeep : kind == "tank" ? EnemyVehicle.Kind.Tank : kind == "aa" ? EnemyVehicle.Kind.AA : kind == "sam" ? EnemyVehicle.Kind.Sam : EnemyVehicle.Kind.Bunker;
                var v = EnemyVehicle.Build(parent, lib, k, at, team); v.enabled = false; go = v.gameObject; hp = v.health;
            }
            else if (kind == "eplane" || kind == "eheli" || kind == "ufo")
            {
                var p = EnemyPlane.Build(parent, lib, at, 0f, team, kind == "eheli", kind == "ufo"); p.enabled = false; go = p.gameObject; hp = p.health; air = true;
            }
            else if (kind == "dragon") { var d = Dragon.Build(parent, lib, at); d.enabled = false; go = d.gameObject; hp = d.health; air = true; }
            else
            {
                if (spawner == null) return null;
                var e = spawner.Spawn(at, false, team);
                bool creature = kind == "gob" || kind == "ape" || kind == "raptor" || kind == "stalk" || kind == "trex" || kind == "eninja" || kind == "eshade" || kind == "esling" || kind == "clown" || kind == "colossus" || kind == "beast" || kind == "kaiju";
                if (creature) e.MakeCreature(kind, (kind == "eninja" || kind == "eshade" || kind == "esling" || kind == "clown") ? null : lib);
                else if (kind == "grey") e.MakeGrey(lib);
                else { e.kind = kind; if (kind == "ejet") e.health.isAir = true; }
                e.enabled = false; go = e.gameObject; hp = e.health; model = e.model;
            }
            var g = go.AddComponent<NetGhost>(); g.isPlayer = false; g.netId = id; g.kind = kind; g.team = team; g.lib = lib; g.health = hp; g.model = model; g.air = air;
            g.transform.position = at; g.targetPos = at; g.targetRot = go.transform.rotation;
            return g;
        }

        public void ApplyEnemy(Vector3 pos, Quaternion rot, float hp, bool fly)
        {
            if (age == 0f) transform.position = pos;
            speed = Vector3.Distance(targetPos, pos) * 10f; targetPos = pos; targetRot = rot; flying = fly;
            if (health != null) health.current = hp * health.max;
        }

        public void Tick(float dt)
        {
            age += dt;
            if (!alive) return;
            float k = Mathf.Min(1f, dt * 12f);
            transform.position = Vector3.Lerp(transform.position, targetPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, k);
            if (model != null && !model.IsDead) model.Animate(speed, pitch, crouch ? 1f : 0f, flying && !isPlayer ? false : (isPlayer && flying), dt);
            if (label != null) label.Face();
        }

        /// <summary>Host says it died: let its own death play (the kill goes to whoever the host said).</summary>
        public void Die(Vector3 dir, bool head, bool expl, float amount, bool mine)
        {
            alive = false;
            if (health == null) { Destroy(gameObject); return; }
            NetSync.Applying = true; GameState.SuppressScore = !mine;
            health.TakeDamage(new DamageInfo { amount = Mathf.Max(amount, health.current + 1f), point = transform.position + Vector3.up, direction = dir, headshot = head, explosive = expl, foot = !expl, source = mine && GameState.Player != null ? GameState.Player.gameObject : null });
            GameState.SuppressScore = false; NetSync.Applying = false;
            Destroy(gameObject, 20f);
        }
        public void Vanish() { alive = false; Destroy(gameObject); }
    }

    /// <summary>A name floating over a buddy.</summary>
    public class Text3 : MonoBehaviour
    {
        TextMesh tm;
        public static Text3 Create(Transform parent, string text, float height)
        {
            var go = new GameObject("Name"); go.transform.SetParent(parent, false); go.transform.localPosition = new Vector3(0f, height, 0f);
            var t = go.AddComponent<Text3>(); t.tm = go.AddComponent<TextMesh>(); t.tm.text = text; t.tm.fontSize = 48; t.tm.characterSize = 0.06f; t.tm.anchor = TextAnchor.LowerCenter; t.tm.alignment = TextAlignment.Center; t.tm.color = new Color(0.75f, 0.85f, 1f);
            t.tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); t.GetComponent<MeshRenderer>().material = t.tm.font.material;
            return t;
        }
        public void SetText(string s) { if (tm.text != s) tm.text = s; }
        public void Face() { var cam = Camera.main; if (cam != null) transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position); }
    }
}
