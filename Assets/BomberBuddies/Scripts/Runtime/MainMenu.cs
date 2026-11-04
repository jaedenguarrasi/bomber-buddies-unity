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
        Text bankText;
        void RefreshBank() { if (bankText != null) bankText.text = "● " + Progress.Fmt(Progress.Bank) + " credits  ·  " + (Progress.Def(Progress.Craft) != null ? Progress.Def(Progress.Craft).name : ""); }

        Font font;
        Action<string> onPick;
        GameObject modePage, titlePage; Text modeBtnLabel;
        /// <summary>The mode the title's Play 3D starts (the original's save.gm, shown on the "Mode: …" button).</summary>
        public static string Chosen = "battle";

        static readonly Color Cream = new Color(239f / 255f, 233f / 255f, 214f / 255f), Gold = new Color(226f / 255f, 179f / 255f, 60f / 255f), Sky = new Color(127f / 255f, 196f / 255f, 216f / 255f), InkDark = new Color(29f / 255f, 42f / 255f, 46f / 255f);

        /// <summary>The title as the original drew it (oTitle): the tilted logo, "Fly low. Break ground.", Play 3D / Play 2D /
        /// Play together, then Hangar · Mode · Settings · Credits, the version line, and the sound and pause buttons top right.</summary>
        void BuildTitle()
        {
            Chosen = PlayerPrefs.GetString("bb_mode", "battle"); ModeDefs.Mode md0; if (!ModeDefs.All.TryGetValue(Chosen, out md0) || !md0.playable) Chosen = "battle";
            titlePage = Rect("Title page", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject; var tr = titlePage.GetComponent<RectTransform>(); tr.offsetMin = Vector2.zero; tr.offsetMax = Vector2.zero;
            var T = titlePage.transform;
            // positions are the original's at 1280×720, measured from the top-left
            Func<float, float, Vector2> at = (x, y) => new Vector2(x, -y);
            var logoTex = Resources.Load<Texture2D>("Art/title_logo");
            var logo = Rect("Logo", T, new Vector2(0f, 1f), new Vector2(0f, 1f), at(405f + 227f, 71f + 106f), new Vector2(453f, 212f));
            var li = logo.gameObject.AddComponent<Image>(); li.raycastTarget = false;
            if (logoTex != null) li.sprite = Sprite.Create(logoTex, new UnityEngine.Rect(0, 0, logoTex.width, logoTex.height), new Vector2(0.5f, 0.5f)); else { li.enabled = false; var lt = Txt("LogoText", logo, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(453f, 212f), 96, TextAnchor.MiddleLeft, Cream, true); lt.text = "Bomber\nBuddies"; lt.transform.localRotation = Quaternion.Euler(0f, 0f, 5f); }
            Txt("Lede", T, new Vector2(0f, 1f), at(420f + 150f, 300f + 12f), new Vector2(300f, 26f), 19, TextAnchor.MiddleLeft, Cream).text = "Fly low. Break ground.";
            TitleBtn(T, "Play 3D", 420f, 336f, 162f, 58f, 26, Gold, InkDark, null, () => { Sfx.Play("ui_click", 0.6f); Pick(Chosen); });
            TitleBtn(T, "Play 2D", 592f, 340f, 131f, 50f, 22, Gold, InkDark, null, () => { Sfx.Play("ui_click", 0.6f); Toast("The 2D game is not in this version yet."); });
            TitleBtn(T, "Play together", 420f, 404f, 191f, 50f, 22, Sky, new Color(16f / 255f, 32f / 255f, 42f / 255f), null, () => { Sfx.Play("ui_click", 0.6f); ShowNet(); });
            TitleBtn(T, "Hangar", 420f, 464f, 120f, 55f, 22, Color.clear, Cream, Cream, () => { Sfx.Play("ui_click", 0.5f); gameObject.SetActive(false); Hangar.Open(transform.parent, () => { gameObject.SetActive(true); RefreshBank(); RefreshModeButton(); }); });
            modeBtnLabel = TitleBtn(T, "Mode: Battle", 550f, 464f, 176f, 55f, 22, Color.clear, Cream, Cream, () => { Sfx.Play("ui_click", 0.5f); ShowModes(); });
            TitleBtn(T, "Settings", 420f, 529f, 131f, 55f, 22, Color.clear, Cream, Cream, () => { Sfx.Play("ui_click", 0.5f); OptionsScreen.Open(transform, null); });
            TitleBtn(T, "Credits", 561f, 529f, 120f, 55f, 22, Color.clear, Cream, Cream, () => ShowCredits());
            Txt("Ver", T, new Vector2(0f, 1f), at(420f + 100f, 597f + 8f), new Vector2(200f, 18f), 12, TextAnchor.MiddleLeft, new Color(0.7f, 0.72f, 0.7f)).text = "Game version " + Application.version;
            // sound and pause, top right (the original's round buttons)
            var snd = TitleBtn(T, Sfx.Muted ? "✕" : "♪", 1187f, 7f, 40f, 40f, 18, new Color(0.08f, 0.1f, 0.12f, 0.9f), Cream, null, null); var sb = snd.transform.parent.GetComponent<Button>();
            sb.onClick.AddListener(() => { Sfx.Muted = !Sfx.Muted; snd.text = Sfx.Muted ? "✕" : "♪"; Sfx.Play("ui_click", 0.5f); });
            TitleBtn(T, "❚❚", 1237f, 7f, 40f, 40f, 14, new Color(0.08f, 0.1f, 0.12f, 0.9f), Cream, null, () => { });
            RefreshModeButton();
        }
        Text TitleBtn(Transform parent, string label, float x, float y, float w, float h, int fs, Color bg, Color ink, Color? border, UnityEngine.Events.UnityAction onClick)
        {
            var rt = Rect("B " + label, parent, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + w * 0.5f, -(y + h * 0.5f)), new Vector2(w, h));
            var im = rt.gameObject.AddComponent<Image>(); im.color = bg.a > 0f ? bg : new Color(0f, 0f, 0f, 0.001f);
            if (border.HasValue)
            {
                var bc = new Color(border.Value.r, border.Value.g, border.Value.b, 0.45f);
                foreach (var e in new[] { new[] { 0f, 1f, 1f, 1f, 0f, -2f }, new[] { 0f, 0f, 1f, 0f, 0f, 2f }, new[] { 0f, 0f, 0f, 1f, 2f, 0f }, new[] { 1f, 0f, 1f, 1f, -2f, 0f } })
                {
                    var eg = Rect("Edge", rt, new Vector2(e[0], e[1]), new Vector2(e[2], e[3]), Vector2.zero, Vector2.zero); var er = eg; er.offsetMin = Vector2.zero; er.offsetMax = Vector2.zero;
                    if (e[5] != 0f) { er.offsetMin = new Vector2(0f, e[5] < 0f ? e[5] : 0f); er.offsetMax = new Vector2(0f, e[5] > 0f ? e[5] : 0f); } else { er.offsetMin = new Vector2(e[4] < 0f ? e[4] : 0f, 0f); er.offsetMax = new Vector2(e[4] > 0f ? e[4] : 0f, 0f); }
                    var ei = eg.gameObject.AddComponent<Image>(); ei.color = bc; ei.raycastTarget = false;
                }
            }
            var b = rt.gameObject.AddComponent<Button>(); if (onClick != null) b.onClick.AddListener(onClick);
            var cols = b.colors; cols.highlightedColor = new Color(1f, 1f, 1f, 0.85f); cols.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f); b.colors = cols;
            var t = Txt("Label", rt, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h), fs, TextAnchor.MiddleCenter, ink, true); t.text = label;
            return t;
        }
        void RefreshModeButton() { ModeDefs.Mode md; if (modeBtnLabel != null) modeBtnLabel.text = "Mode: " + (ModeDefs.All.TryGetValue(Chosen, out md) ? md.name : Chosen); }
        void ShowTitle() { if (modePage != null) modePage.SetActive(false); if (titlePage != null) titlePage.SetActive(true); RefreshModeButton(); RefreshBank(); }
        void ShowModes() { titlePage.SetActive(false); modePage.SetActive(true); Render(); }
        GameObject toast;
        void Toast(string text)
        {
            if (toast != null) Destroy(toast);
            var t = Txt("Toast", titlePage.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(900f, 30f), 18, TextAnchor.MiddleCenter, Cream); t.text = text; toast = t.gameObject; Destroy(toast, 3f);
        }
        /// <summary>Play together (the original's oNet): hosting and joining by room code is not in this version yet; say so plainly.</summary>
        GameObject net;
        void ShowNet()
        {
            titlePage.SetActive(false);
            NetScreen.Open(transform, false, () => { titlePage.SetActive(true); });
            return;
#pragma warning disable CS0162
            if (net != null) { net.SetActive(true); return; }
            net = new GameObject("Net", typeof(RectTransform)); net.transform.SetParent(transform, false);
            var rt = net.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            net.AddComponent<Image>().color = new Color(0.05f, 0.07f, 0.12f, 0.92f);
            Btn("Arrow", net.transform, new Vector2(0f, 1f), new Vector2(66f, -34f), new Vector2(44f, 44f), "←", 24, () => net.SetActive(false));
            Txt("H", net.transform, new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(900f, 50f), 36, TextAnchor.MiddleCenter, Cream, true).text = "Fly together";
            var p = Txt("P", net.transform, new Vector2(0.5f, 0.5f), new Vector2(0f, 40f), new Vector2(820f, 300f), 20, TextAnchor.MiddleCenter, Cream); p.horizontalOverflow = HorizontalWrapMode.Wrap;
            p.text = "One of you hosts and reads out the room code. The others type it in and join, from anywhere.\n\nOnline play is the one part of the original not in this version yet: the room-code host/join is being rebuilt on Unity's networking and will arrive in a later build. Everyone must be on the same game version.";
            Btn("Back", net.transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(200f, 54f), "Back", 20, () => net.SetActive(false));
#pragma warning restore CS0162
        }

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
            PadNav.Create(transform.parent);
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem"); es.transform.SetParent(transform.parent, false);
                es.AddComponent<EventSystem>(); es.AddComponent<InputSystemUIInputModule>();
            }
            // the backdrop: the original's dusk painting behind every title-side screen
            var bgImg = Img("Background", transform, Vector2.zero, Vector2.one, Color.white);
            var bgTex = Resources.Load<Texture2D>("Art/title_bg"); if (bgTex != null) { bgImg.sprite = Sprite.Create(bgTex, new UnityEngine.Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f)); bgImg.preserveAspect = false; } else bgImg.color = new Color(0.16f, 0.22f, 0.27f, 1f);
            BuildTitle();
            modePage = Rect("Mode page", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).gameObject; var mpr = modePage.GetComponent<RectTransform>(); mpr.offsetMin = Vector2.zero; mpr.offsetMax = Vector2.zero;
            Img("Dim", modePage.transform, Vector2.zero, Vector2.one, new Color(0.05f, 0.07f, 0.12f, 0.92f));
            var safe = Rect("Safe", modePage.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); safe.offsetMin = new Vector2(44f, 12f); safe.offsetMax = new Vector2(-44f, -8f);
            // the original's header bar: chevron on the left, "Game mode" big on the right, the note under it
            Btn("Arrow", modePage.transform, new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(44f, 44f), "←", 24, () => ShowTitle());
            var head = Rect("Head", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); head.offsetMin = new Vector2(0f, -78f); head.offsetMax = new Vector2(0f, -20f);
            head.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.14f, 0.18f, 0.9f);
            Btn("Chevron", head, new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(44f, 44f), "‹", 28, () => ShowTitle());
            Txt("Title", head, new Vector2(1f, 0.5f), new Vector2(-250f, 0f), new Vector2(480f, 56f), 40, TextAnchor.MiddleRight, Cream, true).text = "Game mode";
            Txt("Note", safe, new Vector2(0f, 1f), new Vector2(500f, -94f), new Vector2(1000f, 20f), 14, TextAnchor.MiddleLeft, new Color(0.85f, 0.87f, 0.9f)).text = "What kind of fight the 3D game is. In a linked game the host's choice is used for everyone.";
            Btn("Setup", safe, new Vector2(1f, 1f), new Vector2(-80f, -130f), new Vector2(150f, 36f), "Battle setup", 15, () => ShowSetup());
            bankText = Txt("Bank", safe, new Vector2(1f, 1f), new Vector2(-280f, -130f), new Vector2(240f, 30f), 14, TextAnchor.MiddleRight, Gold); RefreshBank();

            // the category tabs
            float x = 0f;
            foreach (var g in ModeDefs.Groups)
            {
                string gg = g;
                float w = 36f + g.Length * 9f;
                var b = Btn("Tab " + g, safe, new Vector2(0f, 1f), new Vector2(x + w * 0.5f, -130f), new Vector2(w, 36f), g, 15, () => { cat = gg; PlayerPrefs.SetString("bb_mcat", cat); Sfx.Tone(520f, 0.04f, 0.04f, "triangle"); Render(); });
                tabs.Add(b); x += w + 8f;
            }

            // the scrolling tile grid
            var view = Rect("View", safe, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero); view.offsetMin = new Vector2(0f, 40f); view.offsetMax = new Vector2(0f, -156f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f); view.gameObject.AddComponent<RectMask2D>();
            scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Rect("Tiles", view, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); content.pivot = new Vector2(0.5f, 1f);
            var grid = content.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(268f, 272f); grid.spacing = new Vector2(16f, 16f); grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 4; grid.childAlignment = TextAnchor.UpperCenter; grid.padding = new RectOffset(0, 0, 4, 8);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content; scroll.viewport = view; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
            tiles = content;
            Txt("Hint", safe, new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(1400f, 26f), 14, TextAnchor.MiddleCenter, new Color(0.6f, 0.65f, 0.75f)).text = "Keyboard: WASD move · mouse look · Shift sprint/boost · Space jump/bomb · F get in/out · E/Q throttle · V view · C free look · Tab support · B swap seat";
            Render();
            ShowTitle();
        }

        void Render()
        {
            foreach (var b in tabs) { bool on = b.name == "Tab " + cat; b.GetComponent<Image>().color = on ? Cream : new Color(0.12f, 0.16f, 0.2f, 1f); b.transform.Find("Label").GetComponent<Text>().color = on ? InkDark : Cream; }
            foreach (Transform c in tiles) Destroy(c.gameObject);
            foreach (var kv in ModeDefs.All) if (kv.Value.group == cat) Tile(kv.Value);
            Canvas.ForceUpdateCanvases(); if (scroll != null) scroll.verticalNormalizedPosition = 1f;
        }

        void Tile(ModeDefs.Mode mode)
        {
            bool chosen = mode.id == Chosen;
            var rt = Rect("Tile " + mode.id, tiles, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(268f, 272f));
            var im = rt.gameObject.AddComponent<Image>(); im.color = chosen ? new Color(0.16f, 0.19f, 0.22f, 0.98f) : mode.playable ? new Color(0.1f, 0.13f, 0.16f, 0.95f) : new Color(0.08f, 0.09f, 0.11f, 0.95f);
            if (chosen) { var ol = rt.gameObject.AddComponent<Outline>(); ol.effectColor = Gold; ol.effectDistance = new Vector2(2f, 2f); }
            string id = mode.id;
            // the original's gold button: Play this, or Chosen on the current one
            var pb = Btn("Play", rt, new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(244f, 36f), chosen ? "Chosen" : mode.playable ? "Play this" : "Coming", 17, () => { if (!mode.playable) return; Sfx.Play("ui_click", 0.6f); Chosen = id; PlayerPrefs.SetString("bb_mode", id); ShowTitle(); });
            pb.GetComponent<Image>().color = chosen ? Color.clear : mode.playable ? Gold : new Color(0.2f, 0.2f, 0.22f, 1f); pb.transform.Find("Label").GetComponent<Text>().color = chosen ? Gold : mode.playable ? InkDark : Cream;
            if (chosen) { var ol = pb.gameObject.AddComponent<Outline>(); ol.effectColor = Gold; ol.effectDistance = new Vector2(1.5f, 1.5f); }
            // the art, 320×180 like the original's tiles
            var art = Resources.Load<Sprite>("Art/" + mode.id); if (art == null) { var tex = Resources.Load<Texture2D>("Art/" + mode.id); if (tex != null) art = Sprite.Create(tex, new UnityEngine.Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f)); }
            var pic = Rect("Art", rt, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero); pic.offsetMin = new Vector2(10f, -150f); pic.offsetMax = new Vector2(-10f, -10f);
            var pim = pic.gameObject.AddComponent<Image>(); pim.raycastTarget = false;
            if (art != null) { pim.sprite = art; pim.preserveAspect = false; pim.color = mode.playable ? Color.white : new Color(0.45f, 0.45f, 0.5f); } else pim.color = new Color(0.08f, 0.1f, 0.14f, 1f);
            Txt("Name", rt, new Vector2(0f, 1f), new Vector2(134f, -166f), new Vector2(248f, 24f), 17, TextAnchor.MiddleLeft, mode.playable ? Cream : new Color(0.6f, 0.62f, 0.66f), true).text = mode.name;
            var d = Txt("Desc", rt, new Vector2(0f, 1f), new Vector2(134f, -196f), new Vector2(248f, 36f), 12, TextAnchor.UpperLeft, new Color(0.8f, 0.84f, 0.88f));
            d.text = mode.desc; d.horizontalOverflow = HorizontalWrapMode.Wrap; d.verticalOverflow = VerticalWrapMode.Truncate;
        }

        GameObject setup; Transform setupList;
        /// <summary>Battle setup: the enemy mix for Battle and Horde, the amount, lives and friendly fire (renderMatch).</summary>
        void ShowSetup()
        {
            Sfx.Play("ui_click", 0.5f);
            if (setup != null) { setup.SetActive(!setup.activeSelf); if (setup.activeSelf) RenderSetup(); return; }
            setup = new GameObject("Battle setup", typeof(RectTransform)); setup.transform.SetParent(transform, false);
            var rt = setup.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = setup.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
            Txt("Title", setup.transform, new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(900f, 50f), 32, TextAnchor.MiddleCenter, Color.white, true).text = "BATTLE SETUP  ·  applies to Battle and Horde";
            var panel = Rect("List", setup.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(1000f, 520f));
            var grid = panel.gameObject.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(320f, 50f); grid.spacing = new Vector2(12f, 10f); grid.childAlignment = TextAnchor.UpperCenter;
            setupList = panel;
            Btn("Close", setup.transform, new Vector2(0.5f, 0f), new Vector2(0f, 40f), new Vector2(220f, 50f), "Done", 20, () => { PlayerPrefs.Save(); setup.SetActive(false); });
            RenderSetup();
        }

        void RenderSetup()
        {
            foreach (Transform c in setupList) Destroy(c.gameObject);
            for (int i = 0; i < BattleSetup.Types.Length; i++)
            {
                string t = BattleSetup.Types[i]; bool on = BattleSetup.On(t);
                var b = Btn("T " + t, setupList, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f), (on ? "✓  " : "   ") + BattleSetup.Names[i], 17, () => { BattleSetup.Set(t, !BattleSetup.On(t)); Sfx.Tone(520f, 0.04f, 0.04f, "triangle"); RenderSetup(); });
                b.GetComponent<Image>().color = on ? new Color(0.22f, 0.4f, 0.3f, 1f) : new Color(0.14f, 0.16f, 0.2f, 1f);
            }
            float am = BattleSetup.Amount;
            Btn("Amount", setupList, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f), "Amount: " + (am <= 0.5f ? "half" : am >= 2f ? "double" : "normal"), 17, () => { BattleSetup.Amount = am <= 0.5f ? 1f : am >= 2f ? 0.5f : 2f; RenderSetup(); });
            int lv = BattleSetup.Lives;
            Btn("Lives", setupList, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f), "Lives: " + lv, 17, () => { BattleSetup.Lives = lv >= 5 ? 1 : lv + 2; RenderSetup(); });
            Btn("FF", setupList, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 50f), "Friendly fire: " + (BattleSetup.FriendlyFire ? "on" : "off"), 17, () => { BattleSetup.FriendlyFire = !BattleSetup.FriendlyFire; RenderSetup(); });
        }

        GameObject credits;
        /// <summary>The credits (CREDITS): every line is Jaeden's to write.</summary>
        void ShowCredits()
        {
            Sfx.Play("ui_click", 0.5f);
            if (credits != null) { credits.SetActive(!credits.activeSelf); return; }
            credits = new GameObject("Credits", typeof(RectTransform)); credits.transform.SetParent(transform, false);
            var rt = credits.GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = credits.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.96f);
            var b = credits.AddComponent<Button>(); b.onClick.AddListener(() => credits.SetActive(false));
            string text = "<size=40><b>BOMBER BUDDIES</b></size>\n\nA passion project by Jaeden Guarrasi\nVersion " + Application.version + "  ·  Unity port\n\n<size=26><b>Built with</b></size>\n\n" +
                "Unity 6 and the Universal Render Pipeline\n\n" +
                "The original: PeerJS for online play (MIT License, Copyright (c) 2015 Michelle Bu and Eric Zhang), Expo and React Native for the phone app (MIT License)\n\n" +
                "On the REAL MODELS setting: the ninja, goblin, cowboy, zombie, alien, demon and dinosaur models and the guns (Ultimate Guns) are by Quaternius (quaternius.com), the skeleton is from KayKit by Kay Lousberg (kaylousberg.com), and the soldiers are built on the Godot male base mesh by orange-juice-games (BoQsc); all released CC0 (public domain) and reshaped to fit this game's skeletons\n\n<size=14>tap anywhere to close</size>";
            var t = Txt("Text", credits.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 600f), 18, TextAnchor.MiddleCenter, new Color(0.9f, 0.93f, 1f)); t.text = text; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.supportRichText = true;
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
