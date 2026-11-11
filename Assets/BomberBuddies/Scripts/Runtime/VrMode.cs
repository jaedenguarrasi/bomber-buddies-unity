using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;
using System.Collections.Generic;

namespace BomberBuddies
{
    /// <summary>Phone-in-a-headset VR (Settings → Graphics → Phone headset): the picture is drawn side by side for the two
    /// eyes, the phone's gyro turns your head, and a controller does the rest. On foot your head is your aim; in a vehicle
    /// you look around the cockpit. Hold View (Y) for a second to re-centre.
    /// A real headset on a PC (Valve Index, Vive, Quest Link through SteamVR / OpenXR) is picked up by itself: the headset draws the two
    /// eyes, its tracking turns and leans your head, the hand controllers play as a gamepad, and the menus hang in front of you.</summary>
    [DefaultExecutionOrder(500)]
    public class VrMode : MonoBehaviour
    {
        public static VrMode Instance { get; private set; }
        public static bool On { get { return Instance != null; } }
        const float Ipd = 0.064f;
        Camera left, right; Transform head; Canvas hud; RenderMode hudMode; Vector2 hudSize; Transform hudParent; Vector3 hudScale; CameraRig rig;
        Quaternion gyro = Quaternion.identity, recenter = Quaternion.identity; Vector2 lastGyroLook; bool haveGyro; float viewHeldT;
        float leftFov;
        bool headset; Vector3 headPos, headPos0; bool havePos;
        class Moved { public Canvas c; public RenderMode mode; public Transform parent; public Vector2 size; public Vector3 scale; public bool scaler; }
        readonly List<Moved> moved = new List<Moved>(); float sweepT;
        /// <summary>A real headset is running (OpenXR).</summary>
        public static bool HeadsetActive { get { return UnityEngine.XR.XRSettings.enabled && UnityEngine.XR.XRSettings.isDeviceActive; } }

        /// <summary>Settings.Apply calls this: start or stop VR to match the option.</summary>
        public static void Refresh()
        {
            bool want = (Settings.Vr || HeadsetActive) && GameState.Rig != null && GameState.Rig.cam != null;
            if (want && Instance == null) { Instance = GameState.Rig.cam.gameObject.AddComponent<VrMode>(); Instance.Begin(); }
            else if (!want && Instance != null) { Instance.End(); }
        }

        void Begin()
        {
            rig = GameState.Rig; left = rig.cam; head = left.transform;
            headset = HeadsetActive;
            // the right eye: a copy of the main camera, half a screen each, eyes 64 mm apart (a real headset draws its own two eyes)
            if (!headset) {
            var rgo = new GameObject("Right eye"); rgo.transform.SetParent(head, false); rgo.transform.localPosition = new Vector3(Ipd, 0f, 0f);
            right = rgo.AddComponent<Camera>(); right.CopyFrom(left); right.rect = new Rect(0.5f, 0f, 0.5f, 1f);
            var rd = right.GetUniversalAdditionalCameraData(); var ld = left.GetUniversalAdditionalCameraData();
            rd.renderPostProcessing = ld.renderPostProcessing; rd.antialiasing = ld.antialiasing; rd.renderShadows = ld.renderShadows;
            left.rect = new Rect(0f, 0f, 0.5f, 1f);
            }
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
            if (!headset && AttitudeSensor.current != null) { InputSystem.EnableDevice(AttitudeSensor.current); haveGyro = true; }
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
            foreach (var m in moved) if (m.c != null) { var rt = m.c.GetComponent<RectTransform>(); rt.SetParent(m.parent, false); m.c.renderMode = m.mode; rt.sizeDelta = m.size; rt.localScale = m.scale; var sc = m.c.GetComponent<CanvasScaler>(); if (sc != null) sc.enabled = m.scaler; }
            moved.Clear();
            if (left != null) left.transform.localRotation = Quaternion.identity;
            Instance = null; Destroy(this);
        }

        /// <summary>Where you are looking now counts as straight ahead.</summary>
        public void Recenter() { ReadGyro(); recenter = Quaternion.Inverse(Quaternion.Euler(0f, gyro.eulerAngles.y, 0f)); lastGyroLook = GyroLook(); headPos0 = headPos; }

        void ReadGyro()
        {
            if (headset)
            {
                // the headset's own tracking: where the head points and where it has moved to
                var dev = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.Head);
                Quaternion q; Vector3 p;
                if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out q)) gyro = q;
                if (dev.isValid && dev.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out p)) { headPos = p; if (!havePos) { havePos = true; headPos0 = p; } }
                return;
            }
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
            if (headset)
            {
                // leaning: the head's movement since the last re-centre, carried along whichever way the view now faces
                if (havePos)
                {
                    var local = Quaternion.Inverse(Quaternion.Euler(0f, gyro.eulerAngles.y, 0f)) * (headPos - headPos0);
                    left.transform.position += Quaternion.Euler(0f, left.transform.eulerAngles.y, 0f) * local;
                }
                // the menus: any flat screen that opens (pause, deploy, hangar, game over) is hung in front of the face
                sweepT -= Time.unscaledDeltaTime;
                if (sweepT <= 0f)
                {
                    sweepT = 0.4f;
                    foreach (var c in FindObjectsOfType<Canvas>())
                    {
                        if (c == hud || !c.isRootCanvas || c.renderMode != RenderMode.ScreenSpaceOverlay) continue;
                        var rt = c.GetComponent<RectTransform>(); var sc = c.GetComponent<CanvasScaler>();
                        moved.Add(new Moved { c = c, mode = c.renderMode, parent = rt.parent, size = rt.sizeDelta, scale = rt.localScale, scaler = sc != null && sc.enabled });
                        var size = sc != null && sc.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize ? sc.referenceResolution : new Vector2(1280f, 720f);
                        if (sc != null) sc.enabled = false;
                        c.renderMode = RenderMode.WorldSpace; c.worldCamera = left;
                        rt.SetParent(head, false); rt.sizeDelta = size; rt.localScale = Vector3.one * (1.5f / size.x); rt.localPosition = new Vector3(0f, 0f, 1.25f - 0.002f * c.sortingOrder); rt.localRotation = Quaternion.identity;
                    }
                    moved.RemoveAll(m => m.c == null);
                }
            }
            // the eyes: left stays put, right sits beside it; same lens
            if (right != null) { right.fieldOfView = left.fieldOfView; right.nearClipPlane = left.nearClipPlane; right.farClipPlane = left.farClipPlane; }
        }
        void OnDestroy() { if (Instance == this) Instance = null; }
    }
}
