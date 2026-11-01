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
        public enum Craft { Plane, Heli }
        public Craft craft = Craft.Plane;
        public bool IsHeli { get { return craft == Craft.Heli; } }
        public string CraftName { get { return IsHeli ? "attack helicopter" : "fighter-bomber"; } }
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

        Transform model, muzzleL, muzzleR, propT, rotor, tailRotor;
        float fwdU, vyU;            // the helicopter's forward and vertical speeds (original units)
        public bool Landed { get; private set; }
        /// <summary>Cockpit view (the original's default) or the chase camera behind the aircraft.</summary>
        public bool cockpit = true;
        float fireT, bombCd, hurtTick, boostLock, camShakeT;
        bool boostOut, lastFire;
        Vector3 lastOk, camVel;
        Transform[] wingTips = new Transform[2];
        float[] wingDamage = new float[2];

        public static PlaneController Build(Transform parent, MaterialLibrary lib, Camera cam, Craft craft = Craft.Plane)
        {
            var go = new GameObject(craft == Craft.Heli ? "Helicopter" : "Plane");
            go.transform.SetParent(parent, false);
            go.layer = 8;
            var p = go.AddComponent<PlaneController>();
            p.lib = lib; p.cam = cam; p.craft = craft;
            if (craft == Craft.Heli) p.thr = 0.5f;
            p.health = go.AddComponent<Health>(); p.health.max = 100f; p.health.current = 100f; p.health.team = 0; p.health.aimHeight = 0f; p.health.isAir = true;
            var zone = go.AddComponent<HitZone>(); zone.owner = p.health; zone.multiplier = 1f;
            var col = go.AddComponent<SphereCollider>(); col.radius = 1.3f;
            if (craft == Craft.Heli) p.BuildHeliModel(); else p.BuildModel();
            p.health.OnDamaged += info => { if (GameState.Hud != null && p.Active) GameState.Hud.Damaged(info.amount); };
            go.SetActive(false);
            return p;
        }

        void BuildModel()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            var body = CraftModels.Plane(model, lib, CraftModels.You, 8);
            propT = body.transform.Find("Prop");
            muzzleL = new GameObject("MuzzleL").transform; muzzleL.SetParent(model, false); muzzleL.localPosition = new Vector3(-4f * U, -0.6f * U, 7f * U);
            muzzleR = new GameObject("MuzzleR").transform; muzzleR.SetParent(model, false); muzzleR.localPosition = new Vector3(4f * U, -0.6f * U, 7f * U);
            wingTips[0] = new GameObject("TipL").transform; wingTips[0].SetParent(model, false); wingTips[0].localPosition = new Vector3(-21f * U, -0.6f * U, 2f * U);
            wingTips[1] = new GameObject("TipR").transform; wingTips[1].SetParent(model, false); wingTips[1].localPosition = new Vector3(21f * U, -0.6f * U, 2f * U);
        }

        void BuildHeliModel()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            var body = CraftModels.Heli(model, lib, CraftModels.You, 8);
            rotor = body.transform.Find("Rotor"); tailRotor = body.transform.Find("Tail rotor");
            muzzleL = new GameObject("MuzzleL").transform; muzzleL.SetParent(model, false); muzzleL.localPosition = new Vector3(-0.5f * U, -5f * U, 14f * U);
            muzzleR = new GameObject("MuzzleR").transform; muzzleR.SetParent(model, false); muzzleR.localPosition = new Vector3(0.5f * U, -5f * U, 14f * U);
            wingTips[0] = new GameObject("TipL").transform; wingTips[0].SetParent(model, false); wingTips[0].localPosition = new Vector3(-8f * U, -2f * U, 0f);
            wingTips[1] = new GameObject("TipR").transform; wingTips[1].SetParent(model, false); wingTips[1].localPosition = new Vector3(8f * U, -2f * U, 0f);
        }

        /// <summary>Put the plane somewhere and hand the controls over.</summary>
        public void Place(Vector3 pos, float yawDeg, Carrier dockedIn, float speed, bool takeControl = true)
        {
            transform.position = pos; lastOk = pos;
            yaw = yawDeg * Mathf.Deg2Rad; pitch = 0f; bank = 0f;
            docked = dockedIn; speedU = speed; launchT = 0f; thr = IsHeli ? 0.5f : 0.73f; heat = 0f; Overheated = false; boost = 1f; boostOut = false;
            fwdU = IsHeli ? speed : 0f; vyU = 0f; Landed = false;
            bombs = maxBombs; wingDamage[0] = wingDamage[1] = 0f;
            health.ResetFull();
            Velocity = Vector3.zero;
            ApplyPose();
            gameObject.SetActive(true);
            health.isAir = dockedIn == null;
            Active = takeControl;
            if (!takeControl) return;
            // start the camera behind the plane rather than sliding in from wherever it was
            if (cam != null)
            {
                if (cockpit) { cam.transform.position = transform.position + Vector3.up * 3.4f * U; cam.transform.rotation = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f); }
                else { cam.transform.position = transform.position - Forward * 12f + Vector3.up * 3.8f; cam.transform.LookAt(transform.position + Forward * 30f); }
            }
        }

        /// <summary>Hand the controls back: the plane stays where it is, parked, until someone gets in again.</summary>
        public void Park()
        {
            Active = false;
            if (model != null) foreach (var r in model.GetComponentsInChildren<Renderer>()) r.enabled = true;
            Velocity = Vector3.zero;
            health.isAir = false;
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
            if (launchT <= 0f && PlayerManager.Instance != null && PlayerManager.Instance.hangarAt != null) hs = PlayerManager.Instance.hangarAt(transform.position);
            if (hs != null || docked != null) { DockStep(hs != null ? hs : docked, sx, boosting, dt); }
            else if (IsHeli) FlyHeli(sx, sy, boosting, dt);
            else Fly(sx, sy, boosting, dt);
            if (rotor != null) { rotor.Rotate(0f, (Landed && thr < 0.2f ? 200f : 1800f) * dt, 0f); if (tailRotor != null) tailRotor.Rotate(0f, 0f, 2600f * dt); }
            if (propT != null) propT.Rotate(0f, 0f, 2400f * dt);

            // guns and bombs
            heat = Mathf.Max(0f, heat - dt * 0.28f);
            if (Overheated && heat < 0.35f) Overheated = false;
            fireT -= dt;
            if (input.Fire && !Overheated && fireT <= 0f && docked == null && !Landed) Shoot();
            bombCd -= dt;
            if (input.BombPressed && docked == null && bombs > 0 && bombCd <= 0f) DropBomb();
            if (input.InteractPressed)
            {
                var pm = PlayerManager.Instance;
                float obst = gm != null ? gm.ObstacleHeight(transform.position.x, transform.position.z) : (GameState.GroundHeight != null ? GameState.GroundHeight(transform.position.x, transform.position.z) : 0f);
                if (docked != null || (IsHeli && Landed)) { if (pm != null) pm.ExitToFoot(); return; }
                else if (pm != null && transform.position.y > obst + 18f * U) { pm.BailOut(); return; }
                else if (GameState.Hud != null) GameState.Hud.Announce(IsHeli ? "Land first to get out" : "Land in a hangar to get out");
            }
            if (input.ViewPressed) cockpit = !cockpit;
            EdgeDoom(dt);
            ApplyPose();
            if (cockpit) CockpitCamera(dt); else ChaseCamera(dt);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.enabled = !cockpit;
        }

        float edgeT;
        /// <summary>The original's edgeDoom (off in Galactic Assault): leave the island and you are warned, then lost over the sea.</summary>
        void EdgeDoom(float dt)
        {
            if (GalacticMode.Instance != null) return;
            float limit = World3.WORLD3 * World3.U * World3.Spread * 0.55f;
            var p = transform.position;
            float d = Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.z));
            if (d < limit) { edgeT = 0f; return; }
            edgeT += dt;
            if (GameState.Hud != null && Mathf.Repeat(edgeT, 2f) < dt) GameState.Hud.Announce("Turn back! You are leaving the island");
            if (edgeT > 8f || d > limit * 1.6f) Crash(p);
        }

        void CockpitCamera(float dt)
        {
            if (cam == null) return;
            var gm = GalacticMode.Instance;
            // eye at p + up*3.4 units, looking along the aircraft, rolled with the bank (0.42 of it for a plane, 0.5 heli)
            var eye = transform.position + Vector3.up * 3.4f * U + Forward * 1f * U;
            var rot = Quaternion.Euler(-pitch * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg, -bank * 50f * (IsHeli ? 0.5f : 0.42f));
            cam.transform.position = eye;
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Mathf.Min(1f, 16f * dt));
            if (gm != null && gm.shake > 0f) cam.transform.position += Random.insideUnitSphere * gm.shake * 0.25f;
            float over = Mathf.Clamp01((speedU - (IsHeli ? 170f : 190f)) / 300f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 58f * (1f + 0.27f * over), Mathf.Min(1f, 4f * dt));
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
            var v = Forward * speedU * U * GameTuning.Pace;
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

        /// <summary>The helicopter: yaw with the stick, push forward to fly forward, the throttle slider is the collective
        /// (half way hovers), boost doubles the engine. It lands on anything flat and can be left there.</summary>
        void FlyHeli(float sx, float sy, bool boosting, float dt)
        {
            float power = boosting ? 2.6f : 1f;
            yaw += sx * 1.7f * dt;
            float fwdTarget = -sy * 205f * power;
            fwdU += (fwdTarget - fwdU) * Mathf.Min(1f, 2.7f * dt);
            float vyTarget = (thr - 0.5f) * 2f * 150f * power;
            if (Mathf.Abs(thr - 0.5f) < 0.06f) vyTarget = 0f;   // the hover notch
            vyU = Mathf.MoveTowards(vyU, vyTarget, 4.6f * 150f * dt);
            if (AltitudeU > 4700f && vyU > 0f) vyU = 0f;
            pitch = Mathf.Lerp(pitch, -fwdU / 205f * 0.35f, Mathf.Min(1f, 3f * dt));
            bank += (Mathf.Clamp(sx, -1f, 1f) * 0.5f - bank) * Mathf.Min(1f, 2.6f * dt);
            var flatFwd = Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward;
            var v = (flatFwd * fwdU * U + Vector3.up * vyU * U) * GameTuning.Pace;
            speedU = Mathf.Abs(fwdU);
            Velocity = v;
            var from = transform.position; var to = from + v * dt;
            var gm = GalacticMode.Instance;
            float ground = GameState.GroundHeight != null ? GameState.GroundHeight(to.x, to.z) : 0f;
            if (PlayerManager.Instance != null && PlayerManager.Instance.cockpitFloor != null) { float f = PlayerManager.Instance.cockpitFloor(to); if (!float.IsNaN(f)) ground = Mathf.Max(ground, f); }
            // the skids
            if (to.y <= ground + 0.9f && vyU <= 0f)
            {
                if (vyU < -120f) { Crash(to); return; }
                to.y = ground + 0.9f; vyU = 0f; Landed = true;
                if (fwdU != 0f) fwdU = Mathf.MoveTowards(fwdU, 0f, 300f * dt);
                to = from + (flatFwd * fwdU * U) * dt; to.y = ground + 0.9f;
                pitch = Mathf.Lerp(pitch, 0f, Mathf.Min(1f, 4f * dt)); bank *= Mathf.Max(0f, 1f - 4f * dt);
            }
            else Landed = false;
            // walls, trees, the carriers
            RaycastHit hit; var seg = to - from; float len = seg.magnitude;
            if (len > 0.0001f && Physics.SphereCast(from, 0.9f, seg / len, out hit, len + 0.3f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
            {
                var ship = hit.collider.GetComponentInParent<Carrier>();
                if (ship != null && ship.IsShield(hit.collider)) { health.TakeDamage(new DamageInfo { amount = 22f, point = hit.point, direction = seg, source = ship.gameObject }); fwdU *= -0.5f; to = lastOk; if (GameState.Hud != null) GameState.Hud.Announce("Their hangar is shielded: destroy the shield generator on the ground first"); }
                else if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f && vyU <= 0f) { to.y = hit.point.y + 0.9f; vyU = 0f; Landed = true; }
                else { to = lastOk; fwdU *= -0.3f; hurtTick -= dt; if (hurtTick <= 0f) { hurtTick = 0.5f; health.TakeDamage(new DamageInfo { amount = 4f, point = hit.point, direction = seg, source = gameObject }); } }
            }
            else lastOk = from;
            transform.position = to;
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
            p.y = Mathf.Lerp(p.y, floor + (IsHeli ? 6f : 4f) * U, Mathf.Min(1f, 3f * dt));
            fwdU *= Mathf.Max(0f, 1f - 2.5f * dt); vyU = 0f;
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
                docked = null; launchT = 1.6f; speedU = IsHeli ? 0f : 230f;
                if (IsHeli) { fwdU = 90f; thr = 0.6f; }
                Velocity = Forward * speedU * U * GameTuning.Pace;
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
            if (IsHeli) dir = Quaternion.Euler(8f, 0f, 0f) * Quaternion.Euler(0f, yaw * Mathf.Rad2Deg, 0f) * Vector3.forward;
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
            // never through a wall: pull in when the hangar is in the way
            RaycastHit hit; var dir = want - transform.position; float len = dir.magnitude;
            if (len > 0.01f && Physics.SphereCast(transform.position, 0.4f, dir / len, out hit, len, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore)) want = transform.position + dir / len * Mathf.Max(2.5f, hit.distance - 0.3f);
            cam.transform.position = Vector3.SmoothDamp(cam.transform.position, want, ref camVel, 0.12f);
            var rot = Quaternion.LookRotation(lookAt - cam.transform.position, Quaternion.AngleAxis(-bank * 30f, Forward) * Vector3.up);
            cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Mathf.Min(1f, 8f * dt));
            var gm = GalacticMode.Instance;
            if (gm != null && gm.shake > 0f) cam.transform.position += Random.insideUnitSphere * gm.shake * 0.4f;
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 58f * 1.1f * (1f + 0.27f * Mathf.Clamp01((speedU - 190f) / 300f)), Mathf.Min(1f, 4f * dt));
        }
    }
}
