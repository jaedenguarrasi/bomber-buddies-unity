using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The title screen as the original drew it: category tabs (Campaign, Battles, Missions, Monsters, Dragons, Underground)
    /// and a tile per mode with its art and blurb. Modes this build cannot run yet are shown dimmed with "coming".</summary>
    public class MainMenu : MonoBehaviour
    {
        Font font;
        Action<string> onPick;
        string cat;
        Transform tiles; readonly List<Button> tabs = new List<Button>();
        ScrollRect scroll;

        public static MainMenu Create(Transform parent, MaterialLibrary lib, Action<string> onPick)
        {
            var go = new GameObject("Main menu");
            go.transform.SetParent(parent, false);
            var m = go.AddComponent<MainMenu>();
            m.onPick = onPick;
            m.Build();
            return m;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            cat = PlayerPrefs.GetString("bb_mcat", "Battles");
            var camGo = new GameObject("Menu camera"); camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem"); es.transform.SetParent(transform.parent, false);
                es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();
            }
            Img("Background", transform, Vector2.zero, Vector2.one, new Color(0.05f, 0.07f, 0.12f, 1f));
            var safe = Rect("Safe", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); safe.offsetMin = new Vector2(44f, 12f); safe.offsetMax = new Vector2(-44f, -8f);

            Txt("Title", safe, new Vector2(0f, 1f), new Vector2(230f, -34f), new Vector2(460f, 50f), 40, TextAnchor.MiddleLeft, Color.white, true).text = "BOMBER BUDDIES";
            // hangar + bank, top right
            var hb = Rect("Hangar button", safe, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-90f, -34f), new Vector2(180f, 48f));
            var hbi = hb.gameObject.AddComponent<Image>(); hbi.color = new Color(0.22f, 0.4f, 0.3f, 1f);
            var hbb = hb.gameObject.AddComponent<Button>(); hbb.onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); gameObject.SetActive(false); Hangar.Open(transform.parent, () => gameObject.SetActive(true)); });
            Txt("Label", hb, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(180f, 48f), 22, TextAnchor.MiddleCenter, Color.white, true).text = "Hangar";
            Txt("Bank", safe, new Vector2(1f, 1f), new Vector2(-340f, -34f), new Vector2(300f, 30f), 16, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f)).text = "● " + Progress.Fmt(Progress.Bank) + " credits  ·  " + (Progress.Def(Progress.Craft) != null ? Progress.Def(Progress.Craft).name : "");

            // the category tabs
            float x = 0f;
            foreach (var g in ModeDefs.Groups)
            {
                string gg = g;
                var b = Btn("Tab " + g, safe, new Vector2(0f, 1f), new Vector2(x + 80f, -84f), new Vector2(156f, 40f), g, 17, () => { cat = gg; PlayerPrefs.SetString("bb_mcat", cat); Sfx.Tone(520f, 0.04f, 0.04f, "triangle"); Render(); });
                tabs.Add(b); x += 162f;
            }

            // the scrolling tile grid
            var view = Rect("View", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); view.offsetMin = new Vector2(0f, 40f); view.offsetMax = new Vector2(0f, -112f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Rect("Tiles", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); content.pivot = new Vector2(0.5f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(282f, 236f); grid.spacing = new Vector2(14f, 14f); grid.childAlignment = TextAnchor.UpperCenter; grid.padding = new RectOffset(0, 0, 4, 8);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = view; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            tiles = content;
            Txt("Hint", safe, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1400f, 26f), 14, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.75f)).text = "Keyboard: WASD move · mouse look · Shift sprint/boost · Space jump/bomb · F get in/out · E/Q throttle · V view · C free look · Tab support · B swap seat";
            Render();
        }

        void Render()
        {
            foreach (var b in tabs) b.GetComponent<Image>().color = b.name == "Tab " + cat ? new Color(0.3f, 0.42f, 0.6f, 1f) : new Color(0.14f, 0.18f, 0.26f, 1f);
            foreach (Transform c in tiles) Destroy(c.gameObject);
            foreach (var kv in ModeDefs.All) if (kv.Value.group == cat) Tile(kv.Value);
            Canvas.ForceUpdateCanvases(); if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        void Tile(ModeDefs.Mode mode)
        {
            var rt = Rect("Tile " + mode.id, tiles, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(282f, 236f));
            var im = rt.gameObject.AddComponent<Image>(); im.color = mode.playable ? new Color(0.14f, 0.18f, 0.26f, 0.95f) : new Color(0.1f, 0.11f, 0.14f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>(); btn.interactable = mode.playable;
            var colors = btn.colors; colors.highlightedColor = new Color(0.22f, 0.3f, 0.42f); colors.pressedColor = new Color(0.35f, 0.5f, 0.7f); colors.disabledColor = Color.white; btn.colors = colors;
            string id = mode.id;
            btn.onClick.AddListener(() => { Sfx.Play("ui_click", 0.6f); Pick(id); });
            // the art, 320×180 like the original's tiles
            var art = Resources.Load<Sprite>("Art/" + mode.id); if (art == null) { var tex = Resources.Load<Texture2D>("Art/" + mode.id); if (tex != null) art = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); }
            var pic = Rect("Art", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); pic.offsetMin = new Vector2(6f, -154f); pic.offsetMax = new Vector2(-6f, -6f);
            var pim = pic.gameObject.AddComponent<Image>(); pim.raycastTarget = false;
            if (art != null) { pim.sprite = art; pim.preserveAspect = false; pim.color = mode.playable ? Color.white : new Color(0.45f, 0.45f, 0.5f); } else pim.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            Txt("Name", rt, new Vector2(0f, 1f), new Vector2(141f, -168f), new Vector2(270f, 24f), 19, TextAnchor.MiddleLeft, mode.playable ? Color.white : new Color(0.6f, 0.62f, 0.66f), true).text = mode.name + (mode.playable ? "" : "   · coming");
            var d = Txt("Desc", rt, new Vector2(0f, 1f), new Vector2(141f, -205f), new Vector2(270f, 48f), 11, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.92f));
            d.text = mode.desc; d.horizontalOverflow = HorizontalWrapMode.Wrap; d.verticalOverflow = VerticalWrapMode.Truncate;
        }

        void Pick(string id)
        {
            var cb = onPick;
            Destroy(gameObject);
            if (cb != null) cb(id);
        }

        RectTransform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos; rt.sizeDelta = size;
            return rt;
        }

        Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Color c)
        {
            var rt = Rect(name, parent, anchorMin, anchorMax, Vector2.zero, Vector2.zero);
            rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var im = rt.gameObject.AddComponent<Image>(); im.color = c; return im;
        }

        Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = c; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        Button Btn(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, int fs, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            rt.gameObject.AddComponent<Image>().color = new Color(0.14f, 0.18f, 0.26f, 1f);
            var b = rt.gameObject.AddComponent<Button>(); b.onClick.AddListener(onClick);
            Txt("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, size, fs, TextAnchor.MiddleCenter, Color.white, true).text = label;
            return b;
        }
    }
}
