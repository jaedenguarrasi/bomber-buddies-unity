using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The army's vehicles, either side: the scout jeep (fast, machine-gun bursts), the tank (slow, shells), and the anti-aircraft
    /// gun (fixed, only shoots aircraft). Numbers from the original's DEF table; they drive straight over the terrain.
    /// </summary>
    public class EnemyVehicle : MonoBehaviour
    {
        public enum Kind { Jeep, Tank, AA, Bunker, Sam }
        const float U = GalacticMode.U;
        public Kind kind;
        public Health health;
        public float speed, range, holdDistance;
        Transform turret, barrel;
        Health target;
        float cd, retargetT, yaw;
        Vector3 vel;

        public static EnemyVehicle Build(Transform parent, MaterialLibrary lib, Kind kind, Vector3 at, int team)
        {
            var go = new GameObject(kind.ToString());
            go.transform.SetParent(parent, false);
            go.transform.position = at;
            int layer = team == 0 ? 8 : 9;
            go.layer = layer;
            var v = go.AddComponent<EnemyVehicle>();
            v.kind = kind;
            v.yaw = Random.Range(0f, 360f);
            var hp = go.AddComponent<Health>(); hp.team = team; hp.aimHeight = 0.9f;
            float K = GalacticMode.K * 1.28f;
            switch (kind)
            {
                case Kind.Jeep: hp.max = 34f * 1.5f * K; hp.armor = 1f; v.speed = 62f * U; v.range = 520f * U; v.holdDistance = 28f; break;
                case Kind.Tank: hp.max = 130f * 1.4f * K; hp.armor = 0.18f; v.speed = 20f * U; v.range = 620f * U; v.holdDistance = 45f; break;
                case Kind.AA: hp.max = 60f * 1.4f * K; hp.armor = 0.6f; v.speed = 0f; v.range = 760f * U; v.holdDistance = 0f; hp.stationary = true; break;
                case Kind.Bunker: hp.max = 220f * 1.3f * K; hp.armor = 0.12f; hp.bombMult = 0.65f; v.speed = 0f; v.range = 500f * U; hp.stationary = true; hp.aimHeight = 0.9f; break;
                case Kind.Sam: hp.max = 55f * 1.4f * K; hp.armor = 0.6f; v.speed = 0f; v.range = 900f * U; hp.stationary = true; break;
            }
            hp.current = hp.max;
            v.health = hp;
            v.BuildModel(lib, team, layer);
            hp.OnDied += info =>
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position + Vector3.up * 0.8f, 1.5f, 4f, 0f, go, team);
                foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { var m = r.material; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.09f)); }
                if (team != 0) GameState.EnemyKilled(false, kind == Kind.Jeep ? 40 : kind == Kind.Tank ? 120 : 80);
                Destroy(go, 20f);
            };
            v.cd = Random.Range(1f, 3f);
            return v;
        }

        void BuildModel(MaterialLibrary lib, int team, int layer)
        {
            CraftModels.Vehicle(transform, lib, kind, team, layer, out turret, out barrel);
            var bc = gameObject.AddComponent<BoxCollider>();
            switch (kind)
            {
                case Kind.Jeep: bc.center = new Vector3(0f, 6f * U, 0f); bc.size = new Vector3(22f * U, 12f * U, 30f * U); break;
                case Kind.Tank: bc.center = new Vector3(0f, 7f * U, 0f); bc.size = new Vector3(30f * U, 14f * U, 31f * U); break;
                case Kind.Bunker: bc.center = new Vector3(0f, 7f * U, 0f); bc.size = new Vector3(36f * U, 14f * U, 36f * U); break;
                case Kind.Sam: bc.center = new Vector3(0f, 5.5f * U, 0f); bc.size = new Vector3(24f * U, 11f * U, 14f * U); break;
                default: bc.center = new Vector3(0f, 5f * U, 0f); bc.size = new Vector3(22f * U, 11f * U, 22f * U); break;
            }
            var hz = gameObject.AddComponent<HitZone>(); hz.owner = health; hz.multiplier = 1f;
        }

        void Update()
        {
            if (health == null || !health.alive) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            cd -= dt; retargetT -= dt;
            if (retargetT <= 0f)
            {
                retargetT = 0.5f;
                target = Health.Nearest(health.team, transform.position, range, kind == Kind.AA || kind == Kind.Sam ? (bool?)true : false);
            }
            // driving: close to holding distance, then stop and shoot
            vel = Vector3.zero;
            if (speed > 0f)
            {
                Vector3 goal = Vector3.zero; bool go = false;
                if (target != null) { goal = target.transform.position; go = Vector3.Distance(goal, transform.position) > holdDistance; }
                if (go)
                {
                    var d = goal - transform.position; d.y = 0f;
                    float wantYaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                    yaw = Mathf.MoveTowardsAngle(yaw, wantYaw, (kind == Kind.Jeep ? 120f : 45f) * dt);
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, wantYaw)) < 50f) vel = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed * GameTuning.Pace;
                }
                var p = transform.position + vel * dt;
                float g = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : p.y;
                if (g < GalacticMode.SeaY + 0.6f && vel.sqrMagnitude > 0f) { vel = Vector3.zero; p = transform.position; }   // not into the sea
                else p.y = g;
                transform.position = p;
                // sit on the slope
                var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                float ahead = GameState.GroundHeight != null ? GameState.GroundHeight(p.x + fwd.x * 2f, p.z + fwd.z * 2f) : p.y;
                float pitch = -Mathf.Atan2(ahead - p.y, 2f) * Mathf.Rad2Deg;
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            // the gun tracks the target
            if (target != null && turret != null)
            {
                var aim = target.transform.position + Vector3.up * target.aimHeight;
                var to = aim - turret.position;
                var flat = Vector3.ProjectOnPlane(to, transform.up);
                if (flat.sqrMagnitude > 0.01f) turret.rotation = Quaternion.RotateTowards(turret.rotation, Quaternion.LookRotation(flat, transform.up), 140f * dt);
                if (barrel != null)
                {
                    float el = Mathf.Atan2(Vector3.Dot(to, transform.up), flat.magnitude) * Mathf.Rad2Deg;
                    barrel.localRotation = Quaternion.RotateTowards(barrel.localRotation, Quaternion.Euler(-Mathf.Clamp(el, kind == Kind.AA ? 5f : -10f, 85f), 0f, 0f), 120f * dt);
                }
                if (cd <= 0f && to.magnitude < range) Shoot(aim, to.magnitude);
            }
        }

        void Shoot(Vector3 aim, float dist)
        {
            if (Projectiles.Instance == null) return;
            var from = turret.position + turret.forward * 1.2f + Vector3.up * 0.3f;
            float K = GalacticMode.K;
            Vector3 lead = target != null ? aim : aim;
            switch (kind)
            {
                case Kind.Jeep:
                    for (int i = 0; i < 4; i++) { var dir = (lead - from).normalized + Random.insideUnitSphere * 0.03f; Projectiles.Instance.FireRound(from, dir, 360f * U, 5f, range * 1.2f, health.team, gameObject, new Color(1f, 0.6f, 0.3f), 0.04f); }
                    cd = Random.Range(1.3f, 2.2f);
                    break;
                case Kind.Tank:
                    {
                        float t = dist / (380f * U);
                        var dir = (lead - from).normalized; dir.y += t * t * 0.5f * 0.3f;   // a little loft
                        Projectiles.Instance.FireShell(from, dir.normalized, 380f * U, 1f, 4.5f, 40f * K, health.team, gameObject);
                        if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, 2.5f);
                        if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(0.15f);
                        cd = Random.Range(3f, 3.8f);
                    }
                    break;
                case Kind.Bunker:
                    for (int i = 0; i < 6; i++) { var dir = (lead - from).normalized + Random.insideUnitSphere * 0.035f; Projectiles.Instance.FireRound(from, dir, 380f * U, 6f, range * 1.2f, health.team, gameObject, new Color(1f, 0.6f, 0.3f), 0.04f); }
                    cd = Random.Range(1.6f, 2.6f);
                    break;
                case Kind.Sam:
                    if (target != null) Projectiles.Instance.FireMissile(from + Vector3.up * 0.8f, (Vector3.up * 0.6f + (lead - from).normalized).normalized, 500f * U, target.transform, 60f * K, health.team, gameObject);
                    if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, Vector3.up, 2f);
                    cd = Random.Range(4f, 6f);
                    break;
                case Kind.AA:
                    {
                        var tv = target != null ? TargetVelocity(target) : Vector3.zero;
                        float t = dist / (450f * U);
                        var at = lead + tv * t * 0.9f;
                        for (int i = 0; i < 3; i++) { var dir = (at - from).normalized + Random.insideUnitSphere * 0.025f; Projectiles.Instance.FireRound(from, dir, 450f * U, 8f, range * 1.2f, health.team, gameObject, new Color(1f, 0.5f, 0.25f), 0.06f); }
                        if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, (at - from).normalized, 1.5f);
                        cd = Random.Range(1.2f, 2f);
                    }
                    break;
            }
        }

        static Vector3 TargetVelocity(Health h)
        {
            var p = h.GetComponent<PlaneController>(); if (p != null) return p.Velocity;
            var e = h.GetComponent<EnemyPlane>(); if (e != null) return e.Velocity;
            return Vector3.zero;
        }
    }
}
