using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>
    /// The original's screen furniture, laid out the way it laid it out: the Hull/Gun/Boost panel top-left with the throttle
    /// readout, the radar top-centre, the Get out / mark / view / sound / pause row top-right, Speed and Height beside the
    /// crosshair, the throttle slider on the right, and the round touch buttons (Gun, Bomb, Boost, Aim, Roll/Jump, Kick, Crouch)
    /// placed from the Gun button exactly as the original's layout() did. Buttons feed a virtual gamepad.
    /// </summary>
    public class Hud : MonoBehaviour
    {
        Canvas canvas;
        Image hullFill, gunFill, boostFill, damageFlash, crossA, crossB, crossC, crossD, throttleKnob, radarBack;
        RectTransform hitMarkerRoot, radar, throttleTrack;
        Text throttleText, livesText, bombsText, speedText, heightText, altText, message, modeText, waveText, creditsText, throttleChip, commsText;
        float hitT, flashT, messageT, crossSpread = 12f, commsT;
        Font font;
        GameObject touchRoot;
        CanvasGroup touchGroup;   // shown/hidden with a CanvasGroup: toggling the objects off and on breaks the on-screen controls' virtual gamepad
        RectTransform safe;   // everything anchored to the screen edges lives in here, inset by the phone's notch / home bar
        Rect lastSafe;
        PlaneController flight;
        public GroundVehicle drive;
        /// <summary>World-space markers for objectives (the original's overlay markers): a diamond and a label, clamped to the screen edge when off it.</summary>
        public class Marker { public System.Func<Vector3> at; public System.Func<string> label; public System.Func<Color> color; public System.Func<bool> alive; }
        public readonly List<Marker> markers = new List<Marker>();
        readonly List<Transform> markerUi = new List<Transform>();
        public void AddMarker(System.Func<Vector3> at, System.Func<string> label, System.Func<Color> color, System.Func<bool> alive = null) { markers.Add(new Marker { at = at, label = label, color = color, alive = alive }); }

        void UpdateMarkers()
        {
            var cam = Camera.main; if (cam == null) return;
            while (markerUi.Count < markers.Count)
            {
                var r = Rect("Marker", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16f, 16f));
                var im = r.gameObject.AddComponent<Image>(); im.sprite = Circle(); im.color = Color.white; im.raycastTarget = false; r.localRotation = Quaternion.Euler(0f, 0f, 45f);
                var t = Txt("Label", r, new Vector2(0.5f, 0.5f), new Vector2(0f, -22f), new Vector2(220f, 20f), 13, TextAnchor.MiddleCenter, Color.white, true); t.transform.localRotation = Quaternion.Euler(0f, 0f, -45f);
                markerUi.Add(r);
            }
            float k = canvas.scaleFactor; float hw = Screen.width * 0.5f / k - 40f, hh = Screen.height * 0.5f / k - 60f;
            for (int i = 0; i < markerUi.Count; i++)
            {
                var ui = markerUi[i];
                if (i >= markers.Count || (markers[i].alive != null && !markers[i].alive())) { ui.gameObject.SetActive(false); continue; }
                var m = markers[i]; var wp = m.at();
                var sp = cam.WorldToScreenPoint(wp);
                var p = new Vector2((sp.x - Screen.width * 0.5f) / k, (sp.y - Screen.height * 0.5f) / k);
                bool behind = sp.z < 0f; if (behind) p = -p;
                bool clamped = behind || Mathf.Abs(p.x) > hw || Mathf.Abs(p.y) > hh;
                if (clamped) { float sx = Mathf.Abs(p.x) / hw, sy = Mathf.Abs(p.y) / hh, sc = Mathf.Max(sx, sy, 0.0001f); p /= sc; }
                ui.gameObject.SetActive(true);
                ui.GetComponent<RectTransform>().anchoredPosition = p;
                var col = m.color(); ui.GetComponent<Image>().color = new Color(col.r, col.g, col.b, clamped ? 0.55f : 0.9f);
                ui.localScale = Vector3.one * (clamped ? 0.7f : 1f);
                float dist = Vector3.Distance(wp, cam.transform.position) / GalacticMode.U;
                var t = ui.GetComponentInChildren<Text>(); t.text = m.label() + (clamped ? "" : "  " + Mathf.RoundToInt(dist)); t.color = col;
            }
        }
        readonly Dictionary<string, Text> labels = new Dictionary<string, Text>();
        readonly Dictionary<string, Image> buttons = new Dictionary<string, Image>();
        readonly List<Image> radarDots = new List<Image>();
        Text getOutLabel;
        Image canopy;
        const float R = 65f;   // the original's button radius (52 css px at 720p) in a 900-tall canvas

        public static Hud Create(Transform parent)
        {
            var go = new GameObject("HUD");
            go.transform.SetParent(parent, false);
            var hud = go.AddComponent<Hud>();
            hud.Build();
            return hud;
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

        Image Img(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, Color c, bool raycast = false)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var im = rt.gameObject.AddComponent<Image>();
            im.color = c;
            im.raycastTarget = raycast;
            return im;
        }

        static Sprite whiteSprite;
        /// <summary>A plain white sprite: a Filled image needs a sprite or it ignores fillAmount.</summary>
        static Sprite White()
        {
            if (whiteSprite != null) return whiteSprite;
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color32[16]; for (int i = 0; i < 16; i++) px[i] = new Color32(255, 255, 255, 255);
            tex.SetPixels32(px); tex.Apply();
            whiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f));
            return whiteSprite;
        }

        static Sprite circleSprite;
        static Sprite Circle()
        {
            if (circleSprite != null) return circleSprite;
            const int S = 128;
            var tex = new Texture2D(S, S, TextureFormat.RGBA32, false);
            var px = new Color32[S * S];
            for (int y = 0; y < S; y++) for (int x = 0; x < S; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(S * 0.5f, S * 0.5f)) / (S * 0.5f);
                float a = Mathf.Clamp01((1f - d) * S * 0.5f);
                px[y * S + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px); tex.Apply();
            circleSprite = Sprite.Create(tex, new Rect(0, 0, S, S), new Vector2(0.5f, 0.5f));
            return circleSprite;
        }

        static Sprite archSprite;
        static Sprite Arch()
        {
            if (archSprite != null) return archSprite;
            const int W = 512, H = 288;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var px = new Color32[W * H];
            // a thin arc from the lower-left corner over the top to the lower-right, like the frame of the canopy
            float cx = W * 0.5f, cy = -H * 0.95f, r = H * 1.9f;
            for (int y = 0; y < H; y++) for (int x = 0; x < W; x++)
            {
                float d = Mathf.Abs(Vector2.Distance(new Vector2(x, y), new Vector2(cx, cy)) - r);
                float a = Mathf.Clamp01(2.2f - d);
                px[y * W + x] = new Color32(255, 255, 255, (byte)(a * 255f));
            }
            tex.SetPixels32(px); tex.Apply();
            archSprite = Sprite.Create(tex, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
            return archSprite;
        }

        Image Disc(string name, Transform parent, Vector2 anchor, Vector2 pos, float diameter, Color c, bool raycast = false)
        {
            var im = Img(name, parent, anchor, pos, new Vector2(diameter, diameter), c, raycast);
            im.sprite = Circle();
            return im;
        }

        Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = c; t.raycastTarget = false;
            t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.6f); sh.effectDistance = new Vector2(1f, -1f);
            return t;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 900f);
            scaler.matchWidthOrHeight = 1f;   // the original sized everything from the screen height
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var es = new GameObject("EventSystem");
                es.transform.SetParent(transform.parent, false);
                es.AddComponent<EventSystem>();
                es.AddComponent<InputSystemUIInputModule>();
            }

            safe = Rect("Safe", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            safe.offsetMin = Vector2.zero; safe.offsetMax = Vector2.zero;
            var mid = new Vector2(0.5f, 0.5f);
            var cross = new Color(1f, 1f, 1f, 0.9f);
            crossA = Img("CrossUp", transform, mid, new Vector2(0f, crossSpread), new Vector2(2f, 10f), cross);
            crossB = Img("CrossDown", transform, mid, new Vector2(0f, -crossSpread), new Vector2(2f, 10f), cross);
            crossC = Img("CrossLeft", transform, mid, new Vector2(-crossSpread, 0f), new Vector2(10f, 2f), cross);
            crossD = Img("CrossRight", transform, mid, new Vector2(crossSpread, 0f), new Vector2(10f, 2f), cross);
            hitMarkerRoot = Rect("HitMarker", transform, mid, mid, Vector2.zero, new Vector2(34f, 34f));
            hitMarkerRoot.localRotation = Quaternion.Euler(0f, 0f, 45f);
            MakeRing(hitMarkerRoot);
            damageFlash = Img("DamageFlash", transform, mid, Vector2.zero, new Vector2(4000f, 4000f), new Color(0.6f, 0f, 0f, 0f));
            // the canopy arch of the cockpit view
            var canopyRt = Rect("Canopy", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            canopyRt.offsetMin = Vector2.zero; canopyRt.offsetMax = Vector2.zero;
            canopy = canopyRt.gameObject.AddComponent<Image>(); canopy.sprite = Arch(); canopy.color = new Color(0.08f, 0.09f, 0.1f, 0.85f); canopy.raycastTarget = false; canopy.preserveAspect = false;

            // Speed / Height beside the crosshair, like the original's small labels
            speedText = Txt("Speed", transform, mid, new Vector2(-150f, -30f), new Vector2(200f, 24f), 15, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f));
            heightText = Txt("Height", transform, mid, new Vector2(150f, -30f), new Vector2(200f, 24f), 15, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f));
            altText = Txt("AltLabel", transform, mid, new Vector2(150f, -48f), new Vector2(200f, 20f), 12, TextAnchor.MiddleCenter, new Color(0.6f, 0.8f, 1f));

            // top-left panel: Hull / Gun / Boost bars, lives, bombs, throttle
            var tl = new Vector2(0f, 1f);
            var panel = Img("Panel", safe, tl, new Vector2(18f + 131f, -18f - 36f), new Vector2(262f, 72f), new Color(0.02f, 0.03f, 0.05f, 0.55f));
            var barBack = new Color(0.25f, 0.27f, 0.3f, 0.9f);
            string[] rows = { "Hull", "Gun", "Boost" };
            Color[] fills = { new Color(0.55f, 0.75f, 0.3f), new Color(0.75f, 0.72f, 0.65f), new Color(0.55f, 0.85f, 0.95f) };
            for (int i = 0; i < 3; i++)
            {
                float y = 22f - i * 22f;
                Txt(rows[i], panel.transform, mid, new Vector2(-95f, y), new Vector2(70f, 20f), 15, TextAnchor.MiddleLeft, new Color(0.85f, 0.88f, 0.92f), true).text = rows[i];
                Img(rows[i] + "Back", panel.transform, mid, new Vector2(-10f, y), new Vector2(105f, 9f), barBack);
                var f = Img(rows[i] + "Fill", panel.transform, mid, new Vector2(-10f, y), new Vector2(105f, 9f), fills[i]);
                f.sprite = White(); f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = 0;
                if (i == 0) hullFill = f; else if (i == 1) gunFill = f; else boostFill = f;
            }
            livesText = Txt("Lives", panel.transform, mid, new Vector2(88f, 22f), new Vector2(90f, 20f), 15, TextAnchor.MiddleLeft, new Color(0.6f, 0.68f, 0.72f));
            bombsText = Txt("Bombs", panel.transform, mid, new Vector2(88f, 0f), new Vector2(90f, 20f), 15, TextAnchor.MiddleLeft, new Color(0.6f, 0.68f, 0.72f));
            throttleText = Txt("Throttle", panel.transform, mid, new Vector2(88f, -22f), new Vector2(110f, 20f), 14, TextAnchor.MiddleLeft, new Color(0.6f, 0.68f, 0.72f), true);

            // radar top-centre
            radar = Rect("Radar", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(82f, 82f));
            radarBack = radar.gameObject.AddComponent<Image>(); radarBack.sprite = Circle(); radarBack.color = new Color(0.05f, 0.08f, 0.12f, 0.45f); radarBack.raycastTarget = false;
            var ring = Disc("Ring", radar, mid, Vector2.zero, 82f, new Color(0.7f, 0.8f, 0.9f, 0.5f)); ring.raycastTarget = false;
            var inner = Disc("Inner", radar, mid, Vector2.zero, 76f, new Color(0.05f, 0.08f, 0.12f, 0.6f));
            var you = Img("You", radar, mid, new Vector2(0f, 2f), new Vector2(6f, 9f), new Color(1f, 0.85f, 0.3f));
            you.sprite = Circle();
            for (int i = 0; i < 40; i++) { var d = Disc("Dot", radar, mid, Vector2.zero, 5f, new Color(1f, 0.25f, 0.2f)); d.enabled = false; radarDots.Add(d); }

            // top-right row: credits, Get out, mark, view, sound, pause
            var tr = new Vector2(1f, 0f + 1f);
            var pill = Img("GetOut", safe, tr, new Vector2(-552f, -40f), new Vector2(130f, 56f), new Color(0.05f, 0.08f, 0.12f, 0.6f), true);
            pill.sprite = Circle(); pill.type = Image.Type.Sliced;
            var pillBtn = pill.gameObject.AddComponent<OnScreenButton>(); pillBtn.controlPath = "<Gamepad>/select";
            getOutLabel = Txt("Label", pill.transform, mid, Vector2.zero, new Vector2(130f, 56f), 19, TextAnchor.MiddleCenter, Color.white, true); getOutLabel.text = "Get out";
            creditsText = Txt("Credits", safe, tr, new Vector2(-702f, -40f), new Vector2(120f, 30f), 17, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f), true); creditsText.text = "● 0";
            RoundButton("View", new Vector2(-196f, -40f), "<Gamepad>/buttonNorth", "◎");
            RoundButton("Support", new Vector2(-272f, -40f), "<Gamepad>/dpad/up", "✦");
            RoundButton("Seat", new Vector2(-348f, -40f), "<Gamepad>/dpad/left", "⇄");
            RoundButton("Items", new Vector2(-424f, -40f), "<Gamepad>/dpad/down", "I");
            RoundButton("Sound", new Vector2(-120f, -40f), "", "♪", () => { if (!Sfx.Muted && Sfx.MusicOn) { Sfx.MusicOn = false; Comms("Music off"); } else if (!Sfx.Muted) { Sfx.Muted = true; Comms("Sound off"); } else { Sfx.Muted = false; Sfx.MusicOn = true; Comms("Sound on"); } PlayerPrefs.Save(); });
            RoundButton("Pause", new Vector2(-44f, -40f), "", "⏸", () => { if (PauseMenu.Instance != null) PauseMenu.Instance.Toggle(); });
            waveText = Txt("Wave", safe, tr, new Vector2(-552f, -82f), new Vector2(240f, 22f), 13, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.9f));
            modeText = Txt("Mode", safe, new Vector2(0.5f, 1f), new Vector2(120f, -102f), new Vector2(900f, 22f), 13, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f, 0.9f));
            commsText = Txt("Comms", safe, new Vector2(1f, 1f), new Vector2(-180f, -112f), new Vector2(300f, 22f), 13, TextAnchor.MiddleRight, new Color(0.95f, 0.95f, 0.95f));
            message = Txt("Message", transform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(1000f, 60f), 34, TextAnchor.MiddleCenter, Color.white, true);
            message.text = "";

            // throttle slider on the right
            throttleTrack = Rect("ThrottleTrack", safe, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-187f, 30f), new Vector2(10f, 150f));
            var trackIm = throttleTrack.gameObject.AddComponent<Image>(); trackIm.color = new Color(0.9f, 0.75f, 0.3f, 0.55f); trackIm.raycastTarget = false;
            Txt("ThrottleLabel", safe, new Vector2(1f, 0.5f), new Vector2(-187f, 128f), new Vector2(120f, 20f), 13, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f), true).text = "Throttle";
            throttleKnob = Img("ThrottleKnob", throttleTrack, mid, Vector2.zero, new Vector2(46f, 30f), new Color(0.97f, 0.95f, 0.9f, 0.95f));
            throttleKnob.sprite = Circle(); throttleKnob.type = Image.Type.Sliced;
            throttleChip = Txt("Chip", throttleKnob.transform, mid, Vector2.zero, new Vector2(46f, 30f), 14, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.12f), true);

            BuildTouchControls();
            PauseMenu.Create(transform);
            ApplyLayoutOptions();
            GameState.Changed += Refresh;
            Refresh();
            SetFlight(null);
        }

        void RoundButton(string name, Vector2 pos, string path, string glyph, UnityEngine.Events.UnityAction onClick = null)
        {
            var im = Disc(name, safe, new Vector2(1f, 1f), pos, 64f, new Color(0.05f, 0.08f, 0.12f, 0.6f), path != "" || onClick != null);
            if (path != "") { var b = im.gameObject.AddComponent<OnScreenButton>(); b.controlPath = path; }
            if (onClick != null) { var b = im.gameObject.AddComponent<Button>(); b.onClick.AddListener(onClick); }
            Txt("Glyph", im.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f), 26, TextAnchor.MiddleCenter, Color.white).text = glyph;
        }

        void MakeRing(RectTransform rt)
        {
            var c = new Color(1f, 1f, 1f, 1f);
            Img("a", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, 12f), new Vector2(2f, 8f), c);
            Img("b", rt, new Vector2(0.5f, 0.5f), new Vector2(0f, -12f), new Vector2(2f, 8f), c);
            Img("c", rt, new Vector2(0.5f, 0.5f), new Vector2(12f, 0f), new Vector2(8f, 2f), c);
            Img("d", rt, new Vector2(0.5f, 0.5f), new Vector2(-12f, 0f), new Vector2(8f, 2f), c);
            foreach (var im in rt.GetComponentsInChildren<Image>()) im.color = new Color(1f, 1f, 1f, 0f);
        }

        void BuildTouchControls()
        {
            touchRoot = new GameObject("Touch", typeof(RectTransform));
            touchRoot.transform.SetParent(safe, false);
            var r = touchRoot.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            // the stick: home at (2r + 44, h - 2r - 10) in the original
            var bl = new Vector2(0f, 0f);
            var stickBack = Disc("StickBack", touchRoot.transform, bl, new Vector2(2f * R + 44f, 2f * R + 10f), 2.2f * R, new Color(1f, 1f, 1f, 0.12f), true);
            var knob = Disc("Stick", stickBack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, R * 0.9f, new Color(1f, 1f, 1f, 0.35f), true);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = R * 0.8f;
            stick.controlPath = "<Gamepad>/leftStick";
            // the throttle slider's touch area (dpad y)
            var thrArea = Img("ThrottleTouch", touchRoot.transform, new Vector2(1f, 0.5f), new Vector2(-187f, 30f), new Vector2(110f, 220f), new Color(1f, 1f, 1f, 0.01f), true);
            thrArea.gameObject.AddComponent<ThrottleDrag>();   // drag up/down anywhere on the slider
            // buttons from the Gun button, exactly the original's offsets (its y grows downward)
            var br = new Vector2(1f, 0f);
            Vector2 gun = new Vector2(-(R + 35f), R + 37f);
            Vector2 bomb = gun + new Vector2(-2.1f * R, -0.22f * R);
            Vector2 boost = gun + new Vector2(-1.0f * R, 1.85f * R);
            Vector2 aim = gun + new Vector2(0.5f * R, 1.55f * R);
            Vector2 act = bomb + new Vector2(-0.35f * R, 1.95f * R);
            Vector2 alt = act + new Vector2(-1.5f * R, -0.75f * R);
            Vector2 pn = alt + new Vector2(-1.4f * R, -0.2f * R);
            Vector2 gb = alt + new Vector2(-0.3f * R, 1.4f * R);
            TouchButton("gun", "Gun", touchRoot.transform, br, gun, 2f * R, "<Gamepad>/rightTrigger", new Color(0.86f, 0.7f, 0.25f, 0.5f), 22);
            TouchButton("bomb", "Bomb", touchRoot.transform, br, bomb, 2f * 0.86f * R, "<Gamepad>/rightShoulder", new Color(0.75f, 0.75f, 0.75f, 0.45f), 19);
            TouchButton("boost", "Boost", touchRoot.transform, br, boost, 2f * 0.8f * R, "<Gamepad>/leftStickPress", new Color(0.35f, 0.65f, 0.7f, 0.5f), 18);
            TouchButton("aim", "Aim", touchRoot.transform, br, aim, 2f * 0.52f * R, "<Gamepad>/leftTrigger", new Color(0.6f, 0.65f, 0.75f, 0.45f), 14);
            TouchButton("act", "Roll", touchRoot.transform, br, act, 2f * 0.68f * R, "<Gamepad>/buttonSouth", new Color(0.7f, 0.7f, 0.72f, 0.45f), 16);
            TouchButton("pn", "Kick", touchRoot.transform, br, pn, 2f * 0.6f * R, "<Gamepad>/buttonWest", new Color(0.8f, 0.45f, 0.45f, 0.45f), 15);
            TouchButton("gb", "Crouch", touchRoot.transform, br, gb, 2f * 0.6f * R, "<Gamepad>/buttonEast", new Color(0.7f, 0.7f, 0.72f, 0.45f), 14);
            touchGroup = touchRoot.AddComponent<CanvasGroup>();
            ShowTouch(InputBridge.Instance != null && InputBridge.Instance.IsTouch);
        }

        void ShowTouch(bool on)
        {
            // on a phone the whole HUD is drawn a quarter larger so the buttons are a comfortable thumb size
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) scaler.referenceResolution = on ? new Vector2(1280f, 720f) : new Vector2(1600f, 900f);
            if (touchGroup == null) return;
            touchGroup.alpha = on ? 1f : 0f; touchGroup.blocksRaycasts = on; touchGroup.interactable = on;
        }

        void TouchButton(string key, string label, Transform parent, Vector2 anchor, Vector2 pos, float size, string path, Color c, int fontSize)
        {
            var im = Disc(key, parent, anchor, pos, size, c, true);
            var b = im.gameObject.AddComponent<OnScreenButton>();
            b.controlPath = path;
            var t = Txt("Label", im.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), fontSize, TextAnchor.MiddleCenter, new Color(0.12f, 0.12f, 0.14f, 0.9f), true);
            t.text = label;
            labels[key] = t; buttons[key] = im;
        }

        void SetButton(string key, string label, bool shown)
        {
            Image im; if (!buttons.TryGetValue(key, out im)) return;
            im.gameObject.SetActive(shown);
            if (shown) labels[key].text = label;
        }

        public void HitMarker(bool headshot)
        {
            hitT = 0.18f;
            var c = headshot ? new Color(1f, 0.25f, 0.2f, 1f) : Color.white;
            foreach (var im in hitMarkerRoot.GetComponentsInChildren<Image>()) im.color = c;
        }

        public void OnShot() { crossSpread = Mathf.Min(28f, crossSpread + 4f); }
        public void Damaged(float amount) { flashT = Mathf.Min(0.6f, flashT + 0.12f + amount * 0.012f); }
        public void Announce(string text) { message.text = text; messageT = 2.6f; }
        public void Announce(string text, float seconds) { message.text = text; messageT = seconds; }
        public void Sting() { Sfx.Play("sting", 0.6f); }
        /// <summary>The small comms line under the top-right row ("▸ On foot", "▸ Launch!").</summary>
        public void Comms(string text) { if (commsText != null) { commsText.text = "▸ " + text; commsT = 3f; } }
        /// <summary>Big buttons (×1.2) and the left-handed mirror, like the original's options.</summary>
        public void ApplyLayoutOptions()
        {
            if (touchRoot == null) return;
            float k = PauseMenu.BigButtons ? 1.2f : 1f;
            foreach (var kv in buttons) kv.Value.rectTransform.localScale = new Vector3(k, k, 1f);
            var rt = touchRoot.GetComponent<RectTransform>();
            rt.localScale = new Vector3(PauseMenu.Lefty ? -1f : 1f, 1f, 1f);
            foreach (var t in touchRoot.GetComponentsInChildren<Text>(true)) t.rectTransform.localScale = new Vector3(PauseMenu.Lefty ? -1f : 1f, 1f, 1f);
        }

        public void SetModeLine(string text) { if (modeText != null) modeText.text = text; }

        /// <summary>Flying or on foot: relabels the buttons and readouts the way the original did.</summary>
        public void SetFlight(PlaneController p)
        {
            flight = p;
            bool fly = p != null;
            SetButton("gun", "Gun", true);
            SetButton("bomb", fly ? "Bomb" : drive != null ? "Shell" : "Grenade", true);
            SetButton("boost", "Boost", true);
            SetButton("aim", "Aim", true);
            SetButton("act", fly ? (p.IsHeli ? "" : "Roll") : "Jump", !(fly && p.IsHeli));
            SetButton("pn", "Kick", !fly);
            SetButton("gb", "Crouch", !fly);
            if (throttleTrack != null) throttleTrack.gameObject.SetActive(fly);
            foreach (Transform t in safe) if (t.name == "ThrottleLabel") t.gameObject.SetActive(fly);
            if (touchRoot != null) { var ta = touchRoot.transform.Find("ThrottleTouch"); if (ta != null) ta.gameObject.SetActive(fly); }
            if (getOutLabel != null) getOutLabel.text = fly ? "Get out" : "Get in";
            if (heightText != null) heightText.gameObject.SetActive(fly);
            if (altText != null) altText.gameObject.SetActive(fly);
        }

        void Refresh()
        {
            if (waveText == null) return;
            if (GalacticMode.Instance != null) waveText.text = "Galactic Assault";
            else if (IslandMode.Instance != null && IslandMode.Instance.ModeDrivesSpawns) waveText.text = ModeDefs.All.ContainsKey(IslandMode.Instance.modeId) ? ModeDefs.All[IslandMode.Instance.modeId].name : "";
            else waveText.text = GameState.Wave > 0 ? "Wave " + GameState.Wave + (GameState.Spawner != null ? " · " + GameState.Spawner.Remaining + " left" : "") : "Get ready";
            if (creditsText != null) creditsText.text = "● " + GameState.Credits;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            ApplySafeArea();
            var p = GameState.Player;
            bool fly = flight != null && flight.Active;
            if (fly)
            {
                hullFill.fillAmount = Mathf.Lerp(hullFill.fillAmount, flight.health.Fraction, Mathf.Min(1f, 10f * dt));
                gunFill.fillAmount = flight.heat; gunFill.color = flight.Overheated ? new Color(1f, 0.3f, 0.2f) : new Color(0.75f, 0.72f, 0.65f);
                boostFill.fillAmount = flight.boost;
                bombsText.text = Dots(flight.bombs, flight.maxBombs);
                throttleText.text = "Throttle " + Mathf.RoundToInt(flight.thr * 100f) + "%";
                speedText.text = "Speed " + Mathf.RoundToInt(flight.speedU);
                heightText.text = "Height " + Mathf.RoundToInt(flight.AltitudeU);
                altText.text = flight.AltitudeU > 3000f ? "Low orbit" : flight.AltitudeU > 1500f ? "High altitude" : "";
                throttleKnob.rectTransform.anchoredPosition = new Vector2(0f, (flight.thr - 0.5f) * 150f);
                throttleChip.text = Mathf.RoundToInt(flight.thr * 100f).ToString();
                crossSpread = Mathf.Lerp(crossSpread, 9f, Mathf.Min(1f, 8f * dt));
                SetCross(true);
            }
            else if (drive != null && drive.Active)
            {
                hullFill.fillAmount = Mathf.Lerp(hullFill.fillAmount, drive.health.Fraction, Mathf.Min(1f, 10f * dt));
                gunFill.fillAmount = drive.Heat; gunFill.color = drive.Overheated ? new Color(1f, 0.3f, 0.2f) : new Color(0.75f, 0.72f, 0.65f);
                boostFill.fillAmount = 1f; bombsText.text = ""; throttleText.text = drive.Name;
                speedText.text = "Speed " + Mathf.RoundToInt(drive.SpeedU);
                crossSpread = Mathf.Lerp(crossSpread, 9f, Mathf.Min(1f, 8f * dt));
                SetCross(true);
            }
            else if (p != null && p.health != null)
            {
                hullFill.fillAmount = Mathf.Lerp(hullFill.fillAmount, p.health.Fraction, Mathf.Min(1f, 10f * dt));
                if (p.rifle != null)
                {
                    gunFill.fillAmount = p.rifle.Reloading ? p.rifle.ReloadFraction : p.rifle.ammo / (float)p.rifle.magSize;
                    gunFill.color = p.rifle.Reloading ? new Color(1f, 0.85f, 0.4f) : new Color(0.75f, 0.72f, 0.65f);
                    bool aiming = p.rifle.Aiming;
                    crossSpread = Mathf.Lerp(crossSpread, aiming ? 5f : 12f + p.MoveSpeed * 1.5f, Mathf.Min(1f, 8f * dt));
                    SetCross(!(GameState.Rig != null && GameState.Rig.firstPerson && aiming));
                    throttleText.text = p.rifle.Reloading ? "Reloading" : p.rifle.ammo + " / " + p.rifle.magSize;
                }
                boostFill.fillAmount = p.Sprinting ? 0.6f : 1f;
                bombsText.text = Dots(GameState.Grenades, 8);
                speedText.text = "Speed " + Mathf.RoundToInt(p.MoveSpeed / GalacticMode.U);
            }
            livesText.text = Lives(Mathf.Max(0, 3 - GameState.Deaths % 3));
            if (canopy != null) canopy.enabled = fly && flight.cockpit;
            UpdateRadar();
            UpdateMarkers();
            if (hitT > 0f) { hitT -= dt; if (hitT <= 0f) foreach (var im in hitMarkerRoot.GetComponentsInChildren<Image>()) im.color = new Color(1f, 1f, 1f, 0f); }
            if (flashT > 0f) { flashT -= dt; damageFlash.color = new Color(0.65f, 0f, 0f, Mathf.Clamp01(flashT) * 0.45f); } else damageFlash.color = new Color(0.65f, 0f, 0f, 0f);
            if (messageT > 0f) { messageT -= dt; if (messageT <= 0f) message.text = ""; else message.color = new Color(1f, 1f, 1f, Mathf.Clamp01(messageT * 1.5f)); }
            if (commsT > 0f) { commsT -= dt; if (commsT <= 0f) commsText.text = ""; }
            if (touchGroup != null && InputBridge.Instance != null && (touchGroup.alpha > 0.5f) != InputBridge.Instance.IsTouch) ShowTouch(InputBridge.Instance.IsTouch);
            if (InputBridge.Instance != null && InputBridge.Instance.PausePressed && PauseMenu.Instance != null) PauseMenu.Instance.Toggle();
        }

        static string Dots(int n, int max) { var s = ""; for (int i = 0; i < max; i++) s += i < n ? "●" : "○"; return s; }
        static string Lives(int n) { var s = ""; for (int i = 0; i < n; i++) s += "▲"; return s; }

        void SetCross(bool on)
        {
            crossA.enabled = crossB.enabled = crossC.enabled = crossD.enabled = on;
            // free look in an aircraft walks the reticle to where the guns point
            var o = Vector2.zero;
            if (flight != null && flight.Active && flight.cam != null && flight.AimOffset.sqrMagnitude > 0.0001f)
            {
                var sp = flight.cam.WorldToScreenPoint(flight.transform.position + flight.AimDir * 300f);
                if (sp.z > 0f) { float k = canvas.scaleFactor; o = new Vector2((sp.x - Screen.width * 0.5f) / k, (sp.y - Screen.height * 0.5f) / k); }
            }
            crossA.rectTransform.anchoredPosition = o + new Vector2(0f, crossSpread);
            crossB.rectTransform.anchoredPosition = o + new Vector2(0f, -crossSpread);
            crossC.rectTransform.anchoredPosition = o + new Vector2(-crossSpread, 0f);
            crossD.rectTransform.anchoredPosition = o + new Vector2(crossSpread, 0f);
        }

        /// <summary>Keeps the edge-anchored HUD and the touch controls out of the notch and the home bar.</summary>
        void ApplySafeArea()
        {
            var sa = Screen.safeArea;
            if (safe == null || sa == lastSafe || Screen.width == 0 || Screen.height == 0) return;
            lastSafe = sa;
            var min = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            var max = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safe.anchorMin = min; safe.anchorMax = max;
            safe.offsetMin = Vector2.zero; safe.offsetMax = Vector2.zero;
        }

        void UpdateRadar()
        {
            // enemies as red dots, friends blue, within 700 units, turned with the view like the original's radar
            Vector3 me = Vector3.zero; float yaw = 0f;
            var cam = Camera.main;
            if (flight != null && flight.Active) { me = flight.transform.position; yaw = flight.yaw * Mathf.Rad2Deg; }
            else if (GameState.Player != null) { me = GameState.Player.transform.position; yaw = GameState.Rig != null ? GameState.Rig.yaw : 0f; }
            else if (cam != null) me = cam.transform.position;
            float range = (Support.Instance != null && Support.Instance.UavActive ? 6000f : 700f) * GalacticMode.U, radius = 36f;
            int used = 0;
            var rot = Quaternion.Euler(0f, -yaw, 0f);
            foreach (var h in Health.All)
            {
                if (used >= radarDots.Count) break;
                if (h == null || !h.alive || !h.gameObject.activeInHierarchy) continue;
                if (GameState.Player != null && h.gameObject == GameState.Player.gameObject) continue;
                if (flight != null && h.gameObject == flight.gameObject) continue;
                var d = rot * (h.transform.position - me);
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist > range) continue;
                var dot = radarDots[used++];
                dot.enabled = true;
                dot.rectTransform.anchoredPosition = new Vector2(d.x, d.z) / range * radius;
                dot.color = h.team == 0 ? new Color(0.45f, 0.65f, 1f) : (h.isAir ? new Color(1f, 0.55f, 0.3f) : new Color(1f, 0.25f, 0.2f));
                dot.rectTransform.sizeDelta = h.isAir ? new Vector2(6f, 6f) : new Vector2(5f, 5f);
            }
            for (int i = used; i < radarDots.Count; i++) radarDots[i].enabled = false;
        }

        void OnDestroy() { GameState.Changed -= Refresh; }
    }

    /// <summary>The throttle slider's touch handling: drag up to open the throttle, down to close it. Feeds InputBridge.TouchThrottle.</summary>
    public class ThrottleDrag : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        Vector2 start;
        public void OnPointerDown(PointerEventData e) { start = e.position; InputBridge.TouchThrottle = 0f; }
        public void OnDrag(PointerEventData e)
        {
            float dy = (e.position.y - start.y) / Mathf.Max(1f, Screen.height * 0.06f);
            InputBridge.TouchThrottle = Mathf.Clamp(dy, -1f, 1f);
        }
        public void OnPointerUp(PointerEventData e) { InputBridge.TouchThrottle = 0f; }
    }
}
