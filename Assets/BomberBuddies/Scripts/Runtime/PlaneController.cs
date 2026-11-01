using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The fighter-bomber, with the original game's arcade flight model (numbers in the original's units, scaled to metres at the
    /// edges): throttle and boost, stall below 70, a soft ceiling, docking in a hangar when you fly in slowly, launching when you
    /// throttle up, twin .50s with heat, and a rack of bombs.
    /// </summary>
    public class PlaneController : MonoBehaviour
    {
        const float U = GalacticMode.U;
        public Health health;
        public Camera cam;
        public MaterialLibrary lib;
        public bool Active { get; private set; }
        public Carrier docked;
        public float speedU = 0f, yaw, pitch, bank, thr = 0.73f, boost = 1f, launchT, heat;
        public int bombs = 8, maxBombs = 8;
        public bool Overheated { get; private set; }
        public bool Airborne { get { return docked == null; } }
        public Vector3 Velocity { get; private set; }
        public float AltitudeU { get { return transform.position.y / U; } }

        Transform model, muzzleL, muzzleR, propT;
        float fireT, bombCd, hurtTick, boostLock, camShakeT;
        bool boostOut, lastFire;
        Vector3 lastOk, camVel;
        Transform[] wingTips = new Transform[2];
        float[] wingDamage = new float[2];

        public static PlaneController Build(Transform parent, MaterialLibrary lib, Camera cam)
        {
            var go = new GameObject("Plane");
            go.transform.SetParent(parent, false);
            go.layer = 8;
            var p = go.AddComponent<PlaneController>();
            p.lib = lib; p.cam = cam;
            p.health = go.AddComponent<Health>(); p.health.max = 100f; p.health.current = 100f; p.health.team = 0; p.health.aimHeight = 0f; p.health.isAir = true;
            var zone = go.AddComponent<HitZone>(); zone.owner = p.health; zone.multiplier = 1f;
            var col = go.AddComponent<SphereCollider>(); col.radius = 1.3f;
            p.BuildModel();
            p.health.OnDamaged += info => { if (GameState.Hud != null && p.Active) GameState.Hud.Damaged(info.amount); };
            go.SetActive(false);
            return p;
        }

        void BuildModel()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            var body = lib != null ? lib.Instance("gunmetal") : null; if (body != null) { body.SetColor("_BaseColor", new Color(0.36f, 0.42f, 0.38f)); body.SetFloat("_Metallic", 0.35f); body.SetFloat("_Smoothness", 0.55f); }
            var dark = lib != null ? lib.Get("dark") : null;
            var glass = lib != null ? lib.Instance("water") : null; if (glass != null) glass.SetColor("_BaseColor", new Color(0.4f, 0.6f, 0.8f, 0.55f));
            var glow = lib != null ? lib.Instance("glow") : null; if (glow != null) { glow.SetColor("_BaseColor", new Color(0.5f, 0.8f, 1f)); glow.SetColor("_EmissionColor", new Color(0.4f, 0.7f, 1f) * 5f); }
            GameObject P(PrimitiveType t, string n, Vector3 pos, Vector3 scale, Material m, Vector3? rot = null)
            {
                var g = GameObject.CreatePrimitive(t); g.name = n; g.layer = 8; Destroy(g.GetComponent<Collider>());
                g.transform.SetParent(model, false); g.transform.localPosition = pos; g.transform.localScale = scale; if (rot.HasValue) g.transform.localRotation = Quaternion.Euler(rot.Value);
                if (m != null) g.GetComponent<Renderer>().sharedMaterial = m; return g;
            }
            // sizes in metres: a 4.8 m span, 6 m long fighter-bomber (the original's 20-unit half span)
            P(PrimitiveType.Capsule, "Fuselage", new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.9f, 5.6f), body, new Vector3(90f, 0f, 0f));
            P(PrimitiveType.Sphere, "Nose", new Vector3(0f, 0f, 2.9f), new Vector3(0.7f, 0.7f, 0.9f), dark);
            P(PrimitiveType.Sphere, "Canopy", new Vector3(0f, 0.42f, 0.9f), new Vector3(0.62f, 0.5f, 1.5f), glass);
            P(PrimitiveType.Cube, "Wing", new Vector3(0f, -0.1f, 0.1f), new Vector3(4.8f, 0.08f, 1.5f), body);
            P(PrimitiveType.Cube, "Wing root", new Vector3(0f, -0.05f, -0.2f), new Vector3(2.2f, 0.16f, 2.3f), body);
            P(PrimitiveType.Cube, "Tailplane", new Vector3(0f, 0.1f, -2.4f), new Vector3(2f, 0.06f, 0.7f), body);
            P(PrimitiveType.Cube, "Fin", new Vector3(0f, 0.55f, -2.4f), new Vector3(0.06f, 0.95f, 0.9f), body);
            P(PrimitiveType.Cylinder, "Gun pod L", new Vector3(-1.2f, -0.15f, 0.9f), new Vector3(0.14f, 0.7f, 0.14f), dark, new Vector3(90f, 0f, 0f));
            P(PrimitiveType.Cylinder, "Gun pod R", new Vector3(1.2f, -0.15f, 0.9f), new Vector3(0.14f, 0.7f, 0.14f), dark, new Vector3(90f, 0f, 0f));
            P(PrimitiveType.Cylinder, "Exhaust", new Vector3(0f, 0f, -2.9f), new Vector3(0.45f, 0.15f, 0.45f), glow, new Vector3(90f, 0f, 0f));
            for (int i = 0; i < 4; i++) P(PrimitiveType.Capsule, "Bomb", new Vector3(-0.9f + i * 0.6f, -0.32f, -0.1f), new Vector3(0.22f, 0.4f, 0.22f), dark, new Vector3(90f, 0f, 0f));
            muzzleL = new GameObject("MuzzleL").transform; muzzleL.SetParent(model, false); muzzleL.localPosition = new Vector3(-1.2f, -0.15f, 1.3f);
            muzzleR = new GameObject("MuzzleR").transform; muzzleR.SetParent(model, false); muzzleR.localPosition = new Vector3(1.2f, -0.15f, 1.3f);
            wingTips[0] = new GameObject("TipL").transform; wingTips[0].SetParent(model, false); wingTips[0].localPosition = new Vector3(-2.4f, -0.1f, 0.1f);
            wingTips[1] = new GameObject("TipR").transform; wingTips[1].SetParent(model, false); wingTips[1].localPosition = new Vector3(2.4f, -0.1f, 0.1f);
        }

        /// <summary>Put the plane somewhere and hand the controls over.</summary>
        public void Place(Vector3 pos, float yawDeg, Carrier dockedIn, float speed)
        {
            transform.position = pos; lastOk = pos;
            yaw = yawDeg * Mathf.Deg2Rad; pitch = 0f; bank = 0f;
            docked = dockedIn; speedU = speed; launchT = 0f; thr = 0.73f; heat = 0f; Overheated = false; boost = 1f; boostOut = false;
            bombs = maxBombs; wingDamage[0] = wingDamage[1] = 0f;
            health.ResetFull();
            Velocity = Vector3.zero;
            ApplyPose();
            gameObject.SetActive(true);
            Active = true;
            // start the camera behind the plane rather than sliding in from wherever it was
            if (cam != null) { cam.transform.position = transform.position - Forward * 12f + Vector3.up * 3.8f; cam.transform.LookAt(transform.position + Forward * 10f); }
        }

        /// <summary>Hand the controls back: the plane stays where it is, parked, until someone gets in again.</summary>
        public void Park()
        {
            Active = false;
            Velocity = Vector3.zero;
        }

        public Vector3 Forward { get { return Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward; } }

        void ApplyPose()
        {
            transform.rotation = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank * 50f);
        }

        void Update()
        {
            if (!Active) return;
            var input = InputBridge.Instance; if (input == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            var gm = GalacticMode.Instance;
            if (!health.alive) { Velocity = Vector3.zero; return; }

            // the stick: keyboard/pad plus mouse or touch drag
            float sx = Mathf.Clamp(input.Move.x + input.Look.x * 0.02f, -1f, 1f);
            float sy = Mathf.Clamp(input.Move.y - input.Look.y * 0.02f, -1f, 1f);
            if (Mathf.Abs(sx) < 0.12f) sx = 0f; if (Mathf.Abs(sy) < 0.12f) sy = 0f;
            thr = Mathf.Clamp01(thr + input.Throttle * 0.9f * dt);
            bool boosting = input.Sprint && !boostOut && boost > 0f && docked == null;
            if (boosting) { boost -= dt / 2.4f; if (boost <= 0f) { boost = 0f; boostOut = true; } }
            else { boost = Mathf.Min(1f, boost + dt / (2.4f * 2.3f)); if (boostOut && boost > 0.25f) boostOut = false; }

            if (launchT > 0f) launchT -= dt;
            health.isAir = docked == null;
            Carrier hs = null;
            if (launchT <= 0f && gm != null) hs = gm.HangarAt(transform.position);
            if (hs != null || docked != null) { DockStep(hs != null ? hs : docked, sx, boosting, dt); }
            else Fly(sx, sy, boosting, dt);

            // guns and bombs
            heat = Mathf.Max(0f, heat - dt * 0.28f);
            if (Overheated && heat < 0.35f) Overheated = false;
            fireT -= dt;
            if (input.Fire && !Overheated && fireT <= 0f && docked == null) Shoot();
            bombCd -= dt;
            if (input.JumpPressed && docked == null && bombs > 0 && bombCd <= 0f) DropBomb();
            if (input.InteractPressed)
            {
                if (docked != null) { if (gm != null) gm.ExitToFoot(); return; }
                else if (gm != null && transform.position.y > gm.ObstacleHeight(transform.position.x, transform.position.z) + 18f * U) { gm.BailOut(); return; }
                else if (GameState.Hud != null) GameState.Hud.Announce("Land in a hangar to get out");
            }
            ApplyPose();
            ChaseCamera(dt);
        }

        void Fly(float sx, float sy, bool boosting, float dt)
        {
            bool stalled = speedU < 70f;
            float k = Mathf.Clamp(speedU / 150f, 0.45f, 1.1f);
            yaw += sx * 1.35f * k * dt;
            pitch += -sy * 1.5f * k * (stalled ? 0.35f : 1f) * dt;
            if (sy == 0f) pitch -= pitch * 0.55f * dt;      // auto-level
            if (stalled) pitch -= 1.4f * dt;
            if (AltitudeU > 4700f && pitch > -0.2f) pitch -= 1.8f * dt;
            pitch = Mathf.Clamp(pitch, -1.3f, 1.3f);
            bank += (Mathf.Clamp(sx, -1.15f, 1.15f) * 0.95f - bank) * Mathf.Min(1f, 2.6f * dt);
            speedU += (140f * (0.45f + 0.75f * thr) + (boosting ? 460f : 0f) - 0.8f * speedU - 110f * Mathf.Sin(pitch) - Mathf.Abs(sx) * 22f) * dt;
            speedU = Mathf.Clamp(speedU, 35f, 820f);
            var v = Forward * speedU * U;
            if (stalled) v.y -= (1f - speedU / 70f) * 90f * U;
            Velocity = v;
            var from = transform.position;
            var to = from + v * dt;

            // what did we fly into?
            RaycastHit hit;
            var seg = to - from; float len = seg.magnitude;
            var gm = GalacticMode.Instance;
            if (len > 0.0001f && Physics.SphereCast(from, 0.6f, seg / len, out hit, len + 0.4f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
            {
                var ship = hit.collider.GetComponentInParent<Carrier>();
                if (ship != null && ship.IsShield(hit.collider))
                {
                    health.TakeDamage(new DamageInfo { amount = 30f * 0.75f, point = hit.point, direction = seg, source = ship.gameObject });
                    yaw += Mathf.PI; speedU *= 0.5f; to = lastOk;
                    if (GameState.Hud != null) GameState.Hud.Announce("Their hangar is shielded: destroy the shield generator on the ground first");
                }
                else if (ship != null)
                {
                    to = lastOk; speedU *= 0.6f;
                    hurtTick -= dt;
                    if (hurtTick <= 0f) { hurtTick = 0.5f; health.TakeDamage(new DamageInfo { amount = 4f, point = hit.point, direction = seg, source = ship.gameObject }); if (GameState.Hud != null) GameState.Hud.Announce("Mind the hangar"); if (gm != null) gm.Shake(0.3f); }
                }
                else
                {
                    // the ground, a tree, a building: that is a crash
                    transform.position = hit.point;
                    Crash(hit.point);
                    return;
                }
            }
            else lastOk = from;
            transform.position = to;
            if (gm != null && transform.position.y < gm.TerrainHeight(transform.position.x, transform.position.z) + 0.8f) { Crash(transform.position); return; }
            // wing tips scraping the hull
            for (int w = 0; w < 2; w++)
            {
                var tip = wingTips[w].position;
                Collider[] cs = Physics.OverlapSphere(tip, 0.15f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore);
                if (cs.Length > 0 && wingDamage[w] < 1f)
                {
                    wingDamage[w] += 0.4f;
                    health.TakeDamage(new DamageInfo { amount = 14f, point = tip, direction = seg, source = gameObject });
                    if (GameState.Hud != null) GameState.Hud.Announce((w == 0 ? "Left" : "Right") + (wingDamage[w] >= 1f ? " wing gone" : " wing clipped"));
                }
            }
        }

        void DockStep(Carrier S, float sx, bool boosting, float dt)
        {
            if (docked == null)
            {
                docked = S;
                if (GameState.Hud != null) GameState.Hud.Announce("Docked. Get out, or throttle up to launch");
            }
            speedU *= Mathf.Max(0f, 1f - 2.5f * dt);
            float floor = S.L2W(0f, S.FloorY, 0f).y;
            var p = transform.position;
            p.y = Mathf.Lerp(p.y, floor + 4f * U, Mathf.Min(1f, 3f * dt));
            pitch *= Mathf.Max(0f, 1f - 6f * dt); bank *= Mathf.Max(0f, 1f - 6f * dt);
            yaw += sx * 1.4f * dt;
            p += Forward * speedU * U * dt * 0.3f;
            // kept inside the hangar box, inset 16
            var l = S.W2L(p); var h = S.HangarBox;
            l.x = Mathf.Clamp(l.x, h.x0 + 16f, h.x1 - 16f); l.z = Mathf.Clamp(l.z, h.z0 + 16f, h.z1 - 16f);
            p = S.L2W(l.x, l.y, l.z);
            transform.position = p; lastOk = p;
            Velocity = Vector3.zero;
            if ((boosting || thr > 0.85f) && speedU < 30f)
            {
                // launch: out of the mouth
                var outDir = S.MouthDirection;
                yaw = Mathf.Atan2(outDir.x, outDir.z);
                pitch = 0.05f; bank = 0f;
                docked = null; launchT = 1.6f; speedU = 230f;
                Velocity = Forward * speedU * U;
                if (GameState.Hud != null) GameState.Hud.Announce("Launch!");
            }
        }

        void Shoot()
        {
            fireT = 0.085f;
            heat += 0.042f;
            if (heat >= 1f) { Overheated = true; heat = 1f; }
            lastFire = !lastFire;
            var m = lastFire ? muzzleL : muzzleR;
            var dir = Forward;
            dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, 0.6f), 0f, 0f) * dir;
            if (Projectiles.Instance != null) Projectiles.Instance.FireRound(m.position, dir, 640f * U, 10f * GalacticMode.K, 640f * U, 0, gameObject, new Color(1f, 0.9f, 0.6f), 0.04f);
            if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(m.position, dir, 1.2f);
            if (GameState.Hud != null) GameState.Hud.OnShot();
        }

        void DropBomb()
        {
            bombs--; bombCd = 0.34f;
            var vel = Velocity; vel.y = Mathf.Min(vel.y, 0f) - 25f * U;
            if (Projectiles.Instance != null) Projectiles.Instance.DropBomb(transform.position - Vector3.up * 0.9f, vel, 36f * U, 86f * U, 175f * GalacticMode.K, gameObject);
        }

        void Crash(Vector3 at)
        {
            if (Projectiles.Instance != null) Projectiles.Instance.Explode(at, 2f, 6f, 40f, gameObject, 0);
            health.TakeDamage(new DamageInfo { amount = 9999f, point = at, direction = Velocity, source = gameObject });
        }

        void ChaseCamera(float dt)
        {
            if (cam == null) return;
            float k = Mathf.Clamp01(speedU / 300f);
            var back = Forward; back.y *= 0.6f;
            var want = transform.position - back.normalized * (12f + 5f * k) + Vector3.up * (3.8f - 1.2f * pitch);
            var lookAt = transform.position + Forward * 30f;
            cam.transform.position = Vector3.SmoothDamp(cam.transform.position, want, ref camVel, 0.12f);
            var rot = Quaternion.LookRotation(lookAt - cam.transform.position, Quaternion.AngleAxis(-bank * 30f, Forward) * Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Mathf.Min(1f, 8f * dt));
            var gm = GalacticMode.Instance;
            if (gm != null && gm.shake > 0f) cam.transform.position += Random.insideUnitSphere * gm.shake * 0.4f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 70f + 14f * Mathf.Clamp01((speedU - 200f) / 500f), Mathf.Min(1f, 4f * dt));
        }
    }
}
