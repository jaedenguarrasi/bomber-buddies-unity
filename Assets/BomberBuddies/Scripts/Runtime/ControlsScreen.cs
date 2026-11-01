using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The Controls screen (the original's oControls): every button action with its key and its pad button, tap one
    /// and press the key or button you want instead; Reset puts the original's map back.</summary>
    public class ControlsScreen : MonoBehaviour
    {
        public static ControlsScreen Instance { get; private set; }
        Font font; GameObject root; Transform list; System.Action onClose; InputActionRebindingExtensions.RebindingOperation op; Text hint;

        public static ControlsScreen Open(Transform canvas, System.Action onClose)
        {
            if (Instance == null) { var go = new GameObject("Controls", typeof(RectTransform)); go.transform.SetParent(canvas, false); Instance = go.AddComponent<ControlsScreen>(); Instance.Build(); }
            Instance.onClose = onClose; Instance.root.SetActive(true); Instance.Render(); return Instance;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); root = gameObject;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.94f); bg.raycastTarget = true;
            Txt("Title", transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 50f), 34, "CONTROLS", TextAnchor.MiddleCenter, true);
            hint = Txt("Hint", transform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(1000f, 30f), 18, "Tap a binding, then press the key or pad button you want. Sticks, the mouse and the touch layout stay as they are.", TextAnchor.MiddleCenter);
            var panel = new GameObject("Panel", typeof(RectTransform)); panel.transform.SetParent(transform, false);
            var pr = panel.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f); pr.sizeDelta = new Vector2(1000f, 480f); pr.anchoredPosition = new Vector2(0f, -10f);
            var grid = panel.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(490f, 48f); grid.spacing = new Vector2(10f, 8f); grid.childAlignment = TextAnchor.UpperCenter;
            list = panel.transform;
            Btn("Reset", transform, new Vector2(0.5f, 0f), new Vector2(-170f, 50f), new Vector2(240f, 56f), "Reset to original", () => { if (InputBridge.Instance != null) InputBridge.Instance.ResetBindings(); Render(); });
            Btn("Back", transform, new Vector2(0.5f, 0f), new Vector2(170f, 50f), new Vector2(240f, 56f), "Back", () => Close());
            root.SetActive(false);
        }

        public void Close() { if (op != null) { op.Cancel(); op = null; } root.SetActive(false); var cb = onClose; onClose = null; if (cb != null) cb(); }

        static string Label(string actionName)
        {
            switch (actionName)
            {
                case "Fire": return "Fire / gun"; case "Aim": return "Aim"; case "Sprint": return "Sprint / boost"; case "Jump": return "Jump"; case "Crouch": return "Crouch (hold: prone)";
                case "Reload": return "Reload / cycle gun"; case "View": return "Camera"; case "Interact": return "Get in / out"; case "Bomb": return "Bomb / grenade"; case "Kick": return "Kick / special";
                case "Pause": return "Pause"; case "FreeLook": return "Free look / cover"; case "Support": return "Support tablet"; case "Seat": return "Swap seat"; case "Items": return "Item wheel"; case "Map": return "Map";
                default: return actionName;
            }
        }

        void Render()
        {
            foreach (Transform c in list) Destroy(c.gameObject);
            var ib = InputBridge.Instance; if (ib == null) return;
            foreach (var a in ib.Rebindable)
            {
                var action = a;
                for (int bi = 0; bi < a.bindings.Count; bi++)
                {
                    var b = a.bindings[bi]; if (b.isComposite || b.isPartOfComposite) continue;
                    bool pad = b.effectivePath.Contains("<Gamepad>");
                    int idx = bi;
                    var row = Btn("Row", list, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(490f, 48f), "", () => Rebind(action, idx));
                    var t = row.transform.Find("Label").GetComponent<Text>(); t.alignment = TextAnchor.MiddleLeft; t.rectTransform.offsetMin = new Vector2(12f, 0f);
                    t.text = Label(a.name) + "  ·  " + (pad ? "pad" : "keys") + "     <b>" + InputControlPath.ToHumanReadableString(b.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice) + "</b>";
                    t.supportRichText = true;
                }
            }
        }

        void Rebind(InputAction a, int idx)
        {
            if (op != null) op.Cancel();
            bool pad = a.bindings[idx].effectivePath.Contains("<Gamepad>");
            hint.text = "Press the " + (pad ? "pad button" : "key or mouse button") + " for " + Label(a.name) + "  (Esc cancels)";
            a.Disable();
            op = a.PerformInteractiveRebinding(idx).WithCancelingThrough("<Keyboard>/escape").WithControlsExcluding("<Mouse>/position").WithControlsExcluding("<Mouse>/delta").WithControlsExcluding("<Pointer>/position")
                .OnMatchWaitForAnother(0.05f)
                .OnComplete(o => { o.Dispose(); op = null; a.Enable(); InputBridge.Instance.SaveBinding(a); hint.text = "Saved."; Render(); Sfx.Play("ui_click", 0.6f, 1.2f); })
                .OnCancel(o => { o.Dispose(); op = null; a.Enable(); hint.text = "Cancelled."; });
            if (pad) op = op.WithControlsExcluding("<Keyboard>").WithControlsExcluding("<Mouse>"); else op = op.WithControlsExcluding("<Gamepad>");
            op.Start();
        }

        Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fs, string text, TextAnchor align, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = align; t.color = Color.white; t.text = text; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.horizontalOverflow = HorizontalWrapMode.Wrap;
            return t;
        }
        Button Btn(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.16f, 0.2f, 0.3f, 1f);
            var b = go.AddComponent<Button>(); b.onClick.AddListener(onClick);
            var t = Txt("Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, 18, label, TextAnchor.MiddleCenter, true); t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero;
            return b;
        }
    }
}
