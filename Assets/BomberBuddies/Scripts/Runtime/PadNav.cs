using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>Menus on a pad: when a stick or d-pad is pushed and nothing is selected, the first button on screen is,
    /// after which the input module's own navigation (d-pad / left stick, A to press, B to go back) takes over.
    /// The selected button is drawn a little brighter so you can see where you are.</summary>
    public class PadNav : MonoBehaviour
    {
        public static PadNav Create(Transform parent) { var go = new GameObject("Pad navigation"); go.transform.SetParent(parent, false); return go.AddComponent<PadNav>(); }
        GameObject lastSel; Color lastColor; Image lastImg;

        void Update()
        {
            var es = EventSystem.current; var gp = Gamepad.current;
            if (es == null) return;
            bool padOn = InputBridge.RealPad(gp);
            if (!padOn) { Restore(); return; }
            var sel = es.currentSelectedGameObject;
            bool wants = gp.dpad.ReadValue().sqrMagnitude > 0.2f || gp.leftStick.ReadValue().sqrMagnitude > 0.3f || gp.buttonSouth.wasPressedThisFrame;
            if ((sel == null || !sel.activeInHierarchy) && wants)
            {
                // the first interactable button that is actually on screen
                Selectable best = null; float bestY = -1e9f;
                foreach (var s in Selectable.allSelectablesArray)
                {
                    if (s == null || !s.IsActive() || !s.interactable || !s.gameObject.activeInHierarchy) continue;
                    var cg = s.GetComponentInParent<CanvasGroup>(); if (cg != null && (!cg.interactable || cg.alpha < 0.5f)) continue;
                    if (s.GetComponent<Button>() == null) continue;
                    // the original lands on the gold "go" button first (Play 3D, Resume, Deploy, Take off again), else the top one
                    var im = s.GetComponent<Image>(); bool go = im != null && im.color.r > 0.8f && im.color.g > 0.6f && im.color.b < 0.4f;
                    float y = s.transform.position.y - Mathf.Abs(s.transform.position.x) * 0.01f + (go ? 100000f : 0f); if (y > bestY) { bestY = y; best = s; }
                }
                if (best != null) { es.SetSelectedGameObject(best.gameObject); sel = best.gameObject; }
            }
            // show it
            if (sel != lastSel) { Restore(); if (sel != null) { lastImg = sel.GetComponent<Image>(); if (lastImg != null) { lastColor = lastImg.color; lastImg.color = Color.Lerp(lastColor, Color.white, 0.35f); } lastSel = sel; } }
            // B backs out of whatever is open
            if (gp.buttonEast.wasPressedThisFrame)
            {
                if (NetScreen.Instance != null && NetScreen.Instance.IsOpen) NetScreen.Instance.Back();
                else if (ControlsScreen.Instance != null && ControlsScreen.Instance.IsOpen) ControlsScreen.Instance.Close();
                else if (OptionsScreen.Instance != null && OptionsScreen.Instance.IsOpen) OptionsScreen.Instance.Close();
                else if (Hangar.IsOpen) Hangar.Instance.Close();
                else if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) PauseMenu.Instance.Close();
                else if (MainMenu.Instance != null) MainMenu.Instance.Back();
            }
            // L1 / R1 step through the tabs of whatever is open, as the original did
            int dir = gp.rightShoulder.wasPressedThisFrame ? 1 : gp.leftShoulder.wasPressedThisFrame ? -1 : 0;
            if (dir != 0)
            {
                if (OptionsScreen.Instance != null && OptionsScreen.Instance.IsOpen) OptionsScreen.Instance.StepTab(dir);
                else if (Hangar.IsOpen) Hangar.Instance.StepTab(dir);
                else if (MainMenu.Instance != null) MainMenu.Instance.StepTab(dir);
            }
        }
        void Restore() { if (lastImg != null) lastImg.color = lastColor; lastImg = null; lastSel = null; }
    }
}
