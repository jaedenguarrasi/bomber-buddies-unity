using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The player's camera: over the shoulder in third person, at the eyes in first; pulls in when a wall is in the way,
    /// zooms when aiming, widens at a sprint, and kicks with the rifle.</summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public FootController player;
        public bool firstPerson = true;
        public float yaw, pitch;
        public float tpDistance = 3.6f, tpAimDistance = 2.0f, shoulder = 0.6f, minDistance = 0.6f;
        public float baseFov = 70f, fpFov = 76f, aimFov = 46f, sprintFovAdd = 7f;

        float dist = 3.6f, fov = 70f, recoilPitch, recoilYaw, recoilVel, bobT, swayX, swayY, lastY, jolt;
        /// <summary>Body-cam first person: wide lens, the camera rides the chest and bobs with the stride, rolls into turns.</summary>
        public bool bodyCam = true;
        public bool CameraInsideBody { get; private set; }

        public void AddLook(Vector2 deg)
        {
            yaw = Mathf.Repeat(yaw + deg.x, 360f);
            pitch = Mathf.Clamp(pitch - deg.y, -82f, 82f);   // pitch is stored as the camera's x rotation: positive looks down
        }

        public void Kick(float pitchDeg, float yawDeg)
        {
            recoilVel += pitchDeg * 18f;
            recoilYaw += yawDeg;
        }

        public Vector3 AimOrigin { get { return cam.transform.position; } }
        public Vector3 AimDirection { get { return cam.transform.forward; } }
        public Quaternion FlatRotation { get { return Quaternion.Euler(0f, yaw, 0f); } }

        void LateUpdate()
        {
            if (player == null || cam == null) return;
            float dt = Time.deltaTime;
            // recoil: a spring that kicks the view up and settles back
            recoilVel -= recoilPitch * 140f * dt;
            recoilVel *= Mathf.Max(0f, 1f - 9f * dt);
            recoilPitch += recoilVel * dt;
            recoilYaw *= Mathf.Max(0f, 1f - 10f * dt);

            var head = player.transform.position + Vector3.up * player.EyeHeight;
            var rot = Quaternion.Euler(pitch - recoilPitch, yaw + recoilYaw, 0f);
            bool aiming = player.rifle != null && player.rifle.Aiming;
            float wantFov = (firstPerson ? fpFov : baseFov) + (player.Sprinting ? sprintFovAdd : 0f);
            if (aiming) wantFov = aimFov;
            fov = Mathf.Lerp(fov, wantFov, Mathf.Min(1f, 10f * dt));
            cam.fieldOfView = fov;

            if (firstPerson)
            {
                if (bodyCam)
                {
                    // a camera strapped to the chest: stride bob, a roll into turns, sway from the look, a jolt on landing
                    float speed = player.MoveSpeed;
                    bobT += dt * Mathf.Clamp(speed, 0f, 8f) * (player.Sprinting ? 1.9f : 1.6f);
                    float k = Mathf.Clamp01(speed / 3f) * (player.Sprinting ? 1.6f : 1f);
                    var look = InputBridge.Instance != null ? InputBridge.Instance.Look : Vector2.zero;
                    swayX = Mathf.Lerp(swayX, Mathf.Clamp(-look.x * 0.12f, -2.5f, 2.5f), Mathf.Min(1f, 6f * dt));
                    swayY = Mathf.Lerp(swayY, Mathf.Clamp(-look.y * 0.08f, -2f, 2f), Mathf.Min(1f, 6f * dt));
                    float dy = player.transform.position.y - lastY; lastY = player.transform.position.y;
                    if (dy > -0.001f && jolt < 0f) { jolt = Mathf.Min(0f, jolt); }
                    jolt = Mathf.Lerp(jolt, dy < -0.12f ? -0.12f : 0f, Mathf.Min(1f, 9f * dt));
                    var bob = new Vector3(Mathf.Sin(bobT) * 0.035f * k, Mathf.Abs(Mathf.Cos(bobT)) * 0.045f * k + jolt, 0f);
                    var chest = player.transform.position + Vector3.up * (player.EyeHeight - 0.12f);
                    var bodyRot = Quaternion.Euler(pitch - recoilPitch + swayY, yaw + recoilYaw, Mathf.Sin(bobT) * 1.4f * k + swayX * 0.6f);
                    cam.transform.position = chest + bodyRot * (new Vector3(0f, 0f, 0.1f) + bob);
                    cam.transform.rotation = bodyRot;
                    CameraInsideBody = true;
                    return;
                }
                cam.transform.position = head + rot * new Vector3(0f, 0f, 0.08f);
                cam.transform.rotation = rot;
                CameraInsideBody = true;
                return;
            }
            float wantDist = (aiming ? tpAimDistance : tpDistance) * Mathf.Max(1f, player.SizeK * 1.15f);
            dist = Mathf.Lerp(dist, wantDist, Mathf.Min(1f, 8f * dt));
            var offset = rot * new Vector3(shoulder, 0.15f, -dist);
            var desired = head + offset;
            RaycastHit hit;
            var dir = desired - head;
            float len = dir.magnitude;
            float use = len;
            if (len > 0.01f && Physics.SphereCast(head, 0.22f, dir / len, out hit, len, ~(1 << 8), QueryTriggerInteraction.Ignore)) use = Mathf.Max(minDistance, hit.distance - 0.05f);
            cam.transform.position = head + dir / Mathf.Max(0.001f, len) * use;
            cam.transform.rotation = rot;
            CameraInsideBody = use < 1.0f;
        }
    }
}
