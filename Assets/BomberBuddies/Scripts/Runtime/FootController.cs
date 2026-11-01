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
        float vy, crouchK;
        bool crouching, grounded, sprinting;
        public bool Sprinting { get { return sprinting; } }
        public float MoveSpeed { get { return horizontal.magnitude; } }
        public bool Crouching { get { return crouching; } }
        public float EyeHeight { get { return Mathf.Lerp(1.64f, 1.12f, crouchK); } }
        public bool ControlsLocked { get; set; }

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

            float speed = aiming ? aimSpeed : crouching ? crouchSpeed : sprinting ? sprintSpeed : walkSpeed;
            var flat = rig.FlatRotation;
            var want = (flat * Vector3.forward * move.y + flat * Vector3.right * move.x) * speed;
            float accel = grounded ? 34f : 9f;
            horizontal = Vector3.MoveTowards(horizontal, want, accel * dt);

            if (grounded)
            {
                vy = -2.5f;
                if (input.JumpPressed) { vy = jumpSpeed; crouching = false; }
            }
            else vy -= gravity * dt;

            var flags = cc.Move((horizontal + Vector3.up * vy) * dt);
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
        }
    }
}
