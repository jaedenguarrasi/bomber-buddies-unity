using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The Options screen (the original's oOptions): a scrolling list of rows, tap a row to cycle its value, with the
    /// same groups the original had — Sound &amp; look, Touch, Flying &amp; crews, Difficulty, Graphics.</summary>
    public class OptionsScreen : MonoBehaviour
    {
        public static OptionsScreen Instance { get; private set; }
        class Row { public string group, label; public Func<string> value; public Action next; }
        readonly List<Row> rows = new List<Row>();
        Font font; GameObject root; Transform list; Action onClose;

        public static OptionsScreen Open(Transform canvas, Action onClose)
        {
            if (Instance == null) { var go = new GameObject("Options", typeof(RectTransform)); go.transform.SetParent(canvas, false); Instance = go.AddComponent<OptionsScreen>(); Instance.Build(); }
            Instance.onClose = onClose; Instance.root.SetActive(true); Instance.transform.SetAsLastSibling(); Instance.Render(); return Instance;
        }
        public void Close() { root.SetActive(false); Settings.Save(); var cb = onClose; onClose = null; if (cb != null) cb(); }

        static string OnOff(bool b) { return b ? "on" : "off"; }
        static float Cycle(float v, float[] steps) { for (int i = 0; i < steps.Length; i++) if (Mathf.Abs(steps[i] - v) < 0.001f) return steps[(i + 1) % steps.Length]; return steps[0]; }
        static string CycleS(string v, string[] steps) { int i = Array.IndexOf(steps, v); return steps[(i + 1) % steps.Length]; }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); root = gameObject;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 1f); bg.raycastTarget = true;
            Txt("Title", transform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 50f), 34, "OPTIONS", TextAnchor.MiddleCenter, true);
            Txt("Hint", transform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(1000f, 30f), 17, "Tap a row to change it. Everything saves on its own.", TextAnchor.MiddleCenter);
            var view = new GameObject("View", typeof(RectTransform)); view.transform.SetParent(transform, false);
            var vr = view.GetComponent<RectTransform>(); vr.anchorMin = new Vector2(0.5f, 0f); vr.anchorMax = new Vector2(0.5f, 1f); vr.offsetMin = new Vector2(-500f, 100f); vr.offsetMax = new Vector2(500f, -105f);
            view.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); view.AddComponent<RectMask2D>();
            var scroll = view.AddComponent<ScrollRect>();
            var panel = new GameObject("Panel", typeof(RectTransform)); panel.transform.SetParent(view.transform, false);
            var pr = panel.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(0f, 1f); pr.anchorMax = new Vector2(1f, 1f); pr.pivot = new Vector2(0.5f, 1f); pr.sizeDelta = Vector2.zero; pr.anchoredPosition = Vector2.zero;
            var vl = panel.AddComponent<VerticalLayoutGroup>(); vl.spacing = 6f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(0, 16, 0, 0);
            panel.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = pr; scroll.viewport = vr; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            list = panel.transform;
            Btn("Controls", transform, new Vector2(0.5f, 0f), new Vector2(-170f, 50f), new Vector2(240f, 56f), "Controls / remap", () => { root.SetActive(false); ControlsScreen.Open(transform.parent, () => { root.SetActive(true); Render(); }); ControlsScreen.Instance.transform.SetAsLastSibling(); });
            Btn("Back", transform, new Vector2(0.5f, 0f), new Vector2(170f, 50f), new Vector2(240f, 56f), "Back", () => Close());
            Define();
            root.SetActive(false);
        }

        void Define()
        {
            rows.Clear();
            var pct = new[] { 0f, 0.3f, 0.5f, 0.7f, 0.85f, 1f }; var sens = new[] { 0.4f, 0.55f, 0.7f, 0.85f, 1f, 1.2f, 1.5f, 2f, 2.5f, 3f };
            Add("Sound & look", "Volume", () => Mathf.RoundToInt(Settings.Volume * 100f) + " %", () => Settings.Volume = Cycle(Settings.Volume, pct));
            Add("Sound & look", "Look sensitivity", () => Mathf.RoundToInt(Settings.LookSens * 100f) + " %", () => Settings.LookSens = Cycle(Settings.LookSens, sens));
            Add("Sound & look", "Aim sensitivity (while aiming)", () => Mathf.RoundToInt(Settings.AimSens * 100f) + " %", () => Settings.AimSens = Cycle(Settings.AimSens, sens));
            Add("Sound & look", "Invert look", () => OnOff(Settings.InvertY), () => Settings.InvertY = !Settings.InvertY);
            Add("Sound & look", "Invert while aiming", () => OnOff(Settings.AimInvert), () => Settings.AimInvert = !Settings.AimInvert);
            Add("Sound & look", "Stick pitch (flying)", () => Settings.PadPitch == "pull" ? "pull back to climb" : "push forward to climb", () => Settings.PadPitch = Settings.PadPitch == "pull" ? "push" : "pull");
            Add("Sound & look", "Time slows while you aim", () => OnOff(Settings.SlowAim), () => Settings.SlowAim = !Settings.SlowAim);
            Add("Sound & look", "Camera banks with the plane", () => OnOff(Settings.CamBank), () => Settings.CamBank = !Settings.CamBank);
            Add("Touch", "Fire button", () => Settings.FireMode == "hold" ? "hold to fire" : Settings.FireMode == "toggle" ? "tap to start / stop" : "a held look-finger fires", () => Settings.FireMode = CycleS(Settings.FireMode, new[] { "hold", "toggle", "drag" }));
            Add("Touch", "Move stick", () => Settings.FixedStick ? "fixed in the corner" : "appears under your thumb", () => Settings.FixedStick = !Settings.FixedStick);
            Add("Touch", "Look by dragging the right half", () => OnOff(Settings.LookPad), () => Settings.LookPad = !Settings.LookPad);
            Add("Touch", "Two fingers to aim", () => OnOff(Settings.TwoFingerAim), () => Settings.TwoFingerAim = !Settings.TwoFingerAim);
            Add("Touch", "Fade the buttons when idle", () => OnOff(Settings.AutoHide), () => Settings.AutoHide = !Settings.AutoHide);
            Add("Touch", "Big buttons", () => OnOff(Settings.BigButtons), () => Settings.BigButtons = !Settings.BigButtons);
            Add("Touch", "Left-handed", () => OnOff(Settings.Lefty), () => Settings.Lefty = !Settings.Lefty);
            Add("Touch", "Steer in the air", () => Settings.Steer == "tilt" ? "tilt the phone" : "stick", () => { Settings.Steer = Settings.Steer == "tilt" ? "stick" : "tilt"; if (Settings.Steer == "tilt") InputBridge.LevelTilt(); });
            Add("Touch", "Tilt does", () => Settings.TiltRole == "fly" ? "the flying" : "the aiming", () => Settings.TiltRole = Settings.TiltRole == "fly" ? "aim" : "fly");
            Add("Touch", "Level the tilt now", () => "tap", () => { InputBridge.LevelTilt(); if (GameState.Hud != null) GameState.Hud.Comms("Tilt levelled"); });
            Add("Flying & crews", "Flight model", () => Settings.FlightModel == "aero" ? "aero: speed is lift, stalls, turns tighten with speed" : "arcade", () => Settings.FlightModel = Settings.FlightModel == "aero" ? "arcade" : "aero");
            Add("Flying & crews", "Helicopter scheme", () => Settings.HeliScheme == "b" ? "B: stick climbs, throttle drives" : "A: throttle climbs, stick drives", () => Settings.HeliScheme = Settings.HeliScheme == "b" ? "a" : "b");
            Add("Flying & crews", "Crew", () => Settings.CrewAI == "solo" ? "solo: the other seat is empty" : "AI crews the other seat", () => Settings.CrewAI = Settings.CrewAI == "solo" ? "ai" : "solo");
            Add("Flying & crews", "Chin gun", () => Settings.Turret == "auto" ? "tracks targets on its own" : "manual", () => Settings.Turret = Settings.Turret == "auto" ? "manual" : "auto");
            Add("Difficulty", "Difficulty", () => Settings.Difficulty, () => Settings.SetDifficulty(CycleS(Settings.Difficulty, new[] { "easy", "normal", "hard" })));
            var mult = new[] { 0.5f, 0.7f, 1f, 1.3f, 1.6f, 2f, 3f };
            Add("Difficulty", "Enemy toughness", () => "×" + Settings.EnemyHp, () => { Settings.EnemyHp = Cycle(Settings.EnemyHp, mult); Settings.Difficulty = "custom"; });
            Add("Difficulty", "Enemy damage", () => "×" + Settings.EnemyDmg, () => { Settings.EnemyDmg = Cycle(Settings.EnemyDmg, mult); Settings.Difficulty = "custom"; });
            Add("Difficulty", "Enemy accuracy", () => "×" + Settings.EnemyAcc, () => { Settings.EnemyAcc = Cycle(Settings.EnemyAcc, mult); Settings.Difficulty = "custom"; });
            Add("Difficulty", "Your toughness", () => "×" + Settings.YouHp, () => { Settings.YouHp = Cycle(Settings.YouHp, mult); Settings.Difficulty = "custom"; });
            Add("Difficulty", "Side", () => Settings.Side == "bad" ? "the monsters (your squad comes as greys)" : "the army", () => Settings.Side = Settings.Side == "bad" ? "good" : "bad");
            Add("Graphics", "Detail", () => Settings.Detail == 0 ? "low (older phones)" : Settings.Detail == 2 ? "high" : "normal", () => Settings.Detail = (Settings.Detail + 1) % 3);
        }
        void Add(string g, string l, Func<string> v, Action n) { rows.Add(new Row { group = g, label = l, value = v, next = n }); }

        void Render()
        {
            foreach (Transform c in list) Destroy(c.gameObject);
            string g = null;
            foreach (var r in rows)
            {
                if (r.group != g) { g = r.group; var h = Txt("Group", list, Vector2.zero, Vector2.zero, new Vector2(900f, 34f), 16, g.ToUpper(), TextAnchor.LowerLeft, true); h.color = new Color(1f, 0.85f, 0.35f); h.gameObject.AddComponent<LayoutElement>().preferredHeight = 34f; }
                var row = r; var b = Btn("Row", list, Vector2.zero, Vector2.zero, new Vector2(900f, 46f), "", () => { row.next(); Settings.Save(); Sfx.Play("ui_click", 0.5f); Render(); });
                b.gameObject.AddComponent<LayoutElement>().preferredHeight = 46f;
                var t = b.transform.Find("Label").GetComponent<Text>(); t.alignment = TextAnchor.MiddleLeft; t.rectTransform.offsetMin = new Vector2(14f, 0f); t.supportRichText = true;
                t.text = r.label + "     <color=#9fd3ff><b>" + r.value() + "</b></color>";
            }
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
