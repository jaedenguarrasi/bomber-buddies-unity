using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>Phone-in-a-headset VR (Settings → Graphics → Phone headset): the picture is drawn side by side for the two
    /// eyes, the phone's gyro turns your head, and a controller does the rest. On foot your head is your aim; in a vehicle
    /// you look around the cockpit. Hold View (Y) for a second to re-centre. Proper headsets (Quest, Vive) come later.</summary>
    [DefaultExecutionOrder(500)]
    public class VrMode : MonoBehaviour
    {
        public static VrMode Instance { get; private set; }
        public static bool On { get { return Instance != null; } }
        const float Ipd = 0.064f;
        Camera left, right; Transform head; Canvas hud; RenderMode hudMode; Vector2 hudSize; Transform hudParent; Vector3 hudScale; CameraRig rig;
        Quaternion gyro = Quaternion.identity, recenter = Quaternion.identity; Vector2 lastGyroLook; bool haveGyro; float viewHeldT;
        float leftFov;

        /// <summary>Settings.Apply calls this: start or stop VR to match the option.</summary>
        public static void Refresh()
        {
            bool want = Settings.Vr && GameState.Rig != null && GameState.Rig.cam != null;
            if (want && Instance == null) { Instance = GameState.Rig.cam.gameObject.AddComponent<VrMode>(); Instance.Begin(); }
            else if (!want && Instance != null) { Instance.End(); }
        }

        void Begin()
        {
            rig = GameState.Rig; left = rig.cam; head = left.transform;
            // the right eye: a copy of the main camera, half a screen each, eyes 64 mm apart
            var rgo = new GameObject("Right eye"); rgo.transform.SetParent(head, false); rgo.transform.localPosition = new Vector3(Ipd, 0f, 0f);
            right = rgo.AddComponent<Camera>(); right.CopyFrom(left); right.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            var rd = right.GetUniversalAdditionalCameraData(); var ld = left.GetUniversalAdditionalCameraData();
            rd.renderPostProcessing = ld.renderPostProcessing; rd.antialiasing = ld.antialiasing; rd.renderShadows = ld.renderShadows;
            left.rect = new Rect(0f, 0f, 0.5f, 1f);
            // the HUD floats a little in front of the face so both eyes see it
            if (GameState.Hud != null)
            {
                hud = GameState.Hud.GetComponent<Canvas>();
                if (hud != null)
                {
                    var rt = hud.GetComponent<RectTransform>(); hudMode = hud.renderMode; hudSize = rt.sizeDelta; hudParent = rt.parent; hudScale = rt.localScale;
                    hud.renderMode = RenderMode.WorldSpace; hud.worldCamera = left;
                    rt.SetParent(head, false); rt.sizeDelta = new Vector2(1280f, 720f); rt.localScale = Vector3.one * 0.0011f; rt.localPosition = new Vector3(0f, 0f, 1.3f); rt.localRotation = Quaternion.identity;
                    var scaler = hud.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = false;
                }
                GameState.Hud.ShowTouch(false);
            }
            if (AttitudeSensor.current != null) { InputSystem.EnableDevice(AttitudeSensor.current); haveGyro = true; }
            Recenter();
            if (GameState.Hud != null) GameState.Hud.Announce("Headset on. Hold View to re-centre.", 3f);
        }

        void End()
        {
            if (right != null) Destroy(right.gameObject);
            if (left != null) left.rect = new Rect(0f, 0f, 1f, 1f);
            if (hud != null)
            {
                var rt = hud.GetComponent<RectTransform>(); rt.SetParent(hudParent, false); hud.renderMode = hudMode; rt.sizeDelta = hudSize; rt.localScale = hudScale; rt.localPosition = Vector3.zero; rt.localRotation = Quaternion.identity;
                var scaler = hud.GetComponent<CanvasScaler>(); if (scaler != null) scaler.enabled = true;
            }
            if (left != null) left.transform.localRotation = Quaternion.identity;
            Instance = null; Destroy(this);
        }

        /// <summary>Where you are looking now counts as straight ahead.</summary>
        public void Recenter() { ReadGyro(); recenter = Quaternion.Inverse(Quaternion.Euler(0f, gyro.eulerAngles.y, 0f)); lastGyroLook = GyroLook(); }

        void ReadGyro()
        {
            if (!haveGyro) { if (AttitudeSensor.current != null) { InputSystem.EnableDevice(AttitudeSensor.current); haveGyro = true; } else return; }
            var q = AttitudeSensor.current.attitude.ReadValue();
            // the phone's frame (right-handed, screen up) to Unity's camera (the input system already allows for the screen orientation)
            gyro = Quaternion.Euler(90f, 0f, 0f) * new Quaternion(-q.x, -q.y, q.z, q.w);
        }
        Vector2 GyroLook() { var e = (recenter * gyro).eulerAngles; return new Vector2(e.y, e.x > 180f ? e.x - 360f : e.x); }

        void LateUpdate()
        {
            if (rig == null || left == null) return;
            ReadGyro();
            // hold View for a second to re-centre
            var ib = InputBridge.Instance; var gp = Gamepad.current;
            bool viewHeld = (gp != null && gp.buttonNorth.isPressed) || (Keyboard.current != null && Keyboard.current.vKey.isPressed);
            if (viewHeld) { viewHeldT += Time.unscaledDeltaTime; if (viewHeldT > 1f) { viewHeldT = -2f; Recenter(); if (GameState.Hud != null) GameState.Hud.Comms("Re-centred"); } } else viewHeldT = 0f;
            var pm = PlayerManager.Instance; bool onFoot = pm != null && pm.OnFoot;
            if (onFoot)
            {
                // the head is the aim: the gyro's change since last frame turns the rig like a stick would
                var now = GyroLook(); var d = now - lastGyroLook; lastGyroLook = now;
                if (d.x > 180f) d.x -= 360f; if (d.x < -180f) d.x += 360f;
                if (Mathf.Abs(d.x) < 90f && Mathf.Abs(d.y) < 90f) rig.AddLook(new Vector2(d.x, -d.y));
                left.transform.localRotation = Quaternion.identity;
            }
            else
            {
                // in a vehicle the camera is bolted on: look around the cockpit with the head
                lastGyroLook = GyroLook();
                left.transform.localRotation = left.transform.localRotation * (recenter * gyro);
            }
            // the eyes: left stays put, right sits beside it; same lens
            if (right != null) { right.fieldOfView = left.fieldOfView; right.nearClipPlane = left.nearClipPlane; right.farClipPlane = left.farClipPlane; }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
