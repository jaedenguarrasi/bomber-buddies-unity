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
            bool padOn = gp != null && !string.IsNullOrEmpty(gp.description.interfaceName);
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
                    float y = s.transform.position.y - Mathf.Abs(s.transform.position.x) * 0.01f; if (y > bestY) { bestY = y; best = s; }
                }
                if (best != null) { es.SetSelectedGameObject(best.gameObject); sel = best.gameObject; }
            }
            // show it
            if (sel != lastSel) { Restore(); if (sel != null) { lastImg = sel.GetComponent<Image>(); if (lastImg != null) { lastColor = lastImg.color; lastImg.color = Color.Lerp(lastColor, Color.white, 0.35f); } lastSel = sel; } }
            // B backs out of whatever is open
            if (gp.buttonEast.wasPressedThisFrame)
            {
                if (Hangar.IsOpen) Hangar.Instance.Close();
                else if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) PauseMenu.Instance.Close();
            }
        }
        void Restore() { if (lastImg != null) lastImg.color = lastColor; lastImg = null; lastSel = null; }
    }
}
