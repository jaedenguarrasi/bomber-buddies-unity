using UnityEngine;

namespace BomberBuddies
{
    /// <summary>An enemy fighter launched from the other carrier: flies at a fixed speed, turns onto you, runs out past you after a
    /// pass, climbs over the ships, and fires short bursts when it has you in front of it.</summary>
    public class EnemyPlane : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public Health health;
        public float yaw, pitch, bank, life = 85f;
        public Vector3 Velocity { get; private set; }
        float outT, gunT, burst, steer, retargetT;
        Health targetH;
        Transform model, prop;

        public int Team { get { return health != null ? health.team : 1; } }

        /// <summary>An attack helicopter instead of a fighter: closes in, hovers, fires bursts.</summary>
        public bool heli, saucer;
        /// <summary>Where the commander sent it: it hunts around here.</summary>
        public Vector3? patrol;
        /// <summary>Abduction: a saucer picks an islander to hover over and beam up; the mode supplies them.</summary>
        public static System.Func<Vector3, Health> Abductee;
        Health abducting; float beamT; Transform beam;
        float fwdU, vyU;
        Transform rotor, tailRotor;

        public static EnemyPlane Build(Transform parent, MaterialLibrary lib, Vector3 pos, float yawRad, int team = 1, bool heli = false, bool saucer = false)
        {
            var go = new GameObject(saucer ? "Flying saucer" : heli ? (team == 0 ? "Gunship" : "Enemy gunship") : (team == 0 ? "Wingman" : "Enemy fighter"));
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = team == 0 ? 8 : 9;
            var e = go.AddComponent<EnemyPlane>();
            e.yaw = yawRad; e.heli = heli || saucer; e.saucer = saucer;
            e.health = go.AddComponent<Health>(); e.health.max = (saucer ? 300f * 1.28f : heli ? 140f * 1.4f * 1.28f : (team == 0 ? 250f : 161f)) * GalacticMode.K; e.health.current = e.health.max; e.health.team = team; e.health.isAir = true; e.health.aimHeight = 0f;
            var zone = go.AddComponent<HitZone>(); zone.owner = e.health; zone.multiplier = 0.9f;
            var col = go.AddComponent<SphereCollider>(); col.radius = saucer ? 3.4f : 1.4f;
            e.BuildModel(lib);
            e.health.OnDied += info =>
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position, 1f, 4f, 0f, go, team);
                if (team != 0) { GameState.Vehicles++; GameState.EnemyKilled(false, saucer ? 600 : 400); }
                Destroy(go);
            };
            return e;
        }

        void BuildModel(MaterialLibrary lib)
        {
            model = new GameObject("Model").transform; model.SetParent(transform, false);
            int layer = gameObject.layer;
            if (saucer) { CraftModels.Saucer(model, lib, layer); }
            else if (heli)
            {
                var body = CraftModels.Heli(model, lib, layer == 8 ? CraftModels.Mate : CraftModels.Foe, layer);
                rotor = body.transform.Find("Rotor"); tailRotor = body.transform.Find("Tail rotor");
            }
            else
            {
                var body = CraftModels.Plane(model, lib, layer == 8 ? CraftModels.Mate : CraftModels.Foe, layer);
                prop = body.transform.Find("Prop");
            }
        }

        Vector3 Forward { get { return Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward; } }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            life -= dt;
            var gm = GalacticMode.Instance;
            if (life <= 0f) { Destroy(gameObject); return; }
            // target: the nearest enemy aircraft, else the nearest enemy on the ground
            retargetT -= dt;
            if (retargetT <= 0f)
            {
                retargetT = 0.5f;
                if (saucer && Abductee != null) { var a = Abductee(transform.position); if (a != null) { abducting = a; } }
                if (abducting != null && (!abducting.alive || !abducting.gameObject.activeInHierarchy)) abducting = null;
                var from = patrol.HasValue ? patrol.Value : transform.position; float reach = patrol.HasValue ? 260f : 1400f * U;
                targetH = Health.Nearest(Team, from, reach, true);
                if (targetH == null) targetH = Health.Nearest(Team, from, reach, false);
            }
            Vector3? target = null; bool airTarget = false;
            if (targetH != null && targetH.alive && targetH.gameObject.activeInHierarchy) { target = targetH.transform.position + Vector3.up * targetH.aimHeight; airTarget = targetH.isAir; }
            else if (patrol.HasValue) { float g0 = GameState.GroundHeight != null ? GameState.GroundHeight(patrol.Value.x, patrol.Value.z) : 0f; target = new Vector3(patrol.Value.x, g0 + 60f * U, patrol.Value.z); }

            float gnd = GameState.GroundHeight != null ? GameState.GroundHeight(transform.position.x, transform.position.z) : 0f;
            float hold = gnd + 210f * U;
            if (saucer && abducting != null) { AbductUpdate(dt, gnd); return; }
            if (heli) { HeliUpdate(dt, target, airTarget, gnd); return; }
            if (target.HasValue)
            {
                var to = target.Value - transform.position;
                float dist = to.magnitude;
                if (outT > 0f) outT -= dt;
                else if (dist < 140f * U) outT = 3.2f;
                float wantYaw = outT > 0f ? yaw : Mathf.Atan2(to.x, to.z);
                steer = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, wantYaw * Mathf.Rad2Deg);
                yaw = Mathf.MoveTowardsAngle(yaw * Mathf.Rad2Deg, wantYaw * Mathf.Rad2Deg, 0.95f * Mathf.Rad2Deg * dt) * Mathf.Deg2Rad;
                hold = airTarget ? Mathf.Clamp(target.Value.y, gnd + 130f * U, gnd + 600f * U) : gnd + 210f * U;
                // guns: when it is ahead of us and not too far
                float ahead = Vector3.Dot(to.normalized, Forward);
                gunT -= dt;
                if (outT <= 0f && ahead > 0.96f && dist < 460f * U && gunT <= 0f)
                {
                    burst += dt;
                    var from = transform.position + Forward * 2.6f;
                    var dir = (target.Value + Vector3.up * 0.4f - from).normalized;
                    dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, 1.6f), 0f, 0f) * dir;
                    if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, dir, 460f * U, 5f * (Team == 0 ? GalacticMode.K : 1f), 460f * U * 1.1f, Team, gameObject, Team == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.45f, 0.3f), 0.04f);
                    if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, 0.8f);
                    gunT = 0.12f;
                }
            }
            // climb over whatever is below (the carriers)
            float obst = gm != null ? gm.ObstacleHeight(transform.position.x, transform.position.z) : (World3.Instance != null ? World3.Instance.ObstacleMetres(transform.position.x + Forward.x * 30f, transform.position.z + Forward.z * 30f) : gnd);
            if (obst + 40f * U > hold) hold = obst + 40f * U;
            float wantPitch = Mathf.Clamp((hold - transform.position.y) / (170f * U), -0.35f, 0.4f);
            pitch = Mathf.MoveTowards(pitch, wantPitch, 1.8f * dt);
            Velocity = Forward * 160f * U * GameTuning.Pace;
            if (prop != null) prop.Rotate(0f, 0f, 2400f * dt);
            transform.position += Velocity * dt;
            bank = Mathf.Lerp(bank, Mathf.Clamp(steer * 0.8f, -60f, 60f), Mathf.Min(1f, 3f * dt));
            transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank);
            if (transform.position.y < gnd + 1f) health.TakeDamage(new DamageInfo { amount = 9999f, point = transform.position, direction = Velocity, source = gameObject });
        }

        /// <summary>The gunship: flies at its target, slows to a hover 200 units out at 150 up, and fires bursts from the chin gun.</summary>
        /// <summary>Hover over the islander and hold the beam on him; a few seconds and he is pulled inside.</summary>
        void AbductUpdate(float dt, float gnd)
        {
            var p = abducting.transform.position; var me = transform.position;
            var flat = new Vector3(p.x - me.x, 0f, p.z - me.z); float dist = flat.magnitude;
            float hold = p.y + 32f * U;
            var vel = flat.normalized * Mathf.Min(dist * 1.5f, 70f * U) + Vector3.up * Mathf.Clamp((hold - me.y) * 1.5f, -30f * U, 30f * U);
            transform.position = me + vel * dt; yaw += dt * 0.8f;
            transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, Mathf.Sin(Time.time * 2f) * 4f);
            if (rotor != null) rotor.Rotate(0f, 400f * dt, 0f);
            bool over = dist < 6f * U && Mathf.Abs(me.y - hold) < 10f * U;
            if (over)
            {
                if (beam == null)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(b.GetComponent<Collider>()); b.name = "Beam"; b.transform.SetParent(transform, false); b.layer = gameObject.layer;
                    var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", new Color(0.6f, 1f, 0.8f, 0.3f)); m.SetFloat("_Surface", 1f); m.SetFloat("_Blend", 0f); m.renderQueue = 3000; m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    b.GetComponent<Renderer>().sharedMaterial = m; beam = b.transform;
                    Sfx.Play("ufo_hum", 0.6f, 1f, me);
                }
                float h = (me.y - gnd); beam.localPosition = new Vector3(0f, -h * 0.5f, 0f); beam.localScale = new Vector3(2.2f, h * 0.5f, 2.2f);
                beamT += dt;
                // the islander rises
                if (beamT > 1.5f) { var c = abducting.GetComponent<Civilian>(); if (c != null) c.lifted = true; abducting.transform.position = Vector3.MoveTowards(p, new Vector3(me.x, me.y - 1f, me.z), 4f * dt); }
                if (beamT > 4.5f || abducting.transform.position.y > me.y - 1.2f)
                {
                    var c = abducting.GetComponent<Civilian>(); if (c != null) c.Taken(); else Destroy(abducting.gameObject);
                    abducting = null; beamT = 0f; if (beam != null) { Destroy(beam.gameObject); beam = null; }
                }
            }
            else { beamT = 0f; if (beam != null) { Destroy(beam.gameObject); beam = null; } }
        }

        void HeliUpdate(float dt, Vector3? target, bool airTarget, float gnd)
        {
            float hold = gnd + 150f * U;
            float wantFwd = 0f;
            if (target.HasValue)
            {
                var to = target.Value - transform.position; var flat = new Vector3(to.x, 0f, to.z);
                float dist = flat.magnitude;
                float wantYaw = Mathf.Atan2(to.x, to.z);
                steer = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, wantYaw * Mathf.Rad2Deg);
                yaw = Mathf.MoveTowardsAngle(yaw * Mathf.Rad2Deg, wantYaw * Mathf.Rad2Deg, 1.2f * Mathf.Rad2Deg * dt) * Mathf.Deg2Rad;
                wantFwd = Mathf.Clamp((dist - 200f * U) / (120f * U), -0.4f, 1f) * 120f * U;
                if (airTarget) hold = Mathf.Clamp(target.Value.y + 20f * U, gnd + 80f * U, gnd + 500f * U);
                gunT -= dt;
                if (Mathf.Abs(steer) < 35f && dist < 500f * U && gunT <= 0f)
                {
                    burst += dt;
                    if (burst < 0.6f)
                    {
                        var from = transform.position + Forward * 1.2f - Vector3.up * 0.6f;
                        var dir = (target.Value - from).normalized;
                        dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, 2f), 0f, 0f) * dir;
                        if (saucer)
                        {
                            if (Projectiles.Instance != null) { Projectiles.Instance.FireBolt(from + transform.right * 1.5f, dir, 900f * U, 9f, 1.2f, Team, gameObject); Projectiles.Instance.FireBolt(from - transform.right * 1.5f, dir, 900f * U, 9f, 1.2f, Team, gameObject); }
                            Sfx.Play("gun_zap", 0.5f, 0.8f, from); gunT = 0.22f;
                        }
                        else
                        {
                            if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, dir, 420f * U, 5f * (Team == 0 ? GalacticMode.K : 1f), 500f * U, Team, gameObject, Team == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.45f, 0.3f), 0.04f);
                            if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, 0.8f);
                            gunT = 0.1f;
                        }
                    }
                    else { burst = 0f; gunT = Random.Range(1.2f, 2f); }
                }
            }
            var gm = GalacticMode.Instance;
            float obst = gm != null ? gm.ObstacleHeight(transform.position.x, transform.position.z) : (World3.Instance != null ? World3.Instance.ObstacleMetres(transform.position.x + Forward.x * 30f, transform.position.z + Forward.z * 30f) : gnd);
            if (obst + 40f * U > hold) hold = obst + 40f * U;
            fwdU = Mathf.MoveTowards(fwdU, wantFwd, 60f * U * dt);
            vyU = Mathf.MoveTowards(vyU, Mathf.Clamp((hold - transform.position.y) * 1.5f, -40f * U, 40f * U), 50f * U * dt);
            pitch = Mathf.Lerp(pitch, -fwdU / (120f * U) * 0.25f, Mathf.Min(1f, 3f * dt));
            var flatFwd = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward;
            Velocity = (flatFwd * fwdU + Vector3.up * vyU) * GameTuning.Pace;
            transform.position += Velocity * dt;
            bank = Mathf.Lerp(bank, Mathf.Clamp(steer * 0.3f, -25f, 25f), Mathf.Min(1f, 3f * dt));
            transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank);
            if (rotor != null) rotor.Rotate(0f, 1800f * dt, 0f);
            if (tailRotor != null) tailRotor.Rotate(0f, 0f, 2600f * dt);
            if (transform.position.y < gnd + 1f) health.TakeDamage(new DamageInfo { amount = 9999f, point = transform.position, direction = Velocity, source = gameObject });
        }
    }
}
