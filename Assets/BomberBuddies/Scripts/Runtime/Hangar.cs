using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The hangar (the original's shop): bank, upgrades, guns, craft and team on four tabs. Opened from the title or the pause menu.</summary>
    public class Hangar : MonoBehaviour
    {
        public static Hangar Instance { get; private set; }
        public bool IsOpen { get { return gameObject.activeSelf; } }
        Font font;
        Action onClose;
        string tab = "Upgrades";
        Text bankText, title;
        RectTransform list;
        Button[] tabButtons;
        bool eraseArmed;
        static readonly string[] Tabs = { "Upgrades", "Guns", "Craft", "Team" };

        public static Hangar Open(Transform parent, Action onClose)
        {
            if (Instance != null) { Instance.onClose = onClose; Instance.gameObject.SetActive(true); Instance.Render(); return Instance; }
            var go = new GameObject("Hangar");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hangar>();
            Instance = h; h.onClose = onClose;
            h.Build();
            h.Render();
            return h;
        }

#if UNITY_EDITOR
        void Update() { if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.equalsKey.wasPressedThisFrame) { Progress.Bank += 1000; Render(); } }   // editor: = gives 1,000 credits for testing
#endif

        public static bool IsOpen { get { return Instance != null && Instance.gameObject.activeSelf; } }
        float flashT; string flashText;
        public static void Flash(string text) { if (Instance != null) { Instance.flashT = 1.6f; Instance.flashText = text; } }
        void LateUpdate() { if (flashT > 0f) { flashT -= Time.unscaledDeltaTime; if (bankText != null) bankText.text = flashT > 0f ? flashText : "● " + Progress.Fmt(Progress.Bank) + " credits"; } }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var camGo = new GameObject("Hangar camera"); camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f); cam.depth = 50f; cam.cullingMask = 0;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            var bg = Img("Background", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.07f, 0.12f, 0.97f)); bg.raycastTarget = true;
            var sa = Rect("Safe", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var safe = sa.GetComponent<RectTransform>(); safe.offsetMin = new Vector2(44f, 20f); safe.offsetMax = new Vector2(-44f, -16f);

            title = Txt("Title", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, -26f), new Vector2(500f, 44f), 30, TextAnchor.MiddleLeft, Color.white, true); title.text = "HANGAR";
            var tl = title.rectTransform; tl.pivot = new Vector2(0f, 0.5f); tl.anchoredPosition = new Vector2(0f, -26f);
            bankText = Txt("Bank", safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(0f, -26f), new Vector2(420f, 44f), 24, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f), true);
            bankText.rectTransform.pivot = new Vector2(1f, 0.5f);
            var back = Btn("Back", safe, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(260f, 56f), "Back", 22, () => Close());

            tabButtons = new Button[Tabs.Length];
            for (int i = 0; i < Tabs.Length; i++)
            {
                string t = Tabs[i];
                tabButtons[i] = Btn("Tab " + t, safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(110f + i * 190f, -84f), new Vector2(180f, 44f), t, 20, () => { tab = t; Render(); });
            }

            // the scrolling list
            var view = Rect("View", safe, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var vr = view.GetComponent<RectTransform>(); vr.offsetMin = new Vector2(0f, 72f); vr.offsetMax = new Vector2(0f, -116f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Rect("Content", view.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            list = content.GetComponent<RectTransform>(); list.pivot = new Vector2(0.5f, 1f);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 8f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(0, 16, 0, 0);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list; scroll.viewport = vr; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        }

        public void Close()
        {
            gameObject.SetActive(false);
            var cb = onClose; onClose = null;
            if (cb != null) cb();
        }

        void Render()
        {
            bankText.text = "● " + Progress.Fmt(Progress.Bank) + " credits";
            for (int i = 0; i < Tabs.Length; i++) { var im = tabButtons[i].GetComponent<Image>(); im.color = Tabs[i] == tab ? new Color(0.3f, 0.42f, 0.6f, 1f) : new Color(0.14f, 0.18f, 0.26f, 1f); }
            foreach (Transform c in list) Destroy(c.gameObject);
            string craft = Progress.Craft;
            var cd = Progress.Def(craft);
            title.text = "HANGAR  ·  " + (cd != null ? cd.name : craft);
            switch (tab)
            {
                case "Upgrades":
                    {
                        string group = null;
                        foreach (var it in Progress.Catalog)
                        {
                            if (it.group != group) { group = it.group; Header(group); }
                            int lvl = Progress.Level(craft, it.id);
                            bool more = Progress.CanBuyMore(it, lvl);
                            int cost = more ? Progress.CostOf(it, lvl) : 0;
                            string lv = "Level " + lvl + (it.endless ? "" : " / " + it.max) + (lvl > it.max ? "  (past the top)" : "");
                            string bid = it.id;
                            Row(it.name, it.desc + "\n" + lv, more ? Progress.Fmt(cost) : "Maxed", more && cost <= Progress.Bank, more ? (Action)(() => { if (Progress.Buy(craft, bid)) Render(); }) : null);
                        }
                        Footer();
                    }
                    break;
                case "Guns":
                    {
                        Header("Guns for the " + (cd != null ? cd.name.ToLower() : craft) + " — the fitted gun is marked");
                        string cur = Progress.GunOf(craft);
                        var gunList = new List<Progress.Gun>(); foreach (var g0 in Progress.Guns) if (Progress.GunFits(craft, g0)) gunList.Add(g0); foreach (var g0 in Progress.FootGuns) if (Progress.GunFits(craft, g0)) gunList.Add(g0);
                        foreach (var g in gunList)
                        {
                            bool own = Progress.OwnsGun(craft, g.id); int price = Progress.GunPrice(craft, g.id);
                            string gid = g.id;
                            string label = g.id == cur ? "Fitted" : own ? "Fit" : Progress.Fmt(price);
                            Row(g.name + (g.id == cur ? "  ✓" : ""), g.desc + "\nDamage " + g.dmg + " · rate " + (1f / g.interval).ToString("F0") + "/s · reach " + g.range, label, g.id == cur ? false : (own || price <= Progress.Bank), g.id == cur ? null : (Action)(() => { if (Progress.BuyGun(craft, gid)) Render(); }));
                        }
                    }
                    break;
                case "Craft":
                    {
                        Header("Flying now: " + (cd != null ? cd.name : craft));
                        foreach (var c in Progress.Crafts)
                        {
                            bool own = Progress.OwnsCraft(c.id); string cid = c.id;
                            string label = !c.available ? "Coming later" : c.id == craft ? "Chosen" : own ? "Choose" : Progress.Fmt(c.price);
                            bool can = c.available && c.id != craft && (own || c.price <= Progress.Bank);
                            Row(c.name + (c.id == craft ? "  ✓" : ""), c.desc + (c.available ? "" : "\nNot in this build yet — " + (c.price == 0 ? "free" : Progress.Fmt(c.price) + " credits") + " when it lands"), label, can, can ? (Action)(() => { if (Progress.PickCraft(cid)) Render(); }) : null);
                        }
                    }
                    break;
                case "Team":
                    {
                        Header("Teammates join every flight, fight on their own, and come back next flight if they are lost");
                        foreach (var m in Progress.Mates)
                        {
                            int have = Progress.MatesOf(m.id); int cost = Progress.MateCost(m.id); string mid = m.id;
                            var row = Row(m.name + "  ×" + have, m.desc, "Hire " + Progress.Fmt(cost), cost <= Progress.Bank, () => { if (Progress.Hire(mid)) Render(); });
                            if (have > 0) Btn("Fire", row, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(110f, 44f), "Let go", 16, () => { if (Progress.Fire(mid)) Render(); });
                        }
                    }
                    break;
            }
            Canvas.ForceUpdateCanvases();
            list.anchoredPosition = Vector2.zero;
        }

        void Footer()
        {
            var r = Rect("Erase", list, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 60f));
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 60f;
            Btn("EraseBtn", r.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 44f), eraseArmed ? "Really erase everything?" : "Erase all progress", 16, () =>
            {
                if (!eraseArmed) { eraseArmed = true; Render(); return; }
                eraseArmed = false; Progress.EraseAll(); Render();
            }).GetComponent<Image>().color = new Color(0.45f, 0.15f, 0.15f, 1f);
        }

        void Header(string text)
        {
            var r = Rect("Header", list, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 36f));
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 36f;
            var t = Txt("Text", r.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 18, TextAnchor.LowerLeft, new Color(0.75f, 0.85f, 1f), true); t.text = text.ToUpper();
            var tr = t.rectTransform; tr.offsetMin = new Vector2(8f, 0f); tr.offsetMax = new Vector2(-8f, -4f);
        }

        Transform Row(string name, string desc, string buttonLabel, bool enabled, Action onClick)
        {
            var r = Rect("Row " + name, list, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 84f));
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 84f;
            var im = r.gameObject.AddComponent<Image>(); im.color = new Color(0.12f, 0.15f, 0.22f, 0.95f);
            var n = Txt("Name", r.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 21, TextAnchor.UpperLeft, Color.white, true); n.text = name;
            n.rectTransform.offsetMin = new Vector2(16f, -34f); n.rectTransform.offsetMax = new Vector2(-240f, -8f);
            var d = Txt("Desc", r.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 14, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.92f)); d.text = desc;
            d.rectTransform.offsetMin = new Vector2(16f, 6f); d.rectTransform.offsetMax = new Vector2(-240f, -36f); d.horizontalOverflow = HorizontalWrapMode.Wrap;
            var b = Btn("Buy", r.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-16f, 0f), new Vector2(200f, 50f), buttonLabel, 18, onClick);
            b.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            b.interactable = enabled && onClick != null;
            b.GetComponent<Image>().color = enabled && onClick != null ? new Color(0.22f, 0.5f, 0.3f, 1f) : new Color(0.2f, 0.22f, 0.28f, 1f);
            return r.transform;
        }

        // ---- widgets ----
        Transform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go.transform;
        }
        Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color c)
        {
            var t = Rect(name, parent, anchorMin, anchorMax, pos, size); var im = t.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
        }
        Text Txt(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false)
        {
            var t = Rect(name, parent, anchorMin, anchorMax, pos, size); var tx = t.gameObject.AddComponent<Text>();
            tx.font = font; tx.fontSize = fontSize; tx.alignment = align; tx.color = c; tx.raycastTarget = false; tx.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
            return tx;
        }
        Button Btn(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, string label, int fontSize, Action onClick)
        {
            var t = Rect(name, parent, anchorMin, anchorMax, pos, size);
            var im = t.gameObject.AddComponent<Image>(); im.color = new Color(0.16f, 0.2f, 0.3f, 1f);
            var b = t.gameObject.AddComponent<Button>(); if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); onClick(); });
            var cols = b.colors; cols.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f); b.colors = cols;
            var tx = Txt("Label", t, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter, Color.white, true); tx.text = label;
            return b;
        }
    }
}
