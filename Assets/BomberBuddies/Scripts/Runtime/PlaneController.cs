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
        public enum Craft { Plane, Heli, Heli2, AC130, Saucer, B52, Bomber2, Transport, Drone, Dragon, Ptero, Skyjack }
        public bool IsBeast { get { return craft == Craft.Dragon || craft == Craft.Ptero; } }
        Transform wingL, wingR, neckT, tailT; float flap;
        public bool IsSaucer { get { return craft == Craft.Saucer; } }
        /// <summary>Abduction: raid — soldiers pulled up the beam so far.</summary>
        public static int Abducted; Health beaming; float beamT; Transform beamFx;
        public Craft craft = Craft.Plane;
        public bool IsHeli { get { return craft == Craft.Heli || craft == Craft.Heli2 || craft == Craft.Saucer || craft == Craft.Transport || craft == Craft.Drone || craft == Craft.Skyjack; } }
        public bool IsAC130 { get { return craft == Craft.AC130; } }
        public int Seats { get { return craft == Craft.Heli2 || craft == Craft.AC130 || craft == Craft.Bomber2 || craft == Craft.Transport ? 2 : 1; } }
        /// <summary>1 pilot, 2 gunner. In the gunner seat the AI flies (a hover for the gunship, an orbit for the AC-130).</summary>
        public int seat = 1;
        public bool Gunner { get { return Seats == 2 && seat == 2; } }
        public string CraftName { get { return craft == Craft.Dragon ? "dragon" : craft == Craft.Ptero ? "pterodactyl" : craft == Craft.Skyjack ? "Skyjack" : craft == Craft.B52 ? "B-52" : craft == Craft.Bomber2 ? "two-seat bomber" : craft == Craft.Transport ? "transport helicopter" : craft == Craft.Drone ? "bomber drone" : craft == Craft.Saucer ? "flying saucer" : craft == Craft.AC130 ? "AC-130 gunship" : craft == Craft.Heli2 ? "two-seat gunship" : IsHeli ? "attack helicopter" : "fighter-bomber"; } }
        public string ProgressId { get { return craft == Craft.Dragon ? "dragon" : craft == Craft.Ptero ? "ptero" : craft == Craft.Skyjack ? "fiend" : craft == Craft.B52 ? "b52" : craft == Craft.Bomber2 ? "bomber2" : craft == Craft.Transport ? "transport" : craft == Craft.Drone ? "drone" : craft == Craft.Saucer ? "ufo" : craft == Craft.AC130 ? "ac130" : craft == Craft.Heli2 ? "heli2" : IsHeli ? "heli" : "plane"; } }
        // the gunner's gun: where it looks (degrees, relative to the aircraft), and which of the AC-130's three it is
        public float gunYaw, gunPitch = -20f; public int ac130Gun;
        public static readonly string[] AC130Guns = { "25mm Gatling", "40mm Bofors", "105mm howitzer" };
        float gunT, aiGunT; Vector3 orbitCentre; bool orbitSet; Transform gunMount;
        /// <summary>The original's damage states: a hit engine (half power) and a hit tail (the heli spins, the plane will not hold a line).</summary>
        public bool engineHit, tailHit; float spinDir = 1f;
        public Health health;
        public Camera cam;
        public MaterialLibrary lib;
        public bool Active { get; private set; }
        public Carrier docked;
        public float speedU = 0f, yaw, pitch, bank, thr = 0.73f, boost = 1f, launchT, heat;
        public int bombs = 8, maxBombs = 8;
        /// <summary>The hangar's upgrades and gun for this craft (original units).</summary>
        public Progress.Stats st;
        float bombT, rocketT;
        public bool IsPlayers { get { return PlayerManager.Instance != null && PlayerManager.Instance.craft.Contains(this); } }
        public bool Overheated { get; private set; }
        public bool Airborne { get { return docked == null; } }
        public Vector3 Velocity { get; private set; }
        public float AltitudeU { get { return transform.position.y / U; } }

        Transform model, muzzleL, muzzleR, propT, rotor, tailRotor;
        float fwdU, vyU;            // the helicopter's forward and vertical speeds (original units)
        public bool Landed { get; private set; }
        public bool Boosting { get; private set; }
        /// <summary>Cockpit view (the original's default) or the chase camera behind the aircraft.</summary>
        public bool cockpit = true;
        float fireT, bombCd, hurtTick, boostLock, camShakeT;
        bool boostOut, lastFire;
        Vector3 lastOk, camVel;
        Transform[] wingTips = new Transform[2];
        float[] wingDamage = new float[2];

        public static PlaneController Build(Transform parent, MaterialLibrary lib, Camera cam, Craft craft = Craft.Plane)
        {
            var go = new GameObject(craft == Craft.Saucer ? "Saucer" : craft == Craft.AC130 ? "AC-130" : craft == Craft.Heli2 ? "Gunship" : craft == Craft.Heli ? "Helicopter" : "Plane");
            go.transform.SetParent(parent, false);
            go.layer = 8;
            var p = go.AddComponent<PlaneController>();
            p.lib = lib; p.cam = cam; p.craft = craft;
            if (craft == Craft.Heli || craft == Craft.Saucer) p.thr = 0.5f;
            p.health = go.AddComponent<Health>(); p.health.max = 100f; p.health.current = 100f; p.health.team = 0; p.health.aimHeight = 0f; p.health.isAir = true;
            var zone = go.AddComponent<HitZone>(); zone.owner = p.health; zone.multiplier = 1f;
            var col = go.AddComponent<SphereCollider>(); col.radius = 1.3f;
            if (p.IsSaucer) p.BuildSaucerModel(); else if (p.IsBeast) p.BuildBeastModel(); else if (craft == Craft.Skyjack) p.BuildBoardModel(); else if (p.IsHeli) p.BuildHeliModel(); else p.BuildModel();
            if (craft == Craft.Dragon) col.radius = 3f;
            if (craft == Craft.Heli2) p.model.localScale = Vector3.one * 1.2f;
            if (craft == Craft.Transport) { p.model.localScale = new Vector3(1.5f, 1.5f, 1.8f); col.radius = 2.2f; }
            if (craft == Craft.Drone) { p.model.localScale = Vector3.one * 0.3f; col.radius = 0.5f; p.cockpit = true; }
            if (craft == Craft.B52) { p.model.localScale = new Vector3(2.6f, 2.2f, 2.6f); col.radius = 3.4f; }
            if (craft == Craft.Bomber2) { p.model.localScale = Vector3.one * 1.3f; col.radius = 1.7f; }
            if (craft == Craft.AC130) { p.model.localScale = Vector3.one * 2.6f; col.radius = 3.2f; }
            p.gunMount = new GameObject("GunMount").transform; p.gunMount.SetParent(p.model, false);
            p.gunMount.localPosition = craft == Craft.AC130 ? new Vector3(-2.6f * U, -0.5f * U, 0f) : craft == Craft.Bomber2 ? new Vector3(0f, -4f * U, -2f * U) : craft == Craft.Transport ? new Vector3(6f * U, -1f * U, 0f) : new Vector3(0f, -4f * U, 9f * U);
            p.ApplyLoadout();
            p.health.OnDamaged += info =>
            {
                if (GameState.Hud != null && p.Active) GameState.Hud.Damaged(info.amount);
                float f = p.health.Fraction;
                if (!p.engineHit && f < 0.45f) { p.engineHit = true; if (p.Active && GameState.Hud != null) GameState.Hud.Announce(p.IsHeli ? "Engine hit: losing lift" : "Engine hit: losing power", 2.5f); }
                if (!p.tailHit && f < 0.22f && Random.value < 0.5f) { p.tailHit = true; p.spinDir = Random.value < 0.5f ? -1f : 1f; if (p.Active && GameState.Hud != null) GameState.Hud.Announce(p.IsHeli ? "Tail rotor hit! You are spinning" : "Tail fin shot off: she will not hold a line", 3f); }
            };
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

        void BuildBeastModel()
        {
            model = new GameObject("Model").transform; model.SetParent(transform, false);
            var inner = Dragon.BuildDragonModel(model, lib, 8, out wingL, out wingR, out neckT, out tailT);
            if (craft == Craft.Ptero) { inner.localScale = new Vector3(0.45f, 0.35f, 0.5f); foreach (var r in inner.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(0.6f, 0.7f, 0.5f, 1f)); }
            muzzleL = new GameObject("MuzzleL").transform; muzzleL.SetParent(model, false); muzzleL.localPosition = new Vector3(0f, 18f * U, 62f * U) * (craft == Craft.Ptero ? 0.45f : 1f);
            muzzleR = muzzleL;
            wingTips[0] = new GameObject("TipL").transform; wingTips[0].SetParent(model, false); wingTips[0].localPosition = new Vector3(-60f * U, 18f * U, 0f);
            wingTips[1] = new GameObject("TipR").transform; wingTips[1].SetParent(model, false); wingTips[1].localPosition = new Vector3(60f * U, 18f * U, 0f);
        }
        /// <summary>Skyjack's jet board: a plank with two jets and the pirate standing on it.</summary>
        void BuildBoardModel()
        {
            model = new GameObject("Model").transform; model.SetParent(transform, false);
            var mat = lib != null ? lib.Get("lowpoly") : null; var mb = new MeshBuilder();
            mb.DBox(0f, 0f, 0f, 10f, 1.2f, 22f, MeshBuilder.Rgb(60, 60, 70)); mb.DBox(-3.5f, -1.5f, -8f, 2.5f, 2.5f, 6f, MeshBuilder.Rgb(120, 120, 130)); mb.DBox(3.5f, -1.5f, -8f, 2.5f, 2.5f, 6f, MeshBuilder.Rgb(120, 120, 130));
            mb.DBox(-3.5f, -1.5f, -11.5f, 1.5f, 1.5f, 1f, MeshBuilder.Emissive(MeshBuilder.Rgb(120, 200, 255))); mb.DBox(3.5f, -1.5f, -11.5f, 1.5f, 1.5f, 1f, MeshBuilder.Emissive(MeshBuilder.Rgb(120, 200, 255)));
            mb.DBox(0f, 7f, 0f, 5f, 12f, 3f, MeshBuilder.Rgb(50, 40, 60)); mb.DBox(0f, 15f, 0f, 3.5f, 4f, 3.5f, MeshBuilder.Rgb(214, 170, 130)); mb.DBox(0f, 17.5f, 0f, 4f, 1.5f, 4f, MeshBuilder.Rgb(160, 30, 40));
            mb.Spawn("Board", model, mat, false).layer = 8;
            muzzleL = new GameObject("MuzzleL").transform; muzzleL.SetParent(model, false); muzzleL.localPosition = new Vector3(-2f * U, 9f * U, 4f * U);
            muzzleR = new GameObject("MuzzleR").transform; muzzleR.SetParent(model, false); muzzleR.localPosition = new Vector3(2f * U, 9f * U, 4f * U);
            wingTips[0] = new GameObject("TipL").transform; wingTips[0].SetParent(model, false); wingTips[0].localPosition = new Vector3(-5f * U, 0f, -10f * U);
            wingTips[1] = new GameObject("TipR").transform; wingTips[1].SetParent(model, false); wingTips[1].localPosition = new Vector3(5f * U, 0f, -10f * U);
        }

        void BuildSaucerModel()
        {
            model = new GameObject("Model").transform;
            model.SetParent(transform, false);
            CraftModels.Saucer(model, lib, 8);
            muzzleL = new GameObject("MuzzleL").transform; muzzleL.SetParent(model, false); muzzleL.localPosition = new Vector3(-10f * U, -3f * U, 10f * U);
            muzzleR = new GameObject("MuzzleR").transform; muzzleR.SetParent(model, false); muzzleR.localPosition = new Vector3(10f * U, -3f * U, 10f * U);
            wingTips[0] = new GameObject("TipL").transform; wingTips[0].SetParent(model, false); wingTips[0].localPosition = new Vector3(-14f * U, -2f * U, 0f);
            wingTips[1] = new GameObject("TipR").transform; wingTips[1].SetParent(model, false); wingTips[1].localPosition = new Vector3(14f * U, -2f * U, 0f);
        }

        /// <summary>The gravity beam (ufoAbduct3 for the player): hold Bomb while hovering low and the nearest soldier under you is pulled up into the hull.</summary>
        void Beam(float dt)
        {
            var input = InputBridge.Instance; bool want = input != null && input.BombHeld && docked == null && !Landed;
            if (want && (beaming == null || !beaming.alive))
            {
                beaming = null; float bd = 14f;
                foreach (var h in Health.All)
                {
                    if (h == null || !h.alive || h.team == 0 || h.isAir || h.GetComponent<EnemySoldier>() == null) continue;
                    var d = h.transform.position - transform.position; if (d.y > -2f || d.y < -60f) continue; d.y = 0f;
                    if (d.magnitude < bd) { bd = d.magnitude; beaming = h; }
                }
                beamT = 0f;
            }
            if (!want) { beaming = null; beamT = 0f; }
            if (beaming != null)
            {
                if (beamFx == null)
                {
                    var b = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(b.GetComponent<Collider>()); b.name = "Beam"; b.transform.SetParent(model, false); b.layer = 8;
                    var m = new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.SetColor("_BaseColor", new Color(0.6f, 1f, 0.8f, 0.3f)); m.SetFloat("_Surface", 1f); m.renderQueue = 3000; m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha); m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha); m.SetInt("_ZWrite", 0); m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    b.GetComponent<Renderer>().sharedMaterial = m; beamFx = b.transform; Sfx.Play("ufo_hum", 0.5f, 1.2f, transform.position);
                }
                float h = Mathf.Max(2f, transform.position.y - beaming.transform.position.y);
                beamFx.localPosition = new Vector3(0f, -h * 0.5f, 0f); beamFx.localScale = new Vector3(2f, h * 0.5f, 2f);
                beamT += dt;
                var es = beaming.GetComponent<EnemySoldier>(); if (es != null) es.lifted = true;
                var dest = transform.position - Vector3.up * 1f;
                beaming.transform.position = Vector3.MoveTowards(beaming.transform.position, dest, (3f + beamT) * dt);
                if (Vector3.Distance(beaming.transform.position, dest) < 1.2f)
                {
                    Abducted++; GameState.Earn(80);
                    if (GameState.Hud != null) GameState.Hud.Comms("Got one  (" + Abducted + ")");
                    Sfx.Play("scream", 0.5f, 1.3f, transform.position); Sfx.Play("squeak", 0.4f, 0.8f, transform.position);
                    Destroy(beaming.gameObject); beaming = null;
                }
            }
            else if (beamFx != null) { Destroy(beamFx.gameObject); beamFx = null; }
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
            bombs = maxBombs; wingDamage[0] = wingDamage[1] = 0f; seat = 1; engineHit = false; tailHit = false; orbitSet = false; gunYaw = IsAC130 ? -90f : 0f; gunPitch = IsAC130 ? -40f : -15f;
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
        /// <summary>Free look (L1 / C held): the guns swivel a little so the reticle walks across the screen (gunAim). x yaw, y pitch, degrees.</summary>
        public Vector2 AimOffset;
        /// <summary>CCIP: where a bomb let go now would land (the same drop as DropBomb, stepped forward until it meets the ground).</summary>
        public Vector3? BombImpact()
        {
            if (docked != null || Landed) return null;
            var p = transform.position - Vector3.up * 0.9f; var v = Velocity; v.y = Mathf.Min(v.y, 0f) - 25f * U;
            for (int i = 0; i < 400; i++)
            {
                v.y -= GalacticMode.BombG * 0.05f; p += v * 0.05f;
                float g = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : 0f;
                if (World3.Instance != null) g = Mathf.Max(g, World3.Instance.ObstacleMetres(p.x, p.z));
                if (p.y <= g) { p.y = g; return p; }
            }
            return null;
        }

        public Vector3 AimDir { get { return Quaternion.Euler(-pitch * Mathf.Rad2Deg - AimOffset.y, yaw * Mathf.Rad2Deg + AimOffset.x, 0f) * Vector3.forward; } }

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

            if (Seats == 2 && input.SeatPressed && docked == null)
            {
                seat = 3 - seat; orbitSet = false;
                if (GameState.Hud != null) GameState.Hud.Announce(seat == 2 ? (IsAC130 ? "Gunner seat  ·  X cycles the gun" : "Gunner seat  ·  the AI holds the aircraft") : "Pilot seat", 2.5f);
            }
            if (Gunner) { GunnerUpdate(input, dt); return; }
            // the stick: keyboard/pad plus mouse or touch drag
            float lx = input.Look.x, ly = input.Look.y;
            if (input.LookHeld)
            {
                // the right stick / mouse walks the reticle; the aircraft keeps flying straight on the left stick
                float zk = input.Aim ? 0.45f : 1f;
                AimOffset.x = Mathf.Clamp(AimOffset.x + lx * 0.9f * zk, -35f, 35f);
                AimOffset.y = Mathf.Clamp(AimOffset.y + ly * 0.8f * zk, -26f, 23f);
                lx = 0f; ly = 0f;
            }
            else { float k = Mathf.Min(1f, 6f * dt); AimOffset *= 1f - k; }
            float sx = Mathf.Clamp(input.Move.x + lx * 0.02f, -1f, 1f);
            float sy = Mathf.Clamp(input.Move.y - ly * 0.02f, -1f, 1f);
            if (Mathf.Abs(sx) < 0.12f) sx = 0f; if (Mathf.Abs(sy) < 0.12f) sy = 0f;
            thr = Mathf.Clamp01(thr + input.Throttle * 0.9f * dt);
            bool boosting = input.Sprint && !boostOut && boost > 0f && docked == null; Boosting = boosting;
            float bsec = st != null ? st.boostSec : 2.4f, brk = st != null ? st.boostRegenK : 1f;
            if (boosting) { boost -= dt / bsec; if (boost <= 0f) { boost = 0f; boostOut = true; } }
            else { boost = Mathf.Min(1f, boost + dt * brk / (bsec * 2.3f)); if (boostOut && boost > 0.25f) boostOut = false; }
            if (st != null)
            {
                if (st.regen > 0f && health.alive && health.current < health.max) health.current = Mathf.Min(health.max, health.current + st.regen * dt);
                if (bombs < maxBombs) { bombT += dt; if (bombT >= st.bombReload * 4f) { bombT = 0f; bombs++; } } else bombT = 0f;
                rocketT -= dt;
            }

            if (launchT > 0f) launchT -= dt;
            health.isAir = docked == null;
            Carrier hs = null;
            if (launchT <= 0f && PlayerManager.Instance != null && PlayerManager.Instance.hangarAt != null) hs = PlayerManager.Instance.hangarAt(transform.position);
            if (hs != null || docked != null) { DockStep(hs != null ? hs : docked, sx, boosting, dt); }
            else if (IsHeli) FlyHeli(sx, sy, boosting, dt);
            else Fly(sx, sy, boosting, dt);
            if (rotor != null) { rotor.Rotate(0f, (Landed && thr < 0.2f ? 200f : 1800f) * dt, 0f); if (tailRotor != null) tailRotor.Rotate(0f, 0f, 2600f * dt); }
            if (IsBeast) { flap += dt * (boosting ? 6f : 3.5f + thr * 2f); float wa = Mathf.Sin(flap) * 30f; if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, wa); if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, -wa); if (tailT != null) tailT.localRotation = Quaternion.Euler(0f, 180f + Mathf.Sin(flap * 0.5f) * 10f, 0f); }
            if (propT != null) propT.Rotate(0f, 0f, 2400f * dt);
            EngineSound(boosting);

            // guns and bombs
            heat = Mathf.Max(0f, heat - dt * 0.28f);
            if (Overheated && heat < 0.35f) Overheated = false;
            fireT -= dt;
            if (input.Fire && !Overheated && fireT <= 0f && docked == null && !Landed) Shoot();
            if (Seats == 2 && docked == null && !Landed) AiGunner(dt);
            bombCd -= dt;
            if (IsSaucer) Beam(dt); else if (input.BombPressed && docked == null && bombs > 0 && bombCd <= 0f) DropBomb();
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
            if (GameState.Hud != null && Mathf.Repeat(edgeT, 2f) < dt) GameState.Hud.Announce("Turn around or something bad will happen");
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
            float tk = st != null ? st.turnK : 1f, thk = (st != null ? st.thrustK : 1f) * (engineHit ? 0.5f : 1f);
            if (tailHit) yaw += spinDir * 0.35f * dt + Mathf.Sin(Time.time * 3f) * 0.3f * dt;   // she will not hold a line
            yaw += sx * 1.35f * k * tk * dt;
            pitch += -sy * 1.5f * k * tk * (stalled ? 0.35f : 1f) * dt;
            if (sy == 0f) pitch -= pitch * 0.55f * dt;      // auto-level
            if (stalled) pitch -= 1.4f * dt;
            if (AltitudeU > 4700f && pitch > -0.2f) pitch -= 1.8f * dt;
            pitch = Mathf.Clamp(pitch, -1.3f, 1.3f);
            bank += (Mathf.Clamp(sx, -1.15f, 1.15f) * 0.95f - bank) * Mathf.Min(1f, 2.6f * dt);
            speedU += (140f * thk * (0.45f + 0.75f * thr) + (boosting ? 460f : 0f) - 0.8f * speedU - 110f * Mathf.Sin(pitch) - Mathf.Abs(sx) * 22f) * dt;
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
            float groundHere = GameState.GroundHeight != null ? GameState.GroundHeight(transform.position.x, transform.position.z) : (gm != null ? gm.TerrainHeight(transform.position.x, transform.position.z) : -999f);
            if (transform.position.y < groundHere + 0.8f) { Crash(transform.position); return; }
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
            float power = (boosting ? 2.6f : 1f) * (st != null ? st.thrustK : 1f) * (engineHit ? 0.6f : 1f);
            yaw += sx * 1.7f * (st != null ? st.turnK : 1f) * dt;
            if (tailHit) yaw += spinDir * 1.6f * dt;   // the tail rotor is gone: round and round
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

        // ---------- the second seat ----------
        /// <summary>The gunner: the AI flies, you look through the gun. Heli: the chin gun swings most of the way round; AC-130: the left side.</summary>
        void GunnerUpdate(InputBridge input, float dt)
        {
            float zk = input.Aim ? 0.35f : 1f;
            gunYaw += input.Look.x * 0.5f * zk; gunPitch += input.Look.y * 0.45f * zk;
            if (IsAC130) { gunYaw = Mathf.Clamp(gunYaw, -135f, -45f); gunPitch = Mathf.Clamp(gunPitch, -78f, -4f); }
            else { gunYaw = Mathf.Clamp(gunYaw, -120f, 120f); gunPitch = Mathf.Clamp(gunPitch, -75f, 22f); }
            AimOffset = Vector2.zero;
            if (IsAC130 && (input.KickPressed || input.ReloadPressed)) { ac130Gun = (ac130Gun + 1) % 3; if (GameState.Hud != null) GameState.Hud.Announce(AC130Guns[ac130Gun], 1.5f); }

            if (IsHeli) AiHover(dt); else AiOrbit(dt);
            EngineSound(false);
            if (rotor != null) { rotor.Rotate(0f, 1800f * dt, 0f); if (tailRotor != null) tailRotor.Rotate(0f, 0f, 2600f * dt); }
            if (propT != null) propT.Rotate(0f, 0f, 2400f * dt);

            heat = Mathf.Max(0f, heat - dt * 0.28f); if (Overheated && heat < 0.35f) Overheated = false;
            gunT -= dt; bombCd -= dt;
            if (input.Fire && gunT <= 0f && !Overheated) FireGunnerGun(GunnerDir);
            if (input.BombPressed && bombs > 0 && bombCd <= 0f && !IsAC130) DropBomb();
            if (input.ViewPressed) cockpit = !cockpit;
            if (input.InteractPressed && GameState.Hud != null) GameState.Hud.Announce("Swap back to the pilot seat to get out (d-pad left / B)");
            EdgeDoom(dt);
            ApplyPose();
            GunnerCamera(dt);
            foreach (var r in model.GetComponentsInChildren<Renderer>()) r.enabled = !cockpit;
        }

        public Vector3 GunnerDir { get { return transform.rotation * Quaternion.Euler(-gunPitch, gunYaw, 0f) * Vector3.forward; } }

        void GunnerCamera(float dt)
        {
            if (cam == null) return;
            var mount = gunMount != null ? gunMount.position : transform.position;
            var rot = Quaternion.LookRotation(GunnerDir, transform.up);
            if (cockpit) { cam.transform.position = mount + GunnerDir * 0.3f; cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Mathf.Min(1f, 18f * dt)); }
            else { cam.transform.position = Vector3.Lerp(cam.transform.position, mount - GunnerDir * 6f + transform.up * 2f, Mathf.Min(1f, 8f * dt)); cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, rot, Mathf.Min(1f, 10f * dt)); }
            var input = InputBridge.Instance;
            float fov = input != null && input.Aim ? (IsAC130 ? 14f : 24f) : (IsAC130 ? 36f : 58f);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, fov, Mathf.Min(1f, 6f * dt));
        }

        /// <summary>The gunner's gun. Heli: the chin gun from the hangar. AC-130: 25 mm (fast), 40 mm (shells), 105 mm (one big round).</summary>
        void FireGunnerGun(Vector3 dir)
        {
            var from = gunMount != null ? gunMount.position : transform.position;
            if (IsAC130)
            {
                switch (ac130Gun)
                {
                    case 0: gunT = 0.11f; heat += 0.02f; if (Projectiles.Instance != null) Projectiles.Instance.FireRound(from, Spread(dir, 1.2f), 720f * U, 30f * GalacticMode.K, 1400f * U, 0, gameObject, new Color(1f, 0.85f, 0.5f), 0.05f); break;
                    case 1: gunT = 0.55f; heat += 0.05f; if (Projectiles.Instance != null) Projectiles.Instance.FireShell(from, Spread(dir, 0.6f), 700f * U, 10f * U, 28f * U, 150f * GalacticMode.K, 0, gameObject, false, 0f); break;
                    default: gunT = 3.2f; heat += 0.2f; if (Projectiles.Instance != null) Projectiles.Instance.FireShell(from, Spread(dir, 0.3f), 620f * U, 30f * U, 70f * U, 650f * GalacticMode.K, 0, gameObject, false, 0f); camShakeT = 0.3f; break;
                }
            }
            else
            {
                var g = st != null ? st.gun : Progress.GunDef("m2");
                gunT = st != null ? st.fireInt : g.interval; heat += st != null ? st.heatShot : g.heat;
                float dmg = (st != null ? st.dmg : g.dmg) * GalacticMode.K;
                if (Projectiles.Instance != null)
                {
                    if (g.splash > 0f) Projectiles.Instance.FireShell(from, Spread(dir, g.spread * 0.4f), g.speed * U, g.splash * 0.5f * U, g.splash * 1.6f * U, dmg, 0, gameObject, false, 0f);
                    else Projectiles.Instance.FireRound(from, Spread(dir, g.spread * 0.4f), g.speed * U, dmg, g.range * U, 0, gameObject, new Color(1f, 0.9f, 0.6f), 0.04f);
                }
            }
            if (heat >= 1f) { Overheated = true; heat = 1f; }
            Sfx.Play(IsAC130 ? (ac130Gun == 0 ? "gun_lmg" : "gun_cannon") : Sfx.GunSound(st != null ? st.gun.id : "m2"), IsAC130 && ac130Gun == 2 ? 1f : 0.6f, IsAC130 && ac130Gun == 2 ? 0.7f : 1f, from);
            if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(from, dir, IsAC130 && ac130Gun == 2 ? 3f : 1.2f);
            if (GameState.Hud != null) GameState.Hud.OnShot();
        }

        static Vector3 Spread(Vector3 dir, float deg) { return Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, deg), 0f, 0f) * dir; }

        /// <summary>While you fly a two-seater the AI works the second gun at whatever is nearest.</summary>
        void AiGunner(float dt)
        {
            aiGunT -= dt; if (aiGunT > 0f) return;
            var from = gunMount != null ? gunMount.position : transform.position;
            var t = Health.Nearest(0, from, IsAC130 ? 900f : 320f);
            if (t == null) { aiGunT = 0.4f; return; }
            var dir = (t.transform.position + Vector3.up * t.aimHeight - from).normalized;
            var local = Quaternion.Inverse(transform.rotation) * dir;
            float ly = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            if (IsAC130 ? (ly > -150f && ly < -30f) : Mathf.Abs(ly) < 120f)
            {
                int keep = ac130Gun; ac130Gun = 0;
                FireGunnerGun(Spread(dir, 1.5f));
                ac130Gun = keep; aiGunT = IsAC130 ? 0.14f : 0.12f;
            }
            else aiGunT = 0.3f;
        }

        /// <summary>The AI pilot of a gunship: holds a hover, turns the nose toward the nearest enemy, and edges closer when it is far.</summary>
        void AiHover(float dt)
        {
            var t = Health.Nearest(0, transform.position, 1200f);
            float sx = 0f, sy = 0f;
            if (t != null)
            {
                var d = t.transform.position - transform.position; float dist = new Vector2(d.x, d.z).magnitude;
                float want = Mathf.Atan2(d.x, d.z); float diff = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg);
                sx = Mathf.Clamp(diff / 30f, -0.6f, 0.6f);
                sy = dist > 260f ? -0.45f : dist < 120f ? 0.35f : 0f;
            }
            float ground = GameState.GroundHeight != null ? GameState.GroundHeight(transform.position.x, transform.position.z) : 0f;
            float wantH = ground + 55f; thr = Mathf.Clamp01(0.5f + Mathf.Clamp((wantH - transform.position.y) / 40f, -0.3f, 0.3f));
            FlyHeli(sx, sy, false, dt);
        }

        /// <summary>The AI pilot of the AC-130: a left-hand orbit, 380 m out and 240 m up, round the nearest fight, so the guns on the left look down at it.</summary>
        void AiOrbit(float dt)
        {
            if (!orbitSet) { var t = Health.Nearest(0, transform.position, 2500f); orbitCentre = t != null ? t.transform.position : transform.position + Forward * 300f; orbitSet = true; }
            var rel = transform.position - orbitCentre; rel.y = 0f;
            float r = rel.magnitude; const float R = 380f;
            var tangent = new Vector3(rel.z, 0f, -rel.x).normalized;   // a left-hand circle
            var inward = -rel.normalized;
            var wantDir = (tangent + inward * Mathf.Clamp((r - R) / 120f, -1f, 1f)).normalized;
            float want = Mathf.Atan2(wantDir.x, wantDir.z); float diff = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, want * Mathf.Rad2Deg);
            float sx = Mathf.Clamp(diff / 25f, -0.7f, 0.7f);
            float ground = GameState.GroundHeight != null ? GameState.GroundHeight(orbitCentre.x, orbitCentre.z) : 0f;
            float wantH = ground + 240f; float sy = Mathf.Clamp((transform.position.y - wantH) / 60f, -0.5f, 0.5f);
            thr = 0.62f;
            Fly(sx, sy, false, dt);
            if (Time.frameCount % 240 == 0) { var t = Health.Nearest(0, transform.position, 2500f); if (t != null) orbitCentre = Vector3.Lerp(orbitCentre, t.transform.position, 0.5f); }
        }

        /// <summary>Reads the hangar: hull, engine, boost, bombs, gun. Upgrades bought mid-flight take effect at once (applyLoadout).</summary>
        public void ApplyLoadout()
        {
            var ns = Progress.StatsFor(ProgressId);
            float oldMax = health != null ? health.max : 100f;
            st = ns;
            if (health != null) { health.max = ns.maxHp; if (health.alive) health.current = Mathf.Min(health.max, health.current + Mathf.Max(0f, health.max - oldMax)); }
            int oldMaxBombs = maxBombs; maxBombs = ns.maxBombs; bombs = Mathf.Min(maxBombs, bombs + Mathf.Max(0, maxBombs - oldMaxBombs));
        }

        void Shoot()
        {
            var g = st != null ? st.gun : Progress.GunDef("m2");
            fireT = st != null ? st.fireInt : 0.085f;
            heat += st != null ? st.heatShot : 0.042f;
            if (heat >= 1f) { Overheated = true; heat = 1f; }
            lastFire = !lastFire;
            int barrels = st != null ? st.barrels : 1;
            for (int b = 0; b < (barrels > 1 ? 2 : 1); b++)
            {
                var m = (barrels > 1 ? b == 0 : lastFire) ? muzzleL : muzzleR;
                var dir = AimDir;
                dir = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, 0.4f * g.spread), 0f, 0f) * dir;
                if (IsHeli) dir = Quaternion.Euler(8f - AimOffset.y, yaw * Mathf.Rad2Deg + AimOffset.x, 0f) * Vector3.forward;
                float dmg = (st != null ? st.dmg : 10f) * GalacticMode.K;
                if (IsSaucer && Projectiles.Instance != null) { Projectiles.Instance.FireBolt(m.position, dir, 520f * U, 22f * GalacticMode.K, 1.6f, 0, gameObject); continue; }
                if (craft == Craft.Dragon && Projectiles.Instance != null) { var bd = Quaternion.AngleAxis(Random.Range(0f, 360f), dir) * Quaternion.Euler(Random.Range(0f, 4f), 0f, 0f) * dir; Projectiles.Instance.FireShell(m.position, bd, 280f * U, 7f * U, 14f * U, 6f * GalacticMode.K, 0, gameObject, true, 70f * U); continue; }
                if (craft == Craft.Ptero && Projectiles.Instance != null) { Projectiles.Instance.FireShell(m.position, dir, 200f * U, 3f * U, 6f * U, 18f * GalacticMode.K, 0, gameObject, true, 0f); continue; }
                if (craft == Craft.Skyjack && Projectiles.Instance != null) { Projectiles.Instance.FireRound(m.position, dir, 900f * U, 14f * GalacticMode.K, 700f * U, 0, gameObject, new Color(0.8f, 0.9f, 1f), 0.02f); continue; }
                if (Projectiles.Instance != null)
                {
                    if (g.splash > 0f) Projectiles.Instance.FireShell(m.position, dir, g.speed * U, g.splash * 0.5f * U, g.splash * 1.6f * U, dmg, 0, gameObject, false, 0f);
                    else Projectiles.Instance.FireRound(m.position, dir, g.speed * U, dmg, g.range * U, 0, gameObject, new Color(1f, 0.9f, 0.6f), g.id == "mini" ? 0.03f : 0.04f + g.dmg * 0.001f);
                }
                if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(m.position, dir, 1.2f);
            }
            Sfx.Play(craft == Craft.Dragon ? "breath" : craft == Craft.Ptero ? "screech" : craft == Craft.Skyjack ? "thwip" : IsSaucer ? "gun_zap" : Sfx.GunSound(g.id), g.id == "m2" ? 0.5f : 0.75f, 1f, transform.position);
            // rocket pods: a rocket now and then while the gun is held
            if (st != null && st.rocketInt > 0f && rocketT <= 0f && Projectiles.Instance != null)
            {
                rocketT = st.rocketInt; Sfx.Play("gun_rocket", 0.7f, 1f, transform.position);
                Projectiles.Instance.FireShell(transform.position - Vector3.up * 0.4f, AimDir, 520f * U, 20f * U, 60f * U, 90f * GalacticMode.K, 0, gameObject, false, 0f);
            }
            if (GameState.Hud != null) GameState.Hud.OnShot();
        }

        /// <summary>The engine: the plane's drone rises with throttle and speed, the rotor beats with the collective; boost pushes the music.</summary>
        void EngineSound(bool boosting)
        {
            if (!Active) return;
            if (boosting && docked == null) Sfx.Boosting = true;
            if (IsSaucer) Sfx.Loop("engine", "ufo_hum", 0.35f, 0.9f + thr * 0.3f);
            else if (IsBeast) { if (Mathf.Repeat(Time.time, 1.3f) < Time.deltaTime) Sfx.Play("flap", 0.4f, craft == Craft.Ptero ? 1.4f : 0.8f, transform.position); }
            else if (craft == Craft.Skyjack) Sfx.Loop("engine", "rotor_loop", 0.3f, 1.8f + thr * 0.4f);
            else if (IsHeli) Sfx.Loop("engine", "rotor_loop", Landed && thr < 0.2f ? 0.25f : 0.5f, 0.85f + thr * 0.35f + (boosting ? 0.15f : 0f));
            else Sfx.Loop("engine", "engine_loop", docked != null ? 0.2f : 0.45f, 0.7f + thr * 0.5f + Mathf.Clamp01(speedU / 600f) * 0.3f + (boosting ? 0.25f : 0f));
        }

        void DropBomb()
        {
            if (craft == Craft.B52) { StartCoroutine(Carpet()); return; }
            if (craft == Craft.Dragon) { bombs--; bombCd = 1.2f; if (Projectiles.Instance != null) Projectiles.Instance.FireShell(muzzleL != null ? muzzleL.position : transform.position, AimDir, 420f * U, 20f * U, 70f * U, 220f * GalacticMode.K, 0, gameObject, true, 40f * U); Sfx.Play("breath", 0.8f, 0.6f, transform.position); Sfx.Play("roar_big", 0.5f, 0.9f, transform.position); return; }
            if (craft == Craft.Skyjack) { bombs--; bombCd = 0.8f; var v0 = Velocity; v0.y = Mathf.Min(v0.y, 0f) - 20f * U; if (Projectiles.Instance != null) for (int i = 0; i < 5; i++) Projectiles.Instance.DropBomb(transform.position - Vector3.up * 0.5f, v0 + new Vector3(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f)), 12f * U, 40f * U, 60f * GalacticMode.K, gameObject); Sfx.Play("whistle", 0.4f, 1.3f, transform.position); return; }
            if (craft == Craft.Drone) { bombs--; bombCd = 0.6f; if (Projectiles.Instance != null) Projectiles.Instance.FireShell(transform.position - Vector3.up * 0.4f, Vector3.down, 20f * U, 9f * U, 58f * U, 120f * GalacticMode.K, 0, gameObject, false, 250f * U); Sfx.Play("whistle", 0.25f, 1.4f, transform.position); return; }
            bombs--; bombCd = 0.34f; Sfx.Play("whistle", 0.35f, 1f, transform.position);
            var vel = Velocity; vel.y = Mathf.Min(vel.y, 0f) - 25f * U;
            if (Projectiles.Instance != null) { float bk = st != null ? st.blastK : 1f; Projectiles.Instance.DropBomb(transform.position - Vector3.up * 0.9f, vel, 36f * U * bk, 86f * U * bk, 175f * GalacticMode.K * bk, gameObject); }
        }

        /// <summary>The B-52's carpet: five bombs in a line, a third of a second apart.</summary>
        System.Collections.IEnumerator Carpet()
        {
            bombCd = 2.2f;
            for (int i = 0; i < 5 && bombs > 0; i++)
            {
                bombs--; Sfx.Play("whistle", 0.35f, 0.9f, transform.position);
                var vel = Velocity; vel.y = Mathf.Min(vel.y, 0f) - 25f * U;
                if (Projectiles.Instance != null) { float bk = st != null ? st.blastK : 1f; Projectiles.Instance.DropBomb(transform.position - Vector3.up * 1.5f + transform.right * Random.Range(-1f, 1f), vel, 36f * U * bk, 86f * U * bk, 175f * GalacticMode.K * bk, gameObject); }
                yield return new WaitForSeconds(0.33f);
            }
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
