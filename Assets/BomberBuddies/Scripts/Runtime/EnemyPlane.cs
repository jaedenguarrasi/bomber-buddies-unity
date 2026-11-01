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

        public static EnemyPlane Build(Transform parent, MaterialLibrary lib, Vector3 pos, float yawRad, int team = 1)
        {
            var go = new GameObject(team == 0 ? "Wingman" : "Enemy fighter");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.layer = team == 0 ? 8 : 9;
            var e = go.AddComponent<EnemyPlane>();
            e.yaw = yawRad;
            e.health = go.AddComponent<Health>(); e.health.max = (team == 0 ? 250f : 161f) * GalacticMode.K; e.health.current = e.health.max; e.health.team = team; e.health.isAir = true; e.health.aimHeight = 0f;
            var zone = go.AddComponent<HitZone>(); zone.owner = e.health; zone.multiplier = 0.9f;
            var col = go.AddComponent<SphereCollider>(); col.radius = 1.4f;
            e.BuildModel(lib);
            e.health.OnDied += info =>
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position, 1f, 4f, 0f, go, team);
                if (team != 0) GameState.EnemyKilled(false, 400);
                Destroy(go);
            };
            return e;
        }

        void BuildModel(MaterialLibrary lib)
        {
            model = new GameObject("Model").transform; model.SetParent(transform, false);
            int layer = gameObject.layer;
            var body = CraftModels.Plane(model, lib, layer == 8 ? CraftModels.Mate : CraftModels.Foe, layer);
            prop = body.transform.Find("Prop");
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
                targetH = Health.Nearest(Team, transform.position, 1400f * U, true);
                if (targetH == null) targetH = Health.Nearest(Team, transform.position, 1400f * U, false);
            }
            Vector3? target = null; bool airTarget = false;
            if (targetH != null && targetH.alive && targetH.gameObject.activeInHierarchy) { target = targetH.transform.position + Vector3.up * targetH.aimHeight; airTarget = targetH.isAir; }

            float gnd = GameState.GroundHeight != null ? GameState.GroundHeight(transform.position.x, transform.position.z) : 0f;
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
                    if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, dir, 460f * U, 5f * (Team == 0 ? GalacticMode.K : 1f), 460f * U * 1.1f, Team, gameObject, Team == 0 ? new Color(1f, 0.9f, 0.6f) : new Color(1f, 0.45f, 0.3f), 0.04f);
                    if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, 0.8f);
                    gunT = 0.12f;
                }
            }
            // climb over whatever is below (the carriers)
            float obst = gm != null ? gm.ObstacleHeight(transform.position.x, transform.position.z) : gnd;
            if (obst + 40f * U > hold) hold = obst + 40f * U;
            float wantPitch = Mathf.Clamp((hold - transform.position.y) / (170f * U), -0.35f, 0.4f);
            pitch = Mathf.MoveTowards(pitch, wantPitch, 1.8f * dt);
            Velocity = Forward * 160f * U;
            if (prop != null) prop.Rotate(0f, 0f, 2400f * dt);
            transform.position += Velocity * dt;
            bank = Mathf.Lerp(bank, Mathf.Clamp(steer * 0.8f, -60f, 60f), Mathf.Min(1f, 3f * dt));
            transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank);
            if (transform.position.y < gnd + 1f) health.TakeDamage(new DamageInfo { amount = 9999f, point = transform.position, direction = Velocity, source = gameObject });
        }
    }
}
