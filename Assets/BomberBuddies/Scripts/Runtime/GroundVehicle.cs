using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// A ground vehicle you drive: the battle tank, the scout car and the dirt bike (the original's GV table). The left stick
    /// drives and steers, the look turns the turret / your aim, Gun is the machine gun, Bomb (R1) is the shell, Back / F gets out.
    /// A chase camera sits behind at the table's distance and turns with the look; the turret follows it.
    /// </summary>
    public class GroundVehicle : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public enum Kind { Tank, Scout, Bike }
        public Kind kind;
        public Health health;
        public Camera cam;
        public bool Active { get; private set; }
        public float yaw, camYaw, camPitch = 12f;
        public Progress.Stats st;
        // GV: speed, reverse, turn rate (rad/s), hull, length, shell
        float spd, rev, turn, len, shellV, shellG, shellDmg, shellDr, shellCr, reloadK, reach, camDist;
        float speedU, mgT, shellT, heat;
        Transform model, turret, barrel;
        public string ProgressId { get { return kind == Kind.Tank ? "tank" : kind == Kind.Scout ? "scout" : "bike"; } }
        public string Name { get { return kind == Kind.Tank ? "battle tank" : kind == Kind.Scout ? "scout car" : "dirt bike"; } }
        public float SpeedU { get { return Mathf.Abs(speedU); } }
        public bool Overheated { get; private set; }
        public float Heat { get { return heat; } }

        public static GroundVehicle Build(Transform parent, MaterialLibrary lib, Camera cam, Kind kind, Vector3 at, float yawDeg)
        {
            var go = new GameObject(kind.ToString()); go.transform.SetParent(parent, false); go.transform.position = at; go.layer = 8;
            var v = go.AddComponent<GroundVehicle>(); v.kind = kind; v.cam = cam; v.yaw = yawDeg; v.camYaw = yawDeg;
            switch (kind)
            {
                case Kind.Tank: v.spd = 78f; v.rev = 34f; v.turn = 1.15f; v.len = 30f; v.shellV = 540f; v.shellG = 60f; v.shellDmg = 190f; v.shellDr = 62f; v.shellCr = 17f; v.reloadK = 1.0f; v.reach = 640f; v.camDist = 74f; break;
                case Kind.Scout: v.spd = 165f; v.rev = 60f; v.turn = 1.9f; v.len = 22f; v.shellV = 300f; v.shellG = 140f; v.shellDmg = 80f; v.shellDr = 44f; v.shellCr = 9f; v.reloadK = 0.55f; v.reach = 600f; v.camDist = 62f; break;
                default: v.spd = 240f; v.rev = 70f; v.turn = 3.1f; v.len = 8f; v.shellV = 300f; v.shellG = 140f; v.shellDmg = 80f; v.shellDr = 44f; v.shellCr = 9f; v.reloadK = 0.55f; v.reach = 520f; v.camDist = 42f; break;
            }
            var hp = go.AddComponent<Health>(); hp.team = 0; hp.aimHeight = 0.9f; hp.armor = kind == Kind.Tank ? 0.18f : 1f; v.health = hp;
            var zone = go.AddComponent<HitZone>(); zone.owner = hp; zone.multiplier = 1f;
            var col = go.AddComponent<SphereCollider>(); col.radius = kind == Kind.Tank ? 2.2f : kind == Kind.Scout ? 1.6f : 0.9f; col.center = Vector3.up * col.radius * 0.8f;
            v.model = new GameObject("Model").transform; v.model.SetParent(go.transform, false);
            Transform t, b;
            var body = CraftModels.Vehicle(v.model, lib, kind == Kind.Tank ? EnemyVehicle.Kind.Tank : EnemyVehicle.Kind.Jeep, 0, 8, out t, out b);
            if (kind == Kind.Bike) body.transform.localScale = new Vector3(0.45f, 0.7f, 0.75f);
            v.turret = t; v.barrel = b;
            v.ApplyLoadout();
            hp.OnDied += info =>
            {
                if (Projectiles.Instance != null) Projectiles.Instance.Explode(go.transform.position + Vector3.up * 0.8f, 1.5f, 4f, 0f, go, 0);
                foreach (var r in go.GetComponentsInChildren<Renderer>()) { var m = r.material; if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", new Color(0.12f, 0.1f, 0.09f)); }
                if (v.Active && PlayerManager.Instance != null) PlayerManager.Instance.VehicleLost(v);
                Destroy(go, 20f);
            };
            return v;
        }

        public void ApplyLoadout()
        {
            st = Progress.StatsFor(ProgressId);
            float f = health.alive ? health.Fraction : 1f;
            health.max = st.maxHp * GalacticMode.K * 1.28f; health.current = health.max * f;
        }

        public void TakeControl(bool on)
        {
            Active = on;
            if (on) { camYaw = yaw; camPitch = 12f; }
        }

        void Update()
        {
            if (!Active || !health.alive) return;
            var input = InputBridge.Instance; if (input == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);

            // driving
            float fwdIn = input.Move.y, steer = input.Move.x;
            float thk = st != null ? st.thrustK : 1f, tk = st != null ? st.turnK : 1f;
            float target = fwdIn > 0f ? spd * thk * fwdIn : rev * fwdIn;
            float accel = kind == Kind.Tank ? 45f : 120f;
            speedU = Mathf.MoveTowards(speedU, target, accel * dt * (fwdIn == 0f ? 1.6f : 1f));
            float sk = Mathf.Clamp(Mathf.Abs(speedU) / (spd * 0.35f), kind == Kind.Tank ? 1f : 0.2f, 1f);
            yaw += steer * turn * tk * sk * Mathf.Rad2Deg * dt * (speedU < 0f ? -1f : 1f);
            var fwd = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            var vel = fwd * speedU * U * GameTuning.Pace;
            var p = transform.position + vel * dt;
            float g = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : p.y;
            if (g < GalacticMode.SeaY + 0.6f) { p = transform.position; speedU = 0f; }   // not into the sea
            else
            {
                // a wall in the way (a building) stops it
                RaycastHit hit;
                if (vel.sqrMagnitude > 0.001f && Physics.SphereCast(transform.position + Vector3.up * 1.2f, 0.8f, vel.normalized, out hit, vel.magnitude * dt + 1.2f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) { p = transform.position; speedU *= -0.2f; }
                else p.y = g;
            }
            transform.position = p;
            float ahead = GameState.GroundHeight != null ? GameState.GroundHeight(p.x + fwd.x * 2f, p.z + fwd.z * 2f) : p.y;
            float pitch = -Mathf.Atan2(ahead - p.y, 2f) * Mathf.Rad2Deg;
            float lean = kind == Kind.Bike ? -steer * 22f * Mathf.Clamp01(Mathf.Abs(speedU) / 80f) : 0f;
            transform.rotation = Quaternion.Euler(pitch, yaw, lean);

            // the look: camera orbit, and the turret follows it
            float zk = input.Aim ? 0.5f : 1f;
            camYaw += input.Look.x * 0.5f * zk; camPitch = Mathf.Clamp(camPitch - input.Look.y * 0.45f * zk, -20f, 60f);
            if (turret != null)
            {
                var want = Quaternion.Euler(0f, camYaw, 0f);
                turret.rotation = Quaternion.RotateTowards(turret.rotation, want, (kind == Kind.Tank ? 90f : 200f) * dt);
                if (barrel != null) barrel.localRotation = Quaternion.RotateTowards(barrel.localRotation, Quaternion.Euler(Mathf.Clamp(camPitch - 12f, -10f, 45f) * -0.6f, 0f, 0f), 120f * dt);
            }

            // guns: the machine gun on Gun, the shell on Bomb
            heat = Mathf.Max(0f, heat - dt * 0.28f); if (Overheated && heat < 0.35f) Overheated = false;
            mgT -= dt; shellT -= dt;
            if (input.Fire && mgT <= 0f && !Overheated) FireMg();
            if (input.BombPressed && shellT <= 0f) FireShell();
            if (input.InteractPressed && PlayerManager.Instance != null) { PlayerManager.Instance.ExitVehicle(); return; }
            Camera(dt);
        }

        Vector3 AimDir
        {
            get
            {
                // where the crosshair points: from the camera through the centre of the screen, out to the gun's reach
                var from = cam != null ? cam.transform.position : transform.position;
                var dir = cam != null ? cam.transform.forward : Quaternion.Euler(0f, camYaw, 0f) * Vector3.forward;
                RaycastHit hit; Vector3 target = from + dir * reach * U;
                if (Physics.Raycast(from, dir, out hit, reach * U, ~(1 << 8), QueryTriggerInteraction.Ignore)) target = hit.point;
                var muzzle = turret != null ? turret.position + Vector3.up * 0.4f : transform.position + Vector3.up * 1.2f;
                return (target - muzzle).normalized;
            }
        }

        void FireMg()
        {
            var g = st != null ? st.gun : Progress.GunDef("m2");
            mgT = st != null ? st.fireInt : g.interval; heat += st != null ? st.heatShot : g.heat;
            if (heat >= 1f) { Overheated = true; heat = 1f; }
            var from = turret != null ? turret.position + turret.forward * 1.2f + Vector3.up * 0.4f : transform.position + Vector3.up * 1.2f;
            var dir = Quaternion.AngleAxis(Random.Range(0f, 360f), AimDir) * Quaternion.Euler(Random.Range(0f, 0.5f * g.spread), 0f, 0f) * AimDir;
            if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, dir, g.speed * U, (st != null ? st.dmg : g.dmg) * GalacticMode.K, g.range * U, 0, gameObject, new Color(1f, 0.9f, 0.6f), 0.04f);
            if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, 1f);
            if (GameState.Hud != null) GameState.Hud.OnShot();
        }

        void FireShell()
        {
            shellT = 2.4f * reloadK * (st != null ? st.bombReload / 2f : 1f);
            var from = barrel != null ? barrel.position + barrel.forward * 1.5f : transform.position + Vector3.up * 1.5f;
            float bk = st != null ? st.blastK : 1f;
            if (Projectiles.Instance != null) Projectiles.Instance.FireShell(from, AimDir, shellV * U, shellCr * U * bk, shellDr * U * bk, shellDmg * GalacticMode.K * bk, 0, gameObject, false, shellG * U);
            if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, AimDir, kind == Kind.Tank ? 2.6f : 1.4f);
        }

        void Camera(float dt)
        {
            if (cam == null) return;
            var rot = Quaternion.Euler(camPitch, camYaw, 0f);
            float dist = camDist * U * (InputBridge.Instance != null && InputBridge.Instance.Aim ? 0.55f : 1f);
            var pivot = transform.position + Vector3.up * (kind == Kind.Tank ? 2.4f : 1.6f);
            var want = pivot - rot * Vector3.forward * dist;
            RaycastHit hit;
            var dirBack = (want - pivot); float d = dirBack.magnitude;
            if (Physics.SphereCast(pivot, 0.3f, dirBack / d, out hit, d, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) want = pivot + dirBack / d * Mathf.Max(0.8f, hit.distance - 0.3f);
            float ground = GameState.GroundHeight != null ? GameState.GroundHeight(want.x, want.z) : 0f;
            if (want.y < ground + 0.8f) want.y = ground + 0.8f;
            cam.transform.position = Vector3.Lerp(cam.transform.position, want, Mathf.Min(1f, 12f * dt));
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Mathf.Min(1f, 14f * dt));
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, InputBridge.Instance != null && InputBridge.Instance.Aim ? 36f : 60f, Mathf.Min(1f, 6f * dt));
        }
    }
}
