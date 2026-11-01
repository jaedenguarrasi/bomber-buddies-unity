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
        PlaneController flight;
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
            var panel = Img("Panel", transform, tl, new Vector2(18f + 131f, -18f - 36f), new Vector2(262f, 72f), new Color(0.02f, 0.03f, 0.05f, 0.55f));
            var barBack = new Color(0.25f, 0.27f, 0.3f, 0.9f);
            string[] rows = { "Hull", "Gun", "Boost" };
            Color[] fills = { new Color(0.55f, 0.75f, 0.3f), new Color(0.75f, 0.72f, 0.65f), new Color(0.55f, 0.85f, 0.95f) };
            for (int i = 0; i < 3; i++)
            {
                float y = 22f - i * 22f;
                Txt(rows[i], panel.transform, mid, new Vector2(-95f, y), new Vector2(70f, 20f), 15, TextAnchor.MiddleLeft, new Color(0.85f, 0.88f, 0.92f), true).text = rows[i];
                Img(rows[i] + "Back", panel.transform, mid, new Vector2(-10f, y), new Vector2(105f, 9f), barBack);
                var f = Img(rows[i] + "Fill", panel.transform, mid, new Vector2(-10f, y), new Vector2(105f, 9f), fills[i]);
                f.type = Image.Type.Filled; f.fillMethod = Image.FillMethod.Horizontal; f.fillOrigin = 0;
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
            var pill = Img("GetOut", transform, tr, new Vector2(-250f, -36f), new Vector2(118f, 44f), new Color(0.05f, 0.08f, 0.12f, 0.6f), true);
            pill.sprite = Circle(); pill.type = Image.Type.Sliced;
            var pillBtn = pill.gameObject.AddComponent<OnScreenButton>(); pillBtn.controlPath = "<Gamepad>/select";
            getOutLabel = Txt("Label", pill.transform, mid, Vector2.zero, new Vector2(118f, 44f), 17, TextAnchor.MiddleCenter, Color.white, true); getOutLabel.text = "Get out";
            creditsText = Txt("Credits", transform, tr, new Vector2(-350f, -36f), new Vector2(120f, 30f), 17, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.35f), true); creditsText.text = "● 0";
            RoundButton("View", new Vector2(-160f, -36f), "<Gamepad>/buttonNorth", "👁");
            RoundButton("Sound", new Vector2(-100f, -36f), "", "♪");
            RoundButton("Pause", new Vector2(-40f, -36f), "", "⏸");
            waveText = Txt("Wave", transform, tr, new Vector2(-250f, -72f), new Vector2(240f, 22f), 13, TextAnchor.MiddleCenter, new Color(0.9f, 0.9f, 0.9f));
            modeText = Txt("Mode", transform, new Vector2(0.5f, 1f), new Vector2(120f, -102f), new Vector2(900f, 22f), 13, TextAnchor.MiddleCenter, new Color(0.75f, 0.85f, 1f, 0.9f));
            commsText = Txt("Comms", transform, new Vector2(1f, 1f), new Vector2(-180f, -112f), new Vector2(300f, 22f), 13, TextAnchor.MiddleRight, new Color(0.95f, 0.95f, 0.95f));
            message = Txt("Message", transform, new Vector2(0.5f, 0.78f), Vector2.zero, new Vector2(1000f, 60f), 34, TextAnchor.MiddleCenter, Color.white, true);
            message.text = "";

            // throttle slider on the right
            throttleTrack = Rect("ThrottleTrack", transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-187f, -20f), new Vector2(10f, 150f));
            var trackIm = throttleTrack.gameObject.AddComponent<Image>(); trackIm.color = new Color(0.9f, 0.75f, 0.3f, 0.55f); trackIm.raycastTarget = false;
            Txt("ThrottleLabel", transform, new Vector2(1f, 0.5f), new Vector2(-187f, 78f), new Vector2(120f, 20f), 13, TextAnchor.MiddleCenter, new Color(0.95f, 0.95f, 0.95f), true).text = "Throttle";
            throttleKnob = Img("ThrottleKnob", throttleTrack, mid, Vector2.zero, new Vector2(46f, 30f), new Color(0.97f, 0.95f, 0.9f, 0.95f));
            throttleKnob.sprite = Circle(); throttleKnob.type = Image.Type.Sliced;
            throttleChip = Txt("Chip", throttleKnob.transform, mid, Vector2.zero, new Vector2(46f, 30f), 14, TextAnchor.MiddleCenter, new Color(0.1f, 0.1f, 0.12f), true);

            BuildTouchControls();
            GameState.Changed += Refresh;
            Refresh();
            SetFlight(null);
        }

        void RoundButton(string name, Vector2 pos, string path, string glyph)
        {
            var im = Disc(name, transform, new Vector2(1f, 1f), pos, 44f, new Color(0.05f, 0.08f, 0.12f, 0.6f), path != "");
            if (path != "") { var b = im.gameObject.AddComponent<OnScreenButton>(); b.controlPath = path; }
            Txt("Glyph", im.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f), 18, TextAnchor.MiddleCenter, Color.white).text = glyph;
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
            touchRoot.transform.SetParent(transform, false);
            var r = touchRoot.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            // the stick: home at (2r + 44, h - 2r - 10) in the original
            var bl = new Vector2(0f, 0f);
            var stickBack = Disc("StickBack", touchRoot.transform, bl, new Vector2(2f * R + 44f, 2f * R + 10f), 2.2f * R, new Color(1f, 1f, 1f, 0.12f), true);
            var knob = Disc("Stick", stickBack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, R * 0.9f, new Color(1f, 1f, 1f, 0.35f), true);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = R * 0.8f;
            stick.controlPath = "<Gamepad>/leftStick";
            // the throttle slider's touch area (dpad y)
            var thrArea = Img("ThrottleTouch", touchRoot.transform, new Vector2(1f, 0.5f), new Vector2(-187f, -20f), new Vector2(90f, 200f), new Color(1f, 1f, 1f, 0.01f), true);
            var thrKnob = Img("ThrottleStick", thrArea.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 40f), new Color(1f, 1f, 1f, 0.0f), true);
            var thr = thrKnob.gameObject.AddComponent<OnScreenStick>(); thr.movementRange = 80f; thr.controlPath = "<Gamepad>/dpad";
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
            TouchButton("bomb", "Bomb", touchRoot.transform, br, bomb, 2f * 0.86f * R, "<Gamepad>/buttonEast", new Color(0.75f, 0.75f, 0.75f, 0.45f), 19);
            TouchButton("boost", "Boost", touchRoot.transform, br, boost, 2f * 0.8f * R, "<Gamepad>/leftShoulder", new Color(0.35f, 0.65f, 0.7f, 0.5f), 18);
            TouchButton("aim", "Aim", touchRoot.transform, br, aim, 2f * 0.52f * R, "<Gamepad>/leftTrigger", new Color(0.6f, 0.65f, 0.75f, 0.45f), 14);
            TouchButton("act", "Roll", touchRoot.transform, br, act, 2f * 0.68f * R, "<Gamepad>/buttonSouth", new Color(0.7f, 0.7f, 0.72f, 0.45f), 16);
            TouchButton("pn", "Kick", touchRoot.transform, br, pn, 2f * 0.6f * R, "<Gamepad>/buttonWest", new Color(0.8f, 0.45f, 0.45f, 0.45f), 15);
            TouchButton("gb", "Crouch", touchRoot.transform, br, gb, 2f * 0.6f * R, "<Gamepad>/rightShoulder", new Color(0.7f, 0.7f, 0.72f, 0.45f), 14);
            touchRoot.SetActive(InputBridge.Instance != null && InputBridge.Instance.IsTouch);
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
        /// <summary>The small comms line under the top-right row ("▸ On foot", "▸ Launch!").</summary>
        public void Comms(string text) { if (commsText != null) { commsText.text = "▸ " + text; commsT = 3f; } }
        public void SetModeLine(string text) { if (modeText != null) modeText.text = text; }

        /// <summary>Flying or on foot: relabels the buttons and readouts the way the original did.</summary>
        public void SetFlight(PlaneController p)
        {
            flight = p;
            bool fly = p != null;
            SetButton("gun", "Gun", true);
            SetButton("bomb", fly ? "Bomb" : "Grenade", true);
            SetButton("boost", "Boost", true);
            SetButton("aim", "Aim", true);
            SetButton("act", fly ? (p.IsHeli ? "" : "Roll") : "Jump", !(fly && p.IsHeli));
            SetButton("pn", "Kick", !fly);
            SetButton("gb", "Crouch", !fly);
            if (throttleTrack != null) throttleTrack.gameObject.SetActive(fly);
            foreach (Transform t in transform) if (t.name == "ThrottleLabel") t.gameObject.SetActive(fly);
            if (touchRoot != null) { var ta = touchRoot.transform.Find("ThrottleTouch"); if (ta != null) ta.gameObject.SetActive(fly); }
            if (getOutLabel != null) getOutLabel.text = fly ? "Get out" : "Get in";
            if (heightText != null) heightText.gameObject.SetActive(fly);
            if (altText != null) altText.gameObject.SetActive(fly);
        }

        void Refresh()
        {
            if (waveText == null) return;
            if (GalacticMode.Instance != null) waveText.text = "Galactic Assault";
            else waveText.text = GameState.Wave > 0 ? "Wave " + GameState.Wave + (GameState.Spawner != null ? " · " + GameState.Spawner.Remaining + " left" : "") : "Get ready";
            if (creditsText != null) creditsText.text = "● " + GameState.Credits;
        }

        void Update()
        {
            float dt = Time.deltaTime;
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
            if (hitT > 0f) { hitT -= dt; if (hitT <= 0f) foreach (var im in hitMarkerRoot.GetComponentsInChildren<Image>()) im.color = new Color(1f, 1f, 1f, 0f); }
            if (flashT > 0f) { flashT -= dt; damageFlash.color = new Color(0.65f, 0f, 0f, Mathf.Clamp01(flashT) * 0.45f); } else damageFlash.color = new Color(0.65f, 0f, 0f, 0f);
            if (messageT > 0f) { messageT -= dt; if (messageT <= 0f) message.text = ""; else message.color = new Color(1f, 1f, 1f, Mathf.Clamp01(messageT * 1.5f)); }
            if (commsT > 0f) { commsT -= dt; if (commsT <= 0f) commsText.text = ""; }
            if (touchRoot != null && InputBridge.Instance != null && touchRoot.activeSelf != InputBridge.Instance.IsTouch) touchRoot.SetActive(InputBridge.Instance.IsTouch);
        }

        static string Dots(int n, int max) { var s = ""; for (int i = 0; i < max; i++) s += i < n ? "●" : "○"; return s; }
        static string Lives(int n) { var s = ""; for (int i = 0; i < n; i++) s += "▲"; return s; }

        void SetCross(bool on)
        {
            crossA.enabled = crossB.enabled = crossC.enabled = crossD.enabled = on;
            crossA.rectTransform.anchoredPosition = new Vector2(0f, crossSpread);
            crossB.rectTransform.anchoredPosition = new Vector2(0f, -crossSpread);
            crossC.rectTransform.anchoredPosition = new Vector2(-crossSpread, 0f);
            crossD.rectTransform.anchoredPosition = new Vector2(crossSpread, 0f);
        }

        void UpdateRadar()
        {
            // enemies as red dots, friends blue, within 700 units, turned with the view like the original's radar
            Vector3 me = Vector3.zero; float yaw = 0f;
            var cam = Camera.main;
            if (flight != null && flight.Active) { me = flight.transform.position; yaw = flight.yaw * Mathf.Rad2Deg; }
            else if (GameState.Player != null) { me = GameState.Player.transform.position; yaw = GameState.Rig != null ? GameState.Rig.yaw : 0f; }
            else if (cam != null) me = cam.transform.position;
            float range = 700f * GalacticMode.U, radius = 36f;
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
}
