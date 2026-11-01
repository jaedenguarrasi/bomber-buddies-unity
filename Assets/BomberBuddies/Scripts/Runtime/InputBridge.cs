using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace BomberBuddies
{
    /// <summary>
    /// One place that turns keyboard + mouse, a gamepad and the phone's touch screen into the same handful of intentions.
    /// The actions are built in code so there is no asset to keep in step. Touch: the left half of the screen is the on-screen
    /// stick (built by the HUD, which feeds a virtual gamepad), the right half is look-by-drag.
    /// </summary>
    public class InputBridge : MonoBehaviour
    {
        public static InputBridge Instance { get; private set; }

        public Vector2 Move { get; private set; }
        public Vector2 Look { get; private set; }          // degrees to turn this frame: x yaw, y pitch (up positive)
        public bool Fire { get; private set; }
        public bool Aim { get; private set; }
        public bool Sprint { get; private set; }
        public bool JumpPressed { get; private set; }
        public bool JumpHeld { get; private set; }
        public bool CrouchPressed { get; private set; }
        public bool CrouchHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool ViewPressed { get; private set; }
        /// <summary>Flying: throttle change per second (-1..1), E/Q, the pad's dpad, or the on-screen slider.</summary>
        public float Throttle { get; private set; }
        /// <summary>Get in / get out of a vehicle: F, the pad's select, or the on-screen button.</summary>
        public bool InteractPressed { get; private set; }
        /// <summary>Bomb in the air, grenade on foot: G, middle mouse, the pad's east button, or the Bomb/Grenade touch button.</summary>
        public bool BombPressed { get; private set; }
        public bool BombHeld { get; private set; }
        /// <summary>Kick (on foot): X or the pad's west button.</summary>
        public bool KickPressed { get; private set; }
        public bool PausePressed { get; private set; }
        /// <summary>L1 (or C) held: the right stick walks the reticle instead of flying the aircraft.</summary>
        public bool LookHeld { get; private set; }
        /// <summary>A support call (the Predator missile) has the controls: the player's craft sees nothing, the call reads RawLook / RawFire.</summary>
        public static bool Hijacked;
        /// <summary>Where "level" is for tilt steering: set from the pause menu (Level tilt).</summary>
        public static Vector2 tiltLevel;
        public static void LevelTilt() { if (Accelerometer.current != null) { if (!Accelerometer.current.enabled) InputSystem.EnableDevice(Accelerometer.current); var a = Accelerometer.current.acceleration.ReadValue(); tiltLevel = new Vector2(a.x, a.y); } }
        public Vector2 RawLook { get; private set; }
        public Vector2 RawMove { get; private set; }
        public bool RawFire { get; private set; }
        public bool SupportPressed { get; private set; }
        public bool SeatPressed { get; private set; }
        public bool ItemsPressed { get; private set; }
        public bool MapPressed { get; private set; }
        bool supportQ, seatQ, itemsQ, mapQ;
        public bool IsTouch { get; private set; }
        /// <summary>The HUD's throttle slider while a finger drags it (-1..1), else 0.</summary>
        public static float TouchThrottle;

        [Header("Sensitivity")]
        public float mouseDegPerPixel = 0.09f;
        public float stickDegPerSecond = 170f;
        public float touchDegPerPixel = 0.16f;

        InputAction mapAct, itemsAct, seatAct, support, freeLook, move, look, fire, aim, sprint, jump, crouch, reload, view, throttle, interact, bomb, kick, pause;
        bool jumpQ, crouchQ, reloadQ, viewQ, interactQ, bombQ, kickQ, pauseQ;

        void Awake()
        {
            Instance = this;
            IsTouch = Application.isMobilePlatform;
            move = new InputAction("Move", InputActionType.Value);
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow").With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            move.AddBinding("<Gamepad>/leftStick");

            look = new InputAction("Look", InputActionType.Value);
            look.AddBinding("<Mouse>/delta");
            look.AddBinding("<Gamepad>/rightStick");

            fire = new InputAction("Fire", InputActionType.Button);
            fire.AddBinding("<Mouse>/leftButton");
            fire.AddBinding("<Gamepad>/rightTrigger");

            aim = new InputAction("Aim", InputActionType.Button);
            aim.AddBinding("<Mouse>/rightButton");
            aim.AddBinding("<Gamepad>/leftTrigger");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");

            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/e").With("Negative", "<Keyboard>/q");
            throttle.AddBinding("<Gamepad>/dpad/y");

            bomb = new InputAction("Bomb", InputActionType.Button);
            bomb.AddBinding("<Keyboard>/g");
            bomb.AddBinding("<Mouse>/middleButton");
            bomb.AddBinding("<Gamepad>/rightShoulder");
            bomb.performed += _ => bombQ = true;

            kick = new InputAction("Kick", InputActionType.Button);
            kick.AddBinding("<Keyboard>/x");
            kick.AddBinding("<Gamepad>/buttonWest");
            kick.performed += _ => kickQ = true;

            pause = new InputAction("Pause", InputActionType.Button);
            pause.AddBinding("<Keyboard>/p");
            pause.AddBinding("<Gamepad>/start");
            pause.performed += _ => pauseQ = true;

            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/f");
            interact.AddBinding("<Gamepad>/buttonWest");
            interact.performed += _ => interactQ = true;

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");
            jump.performed += _ => jumpQ = true;

            crouch = new InputAction("Crouch", InputActionType.Button);
            crouch.AddBinding("<Keyboard>/c");
            crouch.AddBinding("<Keyboard>/leftCtrl");
            crouch.AddBinding("<Gamepad>/buttonEast");
            crouch.performed += _ => crouchQ = true;

            reload = new InputAction("Reload", InputActionType.Button);
            reload.AddBinding("<Keyboard>/r");
            reload.AddBinding("<Gamepad>/dpad/right");
            reload.performed += _ => reloadQ = true;

            view = new InputAction("View", InputActionType.Button);
            view.AddBinding("<Keyboard>/v");
            view.AddBinding("<Gamepad>/buttonNorth");
            view.performed += _ => viewQ = true;

            freeLook = new InputAction("FreeLook", InputActionType.Button);
            freeLook.AddBinding("<Keyboard>/c");
            freeLook.AddBinding("<Gamepad>/leftShoulder");

            support = new InputAction("Support", InputActionType.Button);
            support.AddBinding("<Keyboard>/tab");
            support.AddBinding("<Gamepad>/dpad/up");
            support.performed += _ => supportQ = true;

            seatAct = new InputAction("Seat", InputActionType.Button);
            seatAct.AddBinding("<Keyboard>/b");
            seatAct.AddBinding("<Gamepad>/dpad/left");
            seatAct.performed += _ => seatQ = true;

            itemsAct = new InputAction("Items", InputActionType.Button);
            itemsAct.AddBinding("<Keyboard>/i");
            itemsAct.AddBinding("<Gamepad>/dpad/down");
            itemsAct.performed += _ => itemsQ = true;

            mapAct = new InputAction("Map", InputActionType.Button);
            mapAct.AddBinding("<Keyboard>/m");
            mapAct.AddBinding("<Gamepad>/select");
            mapAct.performed += _ => mapQ = true;

            EnhancedTouchSupport.Enable();
            // saved remaps (the Controls screen): overrides by action name, keyboard/mouse and pad kept apart
            foreach (var a in Rebindable)
            {
                string ov = PlayerPrefs.GetString("bb_bind_" + a.name, ""); if (string.IsNullOrEmpty(ov)) continue;
                foreach (var part in ov.Split(';')) { int eq = part.IndexOf('='); if (eq <= 0) continue; int idx; if (int.TryParse(part.Substring(0, eq), out idx) && idx < a.bindings.Count) a.ApplyBindingOverride(idx, part.Substring(eq + 1)); }
            }
        }

        /// <summary>The button actions the Controls screen can remap (sticks and the mouse look stay as they are).</summary>
        public InputAction[] Rebindable { get { return new[] { fire, aim, sprint, jump, crouch, reload, view, interact, bomb, kick, pause, freeLook, support, seatAct, itemsAct, mapAct }; } }
        /// <summary>Overrides saved by binding index (the actions are built in code, so their ids change every run).</summary>
        public void SaveBinding(InputAction a)
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < a.bindings.Count; i++) if (!string.IsNullOrEmpty(a.bindings[i].overridePath)) { if (sb.Length > 0) sb.Append(';'); sb.Append(i).Append('=').Append(a.bindings[i].overridePath); }
            PlayerPrefs.SetString("bb_bind_" + a.name, sb.ToString()); PlayerPrefs.Save();
        }
        public void ResetBindings() { foreach (var a in Rebindable) { a.RemoveAllBindingOverrides(); PlayerPrefs.DeleteKey("bb_bind_" + a.name); } PlayerPrefs.Save(); }

        void OnEnable()
        {
            mapAct.Enable(); itemsAct.Enable(); seatAct.Enable(); support.Enable(); freeLook.Enable(); move.Enable(); look.Enable(); fire.Enable(); aim.Enable(); sprint.Enable(); jump.Enable(); crouch.Enable(); reload.Enable(); view.Enable(); throttle.Enable(); interact.Enable(); bomb.Enable(); kick.Enable(); pause.Enable();
        }

        void OnDisable()
        {
            mapAct.Disable(); itemsAct.Disable(); seatAct.Disable(); support.Disable(); freeLook.Disable(); move.Disable(); look.Disable(); fire.Disable(); aim.Disable(); sprint.Disable(); jump.Disable(); crouch.Disable(); reload.Disable(); view.Disable(); throttle.Disable(); interact.Disable(); bomb.Disable(); kick.Disable(); pause.Disable();
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);
            // tilt steering (the original's steer: 'tilt'): in the air on a phone, the phone's lean is the stick
            if (PauseMenu.Tilt && IsTouch && Accelerometer.current != null && PlayerManager.Instance != null && !PlayerManager.Instance.OnFoot && PlayerManager.Instance.drive == null)
            {
                if (!Accelerometer.current.enabled) InputSystem.EnableDevice(Accelerometer.current);
                var a = Accelerometer.current.acceleration.ReadValue();
                // landscape: the long axis is x; lean left/right rolls, nose up/down pitches, relative to where you held it when you levelled
                float tx = Mathf.Clamp((a.x - tiltLevel.x) * 2.2f, -1f, 1f), ty = Mathf.Clamp((a.y - tiltLevel.y) * 2.2f, -1f, 1f);
                if (Mathf.Abs(Move.x) < 0.15f) Move = new Vector2(tx, Move.y); if (Mathf.Abs(Move.y) < 0.15f) Move = new Vector2(Move.x, -ty);
            }

            var l = look.ReadValue<Vector2>();
            bool fromMouse = look.activeControl != null && look.activeControl.device is Mouse;
            Vector2 deg = fromMouse ? l * mouseDegPerPixel : l * stickDegPerSecond * Time.deltaTime;

            // touch look: any finger that is not on a UI control and started on the right half of the screen
            if (Touchscreen.current != null)
            {
                foreach (var t in Touch.activeTouches)
                {
                    if (t.startScreenPosition.x < Screen.width * 0.42f) continue;
                    if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject(t.touchId)) continue;
                    if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved) deg += t.delta * touchDegPerPixel;
                }
                if (Touch.activeTouches.Count > 0) IsTouch = true;
            }
            // a real controller hides the touch controls; the on-screen stick and buttons drive a virtual gamepad, which must not
            if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame && !Application.isMobilePlatform && !string.IsNullOrEmpty(Gamepad.current.description.interfaceName)) IsTouch = false;
