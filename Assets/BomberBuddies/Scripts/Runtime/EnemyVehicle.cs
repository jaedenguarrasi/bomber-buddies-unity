using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The army's vehicles, either side: the scout jeep (fast, machine-gun bursts), the tank (slow, shells), and the anti-aircraft
    /// gun (fixed, only shoots aircraft). Numbers from the original's DEF table; they drive straight over the terrain.
    /// </summary>
    public class EnemyVehicle : MonoBehaviour
    {
        public enum Kind { Jeep, Tank, AA }
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
            }
            hp.current = hp.max;
            v.health = hp;
            v.BuildModel(lib, team, layer);
            hp.OnDied += info =>
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position + Vector3.up * 0.8f, 1.5f, 4f, 0f, go, team);
                foreach (var c in go.GetComponentsInChildren<Collider>()) c.enabled = false;
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { var m = r.material; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.09f)); }
                if (team != 0) GameState.EnemyKilled(false);
                Destroy(go, 20f);
            };
            v.cd = Random.Range(1f, 3f);
            return v;
        }

        void BuildModel(MaterialLibrary lib, int team, int layer)
        {
            var paint = lib != null ? lib.Instance("gunmetal") : null;
            if (paint != null) { paint.SetColor("_BaseColor", team == 0 ? new Color(0.36f, 0.42f, 0.3f) : new Color(0.33f, 0.3f, 0.3f)); paint.SetFloat("_Metallic", 0.25f); paint.SetFloat("_Smoothness", 0.45f); }
            var dark = lib != null ? lib.Get("dark") : null;
            GameObject P(PrimitiveType t, string n, Transform parent, Vector3 pos, Vector3 scale, Material m, Vector3? rot = null)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; g.layer = layer;
                g.transform.SetParent(parent, false); g.transform.localPosition = pos; g.transform.localScale = scale; if (rot.HasValue) g.transform.localRotation = Quaternion.Euler(rot.Value);
                if (m != null) g.GetComponent<Renderer>().sharedMaterial = m;
                var hz = g.AddComponent<HitZone>(); hz.owner = health; hz.multiplier = 1f;
                return g;
            }
            var root = transform;
            switch (kind)
            {
                case Kind.Jeep:
                    P(PrimitiveType.Cube, "Body", root, new Vector3(0f, 0.75f, 0f), new Vector3(2.2f, 0.7f, 4f), paint);
                    P(PrimitiveType.Cube, "Cab", root, new Vector3(0f, 1.35f, -0.3f), new Vector3(2f, 0.5f, 1.8f), paint);
                    for (int i = 0; i < 4; i++) P(PrimitiveType.Cylinder, "Wheel", root, new Vector3(i % 2 == 0 ? -1.1f : 1.1f, 0.45f, i < 2 ? 1.3f : -1.3f), new Vector3(0.9f, 0.25f, 0.9f), dark, new Vector3(0f, 0f, 90f));
                    turret = new GameObject("Mount").transform; turret.SetParent(root, false); turret.localPosition = new Vector3(0f, 1.75f, -0.6f);
                    barrel = P(PrimitiveType.Cylinder, "Gun", turret, new Vector3(0f, 0f, 0.5f), new Vector3(0.12f, 0.5f, 0.12f), dark, new Vector3(90f, 0f, 0f)).transform;
                    break;
                case Kind.Tank:
                    P(PrimitiveType.Cube, "Hull", root, new Vector3(0f, 0.9f, 0f), new Vector3(3.4f, 1.1f, 5.6f), paint);
                    P(PrimitiveType.Cube, "Track L", root, new Vector3(-1.5f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 5.8f), dark);
                    P(PrimitiveType.Cube, "Track R", root, new Vector3(1.5f, 0.55f, 0f), new Vector3(0.7f, 1.1f, 5.8f), dark);
                    turret = new GameObject("Turret").transform; turret.SetParent(root, false); turret.localPosition = new Vector3(0f, 1.75f, -0.3f);
                    P(PrimitiveType.Cube, "Turret body", turret, Vector3.zero, new Vector3(2.2f, 0.8f, 2.6f), paint);
                    barrel = P(PrimitiveType.Cylinder, "Barrel", turret, new Vector3(0f, 0.05f, 2.6f), new Vector3(0.22f, 1.8f, 0.22f), dark, new Vector3(90f, 0f, 0f)).transform;
                    break;
                case Kind.AA:
                    P(PrimitiveType.Cube, "Base", root, new Vector3(0f, 0.4f, 0f), new Vector3(2.6f, 0.8f, 2.6f), paint);
                    turret = new GameObject("Turret").transform; turret.SetParent(root, false); turret.localPosition = new Vector3(0f, 1.3f, 0f);
                    P(PrimitiveType.Cube, "Turret body", turret, Vector3.zero, new Vector3(1.4f, 1f, 1.4f), paint);
                    barrel = new GameObject("Barrels").transform; barrel.SetParent(turret, false); barrel.localPosition = new Vector3(0f, 0.2f, 0f); barrel.localRotation = Quaternion.Euler(-60f, 0f, 0f);
                    P(PrimitiveType.Cylinder, "Barrel L", barrel, new Vector3(-0.3f, 0f, 1.2f), new Vector3(0.14f, 1.2f, 0.14f), dark, new Vector3(90f, 0f, 0f));
                    P(PrimitiveType.Cylinder, "Barrel R", barrel, new Vector3(0.3f, 0f, 1.2f), new Vector3(0.14f, 1.2f, 0.14f), dark, new Vector3(90f, 0f, 0f));
                    break;
            }
        }

        void Update()
        {
            if (health == null || !health.alive) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            cd -= dt; retargetT -= dt;
            if (retargetT <= 0f)
            {
                retargetT = 0.5f;
                target = Health.Nearest(health.team, transform.position, range, kind == Kind.AA ? (bool?)true : false);
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
                    if (Mathf.Abs(Mathf.DeltaAngle(yaw, wantYaw)) < 50f) vel = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * speed;
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
