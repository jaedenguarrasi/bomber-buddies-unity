using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The original's Controls screen (oCtl / renderCtl): a back arrow, the eight tabs, and for each tab the boxes of
    /// options with their section heading, label, note and choice buttons, sliders where the original had sliders, the keys
    /// table on the Keyboard tab and the controller status on the Controller tab. Every entry comes from OptionDefs.</summary>
    public class OptionsScreen : MonoBehaviour
    {
        public static OptionsScreen Instance { get; private set; }
        Font font; GameObject root; RectTransform list; ScrollRect scroll; System.Action onClose; string tab = "Touch"; Transform tabsRow; Text padInfo;
        static readonly Color Yellow = new Color(0.91f, 0.75f, 0.29f), Box = new Color(0.08f, 0.12f, 0.16f, 0.85f), BtnC = new Color(0.1f, 0.14f, 0.18f, 1f), BtnOn = new Color(0.93f, 0.73f, 0.3f, 1f), Ink = new Color(0.96f, 0.95f, 0.9f), Dim = new Color(0.75f, 0.78f, 0.82f);

        public static OptionsScreen Open(Transform canvas, System.Action onClose)
        {
            if (Instance == null) { var go = new GameObject("Controls screen", typeof(RectTransform)); go.transform.SetParent(canvas, false); Instance = go.AddComponent<OptionsScreen>(); Instance.Build(); }
            Instance.onClose = onClose; Instance.root.SetActive(true); Instance.transform.SetAsLastSibling(); Instance.Render(); return Instance;
        }
        public bool IsOpen { get { return root != null && root.activeSelf; } }
        public void Close() { root.SetActive(false); Settings.Save(); var cb = onClose; onClose = null; if (cb != null) cb(); }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); root = gameObject;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.16f, 0.22f, 0.27f, 1f); bg.raycastTarget = true;
            // the panel: back chevron, big title at the right, the tab row, then the list
            var head = Rect("Head", transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -40f), new Vector2(0f, 60f)); head.offsetMin = new Vector2(90f, -70f); head.offsetMax = new Vector2(-90f, -10f);
            head.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.14f, 0.18f, 0.9f);
            var chev = Btn("Back", head, new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(44f, 44f), "‹", 28, () => Close());
            var title = Txt("Title", head, new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(600f, 56f), 44, "Controls", TextAnchor.MiddleRight, Ink, true);
            Btn("Arrow", transform, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(44f, 44f), "←", 24, () => Close());
            tabsRow = Rect("Tabs", transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); var tr = (RectTransform)tabsRow; tr.offsetMin = new Vector2(90f, -138f); tr.offsetMax = new Vector2(-90f, -96f);
            var hl = tabsRow.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 8f; hl.childForceExpandWidth = false; hl.childForceExpandHeight = true; hl.childControlWidth = true; hl.childControlHeight = true;
            foreach (var t in OptionDefs.Tabs) { string tt = t; var b = Btn("Tab " + t, tabsRow, Vector2.zero, Vector2.zero, new Vector2(120f, 40f), t, 18, () => { tab = tt; Render(); scroll.verticalNormalizedPosition = 1f; }); var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 60f + t.Length * 11f; le.preferredHeight = 40f; }
            var view = Rect("View", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); view.offsetMin = new Vector2(90f, 16f); view.offsetMax = new Vector2(-90f, -146f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            list = Rect("List", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); list.pivot = new Vector2(0.5f, 1f);
            var vl = list.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 14f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(0, 12, 0, 0);
            list.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list; scroll.viewport = view; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            root.SetActive(false);
        }

        void Render()
        {
            foreach (Transform t in tabsRow) { var im = t.GetComponent<Image>(); var lb = t.Find("Label").GetComponent<Text>(); bool on = t.name == "Tab " + tab; im.color = on ? new Color(0.96f, 0.95f, 0.9f) : BtnC; lb.color = on ? new Color(0.1f, 0.12f, 0.15f) : Ink; }
            foreach (Transform c in list) Destroy(c.gameObject);
            string cur = ""; Transform box = null;
            foreach (var d in OptionDefs.All)
            {
                if (d.sec != null) cur = d.sec;
                string tabOf; if (!OptionDefs.TabOf.TryGetValue(cur, out tabOf)) tabOf = "Touch";
                if (tabOf != tab) continue;
                if (d.sec != null || box == null) box = NewBox(cur);
                if (d.k == "tiltAxis") { var o = OptRow(box, "Tilt level", "Hold the phone the way you fly and set it; the tilt options below are measured from there."); Choice(o, "Set level to how I am holding it now", false, () => { InputBridge.LevelTilt(); Sfx.Play("ui_click", 0.5f); }); }
                if (d.k == "mouseSens") KeysTable(box);
                if (d.k == "padDead") PadStatus(box);
                if (d.slider) { SliderRow(box, d); continue; }
                if (d.range != null) { RangeRow(box, d); continue; }
                var row = OptRow(box, d.label, d.note);
                foreach (var c in d.opts) { var cv = c[0]; Choice(row, (string)c[1], Settings.Is(d.k, cv), () => { Settings.Set(d.k, cv); Sfx.Play("ui_click", 0.5f); Render(); }); }
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(list);
        }

        Transform NewBox(string sec)
        {
            var b = Rect("Box", list, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            b.gameObject.AddComponent<Image>().color = Box;
            var vl = b.gameObject.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(14, 14, 10, 14); vl.spacing = 6f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true;
            b.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var h = Txt("Sec", b, Vector2.zero, Vector2.zero, new Vector2(0f, 34f), 24, sec, TextAnchor.MiddleLeft, Yellow, true); h.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f;
            return b;
        }
        Transform OptRow(Transform box, string label, string note)
        {
            var r = Rect("Opt", box, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var vl = r.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 4f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(0, 0, 6, 4);
            r.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var l = Txt("Label", r, Vector2.zero, Vector2.zero, new Vector2(0f, 24f), 18, label, TextAnchor.MiddleLeft, Ink, true); l.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f;
            if (!string.IsNullOrEmpty(note)) { var n = Txt("Note", r, Vector2.zero, Vector2.zero, new Vector2(0f, 20f), 14, note, TextAnchor.UpperLeft, Dim); n.horizontalOverflow = HorizontalWrapMode.Wrap; n.verticalOverflow = VerticalWrapMode.Overflow; n.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
            var seg = Rect("Seg", r, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hl = seg.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 6f; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true; hl.childAlignment = TextAnchor.MiddleLeft;
            seg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return seg;
        }
        void Choice(Transform seg, string label, bool on, UnityEngine.Events.UnityAction a)
        {
            var b = Btn("Choice", seg, Vector2.zero, Vector2.zero, new Vector2(100f, 40f), label, 17, a);
            b.GetComponent<Image>().color = on ? BtnOn : BtnC; var lb = b.transform.Find("Label").GetComponent<Text>(); lb.color = on ? new Color(0.12f, 0.1f, 0.05f) : Ink;
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 24f + label.Length * 9.5f; le.preferredHeight = 40f; le.flexibleWidth = 0f;
        }
        void SliderRow(Transform box, OptionDefs.Opt d)
        {
            float v = Settings.Num(d.k); var seg = OptRow(box, d.label, null); var pct = Txt("Pct", seg.parent, Vector2.zero, Vector2.zero, new Vector2(0f, 20f), 15, Mathf.RoundToInt(v * 100f) + "%", TextAnchor.MiddleLeft, Yellow, true); pct.transform.SetSiblingIndex(1); pct.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
            Slider(seg, 0.1f, 2f, v, nv => { Settings.Set(d.k, Mathf.Round(nv * 20f) / 20f); pct.text = Mathf.RoundToInt(nv * 100f) + "%"; });
        }
        void RangeRow(Transform box, OptionDefs.Opt d)
        {
            float v = Settings.Num(d.k); var seg = OptRow(box, d.label, d.note); var val = Txt("Val", seg.parent, Vector2.zero, Vector2.zero, new Vector2(0f, 20f), 15, OptionDefs.Fmt(d.k, v), TextAnchor.MiddleLeft, Yellow, true); val.transform.SetSiblingIndex(2); val.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f;
            Slider(seg, d.range[0], d.range[1], v, nv => { float snapped = Mathf.Round(nv / d.range[2]) * d.range[2]; Settings.Set(d.k, snapped); val.text = OptionDefs.Fmt(d.k, snapped); });
        }
        void Slider(Transform seg, float min, float max, float v, System.Action<float> on)
        {
            var go = new GameObject("Slider", typeof(RectTransform)); go.transform.SetParent(seg, false);
            var le = go.AddComponent<LayoutElement>(); le.preferredWidth = 420f; le.preferredHeight = 30f;
            var bgI = new GameObject("Bg", typeof(RectTransform)); bgI.transform.SetParent(go.transform, false); var br = bgI.GetComponent<RectTransform>(); br.anchorMin = new Vector2(0f, 0.5f); br.anchorMax = new Vector2(1f, 0.5f); br.sizeDelta = new Vector2(0f, 6f); bgI.AddComponent<Image>().color = new Color(0.3f, 0.34f, 0.4f);
            var fa = new GameObject("Fill area", typeof(RectTransform)); fa.transform.SetParent(go.transform, false); var far = fa.GetComponent<RectTransform>(); far.anchorMin = new Vector2(0f, 0.5f); far.anchorMax = new Vector2(1f, 0.5f); far.sizeDelta = new Vector2(0f, 6f);
            var fill = new GameObject("Fill", typeof(RectTransform)); fill.transform.SetParent(fa.transform, false); var fr = fill.GetComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = new Vector2(0f, 1f); fr.sizeDelta = Vector2.zero; fill.AddComponent<Image>().color = Yellow;
            var ha = new GameObject("Handle area", typeof(RectTransform)); ha.transform.SetParent(go.transform, false); var har = ha.GetComponent<RectTransform>(); har.anchorMin = Vector2.zero; har.anchorMax = Vector2.one; har.sizeDelta = Vector2.zero;
            var handle = new GameObject("Handle", typeof(RectTransform)); handle.transform.SetParent(ha.transform, false); var hr = handle.GetComponent<RectTransform>(); hr.sizeDelta = new Vector2(26f, 26f); handle.AddComponent<Image>().color = Ink;
            var s = go.AddComponent<Slider>(); s.fillRect = fr; s.handleRect = hr; s.minValue = min; s.maxValue = max; s.value = v; s.targetGraphic = handle.GetComponent<Image>();
            s.onValueChanged.AddListener(nv => on(nv));
        }
        void KeysTable(Transform box)
        {
            var seg = OptRow(box, "Keys", "Click the game once and the mouse is captured for aiming; Esc lets it go.");
            var rows = new[] { "W A S D or arrows — Steer, drive or walk", "Mouse — Aim the gun or turret; in an aircraft it is the flight stick", "Left click or Space — Fire (Space is Jump on foot)", "Right click or Z — Aim down the sight (hold)", "Middle click, G or B — Bomb, shell or grenade", "Shift — Sprint or boost; in the air, throttle up", "Ctrl or C — Crouch (hold: prone); C held in the air is free look", "E / Q — Throttle up / down", "R — Reload or cycle the gun", "F — Get in / out", "V — Camera", "X — Kick / special", "Tab — Support tablet", "I — Item wheel", "M — Map", "P or Esc — Pause" };
            seg.GetComponent<HorizontalLayoutGroup>().enabled = false; var vl = seg.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 2f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true;
            foreach (var r in rows) { var t = Txt("Key", seg, Vector2.zero, Vector2.zero, new Vector2(0f, 20f), 15, r, TextAnchor.MiddleLeft, Dim); t.gameObject.AddComponent<LayoutElement>().preferredHeight = 20f; }
            var remap = OptRow(box, "Buttons", "Open the remap screen to change any key or pad button.");
            Choice(remap, "Remap keys and buttons", false, () => { ControlsScreen.Open(transform.parent, null); ControlsScreen.Instance.transform.SetAsLastSibling(); });
        }
        void PadStatus(Transform box)
        {
            var seg = OptRow(box, "Status", null);
            padInfo = Txt("PadInfo", seg, Vector2.zero, Vector2.zero, new Vector2(0f, 40f), 15, "", TextAnchor.UpperLeft, Dim); padInfo.horizontalOverflow = HorizontalWrapMode.Wrap; var le = padInfo.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 900f; le.preferredHeight = 44f;
            var remap = OptRow(box, "Buttons", "Tap Change on the remap screen, then press the button you want on the controller.");
            Choice(remap, "Remap buttons", false, () => { ControlsScreen.Open(transform.parent, null); ControlsScreen.Instance.transform.SetAsLastSibling(); });
        }

        void Update()
        {
            if (!IsOpen) return;
            if (padInfo != null && tab == "Controller")
            {
                var gp = Gamepad.current;
                if (gp == null || string.IsNullOrEmpty(gp.description.interfaceName)) padInfo.text = "No controller detected. Pair it in the phone's Bluetooth settings, then press any button on it while this screen is open.";
                else { var held = new List<string>(); foreach (var c in gp.allControls) if (c is UnityEngine.InputSystem.Controls.ButtonControl && ((UnityEngine.InputSystem.Controls.ButtonControl)c).isPressed && !c.synthetic) held.Add(c.displayName); padInfo.text = "Connected: " + gp.displayName + " (standard layout). Left stick " + gp.leftStick.ReadValue().x.ToString("0.0") + ", " + gp.leftStick.ReadValue().y.ToString("0.0") + ". Right stick " + gp.rightStick.ReadValue().x.ToString("0.0") + ", " + gp.rightStick.ReadValue().y.ToString("0.0") + ". Pressed: " + (held.Count > 0 ? string.Join(", ", held.ToArray()) : "nothing") + "."; }
            }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) Close();
        }

        RectTransform Rect(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size; return rt;
        }
        Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fs, string text, TextAnchor align, Color c, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = align; t.color = c; t.text = text; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        Button Btn(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, int fs, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = BtnC;
            var b = go.AddComponent<Button>(); b.onClick.AddListener(onClick);
            var t = Txt("Label", go.transform, new Vector2(0.5f, 0.5f), Vector2.zero, size, fs, label, TextAnchor.MiddleCenter, Ink, true); t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.sizeDelta = Vector2.zero; t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return b;
        }
    }
}
