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

        float dist = 3.6f, fov = 70f, recoilPitch, recoilYaw, recoilVel;
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
                cam.transform.position = head + rot * new Vector3(0f, 0f, 0.08f);
                cam.transform.rotation = rot;
                CameraInsideBody = true;
                return;
            }
            float wantDist = aiming ? tpAimDistance : tpDistance;
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
