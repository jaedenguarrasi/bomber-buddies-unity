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
        float outT, gunT, burst, steer;
        Transform model;

        public static EnemyPlane Build(Transform parent, MaterialLibrary lib, Vector3 pos, float yawRad)
        {
            var go = new GameObject("Enemy fighter");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = 9;
            var e = go.AddComponent<EnemyPlane>();
            e.yaw = yawRad;
            e.health = go.AddComponent<Health>(); e.health.max = 161f; e.health.current = 161f; e.health.team = 1;
            var zone = go.AddComponent<HitZone>(); zone.owner = e.health; zone.multiplier = 0.9f;
            var col = go.AddComponent<SphereCollider>(); col.radius = 1.4f;
            e.BuildModel(lib);
            e.health.OnDied += info =>
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position, 1f, 4f, 0f, go, 1);
                GameState.EnemyKilled(false);
                Destroy(go);
            };
            return e;
        }

        void BuildModel(MaterialLibrary lib)
        {
            model = new GameObject("Model").transform; model.SetParent(transform, false);
            var body = lib != null ? lib.Instance("gunmetal") : null; if (body != null) { body.SetColor("_BaseColor", new Color(0.3f, 0.26f, 0.28f)); body.SetFloat("_Metallic", 0.3f); }
            var glow = lib != null ? lib.Instance("glow") : null; if (glow != null) { glow.SetColor("_BaseColor", new Color(1f, 0.4f, 0.25f)); glow.SetColor("_EmissionColor", new Color(1f, 0.35f, 0.2f) * 5f); }
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material m, Vector3? rot = null)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; g.layer = 9; Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(model, false); g.transform.localPosition = pos; g.transform.localScale = scale; if (rot.HasValue) g.transform.localRotation = Quaternion.Euler(rot.Value);
                if (m != null) g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            P(PrimitiveType.Capsule, "Fuselage", Vector3.zero, new Vector3(0.8f, 0.8f, 5f), body, new Vector3(90f, 0f, 0f));
            P(PrimitiveType.Cube, "Wing", new Vector3(0f, 0f, -0.4f), new Vector3(5f, 0.08f, 1.6f), body, new Vector3(0f, 0f, 0f));
            P(PrimitiveType.Cube, "Fin L", new Vector3(-0.7f, 0.5f, -2f), new Vector3(0.06f, 0.9f, 0.8f), body, new Vector3(0f, 0f, -25f));
            P(PrimitiveType.Cube, "Fin R", new Vector3(0.7f, 0.5f, -2f), new Vector3(0.06f, 0.9f, 0.8f), body, new Vector3(0f, 0f, 25f));
            P(PrimitiveType.Cylinder, "Exhaust", new Vector3(0f, 0f, -2.6f), new Vector3(0.4f, 0.12f, 0.4f), glow, new Vector3(90f, 0f, 0f));
        }

        Vector3 Forward { get { return Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward; } }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            life -= dt;
            var gm = GalacticMode.Instance;
            if (life <= 0f || gm == null) { Destroy(gameObject); return; }
            // target: the player in the air, else the player on foot, else nothing
            Vector3? target = null; bool airTarget = false;
            if (gm.plane != null && gm.plane.Active && gm.plane.health.alive) { target = gm.plane.transform.position; airTarget = gm.plane.Airborne; }
            else if (GameState.Player != null && GameState.Player.gameObject.activeInHierarchy && GameState.Player.health.alive) target = GameState.Player.transform.position;

            float gnd = gm.TerrainHeight(transform.position.x, transform.position.z);
            float hold = gnd + 210f * U;
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
                    if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, dir, 460f * U, 5f, 460f * U * 1.1f, 1, gameObject, new Color(1f, 0.45f, 0.3f), 0.04f);
                    if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, 0.8f);
                    gunT = 0.12f;
                }
            }
            // climb over whatever is below (the carriers)
            float obst = gm.ObstacleHeight(transform.position.x, transform.position.z);
            if (obst + 40f * U > hold) hold = obst + 40f * U;
            float wantPitch = Mathf.Clamp((hold - transform.position.y) / (170f * U), -0.35f, 0.4f);
            pitch = Mathf.MoveTowards(pitch, wantPitch, 1.8f * dt);
            Velocity = Forward * 160f * U;
            transform.position += Velocity * dt;
            bank = Mathf.Lerp(bank, Mathf.Clamp(steer * 0.8f, -60f, 60f), Mathf.Min(1f, 3f * dt));
            transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank);
            if (transform.position.y < gnd + 1f) health.TakeDamage(new DamageInfo { amount = 9999f, point = transform.position, direction = Velocity, source = gameObject });
        }
    }
}
