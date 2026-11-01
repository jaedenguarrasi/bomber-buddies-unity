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
        public bool CrouchPressed { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool ViewPressed { get; private set; }
        /// <summary>Flying: throttle change per second (-1..1), E/Q, the pad's dpad, or the on-screen slider.</summary>
        public float Throttle { get; private set; }
        /// <summary>Get in / get out of a vehicle: F, the pad's select, or the on-screen button.</summary>
        public bool InteractPressed { get; private set; }
        public bool IsTouch { get; private set; }

        [Header("Sensitivity")]
        public float mouseDegPerPixel = 0.09f;
        public float stickDegPerSecond = 170f;
        public float touchDegPerPixel = 0.16f;

        InputAction move, look, fire, aim, sprint, jump, crouch, reload, view, throttle, interact;
        bool jumpQ, crouchQ, reloadQ, viewQ, interactQ;

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
            fire.AddBinding("<Gamepad>/rightShoulder");

            aim = new InputAction("Aim", InputActionType.Button);
            aim.AddBinding("<Mouse>/rightButton");
            aim.AddBinding("<Gamepad>/leftTrigger");

            sprint = new InputAction("Sprint", InputActionType.Button);
            sprint.AddBinding("<Keyboard>/leftShift");
            sprint.AddBinding("<Gamepad>/leftStickPress");
            sprint.AddBinding("<Gamepad>/leftShoulder");

            throttle = new InputAction("Throttle", InputActionType.Value);
            throttle.AddCompositeBinding("1DAxis").With("Positive", "<Keyboard>/e").With("Negative", "<Keyboard>/q");
            throttle.AddBinding("<Gamepad>/dpad/y");

            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/f");
            interact.AddBinding("<Gamepad>/select");
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
            reload.AddBinding("<Gamepad>/buttonWest");
            reload.performed += _ => reloadQ = true;

            view = new InputAction("View", InputActionType.Button);
            view.AddBinding("<Keyboard>/v");
            view.AddBinding("<Gamepad>/rightStickPress");
            view.AddBinding("<Gamepad>/buttonNorth");
            view.performed += _ => viewQ = true;

            EnhancedTouchSupport.Enable();
        }

        void OnEnable()
        {
            move.Enable(); look.Enable(); fire.Enable(); aim.Enable(); sprint.Enable(); jump.Enable(); crouch.Enable(); reload.Enable(); view.Enable(); throttle.Enable(); interact.Enable();
        }

        void OnDisable()
        {
            move.Disable(); look.Disable(); fire.Disable(); aim.Disable(); sprint.Disable(); jump.Disable(); crouch.Disable(); reload.Disable(); view.Disable(); throttle.Disable(); interact.Disable();
        }

        void Update()
        {
            Move = Vector2.ClampMagnitude(move.ReadValue<Vector2>(), 1f);

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
            if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame && !Application.isMobilePlatform) IsTouch = false;
            Look = deg;

            Fire = fire.IsPressed();
            Aim = aim.IsPressed();
            Sprint = sprint.IsPressed();
            JumpPressed = jumpQ; jumpQ = false;
            CrouchPressed = crouchQ; crouchQ = false;
            ReloadPressed = reloadQ; reloadQ = false;
            ViewPressed = viewQ; viewQ = false;
            InteractPressed = interactQ; interactQ = false;
            Throttle = Mathf.Clamp(throttle.ReadValue<float>(), -1f, 1f);

            // desktop: the mouse is captured while playing (Escape frees it)
            if (!Application.isMobilePlatform && Mouse.current != null)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Cursor.lockState = CursorLockMode.None;
                else if (Mouse.current.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject())) Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = Cursor.lockState != CursorLockMode.Locked;
                if (Cursor.lockState != CursorLockMode.Locked && fromMouse) Look = Vector2.zero;   // no spinning while the mouse is free
            }
        }
    }
}
