using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The title screen: the mode tiles from the original's mode select, grouped the same way. Tap one to play.</summary>
    public class MainMenu : MonoBehaviour
    {
        Font font;
        Action<string> onPick;

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
            var camGo = new GameObject("Menu camera"); camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f);
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 20;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1600f, 900f); scaler.matchWidthOrHeight = 0.6f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem"); es.transform.SetParent(transform.parent, false);
                es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();
            }
            var bg = Img("Background", transform, Vector2.zero, Vector2.one, new Color(0.05f, 0.07f, 0.12f, 1f));
            Txt("Title", transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(1200f, 90f), 64, TextAnchor.MiddleCenter, Color.white).text = "BOMBER BUDDIES";
            Txt("Sub", transform, new Vector2(0.5f, 1f), new Vector2(0f, -130f), new Vector2(1200f, 40f), 22, TextAnchor.MiddleCenter, new Color(0.7f, 0.78f, 0.9f)).text = "Pick a mode";
            var hb = Rect("Hangar button", transform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-150f, -70f), new Vector2(220f, 60f));
            var hbi = hb.gameObject.AddComponent<Image>(); hbi.color = new Color(0.22f, 0.4f, 0.3f, 1f);
            var hbb = hb.gameObject.AddComponent<Button>(); hbb.onClick.AddListener(() => { gameObject.SetActive(false); Hangar.Open(transform.parent, () => gameObject.SetActive(true)); });
            Txt("Label", hb, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(220f, 60f), 24, TextAnchor.MiddleCenter, Color.white).text = "Hangar";
            Txt("Bank", transform, new Vector2(1f, 1f), new Vector2(-150f, -115f), new Vector2(300f, 26f), 17, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.35f)).text = "● " + Progress.Fmt(Progress.Bank) + " credits  ·  " + (Progress.Def(Progress.Craft) != null ? Progress.Def(Progress.Craft).name : "");

            // tiles, grouped like the original's tabs
            var groups = new List<string>();
            foreach (var kv in ModeDefs.All) if (!groups.Contains(kv.Value.group)) groups.Add(kv.Value.group);
            float y = -165f;
            foreach (var g in groups)
            {
                Txt("Group " + g, transform, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(1300f, 30f), 22, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 1f)).text = g.ToUpper();
                y -= 28f;
                float x = -480f; int col = 0;
                foreach (var kv in ModeDefs.All)
                {
                    var mode = kv.Value; if (mode.group != g) continue;
                    Tile(mode, new Vector2(x, y - 60f));
                    x += 320f; col++;
                    if (col == 4) { col = 0; x = -480f; y -= 132f; }
                }
                y -= (col == 0 ? 0f : 132f) + 40f;
            }
            Txt("Hint", transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(1400f, 30f), 18, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.75f)).text = "Keyboard: WASD move · mouse look · Shift sprint/boost · Space jump/bomb · F get in/out · E/Q throttle · V view · C free look · Tab support";
        }

        void Tile(ModeDefs.Mode mode, Vector2 pos)
        {
            var rt = Rect("Tile " + mode.id, transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), pos, new Vector2(300f, 120f));
            var im = rt.gameObject.AddComponent<Image>(); im.color = new Color(0.14f, 0.18f, 0.26f, 0.95f);
            var btn = rt.gameObject.AddComponent<Button>();
            var colors = btn.colors; colors.highlightedColor = new Color(0.22f, 0.3f, 0.42f); colors.pressedColor = new Color(0.35f, 0.5f, 0.7f); btn.colors = colors;
            string id = mode.id;
            btn.onClick.AddListener(() => { Sfx.Play("ui_click", 0.6f); Pick(id); });
            Txt("Name", rt, new Vector2(0.5f, 1f), new Vector2(0f, -20f), new Vector2(280f, 30f), 23, TextAnchor.MiddleLeft, Color.white).text = mode.name;
            var d = Txt("Desc", rt, new Vector2(0.5f, 1f), new Vector2(0f, -72f), new Vector2(280f, 76f), 14, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.92f));
            d.text = mode.desc; d.horizontalOverflow = HorizontalWrapMode.Wrap;
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

        Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = c; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
