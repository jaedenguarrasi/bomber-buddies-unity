using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace BomberBuddies
{
    /// <summary>A headset's two hand controllers (Valve Index, Vive, Quest Touch) read as one ordinary gamepad, so every binding and
    /// every menu works as it is. Left stick moves, right stick looks; right trigger fires, right grip drops the bomb or throws the
    /// grenade; left trigger aims, left grip is the left shoulder; A and B on the right are jump / choose and crouch / back; A and B
    /// on the left are kick / get out and view (hold: re-centre); the left stick click sprints, the right stick click pauses; the
    /// left trackpad's top and bottom are the D-pad's up and down (throttle).</summary>
    public class XrHands : MonoBehaviour
    {
        public const string PadName = "VR hands";
        Gamepad pad; float pollT;

        public static void Create(Transform parent)
        {
            var go = new GameObject("XrHands"); go.transform.SetParent(parent, false); go.AddComponent<XrHands>();
        }

        static InputDevice Hand(string usage)
        {
            foreach (var d in InputSystem.devices)
            {
                if (!(d is UnityEngine.InputSystem.XR.XRController)) continue;
                foreach (var u in d.usages) if (u.ToString() == usage) return d;
            }
            return null;
        }

        static Vector2 V2(InputDevice d, string a, string b = null)
        {
            if (d == null) return Vector2.zero;
            var c = d.TryGetChildControl<Vector2Control>(a); if (c == null && b != null) c = d.TryGetChildControl<Vector2Control>(b);
            return c != null ? c.ReadValue() : Vector2.zero;
        }
        static float F(InputDevice d, string a, string b = null)
        {
            if (d == null) return 0f;
            var c = d.TryGetChildControl<AxisControl>(a); if (c == null && b != null) c = d.TryGetChildControl<AxisControl>(b);
            return c != null ? c.ReadValue() : 0f;
        }
        static bool B(InputDevice d, string a, string b = null) { return F(d, a, b) > 0.5f; }

        void Update()
        {
            // a headset that woke up after the battle began still gets its view
            pollT -= Time.unscaledDeltaTime;
            if (pollT <= 0f) { pollT = 1f; if (VrMode.HeadsetActive && VrMode.Instance == null) VrMode.Refresh(); }
            if (!VrMode.HeadsetActive) { if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; } return; }
            var L = Hand("LeftHand"); var R = Hand("RightHand");
            if (L == null && R == null) return;
            if (pad == null) pad = (Gamepad)InputSystem.AddDevice("Gamepad", PadName);
            var st = new GamepadState();
            st.leftStick = V2(L, "thumbstick", "primary2DAxis"); st.rightStick = V2(R, "thumbstick", "primary2DAxis");
            st.leftTrigger = F(L, "trigger"); st.rightTrigger = F(R, "trigger");
            var tp = V2(L, "trackpad", "secondary2DAxis");
            st = st.WithButton(GamepadButton.RightShoulder, B(R, "grip", "gripPressed")).WithButton(GamepadButton.LeftShoulder, B(L, "grip", "gripPressed"))
                   .WithButton(GamepadButton.South, B(R, "primaryButton")).WithButton(GamepadButton.East, B(R, "secondaryButton"))
                   .WithButton(GamepadButton.West, B(L, "primaryButton")).WithButton(GamepadButton.North, B(L, "secondaryButton"))
                   .WithButton(GamepadButton.LeftStick, B(L, "thumbstickClicked", "primary2DAxisClick")).WithButton(GamepadButton.Start, B(R, "thumbstickClicked", "primary2DAxisClick"))
                   .WithButton(GamepadButton.DpadUp, tp.y > 0.5f).WithButton(GamepadButton.DpadDown, tp.y < -0.5f).WithButton(GamepadButton.DpadLeft, tp.x < -0.6f && Mathf.Abs(tp.y) < 0.5f).WithButton(GamepadButton.DpadRight, tp.x > 0.6f && Mathf.Abs(tp.y) < 0.5f);
            InputSystem.QueueStateEvent(pad, st);
        }
        void OnDestroy() { if (pad != null) { InputSystem.RemoveDevice(pad); pad = null; } }
    }
}
