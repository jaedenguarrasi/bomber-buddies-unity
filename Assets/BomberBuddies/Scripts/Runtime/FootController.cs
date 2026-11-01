using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The soldier on foot: walk, sprint, crouch, jump, with the body always turned the way the camera looks.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootController : MonoBehaviour
    {
        public float walkSpeed = 4.3f, sprintSpeed = 7.6f, crouchSpeed = 2.3f, aimSpeed = 2.9f, jumpSpeed = 6.6f, gravity = 24f;
        public float standHeight = 1.8f, crouchHeight = 1.25f;
        /// <summary>Prone (hold Crouch until you are flat), the slide (Crouch at a sprint) and swimming (the sea no longer kills a soldier; he swims, slowly).</summary>
        public float proneK; float crouchHoldT, slideT; public bool Prone, Swimming;
        public CameraRig rig;
        public SoldierModelBase model;
        public Health health;
        public Rifle rifle;

        CharacterController cc;
        Vector3 horizontal;
        float vy, crouchK, nadeCd;
        bool crouching, grounded, sprinting;
        public bool Grounded { get { return grounded; } } public bool Aiming { get { return rifle != null && rifle.Aiming; } }
        public int ShotsFired;
        public bool Sprinting { get { return sprinting; } }
        public float MoveSpeed { get { return horizontal.magnitude; } }
        public bool Crouching { get { return crouching; } }
        public float EyeHeight { get { return (Mathf.Lerp(Mathf.Lerp(1.64f, 1.12f, crouchK), 0.55f, proneK) + (InCover ? (Peeking ? 0.42f : -0.18f) : 0f)) * (standHeight / 1.8f); } }
        /// <summary>How big the body is next to a soldier: the camera stands further back from an ape.</summary>
        public float SizeK { get { return standHeight / 1.8f; } }
        /// <summary>Cover (coverNear / coverSnap): crouched against something waist-high you tuck in behind it; aiming peeks over.</summary>
        public bool InCover { get; private set; }
        public bool Peeking { get; private set; }
        float coverT;
        public bool ControlsLocked { get; set; }
        /// <summary>Chute open: falls gently and steers. Jump opens it after a second in the air; landing closes it.</summary>
        public bool parachute;
        float airT, stepT; bool wasGrounded = true;
        public MaterialLibrary lib; bool justLanded; float lastFallSpeed;
        /// <summary>The class from the hangar: rocket trooper, medic, jetpack... and its stats.</summary>
        public Progress.Kit kit;
        public Progress.Stats st;
        float healCd, rocketCd, jetFuel = 1f, knockT;
        /// <summary>A blast or a big hit nearby puts you on the ground for a moment (Knocked off your feet).</summary>
        public void Knock(float seconds, string why)
        {
            if (knockT > 0f) return;
            knockT = seconds; crouching = true;
            if (GameState.Hud != null) GameState.Hud.Announce(why, 1.6f);
            Sfx.Play("thud", 0.6f);
        }
        public bool Jetting { get; private set; }

        /// <summary>Read the hangar: class body, gun, upgrades (statsFrom for a soldier).</summary>
        float swingCd, swingT; bool slamArmed;
        // the ape's arms (ape3 lift/throw): a soldier or a jeep over its head, thrown with Fire
        Health carrying; EnemySoldier carriedMan; EnemyVehicle carriedCar;
        public bool Carrying { get { return carrying != null; } }

        /// <summary>Interact for the ape: pick up a soldier or a jeep within reach; a second press sets it down.</summary>
        public bool TryCarry()
        {
            if (kit == null || !(kit.id == "ape" || kit.id == "devourer" || kit.id == "colossus")) return false;
            if (carrying != null) { Drop(); return true; }
            Health best = null; float bd = 3.5f;
            foreach (var h in Health.All)
            {
                if (h == null || !h.alive || h.team == 0 || h.isAir) continue;
                var es = h.GetComponent<EnemySoldier>(); var ev = h.GetComponent<EnemyVehicle>();
                if (es == null && !(ev != null && ev.kind == EnemyVehicle.Kind.Jeep)) continue;
                float d = Vector3.Distance(h.transform.position, transform.position); if (d < bd) { bd = d; best = h; }
            }
            if (best == null) return false;
            carrying = best; carriedMan = best.GetComponent<EnemySoldier>(); carriedCar = best.GetComponent<EnemyVehicle>();
            if (carriedMan != null) carriedMan.lifted = true;
            if (carriedCar != null) carriedCar.enabled = false;
            foreach (var c in best.GetComponentsInChildren<Collider>()) c.enabled = false;
            Sfx.Play("grunt", 0.6f, 0.7f, transform.position); Sfx.Play("roar_ape", 0.4f, 1.1f, transform.position);
            if (GameState.Hud != null) GameState.Hud.Comms(carriedCar != null ? "Got the jeep. Fire to throw it" : "Got him. Fire to throw");
            return true;
        }
        void Drop()
        {
            if (carrying == null) return;
            var h = carrying; carrying = null;
            if (carriedMan != null) carriedMan.lifted = false;
            if (carriedCar != null) carriedCar.enabled = true;
            foreach (var c in h.GetComponentsInChildren<Collider>()) c.enabled = true;
            var p = transform.position + transform.forward * 2f; p.y = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : p.y; h.transform.position = p;
            carriedMan = null; carriedCar = null;
        }
        void CarryUpdate(float dt, InputBridge input)
        {
            if (carrying == null) return;
            if (!carrying.alive) { carrying = null; carriedMan = null; carriedCar = null; return; }
            carrying.transform.position = transform.position + Vector3.up * (standHeight + 0.3f) + transform.forward * 0.3f;
            carrying.transform.rotation = transform.rotation;
            if (input.Fire && swingCd <= 0f)
            {
                swingCd = 0.8f; var h = carrying; carrying = null;
                var dir = rig != null ? rig.cam.transform.forward : transform.forward; var v = (dir + Vector3.up * 0.25f).normalized * (carriedCar != null ? 22f : 28f);
                StartCoroutine(Thrown(h, v, carriedMan, carriedCar)); carriedMan = null; carriedCar = null;
                Sfx.Play("roar_ape", 0.7f, 0.9f, transform.position); Sfx.Play("swing", 0.6f, 0.6f, transform.position);
            }
        }
        /// <summary>Through the air and into whatever is there: the thrown thing dies on landing and so does what it lands on.</summary>
        System.Collections.IEnumerator Thrown(Health h, Vector3 v, EnemySoldier man, EnemyVehicle car)
        {
            float t = 0f;
            while (t < 6f && h != null)
            {
                float dt = Time.deltaTime; t += dt; v += Vector3.down * 18f * dt;
                var p = h.transform.position + v * dt; float g = GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : p.y;
                h.transform.Rotate(0f, 0f, 540f * dt);
                bool hitSomething = false;
                foreach (var o in Health.All) if (o != null && o.alive && o != h && o.team != 0 && !o.isAir && Vector3.Distance(o.transform.position + Vector3.up, p) < 1.6f) { o.TakeDamage(new DamageInfo { amount = 150f * GalacticMode.K, point = p, direction = v.normalized, source = gameObject, foot = true }); hitSomething = true; }
                if (p.y <= g || hitSomething || t > 0.4f && Physics.CheckSphere(p, 0.5f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
                {
                    p.y = Mathf.Max(p.y, g); h.transform.position = p;
                    if (Projectiles.Instance != null) Projectiles.Instance.Explode(p, 1.2f, car != null ? 6f : 3f, (car != null ? 120f : 60f) * GalacticMode.K, gameObject, 0);
                    Sfx.Play("thud", 0.8f, 0.6f, p); if (Gore.Instance != null && man != null) Gore.Instance.Splat(p, 1f);
                    h.TakeDamage(new DamageInfo { amount = 9999f, point = p, direction = v.normalized, source = gameObject, foot = man != null, explosive = car != null });
                    yield break;
                }
                h.transform.position = p;
                yield return null;
            }
        }
        // the Grapple's lines (the original's web-slinger): an anchor and a length per hand, a tap zips, a hold swings
        Vector3?[] anchor = new Vector3?[2]; float[] ropeLen = new float[2], holdT = new float[2]; LineRenderer[] lines = new LineRenderer[2]; float zipT;
        public bool Swinging { get { return anchor[0].HasValue || anchor[1].HasValue; } }
        public bool Zipped;

        /// <summary>The lines: fire one from a hand at whatever the camera is looking at within 70 m. Held: you swing on it,
        /// the rope only ever pulls. Tapped: a zip, straight at the anchor. Jump lets both go with the momentum you had.</summary>
        void Grapple(float dt, InputBridge input)
        {
            bool[] held = { input.LookHeld, input.BombHeld };
            for (int i = 0; i < 2; i++)
            {
                if (held[i] && !anchor[i].HasValue && holdT[i] <= 0f)
                {
                    RaycastHit hit; var o = rig.cam.transform.position; var d = rig.cam.transform.forward;
                    if (Physics.Raycast(o, d, out hit, 70f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore))
                    {
                        anchor[i] = hit.point; ropeLen[i] = Vector3.Distance(transform.position + Vector3.up * 1.4f, hit.point); holdT[i] = 0.001f;
                        Sfx.Play("swing", 0.5f, 1.6f, transform.position); Sfx.Tone(600f, 0.08f, 0.15f, "triangle");
                        if (grounded && vy < 0f) vy = 3f;   // a hop off the ground so the line can take you
                    }
                    else { holdT[i] = -0.3f; Sfx.Tone(200f, 0.08f, 0.1f, "square"); }
                }
                if (!held[i] && anchor[i].HasValue)
                {
                    // let go: a short hold was a zip
                    if (holdT[i] < 0.22f) { var to = anchor[i].Value - (transform.position + Vector3.up); float dist = to.magnitude; var v = to.normalized * Mathf.Clamp(dist * 1.6f, 14f, 34f); horizontal = new Vector3(v.x, 0f, v.z); vy = Mathf.Max(v.y, 6f); zipT = Mathf.Clamp(dist / 30f, 0.3f, 1.2f); Zipped = true; Sfx.Play("swing", 0.6f, 2f, transform.position); }
                    anchor[i] = null; holdT[i] = 0f;
                }
                if (holdT[i] < 0f) holdT[i] += dt; else if (anchor[i].HasValue) holdT[i] += dt;
                if (anchor[i].HasValue && Vector3.Distance(transform.position, anchor[i].Value) < 1.6f) { anchor[i] = null; holdT[i] = 0f; }
            }
            if (input.JumpPressed && Swinging) { anchor[0] = anchor[1] = null; holdT[0] = holdT[1] = 0f; vy += 4f; Sfx.Play("jump", 0.4f); }
            // the lines pull
            if (zipT > 0f) zipT -= dt;
            for (int i = 0; i < 2; i++)
            {
                if (!anchor[i].HasValue || holdT[i] < 0.22f) continue;
                var hand = transform.position + Vector3.up * 1.4f; var r = hand - anchor[i].Value; float len = r.magnitude;
                var v = horizontal + Vector3.up * vy;
                if (len > ropeLen[i])
                {
                    var n = r / len; float outward = Vector3.Dot(v, n);
                    if (outward > 0f) v -= n * outward;                       // the rope does not stretch
                    v -= n * (len - ropeLen[i]) * 12f;                        // and hauls you back onto the arc
                    ropeLen[i] = Mathf.Max(4f, ropeLen[i] - 2.5f * dt);      // reeling in a little gives the swing its height
                }
                // pump: pushing forward on the stick adds to the swing
                var fwd = rig.FlatRotation * Vector3.forward; v += fwd * input.Move.y * 6f * dt;
                horizontal = new Vector3(v.x, 0f, v.z); vy = v.y;
                if (horizontal.magnitude > 36f) horizontal = horizontal.normalized * 36f;
            }
            // draw them
            for (int i = 0; i < 2; i++)
            {
                if (anchor[i].HasValue)
                {
                    if (lines[i] == null) { var go = new GameObject("Line" + i); go.transform.SetParent(transform, false); lines[i] = go.AddComponent<LineRenderer>(); lines[i].material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); lines[i].material.SetColor("_BaseColor", new Color(0.9f, 0.9f, 0.95f)); lines[i].startWidth = lines[i].endWidth = 0.05f; lines[i].positionCount = 2; lines[i].useWorldSpace = true; }
                    lines[i].enabled = true; lines[i].SetPosition(0, transform.position + Vector3.up * 1.4f + (rig.FlatRotation * Vector3.right) * (i == 0 ? -0.35f : 0.35f)); lines[i].SetPosition(1, anchor[i].Value);
                }
                else if (lines[i] != null) lines[i].enabled = false;
            }
        }
        /// <summary>A monster's Fire: a club, a fist or a bite at whoever is within reach in front (the original's melee strike).
        /// Bomb: the goblin throws a rock, the ape a boulder, the clown honks everyone off their feet.</summary>
        void Melee(float dt, InputBridge input)
        {
            swingCd -= dt; swingT -= dt;
            if (carrying != null) { CarryUpdate(dt, input); return; }
            if (input.Fire && swingCd <= 0f)
            {
                swingCd = kit.id == "ape" ? 0.5f : 0.32f; swingT = 0.22f; int hit = 0;
                var fwd = Quaternion.Euler(0f, rig != null ? rig.yaw : transform.eulerAngles.y, 0f) * Vector3.forward;
                foreach (var h in Health.All)
                {
                    if (h == null || !h.alive || h.team == 0 || h.isAir) continue;
                    var d = h.transform.position - transform.position; float dy = d.y; d.y = 0f;
                    if (d.magnitude > kit.reach + 0.4f || Mathf.Abs(dy) > 2.2f || Vector3.Dot(d.normalized, fwd) < 0.4f) continue;
                    h.TakeDamage(new DamageInfo { amount = kit.meleeDmg * GalacticMode.K * 0.6f, point = h.transform.position + Vector3.up, direction = d.normalized, source = gameObject, foot = true });
                    if (Gunfire.Instance != null) Gunfire.Instance.Impact(h.transform.position + Vector3.up * 0.9f, -d.normalized, true);
                    hit++;
                }
                Sfx.Play(hit > 0 ? (kit.melee == "bite" ? "hit_flesh" : "thud") : "swing", 0.6f, kit.id == "ape" ? 0.7f : 1.1f, transform.position);
                if (kit.id == "ape" && hit > 0) Sfx.Play("roar_ape", 0.4f, 1f, transform.position);
                if (kit.id == "raptor" && hit == 0 && Random.value < 0.3f) Sfx.Play("screech", 0.4f, 1f, transform.position);
                if (GameState.Hud != null) GameState.Hud.OnShot();
            }
            if (input.BombPressed && nadeCd <= 0f && kit.id != "slinger")
            {
                nadeCd = kit.id == "clown" ? 2.5f : 2f;
                var from = transform.position + Vector3.up * (standHeight * 0.8f);
                var dir = rig != null ? rig.cam.transform.forward : transform.forward;
                if (kit.id == "ninjaL") { if (health != null) { health.current = Mathf.Min(health.max, health.current + health.max * 0.35f); } Sfx.Tone(520f, 0.4f, 0.2f, "sine"); Sfx.Tone(780f, 0.4f, 0.15f, "sine"); if (GameState.Hud != null) GameState.Hud.Comms("The mind mends"); nadeCd = 9f; }
                else if (kit.id == "ninjaD" && Projectiles.Instance != null) { for (int i = 0; i < 3; i++) Projectiles.Instance.FireShell(from, Quaternion.Euler(Random.Range(-3f, 3f), Random.Range(-6f, 6f), 0f) * dir, 500f * GalacticMode.U, 3f * GalacticMode.U, 8f * GalacticMode.U, 40f * GalacticMode.K, 0, gameObject, true, 0f); Sfx.Play("gun_bolt", 0.7f, 0.9f, transform.position); nadeCd = 1.2f; }
                else if (kit.id == "neph") { vy = jumpSpeed * 1.6f; horizontal += dir * 6f; Sfx.Play("swing", 0.6f, 0.5f, transform.position); nadeCd = 1.5f; slamArmed = true; }
                else if (kit.id == "devourer")
                {
                    EnemySoldier meal = null; float bd = 4f;
                    foreach (var h in Health.All) { if (h == null || h.team == 0) continue; var es = h.GetComponent<EnemySoldier>(); float d = Vector3.Distance(h.transform.position, transform.position); if (es != null && d < bd && (!h.alive || d < 2.5f)) { bd = d; meal = es; } }
                    if (meal != null) { float was = meal.health.max; if (meal.health.alive) meal.health.TakeDamage(new DamageInfo { amount = 9999f, point = meal.transform.position, direction = dir, source = gameObject, foot = true }); Destroy(meal.gameObject, 0.1f); if (health != null) health.current = Mathf.Min(health.max, health.current + was * 0.5f); Sfx.Play("hit_flesh", 0.8f, 0.6f, transform.position); Sfx.Play("roar_big", 0.5f, 0.8f, transform.position); if (GameState.Hud != null) GameState.Hud.Comms("Devoured"); if (Gore.Instance != null) Gore.Instance.Splat(transform.position + dir, 1.2f); }
                    nadeCd = 1f;
                }
                else if (kit.id == "clown")
                {
                    Sfx.Tone(260f, 0.45f, 0.4f, "square"); Sfx.Tone(390f, 0.45f, 0.25f, "square");
                    foreach (var h in Health.All) { if (h == null || !h.alive || h.team == 0 || h.isAir) continue; var d = h.transform.position - transform.position; if (d.magnitude > 9f) continue; var es = h.GetComponent<EnemySoldier>(); if (es != null) es.Stagger(1.4f, d.normalized * 6f); }
                    if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(0.3f);
                }
                else if (Projectiles.Instance != null)
                {
                    bool boulder = kit.id == "ape";
                    Projectiles.Instance.FireShell(from, (dir + Vector3.up * 0.25f).normalized, (boulder ? 230f : 300f) * GalacticMode.U, (boulder ? 12f : 4f) * GalacticMode.U, (boulder ? 60f : 20f) * GalacticMode.U, (boulder ? 160f : 45f) * GalacticMode.K, 0, gameObject, false, 250f * GalacticMode.U);
                    Sfx.Play("swing", 0.6f, boulder ? 0.6f : 1.2f, transform.position); if (boulder) Sfx.Play("roar_ape", 0.5f, 0.9f, transform.position);
                }
            }
            // the Nephilim's slam: the leap ends in a blast
            if (kit.id == "neph" && justLanded && slamArmed) { slamArmed = false; if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(0.7f); Sfx.Play("stomp", 0.9f, 0.6f, transform.position); Sfx.Play("expl_big", 0.5f, 0.9f, transform.position); if (Projectiles.Instance != null) Projectiles.Instance.Explode(transform.position, 2f, 8f, 140f * GalacticMode.K, gameObject, 0); }
            // the ape's landing: a stomp that shakes the ground (impact3 on landing)
            if (kit.id == "ape" && justLanded && lastFallSpeed > 7f) { if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(0.5f); Sfx.Play("stomp", 0.8f, 0.8f, transform.position); if (Projectiles.Instance != null) Projectiles.Instance.Explode(transform.position, 1.5f, 5f, 60f * GalacticMode.K, gameObject, 0); }
            justLanded = false;
        }

        /// <summary>The gunslinger's dive: the world slows to a third for a moment and he throws himself sideways.</summary>
        System.Collections.IEnumerator SlowDive()
        {
            var side = rig != null ? rig.FlatRotation * Vector3.right * (InputBridge.Instance != null && InputBridge.Instance.Move.x < 0f ? -1f : 1f) : transform.right;
            horizontal += side * 7f; vy = 4.5f; Time.timeScale = 0.35f; Sfx.Play("whoosh", 0.5f, 0.7f); if (GameState.Hud != null) GameState.Hud.Comms("Dive");
            float t = 0f; while (t < 1.4f && !(PauseMenu.Instance != null && PauseMenu.Instance.IsOpen)) { t += Time.unscaledDeltaTime; yield return null; }
            if (!(PauseMenu.Instance != null && PauseMenu.Instance.IsOpen)) Time.timeScale = 1f;
        }
        /// <summary>Volt's shock grenade: everyone near where it lands is knocked down.</summary>
        System.Collections.IEnumerator ShockAfter(Vector3 at, float delay)
        {
            yield return new WaitForSeconds(delay);
            at.y = GameState.GroundHeight != null ? GameState.GroundHeight(at.x, at.z) : at.y;
            foreach (var h in Health.All) { if (h == null || !h.alive || h.team == 0 || h.isAir) continue; var d = h.transform.position - at; if (d.magnitude > 9f) continue; var es = h.GetComponent<EnemySoldier>(); if (es != null) es.Stagger(2.2f, d.normalized * 3f); h.TakeDamage(new DamageInfo { amount = 20f * GalacticMode.K, point = h.transform.position, direction = d.normalized, source = gameObject, foot = true }); }
            Sfx.Play("shock", 0.9f, 1f, at); if (Gunfire.Instance != null) Gunfire.Instance.MuzzleFlash(at + Vector3.up, Vector3.up, 4f);
        }

        public void ApplyKit()
        {
            string craft = Progress.IsFoot(Progress.Craft) ? Progress.Craft : "soldier";
            kit = Progress.KitOf(craft); st = Progress.StatsFor(craft);
            float spdK = kit.spd / 36f * st.thrustK;
            walkSpeed = 4.3f * spdK; sprintSpeed = 7.6f * spdK; crouchSpeed = 2.3f * spdK; aimSpeed = 2.9f * spdK; jumpSpeed = 6.6f * kit.jump / 92f;
            if (health != null) { float f = health.alive ? health.Fraction : 1f; health.max = st.maxHp * 1.25f; health.current = health.max * f; }   // the rifleman's 80 comes out at the 100 it had
            if (rifle != null) { rifle.Fit(Progress.GunDef(Progress.GunOf(craft)), st); rifle.enabled = kit.melee == null; if (rifle.viewModel != null) rifle.viewModel.gameObject.SetActive(kit.melee == null); }
            if (rig != null) rig.aimFov = kit.scope ? 18f : 48f;
            standHeight = kit.height; if (cc != null) { cc.height = standHeight; cc.center = new Vector3(0f, standHeight * 0.5f, 0f); }
            // a monster's own body
            if (kit.body != null && lib != null && !(model is CreatureModel)) { var old = model; model = CreatureModel.Create(transform, lib, kit.body, 8); if (old != null) Destroy(old.gameObject); }
            else if (kit.body == null && model is CreatureModel && lib != null) { var old = model; model = SoldierModelBase.Create(transform, lib, 0, 8); if (old != null) Destroy(old.gameObject); }
            if (kit.id == "ninjaD" && model != null) foreach (var r in model.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(0.2f, 0.15f, 0.25f, 1f));
            if (kit.id == "ninjaL" && model != null) foreach (var r in model.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(0.95f, 0.95f, 1f, 1f));
            if (kit.id == "grey" && model != null) foreach (var r in model.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(0.6f, 0.7f, 0.65f, 1f));
            if (kit.id == "clown" && model != null) foreach (var r in model.GetComponentsInChildren<Renderer>()) foreach (var m in r.materials) if (m.HasProperty("_Tint")) m.SetVector("_Tint", new Vector4(1f, 0.95f, 0.95f, 1f));
            GameState.Grenades = st.maxBombs;
        }

        void Awake()
        {
            cc = GetComponent<CharacterController>();
            cc.height = standHeight;
            cc.radius = 0.36f;
            cc.center = new Vector3(0f, standHeight * 0.5f, 0f);
            cc.slopeLimit = 48f;
            cc.stepOffset = 0.45f;
            gameObject.layer = 8;
        }

        public void Teleport(Vector3 pos, float yawDeg)
        {
            cc.enabled = false;
            transform.position = pos;
            transform.rotation = Quaternion.Euler(0f, yawDeg, 0f);
            cc.enabled = true;
            horizontal = Vector3.zero; vy = 0f;
            if (rig != null) { rig.yaw = yawDeg; rig.pitch = 5f; }
        }

        /// <summary>The ground under the feet, from whoever built the world; used as a safety net against falling through.</summary>
        public System.Func<float, float, float> groundHeight;
        float settleT = 1.5f;

        void LateUpdate()
        {
            // never under the ground: if a long frame (the island generating on a phone) or a stale collider lets the
            // body through, put it back on the surface
            if (groundHeight == null) return;
            var p = transform.position;
            float g = groundHeight(p.x, p.z);
            if (p.y < g - 0.3f)
            {
                cc.enabled = false; transform.position = new Vector3(p.x, g + 0.1f, p.z); cc.enabled = true;
                vy = -2f; horizontal = Vector3.zero;
            }
        }

        void Update()
        {
            var input = InputBridge.Instance;
            if (input == null || rig == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.05f);   // a long frame moves us at most as far as 1/20 s would
            if (settleT > 0f) { settleT -= Time.deltaTime; vy = -2f; }   // let the world finish appearing before gravity bites
            bool alive = health == null || health.alive;
            if (!alive || ControlsLocked)
            {
                vy -= gravity * dt;
                cc.Move(Vector3.up * vy * dt);
                if (cc.isGrounded) vy = -2f;
                if (model != null) model.Animate(0f, 0f, crouchK, false, dt);
                return;
            }

            rig.AddLook(input.Look);
            if (input.ViewPressed) rig.firstPerson = !rig.firstPerson;
            if (input.CrouchPressed)
            {
                if (sprinting && grounded && slideT <= 0f && kit != null && kit.melee == null) { slideT = 0.7f; crouching = true; Sfx.Play("footstep", 0.4f, 0.6f); if (GameState.Hud != null) GameState.Hud.Comms("Slide"); }
                else if (Prone) { Prone = false; crouching = false; }
                else crouching = !crouching;
            }
            // hold crouch to go flat
            if (input.CrouchHeld && crouching && grounded) { crouchHoldT += dt; if (crouchHoldT > 0.6f && !Prone) { Prone = true; if (GameState.Hud != null) GameState.Hud.Comms("Prone"); } } else crouchHoldT = 0f;
            if (!crouching) Prone = false;
            proneK = Mathf.MoveTowards(proneK, Prone ? 1f : 0f, dt * 4f);
            if (slideT > 0f) { slideT -= dt; if (slideT <= 0f) crouching = false; }

            bool aiming = rifle != null && rifle.Aiming;
            var move = input.Move;
            sprinting = input.Sprint && move.y > 0.35f && !crouching && !aiming && !input.Fire;
            if (sprinting) crouching = false;
            crouchK = Mathf.MoveTowards(crouchK, crouching ? 1f : 0f, dt * 6f);
            float h = Mathf.Lerp(Mathf.Lerp(standHeight, crouchHeight, crouchK), standHeight * 0.4f, proneK);
            cc.height = h; cc.center = new Vector3(0f, h * 0.5f, 0f);

            if (knockT > 0f) { knockT -= dt; move = Vector2.zero; sprinting = false; if (knockT <= 0f) crouching = false; }
            // cover: crouched and nearly touching something solid in front
            bool wasCover = InCover;
            if (crouching && grounded)
            {
                RaycastHit ch; var fwd = rig.FlatRotation * Vector3.forward;
                InCover = Physics.Raycast(transform.position + Vector3.up * 0.8f, fwd, out ch, 1.1f, ~((1 << 8) | (1 << 9)), QueryTriggerInteraction.Ignore) && ch.collider.GetComponent<Health>() == null;
            }
            else InCover = false;
            Peeking = InCover && aiming;
            if (InCover != wasCover && GameState.Hud != null) GameState.Hud.Comms(InCover ? "In cover: aim to peek" : "Out of cover");
            float speed = aiming ? aimSpeed : Prone ? crouchSpeed * 0.45f : crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            if (slideT > 0f) speed = sprintSpeed * 1.25f;
            var wheel = ItemWheel.Instance;
            if (wheel != null && wheel.smokeT > 0f) speed *= 1.3f;
            var flat = rig.FlatRotation;
            var want = (flat * Vector3.forward * move.y + flat * Vector3.right * move.x) * speed;
            float accel = grounded ? 34f : 9f;
            if (!(Swinging || zipT > 0f)) horizontal = Vector3.MoveTowards(horizontal, want, accel * dt);

            if (grounded)
            {
                if (vy < -20f && health != null && !(wheel != null && wheel.drinkT > 0f) && !(kit != null && kit.id == "slinger")) health.TakeDamage(new DamageInfo { amount = (-vy - 20f) * 5f, point = transform.position, direction = Vector3.down, source = gameObject });
                if (!Swinging && zipT <= 0f) vy = -2.5f; airT = 0f; parachute = false;
                if (input.JumpPressed) { vy = jumpSpeed * (wheel != null && wheel.drinkT > 0f ? 1.9f : 1f); crouching = false; Sfx.Play("jump", 0.4f); }
                if (!wasGrounded) { justLanded = true; Sfx.Play("land", 0.45f); }
                stepT += horizontal.magnitude * dt; if (stepT > 2.2f) { stepT = 0f; Sfx.Play("footstep", crouching ? 0.12f : 0.25f, sprinting ? 1.15f : 1f); }
            }
            else
            {
                airT += dt; lastFallSpeed = -vy;
                vy -= gravity * dt;
                if (airT > 0.6f && input.JumpPressed && !parachute && !(kit != null && (kit.jetpack || kit.id == "slinger"))) { parachute = true; Sfx.Play("chute", 0.6f); if (GameState.Hud != null) GameState.Hud.Announce("Parachute open"); }
                if (parachute) vy = Mathf.Max(vy, -4.1f);
                else vy = Mathf.Max(vy, -52f);   // terminal velocity
            }

            if (Swimming)
            {
                float sea = GalacticMode.SeaY;
                horizontal = Vector3.ClampMagnitude(horizontal, walkSpeed * 0.5f);
                vy = Mathf.Clamp((sea - 0.9f - transform.position.y) * 4f, -3f, 3f); if (input.JumpPressed) vy = 3.5f;
                if (Mathf.Repeat(Time.time, 1.2f) < dt && horizontal.magnitude > 0.5f) Sfx.Play("splash", 0.25f, Random.Range(0.9f, 1.3f), transform.position);
            }
            var flags = cc.Move((horizontal + Vector3.up * vy) * dt);
            wasGrounded = grounded;
            grounded = cc.isGrounded || (flags & CollisionFlags.Below) != 0;
            if ((flags & CollisionFlags.Above) != 0 && vy > 0f) vy = 0f;

            // the body faces where the camera looks; in third person it lags a touch so turns read as turns
            var bodyYaw = Quaternion.Euler(0f, rig.yaw, 0f);
            transform.rotation = rig.firstPerson ? bodyYaw : Quaternion.Slerp(transform.rotation, bodyYaw, Mathf.Min(1f, 14f * dt));

            if (model != null)
            {
                model.Animate(horizontal.magnitude, -rig.pitch, crouchK, !grounded, dt);
                model.SetVisible(!rig.firstPerson);
            }
            if (rifle != null && kit != null && kit.melee == null) rifle.Tick(input.Fire, input.Aim, input.ReloadPressed, horizontal.magnitude, sprinting, rig.firstPerson);
            if (kit != null && kit.melee != null) Melee(dt, input);
            if (kit != null && kit.id == "slinger") Grapple(dt, input);
            if (kit != null && kit.melee == null && input.BombPressed && nadeCd <= 0f && (kit.id == "gunslinger" || kit.id == "wire"))
            {
                if (kit.id == "gunslinger") { nadeCd = 4f; StartCoroutine(SlowDive()); }
                else { nadeCd = 2.5f; var from = transform.position + Vector3.up * 1.4f; var dir = rig != null ? rig.cam.transform.forward : transform.forward; if (Projectiles.Instance != null) Projectiles.Instance.FireShell(from, (dir + Vector3.up * 0.3f).normalized, 220f * GalacticMode.U, 4f * GalacticMode.U, 50f * GalacticMode.U, 25f * GalacticMode.K, 0, gameObject, false, 250f * GalacticMode.U); Sfx.Play("gun_bolt", 0.5f, 1.3f, transform.position); StartCoroutine(ShockAfter(from + dir * 14f, 1.1f)); }
            }
            // grenades: the original soldier's nade (shell v 190, g 250, blast 9/58, 120 damage), one every 2 s
            nadeCd -= dt; healCd -= dt; rocketCd -= dt;
            // the rocket trooper: Bomb fires an anti-tank rocket in a straight line (RPG_W: v 330, dmg 240, blast 12/60)
            if (kit != null && kit.rocket && input.BombPressed && rocketCd <= 0f)
            {
                rocketCd = 1.6f * (st != null ? st.bombReload / 2f : 1f); Sfx.Play("gun_rocket", 0.8f);
                const float U2 = GalacticMode.U;
                if (Projectiles.Instance != null) Projectiles.Instance.FireShell(rig.AimOrigin + rig.AimDirection * 0.8f, rig.AimDirection, 330f * U2, 12f * U2, 60f * U2, 240f * GalacticMode.K, 0, gameObject, false, 18f * U2);
                rig.Kick(1.4f, 0f);
            }
            // the medic: Kick (X) patches up everyone near you, and himself slowly
            if (kit != null && kit.medic)
            {
                if (health != null && health.alive && health.current < health.max) health.current = Mathf.Min(health.max, health.current + 2f * dt);
                if (input.KickPressed && healCd <= 0f)
                {
                    healCd = 3f; int healed = 0;
                    foreach (var hh in Health.All) if (hh != null && hh.alive && hh.team == 0 && hh.gameObject != gameObject && Vector3.Distance(hh.transform.position, transform.position) < 8f && hh.current < hh.max) { hh.current = hh.max; healed++; }
                    if (health != null) health.current = health.max;
                    if (GameState.Hud != null) GameState.Hud.Comms(healed > 0 ? "Patched up " + healed : "Patched up");
                }
            }
            // the jetpack: hold Jump in the air and the pack lights
            Jetting = false;
            if (kit != null && kit.jetpack)
            {
                if (grounded) jetFuel = Mathf.Min(1f, jetFuel + dt * 0.5f);
                else if (input.JumpHeld && airT > 0.25f && jetFuel > 0f)
                {
                    Jetting = true; jetFuel -= dt * 0.16f;
                    vy = Mathf.Min(vy + 30f * dt, 9f);
                    var flat2 = rig.FlatRotation; var push = (flat2 * Vector3.forward * input.Move.y + flat2 * Vector3.right * input.Move.x) * 14f;
                    horizontal = Vector3.MoveTowards(horizontal, push, 20f * dt);
                    parachute = false;
                }
            }
            if ((kit == null || !kit.rocket) && input.BombPressed && GameState.Grenades > 0 && nadeCd <= 0f)
            {
                nadeCd = 2f; GameState.Grenades--; GameState.Touch(); Sfx.Play("swing", 0.5f);
                const float U = GalacticMode.U;
                var from = rig.AimOrigin + rig.AimDirection * 0.6f + Vector3.up * 0.1f;
                var dir = (rig.AimDirection + Vector3.up * 0.18f).normalized;
                if (Projectiles.Instance != null) Projectiles.Instance.FireShell(from, dir, 190f * U, 9f * U, 58f * U, 120f * GalacticMode.K, 0, gameObject, false, 250f * U);
            }
        }
    }
}
