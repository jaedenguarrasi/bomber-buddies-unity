using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Galactic Assault: two carriers high above the island, yours and theirs, hangars facing each other. Fly out of your hangar,
    /// knock out the shield generator on the ground, fly into their hangar, get out, and fight through to a core. Their cannons
    /// shell your carrier the whole time; lose it and you lose. All the numbers come from the original game.
    /// </summary>
    public class GalacticMode : MonoBehaviour
    {
        /// <summary>Metres per original unit: a 15-unit soldier is 1.8 m tall.</summary>
        public const float U = 0.12f;
        public const float BombG = 230f * U, BombVMax = 250f * U, CloudY = 1900f * U;
        /// <summary>Hit points and damage are the original's × this (the Unity rifle does 26 where the original did 11).</summary>
        public const float K = 2.3f;
        public static GalacticMode Instance { get; private set; }

        public MaterialLibrary lib;
        public World3 world;
        public FootController foot;
        public CameraRig rig;
        public Camera cam;
        public EnemySpawner spawner;
        public PlayerManager pm;
        public PlaneController plane { get { return pm != null ? pm.plane : null; } }
        float parkT = 25f;
        public Carrier friendly, enemy;
        public ShipPart generator;
        public readonly List<PlaneController> allyPlanes = new List<PlaneController>();
        public readonly List<EnemyPlane> fighters = new List<EnemyPlane>();
        public readonly List<EnemyPlane> wingmen = new List<EnemyPlane>();
        public readonly List<CommandPost> posts = new List<CommandPost>();
        public float shake;
        public bool over;
        Transform beam;
        float fighterT = 6f, modeLineT, endT = -1f, elapsed, objT;
        float dropT = 25f, allyDropT = 4f, strikeT = 16f, bombT = 14f, allyT = 3f, pdT = 1f, fbT = 2f;
        bool onFoot { get { return pm != null && pm.OnFoot; } }
        bool respawning { get { return pm != null && pm.Respawning; } }
        readonly List<Vector3> feet = new List<Vector3>();
        class Strike { public Vector3 at; public float t; public Transform marker; }
        readonly List<Strike> strikes = new List<Strike>();
        public int PostsHeld { get { int n = 0; foreach (var p in posts) if (p.own == 1) n++; return n; } }

        public static GalacticMode Create(Transform parent, MaterialLibrary lib, World3 world, FootController foot, CameraRig rig, Camera cam, EnemySpawner spawner)
        {
            var go = new GameObject("Galactic Assault");
            go.transform.SetParent(parent, false);
            var g = go.AddComponent<GalacticMode>();
            g.lib = lib; g.world = world; g.foot = foot; g.rig = rig; g.cam = cam; g.spawner = spawner;
            Instance = g;
            g.Setup();
            return g;
        }

        // ---- the world ----
        public float TerrainHeight(float x, float z) { return world != null ? world.GroundMetres(x, z) : 0f; }
        public const float SeaY = World3.SEA * World3.U;

        /// <summary>The highest thing under a point: a carrier's top or the ground.</summary>
        public float ObstacleHeight(float x, float z)
        {
            float h = TerrainHeight(x, z);
            foreach (var s in new[] { friendly, enemy })
            {
                if (s == null || !s.Solid) continue;
                var l = s.W2L(new Vector3(x, 0f, z));
                if (Mathf.Abs(l.x) < 430f && Mathf.Abs(l.z) < 95f) h = Mathf.Max(h, s.transform.position.y + 130f * U);
            }
            return h;
        }

        public Carrier HangarAt(Vector3 w)
        {
            if (friendly != null && friendly.HangarAt(w)) return friendly;
            if (enemy != null && enemy.HangarAt(w) && !enemy.shield) return enemy;
            return null;
        }

        /// <summary>Where someone on foot can stand: a ship deck when one is under them, else the island.</summary>
        public float GroundHeight(float x, float z)
        {
            var p = foot != null ? foot.transform.position : new Vector3(x, 0f, z);
            p.x = x; p.z = z;
            foreach (var s in new[] { friendly, enemy })
            {
                if (s == null) continue;
                float f = s.FloorAt(p);
                if (!float.IsNaN(f) && p.y > f - 3f) return f;
            }
            return TerrainHeight(x, z);
        }

        public void Shake(float k) { shake = Mathf.Max(shake, k); }

        void Setup()
        {
            // the carriers: 2100 units apart, the enemy 100 higher, hangars facing each other
            friendly = Carrier.Build(transform, lib, 0, new Vector3(0f, 3600f * U, -1050f * U));
            enemy = Carrier.Build(transform, lib, 1, new Vector3(0f, 3700f * U, 1050f * U));
            friendly.OnDestroyed += s => End(false);
            enemy.OnDestroyed += s => End(true);
            BuildCloudDeck();
            ArmEnemyShip();
            BuildGenerator();
            BuildGround();
            pm = PlayerManager.Instance;
            pm.placeOnRespawn = () => { if (friendly != null && friendly.alive && friendly.dying < 0f) { PlaceInHangar(); return true; } return false; };
            pm.hangarAt = HangarAt;
            pm.cockpitFloor = HangarFloorOrNaN;
            // three parked craft in your hangar, like the original: plane, helicopter, plane
            NewCraft(PlaneController.Craft.Plane, friendly.L2W(-10f, friendly.FloorY + 1f, 12f));
            NewCraft(PlaneController.Craft.Heli, friendly.L2W(45f, friendly.FloorY + 0f, 12f));
            NewCraft(PlaneController.Craft.Plane, friendly.L2W(95f, friendly.FloorY + 1f, 12f));
            var own = NewCraft(PlaneController.Craft.Plane, friendly.L2W(20f, friendly.FloorY + 4f, -20f));
            if (foot != null) foot.groundHeight = GroundHeight;
            var md = friendly.MouthDirection;
            pm.Fly(own, own.transform.position, Mathf.Atan2(md.x, md.z) * Mathf.Rad2Deg, friendly, 0f);
            if (GameState.Hud != null)
            {
                GameState.Hud.Announce("Your carrier is here to back the ground war: destroy their shield generator, then board their ship", 5f);
                GameState.Hud.SetFlight(plane);
            }
        }

        void BuildCloudDeck()
        {
            var m = lib != null ? lib.Instance("water") : null;
            if (m != null) { m.SetColor("_BaseColor", new Color(0.92f, 0.94f, 0.97f, 0.88f)); m.SetFloat("_Smoothness", 0.2f); }
            for (int i = 0; i < 2; i++)
            {
                var deck = GameObject.CreatePrimitive(PrimitiveType.Plane);
                deck.name = "Cloud deck";
                deck.transform.SetParent(transform, false);
                deck.transform.position = new Vector3(0f, CloudY + (i == 0 ? 0f : -0.5f), 0f);
                deck.transform.localScale = new Vector3(300f, 1f, 300f);
                if (i == 1) deck.transform.rotation = Quaternion.Euler(180f, 0f, 0f);
                Destroy(deck.GetComponent<Collider>());
                var r = deck.GetComponent<Renderer>(); if (m != null) r.sharedMaterial = m;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            }
        }

        void ArmEnemyShip()
        {
            var E = enemy;
            // heavy cannons on the upper deck, quick guns on the hull top and under the belly
            ShipGun.Build(E, lib, true, false, -220, 55, -38); ShipGun.Build(E, lib, true, false, -220, 55, 38);
            ShipGun.Build(E, lib, true, false, 150, 55, -38); ShipGun.Build(E, lib, true, false, 150, 55, 38);
            ShipGun.Build(E, lib, false, false, -300, 25, -72); ShipGun.Build(E, lib, false, false, -300, 25, 72);
            ShipGun.Build(E, lib, false, false, 300, 25, -72); ShipGun.Build(E, lib, false, false, 300, 25, 72);
            ShipGun.Build(E, lib, false, true, -100, -45, 0); ShipGun.Build(E, lib, false, true, 220, -45, 0);
            // the two cores
            BuildCore(E, -298, "engine", new Color(1f, 0.55f, 0.2f));
            BuildCore(E, 292, "computer", new Color(0.35f, 1f, 0.85f));
            // fifteen guards at their posts
            float[,] g = { { 10, -10 }, { 60, 25 }, { 100, -30 }, { 30, 30 }, { -100, -6 }, { -180, 6 }, { -240, 0 }, { 170, -6 }, { 230, 6 }, { -300, -40 }, { -300, 40 }, { -320, 0 }, { 290, -38 }, { 290, 38 }, { 320, 0 } };
            if (spawner != null)
            {
                for (int i = 0; i < g.GetLength(0); i++)
                {
                    var at = E.L2W(g[i, 0], -30f, g[i, 1]);
                    var ai = spawner.Spawn(at, false);
                    ai.guard = true;
                    ai.health.max = 160f; ai.health.current = 160f; ai.health.stationary = true;
                    ai.transform.SetParent(E.transform, true);
                    var face = g[i, 0] < -40f ? E.LocalDir(1, 0, 0) : g[i, 0] > 120f ? E.LocalDir(-1, 0, 0) : E.MouthDirection;
                    ai.SetFacing(Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg);
                    E.guards.Add(ai);
                }
            }
        }

        void BuildCore(Carrier E, float lx, string name, Color glow)
        {
            var go = new GameObject("Core " + name);
            var m = lib != null ? lib.Instance("glow") : null; if (m != null) { m.SetColor("_BaseColor", glow * 0.8f); m.SetColor("_EmissionColor", glow * 4f); }
            var metal = lib != null ? lib.Instance("gunmetal") : null; if (metal != null) metal.SetColor("_BaseColor", new Color(0.3f, 0.32f, 0.36f));
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material mat)
            {
                var p = GameObject.CreatePrimitive(t); p.name = n; p.transform.SetParent(go.transform, false); p.transform.localPosition = pos * U; p.transform.localScale = scale * U;
                if (mat != null) p.GetComponent<Renderer>().sharedMaterial = mat; return p;
            }
            P(PrimitiveType.Cylinder, "Base", new Vector3(0f, 1.5f, 0f), new Vector3(26f, 1.5f, 26f), metal);
            P(PrimitiveType.Cylinder, "Column", new Vector3(0f, 14f, 0f), new Vector3(16f, 12f, 16f), m);
            P(PrimitiveType.Cylinder, "Cap", new Vector3(0f, 26.5f, 0f), new Vector3(22f, 1.5f, 22f), metal);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI * 0.5f; P(PrimitiveType.Cube, "Pylon", new Vector3(Mathf.Cos(a) * 12f, 14f, Mathf.Sin(a) * 12f), new Vector3(2f, 24f, 2f), metal); }
            var light = new GameObject("Glow").AddComponent<Light>(); light.transform.SetParent(go.transform, false); light.transform.localPosition = new Vector3(0f, 14f * U, 0f);
            light.type = LightType.Point; light.color = glow; light.range = 9f; light.intensity = 3f; light.shadows = LightShadows.None;
            var part = E.AddPart("score", lx, -30f, 0f, 1152f * K, 1f, 1f, go);
            part.OnDied += p =>
            {
                if (GameState.Hud != null) GameState.Hud.Announce("The " + name + " core is destroyed!", 4f);
                if (enemy != null && enemy.alive && enemy.dying < 0f) { enemy.StartDying(); if (GameState.Hud != null) GameState.Hud.Announce("Their ship is going down!", 4f); }
            };
        }

        void BuildGenerator()
        {
            // on the island under the enemy ship (nudged onto land if it would be in the sea)
            var at = new Vector3(90f * U, 0f, -(-760f) * U);
            for (int tries = 0; tries < 60 && TerrainHeight(at.x, at.z) < SeaY + 1.2f; tries++) at += new Vector3(0f, 0f, -6f);
            at.y = TerrainHeight(at.x, at.z);
            var go = new GameObject("Shield generator");
            go.transform.SetParent(transform, false);
            var metal = lib != null ? lib.Instance("gunmetal") : null; if (metal != null) metal.SetColor("_BaseColor", new Color(0.35f, 0.36f, 0.4f));
            var blue = lib != null ? lib.Instance("glow") : null; if (blue != null) { blue.SetColor("_BaseColor", new Color(0.4f, 0.7f, 1f)); blue.SetColor("_EmissionColor", new Color(0.3f, 0.6f, 1f) * 4f); }
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material mat)
            {
                var p = GameObject.CreatePrimitive(t); p.name = n; p.transform.SetParent(go.transform, false); p.transform.localPosition = pos * U; p.transform.localScale = scale * U;
                if (mat != null) p.GetComponent<Renderer>().sharedMaterial = mat; return p;
            }
            P(PrimitiveType.Cube, "Base", new Vector3(0f, 4f, 0f), new Vector3(36f, 8f, 36f), metal);
            P(PrimitiveType.Cube, "Block", new Vector3(0f, 13f, 0f), new Vector3(22f, 10f, 22f), metal);
            for (int i = 0; i < 4; i++) { float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f; P(PrimitiveType.Cube, "Pylon", new Vector3(Mathf.Cos(a) * 15f, 16f, Mathf.Sin(a) * 15f), new Vector3(3f, 24f, 3f), metal); }
            P(PrimitiveType.Sphere, "Dome", new Vector3(0f, 24f, 0f), new Vector3(16f, 16f, 16f), blue);
            var pos = at;
            var part = go.AddComponent<ShipPart>();
            part.Setup("shieldgen", 2048f * K, 0.5f, 1.3f, enemy);
            go.transform.position = pos;
            generator = part;
            // the beam up to the ship
            var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); b.name = "Shield beam"; Destroy(b.GetComponent<Collider>());
            b.transform.SetParent(transform, false);
            float top = enemy.transform.position.y - 45f * U;
            b.transform.position = new Vector3(pos.x, (pos.y + 32f * U + top) * 0.5f, pos.z);
            b.transform.localScale = new Vector3(1.6f * U * 2f, (top - pos.y - 32f * U) * 0.5f, 1.6f * U * 2f);
            var bm = lib != null ? lib.Instance("water") : null; if (bm != null) bm.SetColor("_BaseColor", new Color(120f / 255f, 200f / 255f, 1f, 0.5f));
            if (bm != null) b.GetComponent<Renderer>().sharedMaterial = bm;
            b.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beam = b.transform;
            part.OnDied += p =>
            {
                if (enemy != null) enemy.DropShield();
                if (beam != null) Destroy(beam.gameObject);
                if (GameState.Hud != null) GameState.Hud.Announce("Shield generator destroyed! Their hangar is open: climb up, fly in, and destroy the engine or computer room", 5f);
            };
        }

        /// <summary>Dry, open ground near a point: walks outward until the terrain is above the sea.</summary>
        Vector3 LandNear(Vector3 at, float minHeight = SeaY + 1.2f)
        {
            for (float r = 0f; r < 400f; r += 8f)
            {
                for (int i = 0; i < 12; i++)
                {
                    float a = i / 12f * Mathf.PI * 2f + r * 0.1f;
                    var p = at + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                    float h = TerrainHeight(p.x, p.z);
                    if (h > minHeight) { p.y = h; return p; }
                }
            }
            at.y = TerrainHeight(at.x, at.z);
            return at;
        }

        Vector3 Scatter(Vector3 at, float spread)
        {
            var p = at + new Vector3(Random.Range(-spread, spread), 0f, Random.Range(-spread, spread));
            p.y = TerrainHeight(p.x, p.z);
            if (p.y < SeaY + 0.8f) { p = at; }
            return p;
        }

        void BuildGround()
        {
            // three command posts: Alpha neutral, Bravo and Charlie the enemy's, each with a garrison; and the generator's
            string[] names = { "Alpha", "Bravo", "Charlie" };
            Vector3[] nominal = { new Vector3(-640f * U, 0f, -180f * U), new Vector3(40f * U, 0f, 230f * U), new Vector3(660f * U, 0f, -190f * U) };
            for (int i = 0; i < 3; i++)
            {
                var at = LandNear(nominal[i]);
                var post = CommandPost.Build(transform, lib, names[i], at, i == 0 ? 0 : -1);
                posts.Add(post);
                if (i > 0) { for (int k = 0; k < 4; k++) SpawnInf(Scatter(at, 50f * U), 1); EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Jeep, Scatter(at, 50f * U), 1); }
            }
            if (generator != null)
            {
                var g = generator.transform.position;
                EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.AA, Scatter(g, 80f * U), 1);
                EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.AA, Scatter(g, 80f * U), 1);
                EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Tank, Scatter(g, 80f * U), 1);
                for (int k = 0; k < 4; k++) SpawnInf(Scatter(g, 80f * U), 1);
            }
        }

        EnemySoldier SpawnInf(Vector3 at, int team)
        {
            if (spawner == null) return null;
            var ai = spawner.Spawn(at, false, team);
            ai.transform.SetParent(transform, true);
            ai.attackRange = 50f; ai.sightRange = 90f;
            if (team == 0) { ai.health.max = 120f; ai.health.current = 120f; ai.damage = 7f * K; }
            return ai;
        }

        void EnemyDrop()
        {
            if (enemy == null || !enemy.alive || enemy.dying >= 0f) return;
            var held = new List<CommandPost>(); foreach (var p in posts) if (p.own == 1) held.Add(p);
            var post = held.Count > 0 && Random.value < 0.7f ? held[Random.Range(0, held.Count)] : posts[Random.Range(0, posts.Count)];
            float a = Random.Range(0f, Mathf.PI * 2f);
            var land = LandNear(post.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 110f * U);
            land.y += 7f * U;
            var ship = Dropship.Launch(transform, lib, 1, enemy.L2W(40f, -12f, -100f), land);
            bool tank = elapsed > 150f;
            ship.cargo = at => { for (int k = 0; k < 5; k++) SpawnInf(Scatter(at, 25f * U), 1); EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Jeep, Scatter(at, 25f * U), 1); if (tank) EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Tank, Scatter(at, 25f * U), 1); };
            if (GameState.Hud != null) GameState.Hud.Announce("Enemy dropship heading for " + post.postName);
        }

        void AllyDrop()
        {
            if (friendly == null || !friendly.alive || friendly.dying >= 0f) return;
            CommandPost post = null; foreach (var p in posts) if (p.own == 1) { post = p; break; }
            if (post == null) post = posts[0];
            float a = Random.Range(0f, Mathf.PI * 2f);
            var land = LandNear(post.transform.position + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 40f * U);
            land.y += 7f * U;
            var ship = Dropship.Launch(transform, lib, 0, friendly.L2W(40f, -12f, -100f), land);
            bool tank = PostsHeld >= 2;
            ship.cargo = at => { for (int k = 0; k < 2; k++) SpawnInf(Scatter(at, 25f * U), 0); if (tank) EnemyVehicle.Build(transform, lib, EnemyVehicle.Kind.Tank, Scatter(at, 25f * U), 0); };
        }

        void OrbitalStrike()
        {
            Health best = null; int bestN = 0;
            foreach (var h in Health.All)
            {
                if (h == null || !h.alive || h.team != 1 || h.isAir || !h.gameObject.activeInHierarchy) continue;
                if (h.GetComponent<EnemySoldier>() != null && h.stationary) continue;   // ship guards
                bool near = false; foreach (var p in posts) if (p.own == 1 && Vector3.Distance(p.transform.position, h.transform.position) < 420f * U) { near = true; break; }
                if (!near) continue;
                int n = Health.Count(1, h.transform.position, 60f * U, true, false) - 1;
                if (n > bestN) { bestN = n; best = h; }
            }
            if (best == null) return;
            var m = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(m.GetComponent<Collider>());
            var mat = lib != null ? lib.Instance("glow") : null; if (mat != null) { mat.SetColor("_BaseColor", new Color(1f, 0.3f, 0.2f)); mat.SetColor("_EmissionColor", new Color(1f, 0.25f, 0.15f) * 4f); m.GetComponent<Renderer>().sharedMaterial = mat; }
            m.transform.SetParent(transform, false);
            var at = best.transform.position;
            m.transform.position = at + Vector3.up * 0.3f; m.transform.localScale = new Vector3(40f * U * 2f, 0.1f, 40f * U * 2f);
            strikes.Add(new Strike { at = at, t = 0f, marker = m.transform });
            if (GameState.Hud != null) GameState.Hud.Announce("Orbital strike!");
        }

        void TickStrikes(float dt)
        {
            for (int i = strikes.Count - 1; i >= 0; i--)
            {
                var st = strikes[i]; st.t += dt;
                if (st.t < 1.3f) { float r = Mathf.Lerp(40f, 14f, st.t / 1.3f) * U * 2f; st.marker.localScale = new Vector3(r, 0.1f, r); continue; }
                if (st.marker != null)
                {
                    // the beam from orbit, then the blast
                    st.marker.localScale = new Vector3(2.4f, 1800f * U, 2.4f); st.marker.position = st.at + Vector3.up * 1800f * U;
                    var mat = st.marker.GetComponent<Renderer>().material; mat.SetColor("_BaseColor", Color.white); mat.SetColor("_EmissionColor", new Color(0.8f, 0.9f, 1f) * 5f);
                    Destroy(st.marker.gameObject, 0.5f); st.marker = null;
                    if (Projectiles.Instance != null) Projectiles.Instance.Explode(st.at + Vector3.up * 2f * U, 26f * U, 115f * U, 480f * K, foot != null ? foot.gameObject : gameObject, 0);
                    Shake(0.9f);
                }
                if (st.t > 2f) strikes.RemoveAt(i);
            }
        }

        void Bombard()
        {
            if (enemy == null || !enemy.alive || enemy.dying >= 0f) return;
            var live = new List<ShipGun>(); foreach (var g in enemy.guns) if (g.heavy && g.part.alive) live.Add(g);
            var held = new List<CommandPost>(); foreach (var p in posts) if (p.own == 1) held.Add(p);
            if (live.Count == 0 || held.Count == 0) return;
            var gun = live[Random.Range(0, live.Count)]; var post = held[Random.Range(0, held.Count)];
            var at = post.transform.position + new Vector3(Random.Range(-60f, 60f) * U, 0f, Random.Range(-60f, 60f) * U);
            var from = gun.transform.position + Vector3.up * 1f;
            if (Projectiles.Instance != null) Projectiles.Instance.FireShell(from, (at - from).normalized, 420f * U, 20f * U, 85f * U, 70f * K, 1, gun.gameObject, true);
        }

        void PointDefence()
        {
            if (friendly == null || !friendly.alive || friendly.dying >= 0f) return;
            var F = friendly.transform.position;
            foreach (var f in fighters)
            {
                if (f == null || Vector3.Distance(f.transform.position, F) > 700f * U) continue;
                var from = F + new Vector3(Random.Range(-200f, 200f) * U, 40f * U, 0f);
                var aim = f.transform.position + f.Velocity * (Vector3.Distance(from, f.transform.position) / (520f * U)) * 0.9f;
                if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, (aim - from).normalized, 520f * U, 22f * K, 900f * U, 0, friendly.gameObject, new Color(0.7f, 0.85f, 1f), 0.06f);
                break;
            }
        }

        void Broadside()
        {
            if (friendly == null || enemy == null || !friendly.alive || !enemy.alive) return;
            var F = friendly.transform.position; var E = enemy.transform.position;
            var from = F + new Vector3(Random.Range(-250f, 250f) * U, 50f * U, 60f * U * (E.z > F.z ? 1f : -1f));
            var to = E + new Vector3(Random.Range(-280f, 280f) * U, Random.Range(-20f, 30f) * U, -80f * U * (E.z > F.z ? 1f : -1f));
            if (Projectiles.Instance != null) Projectiles.Instance.FireBolt(from, (to - from).normalized, 300f * U, 0f, 16f, 0, friendly.gameObject);
        }

        void Wingman()
        {
            if (friendly == null || !friendly.alive || friendly.dying >= 0f) return;
            var at = friendly.L2W(40f, -12f, -100f);
            var dir = friendly.MouthDirection;
            var w = EnemyPlane.Build(transform, lib, at, Mathf.Atan2(dir.x, dir.z), 0);
            wingmen.Add(w);
        }

        PlaneController NewCraft(PlaneController.Craft kind, Vector3 at)
        {
            var dir = friendly.MouthDirection;
            return pm.NewCraft(kind, at, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, friendly);
        }

        /// <summary>A deck floor under a point when it is inside one of the ships, else NaN.</summary>
        public float HangarFloorOrNaN(Vector3 w)
        {
            float best = float.NaN;
            foreach (var s in new[] { friendly, enemy }) { if (s == null) continue; float f = s.FloorAt(w); if (!float.IsNaN(f) && (float.IsNaN(best) || f > best)) best = f; }
            return best;
        }

        int ParkedInHangar(Carrier S) { return pm.ParkedIn(S.HangarAt); }

        // ---- the player ----
        void PlaceInHangar()
        {
            var F = friendly;
            PlaneController use = null;
            if (plane != null && plane.health.alive && !plane.Active && !plane.IsHeli && F.HangarAt(plane.transform.position)) use = plane;
            if (use == null) foreach (var c in pm.craft) if (c != null && !c.Active && c.health.alive && !c.IsHeli && F.HangarAt(c.transform.position)) { use = c; break; }
            if (use == null) use = NewCraft(PlaneController.Craft.Plane, F.L2W(20f + Random.Range(-25f, 25f), F.FloorY + 4f, -20f));
            var dir = F.MouthDirection;
            pm.Fly(use, use.transform.position, Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, F, 0f);
        }

        void End(bool won)
        {
            if (over) return;
            over = true; endT = 0f; if (pm != null) pm.over = true;
            string text = won ? "The enemy ship is down! Galactic Assault won." : "Your ship was destroyed.";
            if (GameState.Hud != null) GameState.Hud.Announce(text, 6f);
            StartCoroutine(ShowEnd(won, text));
        }

        System.Collections.IEnumerator ShowEnd(bool won, string text) { yield return new WaitForSeconds(3f); if (MatchEnd.Instance != null) MatchEnd.Instance.Show(won, text); }

        void Update()
        {
            float dt = Time.deltaTime;
            shake = Mathf.Max(0f, shake - dt * 2.2f);
            feet.Clear();
            if (onFoot && foot != null && foot.gameObject.activeInHierarchy) feet.Add(foot.transform.position);
            if (friendly != null) friendly.Tick(dt, feet);
            if (enemy != null) enemy.Tick(dt, feet);
            fighters.RemoveAll(f => f == null);

            if (over)
            {
                endT += dt;
                if (endT > 8f && (InputBridge.Instance != null && (InputBridge.Instance.JumpPressed || InputBridge.Instance.Fire))) UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
                return;
            }


            // the enemy launches fighters while its ship lives
            if (enemy != null && enemy.alive && enemy.dying < 0f)
            {
                fighterT -= dt;
                if (fighterT <= 0f)
                {
                    fighterT = Random.Range(9f, 14f);
                    if (fighters.Count < 5)
                    {
                        var at = enemy.L2W(40f, -12f, -100f);
                        var dir = enemy.MouthDirection;
                        fighters.Add(EnemyPlane.Build(transform, lib, at, Mathf.Atan2(dir.x, dir.z)));
                    }
                }
            }
            // objectives: enemy riflemen march on the nearest post that is not theirs; yours on the nearest that is not yours
            objT -= dt;
            if (objT <= 0f)
            {
                objT = 2f;
                foreach (var h in Health.All)
                {
                    if (h == null || !h.alive) continue;
                    var inf = h.GetComponent<EnemySoldier>(); if (inf == null || inf.guard) continue;
                    CommandPost best = null; float bd = 1e9f; int want = h.team == 1 ? -1 : 1;
                    foreach (var p in posts) { if (p.own == want) continue; float d = Vector3.Distance(p.transform.position, h.transform.position); if (d < bd) { bd = d; best = p; } }
                    inf.objective = best != null ? (Vector3?)best.transform.position : null;
                }
            }
            // the ground war's clocks
            elapsed += dt;
            wingmen.RemoveAll(w => w == null);
            bool shipUp = friendly != null && friendly.alive && friendly.dying < 0f;
            bool enemyUp = enemy != null && enemy.alive && enemy.dying < 0f;
            dropT -= dt; if (dropT <= 0f) { dropT = Random.Range(38f, 52f); if (enemyUp && Health.Count(1, Vector3.zero, 5000f, true, true) < 34) EnemyDrop(); }
            allyDropT -= dt; if (allyDropT <= 0f) { allyDropT = Random.Range(26f, 34f); if (shipUp && Health.Count(0, Vector3.zero, 5000f, true, true) < 16) AllyDrop(); }
            strikeT -= dt; if (strikeT <= 0f) { strikeT = Random.Range(14f, 18f); if (shipUp && PostsHeld > 0) OrbitalStrike(); }
            bombT -= dt; if (bombT <= 0f) { bombT = Random.Range(10f, 14f); if (enemyUp && PostsHeld > 0) Bombard(); }
            allyT -= dt; if (allyT <= 0f) { allyT = Random.Range(12f, 16f); if (shipUp && wingmen.Count < 4) Wingman(); }
            pdT -= dt; if (pdT <= 0f) { pdT = 1.1f; PointDefence(); }
            parkT -= dt; if (parkT <= 0f) { parkT = 25f; if (shipUp && ParkedInHangar(friendly) < 2) NewCraft(PlaneController.Craft.Plane, friendly.L2W(-10f + Random.Range(0f, 100f), friendly.FloorY + 1f, 12f)); }

            fbT -= dt; if (fbT <= 0f) { fbT = Random.Range(2.2f, 3.4f); Broadside(); }
            TickStrikes(dt);
            // the posts
            foreach (var post in posts)
            {
                bool here = onFoot && foot != null && foot.health.alive && Vector3.Distance(foot.transform.position, post.transform.position) < CommandPost.Radius && foot.transform.position.y < post.transform.position.y + 40f * U;
                int allies = Health.Count(0, post.transform.position, CommandPost.Radius, true, true) - (here ? 1 : 0);
                string banner = post.Tick(dt, here, Mathf.Max(0, allies));
                if (banner != null && GameState.Hud != null) GameState.Hud.Announce(banner);
            }

            // your carrier: sunk by their cannons
            if (friendly != null && friendly.alive && friendly.dying < 0f && friendly.home <= 0f)
            {
                friendly.StartDying();
                if (GameState.Hud != null) GameState.Hud.Announce("Your ship is going down!", 4f);
            }

            // the status line
            modeLineT -= dt;
            if (modeLineT <= 0f && GameState.Hud != null)
            {
                modeLineT = 0.25f;
                string line = "YOUR SHIP " + Mathf.RoundToInt((friendly != null ? friendly.home : 0f) * 100f) + "%";
                if (enemy != null && enemy.alive)
                {
                    int cannons = 0; foreach (var g in enemy.guns) if (g.heavy && g.part.alive) cannons++;
                    line += "   ·   " + (enemy.shield ? "Posts " + PostsHeld + "/3 · shield generator up" : (enemy.dying >= 0f ? "Their ship is falling" : "Shield down! Board their ship")) + "   ·   cannons " + cannons + "/4";
                }
                GameState.Hud.SetModeLine(line);
            }
        }
    }
}