#if UNITY_EDITOR
            if (Keyboard.current != null && Keyboard.current.tKey.wasPressedThisFrame) { IsTouch = !IsTouch; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }   // editor: T shows the touch layer for testing with the mouse
#endif
            deg *= PauseMenu.Sensitivity;
            if (PauseMenu.InvertY) deg.y = -deg.y;
            RawLook = deg; Look = Hijacked ? Vector2.zero : deg;
            RawMove = Move; if (Hijacked) Move = Vector2.zero;

            LookHeld = freeLook.IsPressed();
            RawFire = fire.IsPressed(); Fire = !Hijacked && RawFire;
            SupportPressed = supportQ; supportQ = false; SeatPressed = seatQ; seatQ = false; ItemsPressed = itemsQ; itemsQ = false; MapPressed = mapQ; mapQ = false;
            Aim = aim.IsPressed();
            Sprint = sprint.IsPressed();
            JumpPressed = jumpQ; jumpQ = false; JumpHeld = jump.IsPressed() && !Hijacked;
            CrouchPressed = crouchQ; crouchQ = false; CrouchHeld = crouch.IsPressed() && !Hijacked;
            ReloadPressed = reloadQ; reloadQ = false;
            ViewPressed = viewQ; viewQ = false;
            InteractPressed = interactQ; interactQ = false;
            BombPressed = bombQ; bombQ = false; BombHeld = bomb.IsPressed() && !Hijacked;
            KickPressed = kickQ; kickQ = false;
            PausePressed = pauseQ; pauseQ = false;
            Throttle = Mathf.Clamp(throttle.ReadValue<float>() + TouchThrottle, -1f, 1f);

            // desktop: the mouse is captured while playing (Escape frees it)
            if (!Application.isMobilePlatform && Mouse.current != null && !IsTouch)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
                else if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
                if (Cursor.lockState != CursorLockMode.Locked && fromMouse) Look = Vector2.zero;   // no spinning while the mouse is free
            }
            else if (IsTouch && Cursor.lockState == CursorLockMode.Locked) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }
    }
}
