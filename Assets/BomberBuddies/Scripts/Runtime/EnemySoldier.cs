using UnityEngine;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>
    /// A rifleman on either side. Wanders until he sees an enemy, closes to rifle range, stops, shoots in bursts, and shifts
    /// position between bursts. Hearing a shot or taking a hit puts him on alert. Walks the navmesh where there is one, or
    /// straight over the terrain where there is not (the original had no pathfinding either); a guard never leaves his post.
    /// </summary>
    public class EnemySoldier : MonoBehaviour
    {
        public float sightRange = 75f, fieldOfView = 150f, attackRange = 26f, loseAfter = 6f;
        public float damage = 7f, burstGap = 0.12f, pauseBetweenBursts = 2.2f, spreadDeg = 6.5f, reactionTime = 1.6f;
        public float walkSpeed = 1.9f;
        public SoldierModelBase model;
        public Health health;
        /// <summary>A ship's guard: stands his post (no navmesh needed) and shoots whoever comes through the door.</summary>
        public bool guard;
        /// <summary>Somewhere to go when there is nothing to shoot at: a command post, the tower.</summary>
        public Vector3? objective;
        /// <summary>The special soldiers: "inf" rifleman, "medic" (heals friends nearby), "egun" (light machine gun), "vest" (a
        /// sewer slider: runs at you and blows up), "ejet" (jetpack trooper: hovers and fires).</summary>
        public string kind = "inf";
        /// <summary>Creatures (DEF ape/gob/raptor/stalk/trex): melee, fast, built from the soldier body scaled and tinted until they get bodies of their own.</summary>
        public bool melee; public int score = 10;
        /// <summary>Caught in a saucer's beam: no fighting, just rising.</summary>
        public bool lifted;
        float meleeDmg, meleeReach = 1.6f, meleeT, creatureSpeed = 4f, leapT;
        /// <summary>The giants (colossus / Beast / kaiju): a creature body blown up, boulders, a beam, and the ground shakes.</summary>
        public bool giant; float giantScale = 1f, throwT = 4f, stompDist, beamT;

        /// <summary>Every step shakes the ground and flattens what it stands on; at range it throws a boulder (or, the kaiju, fires its beam).</summary>
        void GiantUpdate(float dt, float dist, Vector3 to)
        {
            float moved = agent != null && agent.enabled ? agent.velocity.magnitude * dt : creatureSpeed * dt;
            stompDist += moved;
            float stride = 3f * giantScale;
            if (stompDist > stride)
            {
                stompDist = 0f;
                var foot = transform.position + transform.right * (Random.value < 0.5f ? -1f : 1f) * giantScale * 0.6f;
                Sfx.Play("stomp", 0.9f, 0.5f, foot);
                if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(Mathf.Clamp01(1f - dist / 120f) * 0.9f);
                if (World3.Instance != null) World3.Instance.Blast(foot, 4f * giantScale, 120f);
                // anyone small underfoot
                foreach (var h in Health.All) if (h != null && h.alive && h.team != health.team && !h.isAir && Vector3.Distance(h.transform.position, foot) < 2.5f * giantScale) h.TakeDamage(new DamageInfo { amount = 60f * GalacticMode.K, point = h.transform.position, direction = Vector3.down, source = gameObject, foot = true });
            }
            throwT -= dt; beamT -= dt;
            if (target == null || !target.alive || Projectiles.Instance == null) return;
            var hand = transform.position + Vector3.up * (giantScale * 2.2f) + transform.forward * giantScale;
            if (kind == "kaiju")
            {
                // the beam: a stream of bolts for two seconds, every ten
                if (beamT <= 0f && dist < 220f) { beamT = 10f; Sfx.Play("gun_zap", 0.8f, 0.4f, hand); if (GameState.Hud != null) GameState.Hud.Comms("The giant is charging its beam"); }
                if (beamT > 8f && Mathf.Repeat(Time.time, 0.1f) < dt)
                {
                    var d = (target.transform.position + Vector3.up - hand).normalized; d = Quaternion.Euler(Random.Range(-1.5f, 1.5f), Random.Range(-1.5f, 1.5f), 0f) * d;
                    Projectiles.Instance.FireShell(hand, d, 420f * GalacticMode.U, 6f * GalacticMode.U, 16f * GalacticMode.U, 12f * GalacticMode.K, health.team, gameObject, true, 0f);
                }
            }
            else if (throwT <= 0f && dist > 18f && dist < 260f)
            {
                throwT = kind == "beast" ? 4f : 6f;
                var tp = target.transform.position; float g = 9.81f * 2.5f;
                float flight = Mathf.Clamp(dist / 55f, 1.2f, 3.5f);
                var v = (tp - hand) / flight; v.y += 0.5f * g * flight;
                Projectiles.Instance.FireShell(hand, v.normalized, v.magnitude, 3f * giantScale * GalacticMode.U * 4f, 14f * GalacticMode.U * 4f, 90f * GalacticMode.K, health.team, gameObject, false, g);
                Sfx.Play(kind == "beast" ? "roar_big" : "roar_ape", 0.9f, 0.6f, hand); Sfx.Play("swing", 0.7f, 0.6f, hand);
                if (GameState.Hud != null) GameState.Hud.Comms(kind == "beast" ? "Incoming!" : "It is throwing a boulder");
            }
        }

        public void MakeCreature(string k, MaterialLibrary lib = null)
        {
            kind = k; melee = true;
            float K = GalacticMode.K; Vector3 scale = Vector3.one; Color tint = Color.white;
            switch (k)
            {
                case "gob": health.max = 18f * K; creatureSpeed = 36f * GalacticMode.U * 1.6f; meleeDmg = 9f; meleeReach = 1.5f; score = 15; scale = new Vector3(0.75f, 0.7f, 0.75f); tint = new Color(0.55f, 0.8f, 0.35f); break;
                case "ape": health.max = 70f * K; creatureSpeed = 64f * GalacticMode.U * 1.2f; meleeDmg = 38f; meleeReach = 2.6f; score = 90; scale = new Vector3(1.9f, 1.6f, 1.9f); tint = new Color(0.45f, 0.32f, 0.22f); break;
                case "raptor": health.max = 45f * K; creatureSpeed = 150f * GalacticMode.U * 0.8f; meleeDmg = 14f; meleeReach = 2.0f; score = 80; scale = new Vector3(0.9f, 1.0f, 1.6f); tint = new Color(0.45f, 0.6f, 0.3f); break;
                case "stalk": health.max = 60f * K; creatureSpeed = 60f * GalacticMode.U * 1.3f; meleeDmg = 24f; meleeReach = 2.2f; score = 70; scale = new Vector3(0.8f, 1.7f, 0.8f); tint = new Color(0.85f, 0.85f, 0.9f); break;
                case "colossus": giant = true; giantScale = 4f; health.max = 4000f * K; health.armor = 0.75f; creatureSpeed = 50f * GalacticMode.U * 1.2f; meleeDmg = 140f; meleeReach = 9f; score = 6000; break;
                case "beast": giant = true; giantScale = 3f; health.max = 3000f * K; health.armor = 0.6f; creatureSpeed = 75f * GalacticMode.U * 1.3f; meleeDmg = 110f; meleeReach = 8f; score = 5000; break;
                case "kaiju": giant = true; giantScale = 3.5f; health.max = 12000f * K; health.armor = 0.8f; creatureSpeed = 40f * GalacticMode.U; meleeDmg = 220f; meleeReach = 16f; score = 12000; break;
                case "eninja": health.max = 260f * K * 0.5f; creatureSpeed = 36f * GalacticMode.U * 2.4f * 1.3f; meleeDmg = 70f; meleeReach = 2.2f; score = 260; tint = new Color(0.9f, 0.9f, 0.95f); lib = null; break;
                case "eshade": health.max = 260f * K * 0.5f; creatureSpeed = 36f * GalacticMode.U * 2.4f * 1.3f; meleeDmg = 70f; meleeReach = 2.2f; score = 280; tint = new Color(0.15f, 0.12f, 0.2f); lib = null; break;
                case "esling": health.max = 180f * K * 0.5f; creatureSpeed = 36f * GalacticMode.U * 2f * 1.3f; meleeDmg = 45f; meleeReach = 2.0f; score = 220; tint = new Color(0.8f, 0.2f, 0.25f); lib = null; break;
                case "clown": health.max = 170f * K * 0.6f; creatureSpeed = 62f * GalacticMode.U * 0.9f; meleeDmg = 14f; meleeReach = 2.4f; score = 220; scale = new Vector3(1.1f, 1.1f, 1.1f); tint = new Color(1f, 0.95f, 0.95f); lib = null; break;
                case "trex": health.max = 950f * K; health.armor = 0.7f; creatureSpeed = 88f * GalacticMode.U * 0.9f; meleeDmg = 70f; meleeReach = 5f; score = 900; scale = new Vector3(3.2f, 3.5f, 4.5f); tint = new Color(0.4f, 0.5f, 0.3f); break;
            }
            health.current = health.max; health.aimHeight = 1.1f * scale.y;
            sightRange = 140f;
            if (lib != null)
            {
                // its own body
                if (model != null) Destroy(model.gameObject);
                model = CreatureModel.Create(transform, lib, giant ? (k == "colossus" ? "ape" : k == "beast" ? "stalk" : "trex") : k, gameObject.layer);
                if (giant) { model.transform.localScale = Vector3.one * giantScale; foreach (var r in model.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", k == "beast" ? new Vector4(0.3f, 0.32f, 0.4f, 1f) : k == "kaiju" ? new Vector4(0.25f, 0.4f, 0.35f, 1f) : new Vector4(0.35f, 0.25f, 0.18f, 1f)); }
                scale = Vector3.one * (k == "trex" ? 1f : 1f);
            }
            else if (model != null)
            {
                model.transform.localScale = scale;
                foreach (var r in model.GetComponentsInChildren<Renderer>()) { foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(tint.r, tint.g, tint.b, 1f)); }
            }
            float bodyH = giant ? (k == "kaiju" ? 8f * giantScale : k == "colossus" ? 2.6f * giantScale : 3.2f * giantScale) : k == "trex" ? 8f : k == "ape" ? 2.6f : k == "stalk" ? 3.2f : k == "raptor" ? 1.6f : 1.1f;
            health.aimHeight = bodyH * 0.55f;
            var cc = GetComponent<CapsuleCollider>(); if (cc != null) { cc.height = bodyH; cc.center = new Vector3(0f, bodyH * 0.5f, 0f); cc.radius = k == "trex" ? 1.4f : k == "ape" ? 0.7f : 0.35f; }
            var hz = transform.Find("HeadZone"); if (hz != null) { hz.localPosition = new Vector3(0f, k == "trex" ? 6.5f : k == "ape" ? 2.5f : k == "stalk" ? 2.8f : k == "raptor" ? 1.9f : 1.0f, k == "trex" ? 4f : k == "raptor" ? 1.1f : 0f); var sc = hz.GetComponent<SphereCollider>(); if (sc != null) sc.radius = k == "trex" ? 1.2f : k == "ape" ? 0.4f : 0.2f; }
            if (giant) { var hz2 = transform.Find("HeadZone"); if (hz2 != null) { hz2.localPosition = new Vector3(0f, bodyH * 0.85f, k == "kaiju" ? 4f * giantScale : 0f); var sc2 = hz2.GetComponent<SphereCollider>(); if (sc2 != null) sc2.radius = bodyH * 0.12f; } var cc2 = GetComponent<CapsuleCollider>(); if (cc2 != null) cc2.radius = bodyH * 0.18f; }
            if (agent != null) { agent.speed = creatureSpeed; agent.radius = giant ? Mathf.Min(bodyH * 0.18f, 2f) : k == "trex" ? 1.4f : k == "ape" ? 0.7f : 0.35f; agent.height = bodyH; agent.acceleration = 30f; agent.stoppingDistance = meleeReach * 0.6f; }
        }

        /// <summary>A grey: a small alien with a ray gun that fires dead-straight bolts of light (ray). Jumps about, hard to hit.</summary>
        public bool grey;
        public void MakeGrey(MaterialLibrary lib)
        {
            kind = "grey"; grey = true; score = 60;
            health.max = 30f * GalacticMode.K; health.current = health.max; health.aimHeight = 0.9f;
            damage = 17f; burstGap = 0.28f; pauseBetweenBursts = 0.9f; spreadDeg = 2f; attackRange = 60f;
            if (lib != null && model != null) { Destroy(model.gameObject); model = CreatureModel.Create(transform, lib, "grey", gameObject.layer); }
            var hz = transform.Find("HeadZone"); if (hz != null) hz.localPosition = new Vector3(0f, 1.25f, 0f);
            if (agent != null) { agent.speed = 5.5f; agent.height = 1.4f; }
        }

        /// <summary>A creature: run straight at the nearest thing on the ground and hit it; apes and raptors leap the last stretch.</summary>
        void Creature(float dt)
        {
            if (target == null || !target.alive) { moveSpeed = 0f; if (model != null) model.Animate(0f, 0f, 0f, false, dt); return; }
            var to = target.transform.position - transform.position; float dist = to.magnitude; to.y = 0f;
            bool reach = dist < meleeReach + 0.3f;
            meleeT -= dt; leapT -= dt;
            if (giant) GiantUpdate(dt, dist, to);
            if (reach)
            {
                if (agent != null && agent.enabled && agent.isOnNavMesh) agent.ResetPath();
                if (meleeT <= 0f) { meleeT = kind == "trex" ? 1.6f : kind == "ape" ? 1.1f : kind == "clown" ? 0.45f : 0.8f; Sfx.Play(kind == "trex" ? "roar_big" : kind == "ape" ? "roar_ape" : kind == "raptor" ? "screech" : kind == "gob" ? "cackle" : kind == "clown" ? "hit_flesh" : kind == "eninja" || kind == "eshade" ? "swing" : "scream", 0.6f, 1f, transform.position); Sfx.Play(kind == "trex" ? "stomp" : "swing", 0.5f, 1f, transform.position); target.TakeDamage(new DamageInfo { amount = meleeDmg * GalacticMode.K * 0.45f, point = target.transform.position + Vector3.up, direction = to.normalized, source = gameObject, foot = true }); if (Gunfire.Instance != null) Gunfire.Instance.Impact(target.transform.position + Vector3.up * 0.9f, -to.normalized, true); }
                moveSpeed = 0f;
            }
            else if (agent != null && agent.enabled && agent.isOnNavMesh)
            {
                if (retargetT < 0.2f) agent.SetDestination(target.transform.position);
                moveSpeed = agent.velocity.magnitude;
                // the leap: apes and raptors cover the last 12 m in one bound
                if ((kind == "ape" || kind == "raptor") && dist < 12f && dist > 4f && leapT <= 0f) { leapT = 2.5f; agent.Warp(Vector3.Lerp(transform.position, target.transform.position, 0.7f)); }
                // ninjas and Grapples cover ground in bounds; the clown honks you off your feet
                if ((kind == "eninja" || kind == "eshade" || kind == "esling") && dist < (kind == "esling" ? 40f : 18f) && dist > 5f && leapT <= 0f) { leapT = kind == "esling" ? 3f : 1.8f; Sfx.Play("swing", 0.5f, 1.3f, transform.position); if (model != null) model.Animate(creatureSpeed, 0f, 0f, true, dt); agent.Warp(Vector3.Lerp(transform.position, target.transform.position, kind == "esling" ? 0.8f : 0.65f)); }
                if (kind == "eshade" && dist < 45f && leapT > 1.2f && Projectiles.Instance != null && Mathf.Repeat(Time.time, 1.6f) < dt) { var hand = transform.position + Vector3.up * 1.4f; Projectiles.Instance.FireShell(hand, (target.transform.position + Vector3.up - hand).normalized, 380f * GalacticMode.U, 2f * GalacticMode.U, 5f * GalacticMode.U, 18f * GalacticMode.K, health.team, gameObject, true, 0f); Sfx.Play("gun_zap", 0.5f, 1.4f, hand); }
                if (kind == "clown" && dist < 9f && leapT <= 0f) { leapT = 4.5f; Sfx.Tone(260f, 0.45f, 0.4f, "square"); Sfx.Tone(390f, 0.45f, 0.25f, "square"); var pl = target.GetComponent<FootController>(); if (pl != null) { pl.Knock(1.3f, "A clown honked you off your feet"); } if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(0.4f); }
            }
            else
            {
                var step = to.normalized * creatureSpeed * dt;
                var p = transform.position + step; p.y = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : p.y;
                if (p.y > GalacticMode.SeaY + 0.3f) transform.position = p;
                moveSpeed = creatureSpeed;
            }
            if (to.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to), 540f * dt);
            if (model != null) model.Animate(moveSpeed, 0f, 0f, false, dt);
        }
        float healT, hoverT;
        public int Team { get { return health != null ? health.team : 1; } }

        NavMeshAgent agent;
        Health target;
        enum State { Wander, Chase, Attack, Dead }
        State state = State.Wander;
        float wanderT, lastSeen = -99f, burstT, burstLeft, pauseT, shiftT, lookYaw, deadT, retargetT, moveSpeed;
        Vector3 lastKnown, eye = new Vector3(0f, 1.55f, 0f), muzzleFallback = new Vector3(0.2f, 1.35f, 0.4f), wanderTo;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            lookYaw = transform.eulerAngles.y;
            if (agent == null) return;
            agent.speed = 3.6f;
            agent.angularSpeed = 540f;
            agent.acceleration = 14f;
            agent.radius = 0.35f;
            agent.height = 1.8f;
            agent.updateRotation = false;
            agent.stoppingDistance = 1.5f;
        }

        public void Init(FootController unused, Health hp)
        {
            health = hp;
            health.aimHeight = 1.3f;
            health.OnDied += OnDied;
            health.OnDamaged += OnDamaged;
            wanderT = Random.Range(0.5f, 3f);
            gameObject.layer = Team == 0 ? 8 : 9;
        }

        float noticedAt = -99f;

        void OnDamaged(DamageInfo info)
        {
            if (state == State.Dead) return;
            if (target != null) { lastKnown = target.transform.position; lastSeen = Time.time; if (state == State.Wander) state = State.Chase; }
            else if (info.source != null) { lastKnown = info.source.transform.position; lastSeen = Time.time; if (state == State.Wander) state = State.Chase; }
        }

        void OnDied(DamageInfo info)
        {
            state = State.Dead;
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
            if (agent != null && agent.enabled) agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;
            if (model != null) { model.Die(info.direction); model.Dismember(info.direction, info.headshot, !info.foot && info.amount >= 40f); }
            if (Team != 0) GameState.EnemyKilled(info.headshot, score);
            Sfx.Play(melee ? (kind == "trex" ? "roar_big" : kind == "ape" ? "roar_ape" : "screech") : (info.headshot ? "thud" : Random.value < 0.3f ? "scream" : "grunt"), 0.5f, 1f, transform.position);
            deadT = 0f;
        }

        public void SetFacing(float yawDeg) { lookYaw = yawDeg; transform.rotation = Quaternion.Euler(0f, yawDeg, 0f); }

        public void Alert(Vector3 at)
        {
            if (state == State.Dead) return;
            lastKnown = at; lastSeen = Time.time;
            if (state == State.Wander) state = State.Chase;
        }

        bool CanSee(out Vector3 targetPoint)
        {
            targetPoint = Vector3.zero;
            if (target == null || !target.alive || !target.gameObject.activeInHierarchy) return false;
            var headPos = transform.position + eye;
            targetPoint = target.transform.position + Vector3.up * target.aimHeight;
            var to = targetPoint - headPos;
            float d = to.magnitude;
            if (d > sightRange) return false;
            if (state == State.Wander)
            {
                float ang = Vector3.Angle(new Vector3(to.x, 0f, to.z), Quaternion.Euler(0f, lookYaw, 0f) * Vector3.forward);
                if (ang > fieldOfView * 0.5f && d > 7f) return false;
            }
            RaycastHit hit;
            int mask = ~(1 << (Team == 0 ? 8 : 9));
            if (Physics.Raycast(headPos, to / d, out hit, d, mask, QueryTriggerInteraction.Ignore))
            {
                var h = hit.collider.GetComponentInParent<Health>();
                return h == target;
            }
            return true;
        }

        void Retarget()
        {
            retargetT = Random.Range(0.3f, 0.6f);
            if (target != null && target.alive && target.gameObject.activeInHierarchy && Vector3.Distance(target.transform.position, transform.position) < sightRange * 1.2f) return;
            // ground troops only deal with things on the ground
            target = Health.Nearest(Team, transform.position, sightRange, false);
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (lifted && state != State.Dead) { if (agent != null && agent.enabled) agent.enabled = false; if (model != null) model.Animate(0f, 0f, 0f, true, dt); transform.Rotate(0f, 90f * dt, 0f); return; }
            if (state == State.Dead)
            {
                deadT += dt;
                if (model != null) model.Animate(0f, 0f, 0f, false, dt);
                if (deadT > 12f) { transform.position += Vector3.down * dt * 0.4f; if (deadT > 16f) Destroy(gameObject); }
                return;
            }
            retargetT -= dt;
            if (retargetT <= 0f) Retarget();
            if (melee) { Creature(dt); return; }
            if (kind == "medic") Medic(dt);
            if (kind == "vest") { Vest(dt); return; }
            if (kind == "ejet") { Jet(dt); return; }
            if (guard || agent == null || !agent.isOnNavMesh) { SimpleUpdate(dt, !guard); return; }

            Vector3 tp;
            bool seen = CanSee(out tp);
            if (seen) { if (Time.time - lastSeen > 3f) noticedAt = Time.time; lastKnown = target.transform.position; lastSeen = Time.time; }
            float dist = target != null ? Vector3.Distance(transform.position, target.transform.position) : 999f;

            switch (state)
            {
                case State.Wander:
                    if (seen) { state = State.Chase; break; }
                    wanderT -= dt;
                    if (wanderT <= 0f || (!agent.pathPending && agent.remainingDistance < 1.5f && wanderT < 2f))
                    {
                        wanderT = Random.Range(4f, 9f);
                        var p = objective.HasValue && Vector3.Distance(objective.Value, transform.position) > 8f ? objective.Value + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f)) : transform.position + new Vector3(Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
                        NavMeshHit nh;
                        if (NavMesh.SamplePosition(p, out nh, 6f, NavMesh.AllAreas)) agent.SetDestination(nh.position);
                    }
                    agent.speed = objective.HasValue ? 3.2f : 2.0f;
                    break;
                case State.Chase:
                    agent.speed = 4.2f;
                    if (seen && dist < attackRange) { state = State.Attack; agent.ResetPath(); pauseT = Random.Range(0.2f, 0.6f); shiftT = Random.Range(2.5f, 4.5f); break; }
                    if (Time.time - lastSeen > loseAfter && !agent.pathPending && agent.remainingDistance < 2f) { state = State.Wander; wanderT = 1f; break; }
                    agent.SetDestination(lastKnown);
                    break;
                case State.Attack:
                    if (!seen && Time.time - lastSeen > 1.5f) { state = State.Chase; break; }
                    if (dist > attackRange * 1.3f) { state = State.Chase; break; }
                    shiftT -= dt;
                    if (shiftT <= 0f && !agent.hasPath)
                    {
                        shiftT = Random.Range(2.5f, 5f);
                        var side = Vector3.Cross(Vector3.up, (lastKnown - transform.position).normalized) * (Random.value < 0.5f ? 1f : -1f);
                        var p = transform.position + side * Random.Range(4f, 8f) + (lastKnown - transform.position).normalized * Random.Range(-3f, 3f);
                        NavMeshHit nh;
                        if (NavMesh.SamplePosition(p, out nh, 4f, NavMesh.AllAreas)) agent.SetDestination(nh.position);
                    }
                    if (agent.hasPath && !agent.pathPending && agent.remainingDistance < 0.8f) agent.ResetPath();
                    agent.speed = 3.2f;
                    if (seen && Time.time - noticedAt > reactionTime) Fire(tp, dt);
                    break;
            }
            moveSpeed = agent.velocity.magnitude;
            Turn(seen, agent.velocity, dt);
            Pose(seen ? tp : lastKnown, dt);
        }

        /// <summary>No navmesh: walk straight at the last known position over the terrain, stop at rifle range, shoot.</summary>
        void SimpleUpdate(float dt, bool canMove)
        {
            Vector3 tp;
            bool seen = CanSee(out tp);
            if (seen) { if (Time.time - lastSeen > 3f) noticedAt = Time.time; lastKnown = target.transform.position; lastSeen = Time.time; if (state != State.Attack) state = Vector3.Distance(transform.position, lastKnown) < attackRange ? State.Attack : State.Chase; }
            else if (Time.time - lastSeen > loseAfter) state = State.Wander;
            float dist = Vector3.Distance(transform.position, lastKnown);
            if (state == State.Attack && dist > attackRange * 1.3f) state = State.Chase;
            if (state == State.Chase && seen && dist < attackRange) state = State.Attack;
            Vector3 vel = Vector3.zero;
            if (canMove)
            {
                Vector3 goal = Vector3.zero; bool go = false;
                if (state == State.Chase) { goal = lastKnown; go = dist > attackRange * 0.8f; }
                else if (state == State.Wander)
                {
                    wanderT -= dt;
                    if (objective.HasValue && Vector3.Distance(objective.Value, transform.position) > 8f) { goal = objective.Value; go = true; }
                    else
                    {
                        if (wanderT <= 0f) { wanderT = Random.Range(4f, 9f); wanderTo = transform.position + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f)); }
                        goal = wanderTo; go = Vector3.Distance(transform.position, wanderTo) > 1.5f && wanderT < 7f;
                    }
                }
                if (go)
                {
                    var d = goal - transform.position; d.y = 0f;
                    if (d.sqrMagnitude > 0.01f) vel = d.normalized * (state == State.Chase ? walkSpeed * 1.6f : walkSpeed);
                }
                var p = transform.position + vel * dt;
                if (GameState.GroundHeight != null) p.y = GameState.GroundHeight(p.x, p.z);
                transform.position = p;
            }
            if (state == State.Attack && seen && Time.time - noticedAt > reactionTime) Fire(tp, dt);
            moveSpeed = vel.magnitude;
            Turn(seen || state == State.Attack, vel, dt);
            Pose(state == State.Attack ? (seen ? tp : lastKnown) : lastKnown, dt);
        }

        void Turn(bool facingTarget, Vector3 vel, float dt)
        {
            Vector3 face = facingTarget || state == State.Attack ? (lastKnown - transform.position) : (vel.sqrMagnitude > 0.2f ? vel : Vector3.zero);
            face.y = 0f;
            if (face.sqrMagnitude > 0.01f) lookYaw = Mathf.MoveTowardsAngle(lookYaw, Mathf.Atan2(face.x, face.z) * Mathf.Rad2Deg, 420f * dt);
            transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
        }

        void Pose(Vector3 aimAt, float dt)
        {
            if (model == null) return;
            float aimPitch = 0f;
            if (state == State.Attack) { var to = aimAt - (transform.position + eye); aimPitch = Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg; }
            model.Animate(moveSpeed, aimPitch, 0f, false, dt);
        }

        /// <summary>The medic: every few seconds, the most hurt friend within 8 m gets 25 hp.</summary>
        void Medic(float dt)
        {
            healT -= dt; if (healT > 0f) return;
            healT = 3f;
            Health best = null; float worst = 1f;
            foreach (var h in Health.All) { if (h == null || !h.alive || h.team != Team || h == health || h.isAir) continue; if (Vector3.Distance(h.transform.position, transform.position) > 8f) continue; if (h.Fraction < worst) { worst = h.Fraction; best = h; } }
            if (best != null) { best.Heal(25f); if (Gunfire.Instance != null) Gunfire.Instance.Impact(best.transform.position + Vector3.up, Vector3.up, false); }
        }

        /// <summary>The sewer slider: slides straight at the nearest enemy and detonates within 3 m (blast 120, like a grenade).</summary>
        void Vest(float dt)
        {
            if (target == null || !target.alive) { SimpleUpdate(dt, true); return; }
            var to = target.transform.position - transform.position; to.y = 0f;
            float dist = to.magnitude;
            if (dist < 3f)
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(transform.position + Vector3.up * 0.6f, 9f * GalacticMode.U, 58f * GalacticMode.U, 120f * GalacticMode.K, gameObject, Team);
                health.TakeDamage(new DamageInfo { amount = 9999f, point = transform.position, direction = Vector3.up, source = gameObject, explosive = true });
                return;
            }
            var vel = to.normalized * walkSpeed * 2.6f;
            var p = transform.position + vel * dt;
            if (GameState.GroundHeight != null) p.y = GameState.GroundHeight(p.x, p.z);
            transform.position = p;
            moveSpeed = vel.magnitude;
            lookYaw = Mathf.MoveTowardsAngle(lookYaw, Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg, 540f * dt);
            transform.rotation = Quaternion.Euler(0f, lookYaw, 0f);
            if (model != null) model.Animate(moveSpeed, 0f, 0.6f, false, dt);
        }

        /// <summary>The jetpack trooper: hovers 20 units up, drifts toward the target, keeps 40 m off, and fires like a rifleman.</summary>
        void Jet(float dt)
        {
            Vector3 tp; bool seen = CanSee(out tp);
            if (seen) { if (Time.time - lastSeen > 3f) noticedAt = Time.time; lastKnown = target.transform.position; lastSeen = Time.time; state = State.Attack; }
            hoverT += dt;
            float gnd = GameState.GroundHeight != null ? GameState.GroundHeight(transform.position.x, transform.position.z) : 0f;
            float hold = gnd + 20f * GalacticMode.U + Mathf.Sin(hoverT * 1.3f) * 0.6f;
            Vector3 vel = Vector3.zero;
            if (target != null && target.alive)
            {
                var to = target.transform.position - transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d > 45f) vel = to.normalized * 6f; else if (d < 25f) vel = -to.normalized * 4f;
                else vel = Vector3.Cross(Vector3.up, to.normalized) * 3f;
            }
            else if (objective.HasValue) { var to = objective.Value - transform.position; to.y = 0f; if (to.magnitude > 10f) vel = to.normalized * 5f; }
            var p = transform.position + vel * dt;
            p.y = Mathf.Lerp(p.y, hold, Mathf.Min(1f, 3f * dt));
            transform.position = p;
            moveSpeed = vel.magnitude;
            health.isAir = true;
            if (state == State.Attack && seen && Time.time - noticedAt > reactionTime) Fire(tp, dt);
            Turn(seen || state == State.Attack, vel, dt);
            Pose(seen ? tp : lastKnown, dt);
        }

        void Fire(Vector3 at, float dt)
        {
            if (pauseT > 0f) { pauseT -= dt; if (pauseT <= 0f) burstLeft = kind == "egun" ? Random.Range(10, 18) : Random.Range(3, 6); return; }
            burstT -= dt;
            if (burstT > 0f) return;
            if (burstLeft <= 0) { pauseT = pauseBetweenBursts * Random.Range(0.7f, 1.4f); return; }
            burstLeft--;
            burstT = burstGap;
            Sfx.Play(grey ? "gun_zap" : kind == "egun" ? "gun_lmg" : "gun_rifle", 0.42f, 1.05f, transform.position);
            var from = model != null && model.muzzle != null ? model.muzzle.position : transform.TransformPoint(muzzleFallback);
            float spread = spreadDeg * (moveSpeed > 0.5f ? 1.6f : 1f) * Mathf.Lerp(0.6f, 1.4f, Mathf.InverseLerp(5f, attackRange, Vector3.Distance(from, at)));
            var dir = (at - from).normalized;
            dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, spread), 0f, 0f) * dir;
            var end = from + dir * 200f;
            RaycastHit hit;
            int mask = ~(1 << (Team == 0 ? 8 : 9));
            if (Physics.Raycast(from, dir, out hit, 200f, mask, QueryTriggerInteraction.Ignore))
            {
                end = hit.point;
                var d = hit.collider.GetComponent<IDamageable>(); if (d == null) d = hit.collider.GetComponentInParent<IDamageable>();
                if (d != null) d.TakeDamage(new DamageInfo { amount = damage, point = hit.point, direction = dir, source = gameObject, foot = true });
                if (Gunfire.Instance != null) Gunfire.Instance.Impact(hit.point, hit.normal, d != null);
            }
            if (Gunfire.Instance != null)
            {
                Gunfire.Instance.TracerLine(from, end, grey ? new Color(0.5f, 1f, 0.6f) : Team == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.55f, 0.3f), grey ? 0.06f : 0.025f, grey ? 0.12f : 0.07f);
                Gunfire.Instance.MuzzleFlash(from, dir, 0.8f);
            }
        }
    }
}
