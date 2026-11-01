using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The soldier on foot: walk, sprint, crouch, jump, with the body always turned the way the camera looks.</summary>
    [RequireComponent(typeof(CharacterController))]
    public class FootController : MonoBehaviour
    {
        public float walkSpeed = 4.3f, sprintSpeed = 7.6f, crouchSpeed = 2.3f, aimSpeed = 2.9f, jumpSpeed = 6.6f, gravity = 24f;
        public float standHeight = 1.8f, crouchHeight = 1.25f;
        public CameraRig rig;
        public SoldierModelBase model;
        public Health health;
        public Rifle rifle;

        CharacterController cc;
        Vector3 horizontal;
        float vy, crouchK, nadeCd;
        bool crouching, grounded, sprinting;
        public bool Sprinting { get { return sprinting; } }
        public float MoveSpeed { get { return horizontal.magnitude; } }
        public bool Crouching { get { return crouching; } }
        public float EyeHeight { get { return Mathf.Lerp(1.64f, 1.12f, crouchK) + (InCover ? (Peeking ? 0.42f : -0.18f) : 0f); } }
        /// <summary>Cover (coverNear / coverSnap): crouched against something waist-high you tuck in behind it; aiming peeks over.</summary>
        public bool InCover { get; private set; }
        public bool Peeking { get; private set; }
        float coverT;
        public bool ControlsLocked { get; set; }
        /// <summary>Chute open: falls gently and steers. Jump opens it after a second in the air; landing closes it.</summary>
        public bool parachute;
        float airT, stepT; bool wasGrounded = true;
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
        public void ApplyKit()
        {
            string craft = Progress.IsFoot(Progress.Craft) ? Progress.Craft : "soldier";
            kit = Progress.KitOf(craft); st = Progress.StatsFor(craft);
            float spdK = kit.spd / 36f * st.thrustK;
            walkSpeed = 4.3f * spdK; sprintSpeed = 7.6f * spdK; crouchSpeed = 2.3f * spdK; aimSpeed = 2.9f * spdK; jumpSpeed = 6.6f * kit.jump / 92f;
            if (health != null) { float f = health.alive ? health.Fraction : 1f; health.max = st.maxHp * 1.25f; health.current = health.max * f; }   // the rifleman's 80 comes out at the 100 it had
            if (rifle != null) rifle.Fit(Progress.GunDef(Progress.GunOf(craft)), st);
            if (rig != null) rig.aimFov = kit.scope ? 18f : 48f;
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
            if (input.CrouchPressed) crouching = !crouching;

            bool aiming = rifle != null && rifle.Aiming;
            var move = input.Move;
            sprinting = input.Sprint && move.y > 0.35f && !crouching && !aiming && !input.Fire;
            if (sprinting) crouching = false;
            crouchK = Mathf.MoveTowards(crouchK, crouching ? 1f : 0f, dt * 6f);
            float h = Mathf.Lerp(standHeight, crouchHeight, crouchK);
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
            float speed = aiming ? aimSpeed : crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            var wheel = ItemWheel.Instance;
            if (wheel != null && wheel.smokeT > 0f) speed *= 1.3f;
            var flat = rig.FlatRotation;
            var want = (flat * Vector3.forward * move.y + flat * Vector3.right * move.x) * speed;
            float accel = grounded ? 34f : 9f;
            horizontal = Vector3.MoveTowards(horizontal, want, accel * dt);

            if (grounded)
            {
                if (vy < -20f && health != null && !(wheel != null && wheel.drinkT > 0f)) health.TakeDamage(new DamageInfo { amount = (-vy - 20f) * 5f, point = transform.position, direction = Vector3.down, source = gameObject });
                vy = -2.5f; airT = 0f; parachute = false;
                if (input.JumpPressed) { vy = jumpSpeed * (wheel != null && wheel.drinkT > 0f ? 1.9f : 1f); crouching = false; Sfx.Play("jump", 0.4f); }
                if (!wasGrounded) Sfx.Play("land", 0.45f);
                stepT += horizontal.magnitude * dt; if (stepT > 2.2f) { stepT = 0f; Sfx.Play("footstep", crouching ? 0.12f : 0.25f, sprinting ? 1.15f : 1f); }
            }
            else
            {
                airT += dt;
                vy -= gravity * dt;
                if (airT > 0.6f && input.JumpPressed && !parachute && !(kit != null && kit.jetpack)) { parachute = true; Sfx.Play("chute", 0.6f); if (GameState.Hud != null) GameState.Hud.Announce("Parachute open"); }
                if (parachute) vy = Mathf.Max(vy, -4.1f);
                else vy = Mathf.Max(vy, -52f);   // terminal velocity
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
            if (rifle != null) rifle.Tick(input.Fire, input.Aim, input.ReloadPressed, horizontal.magnitude, sprinting, rig.firstPerson);
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
