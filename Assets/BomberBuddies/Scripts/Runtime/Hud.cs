using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The screen furniture: crosshair, health, ammo, wave and kills, hit marker, damage flash, death and wave messages,
    /// and on phones the on-screen stick and buttons (which feed a virtual gamepad, so the rest of the game never knows).</summary>
    public class Hud : MonoBehaviour
    {
        Canvas canvas;
        Image healthFill, damageFlash, reloadFill, crossA, crossB, crossC, crossD;
        RectTransform hitMarkerRoot;
        Text ammoText, waveText, killText, message, reloadText, modeText, speedText;
        float hitT, flashT, messageT, crossSpread = 12f;
        Font font;
        GameObject touchRoot, flightTouchRoot;
        PlaneController flight;
        Image boostFill, heatRing;

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

        Text Txt(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c)
        {
            var rt = Rect(name, parent, anchor, anchor, pos, size);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = c; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            var sh = rt.gameObject.AddComponent<Shadow>(); sh.effectColor = new Color(0f, 0f, 0f, 0.7f); sh.effectDistance = new Vector2(1.5f, -1.5f);
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
            scaler.matchWidthOrHeight = 0.6f;
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

            var bl = new Vector2(0f, 0f);
            Img("HealthBack", transform, bl, new Vector2(150f, 42f), new Vector2(240f, 18f), new Color(0f, 0f, 0f, 0.55f));
            healthFill = Img("HealthFill", transform, bl, new Vector2(150f, 42f), new Vector2(236f, 14f), new Color(0.35f, 0.85f, 0.3f, 0.95f));
            healthFill.type = Image.Type.Filled; healthFill.fillMethod = Image.FillMethod.Horizontal; healthFill.fillOrigin = 0;
            Txt("HealthLabel", transform, bl, new Vector2(150f, 68f), new Vector2(240f, 24f), 18, TextAnchor.MiddleLeft, Color.white).text = "HEALTH";

            var br = new Vector2(1f, 0f);
            ammoText = Txt("Ammo", transform, br, new Vector2(-150f, 52f), new Vector2(260f, 60f), 40, TextAnchor.MiddleRight, Color.white);
            reloadText = Txt("ReloadLabel", transform, br, new Vector2(-150f, 92f), new Vector2(260f, 24f), 18, TextAnchor.MiddleRight, new Color(1f, 0.85f, 0.4f));
            Img("ReloadBack", transform, br, new Vector2(-150f, 32f), new Vector2(240f, 6f), new Color(0f, 0f, 0f, 0.5f));
            reloadFill = Img("ReloadFill", transform, br, new Vector2(-150f, 32f), new Vector2(240f, 6f), new Color(1f, 0.85f, 0.4f, 0.95f));
            reloadFill.type = Image.Type.Filled; reloadFill.fillMethod = Image.FillMethod.Horizontal;

            var tl = new Vector2(0f, 1f);
            waveText = Txt("Wave", transform, tl, new Vector2(150f, -40f), new Vector2(280f, 40f), 26, TextAnchor.MiddleLeft, Color.white);
            killText = Txt("Kills", transform, tl, new Vector2(150f, -72f), new Vector2(280f, 30f), 20, TextAnchor.MiddleLeft, new Color(0.9f, 0.9f, 0.9f));
            message = Txt("Message", transform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(900f, 80f), 48, TextAnchor.MiddleCenter, Color.white);
            message.text = "";
            modeText = Txt("Mode", transform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(1200f, 34f), 22, TextAnchor.MiddleCenter, new Color(0.92f, 0.95f, 1f));
            modeText.text = "";
            speedText = Txt("Speed", transform, bl, new Vector2(150f, 100f), new Vector2(300f, 30f), 20, TextAnchor.MiddleLeft, new Color(0.85f, 0.9f, 1f));
            speedText.text = "";
            Img("BoostBack", transform, bl, new Vector2(150f, 22f), new Vector2(240f, 6f), new Color(0f, 0f, 0f, 0.5f));
            boostFill = Img("BoostFill", transform, bl, new Vector2(150f, 22f), new Vector2(240f, 6f), new Color(0.5f, 0.8f, 1f, 0.95f));
            boostFill.type = Image.Type.Filled; boostFill.fillMethod = Image.FillMethod.Horizontal; boostFill.enabled = false;

            BuildTouchControls();
            BuildFlightTouchControls();
            GameState.Changed += Refresh;
            Refresh();
        }

        void MakeRing(RectTransform rt)
        {
            // four thin bars round the centre: the hit marker
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
            var bl = new Vector2(0f, 0f);
            var stickBack = Img("StickBack", touchRoot.transform, bl, new Vector2(190f, 190f), new Vector2(200f, 200f), new Color(1f, 1f, 1f, 0.12f), true);
            var knob = Img("Stick", stickBack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f), new Color(1f, 1f, 1f, 0.45f), true);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = 70f;
            stick.controlPath = "<Gamepad>/leftStick";

            var br = new Vector2(1f, 0f);
            TouchButton("Fire", touchRoot.transform, br, new Vector2(-150f, 200f), 150f, "<Gamepad>/rightTrigger", new Color(1f, 0.45f, 0.35f, 0.5f));
            TouchButton("Aim", touchRoot.transform, br, new Vector2(-320f, 150f), 100f, "<Gamepad>/leftTrigger", new Color(0.6f, 0.8f, 1f, 0.45f));
            TouchButton("Jump", touchRoot.transform, br, new Vector2(-110f, 380f), 100f, "<Gamepad>/buttonSouth", new Color(0.7f, 1f, 0.7f, 0.45f));
            TouchButton("Crouch", touchRoot.transform, br, new Vector2(-260f, 330f), 86f, "<Gamepad>/buttonEast", new Color(1f, 0.9f, 0.6f, 0.45f));
            TouchButton("Reload", touchRoot.transform, br, new Vector2(-400f, 280f), 86f, "<Gamepad>/buttonWest", new Color(1f, 1f, 1f, 0.4f));
            TouchButton("View", touchRoot.transform, new Vector2(1f, 1f), new Vector2(-80f, -60f), 72f, "<Gamepad>/buttonNorth", new Color(1f, 1f, 1f, 0.35f));
            TouchButton("Get in", touchRoot.transform, new Vector2(1f, 1f), new Vector2(-170f, -60f), 72f, "<Gamepad>/select", new Color(1f, 1f, 1f, 0.35f));
            touchRoot.SetActive(InputBridge.Instance != null && InputBridge.Instance.IsTouch);
        }

        void BuildFlightTouchControls()
        {
            flightTouchRoot = new GameObject("FlightTouch", typeof(RectTransform));
            flightTouchRoot.transform.SetParent(transform, false);
            var r = flightTouchRoot.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var bl = new Vector2(0f, 0f);
            var stickBack = Img("StickBack", flightTouchRoot.transform, bl, new Vector2(190f, 190f), new Vector2(200f, 200f), new Color(1f, 1f, 1f, 0.12f), true);
            var knob = Img("Stick", stickBack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(84f, 84f), new Color(1f, 1f, 1f, 0.45f), true);
            var stick = knob.gameObject.AddComponent<OnScreenStick>();
            stick.movementRange = 70f;
            stick.controlPath = "<Gamepad>/leftStick";
            // throttle: a tall slider on the right edge (pushes the pad's dpad up/down)
            var br = new Vector2(1f, 0f);
            var thrBack = Img("ThrottleBack", flightTouchRoot.transform, br, new Vector2(-60f, 420f), new Vector2(44f, 260f), new Color(1f, 1f, 1f, 0.12f), true);
            var thrKnob = Img("Throttle", thrBack.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(60f, 60f), new Color(1f, 1f, 1f, 0.45f), true);
            var thr = thrKnob.gameObject.AddComponent<OnScreenStick>();
            thr.movementRange = 100f;
            thr.controlPath = "<Gamepad>/dpad";
            Txt("Label", thrBack.transform, new Vector2(0.5f, 1f), new Vector2(0f, 18f), new Vector2(120f, 24f), 16, TextAnchor.MiddleCenter, Color.white).text = "THR";
            TouchButton("Gun", flightTouchRoot.transform, br, new Vector2(-150f, 200f), 150f, "<Gamepad>/rightTrigger", new Color(1f, 0.45f, 0.35f, 0.5f));
            TouchButton("Bomb", flightTouchRoot.transform, br, new Vector2(-330f, 170f), 120f, "<Gamepad>/buttonSouth", new Color(1f, 0.85f, 0.4f, 0.5f));
            TouchButton("Boost", flightTouchRoot.transform, br, new Vector2(-250f, 330f), 100f, "<Gamepad>/leftShoulder", new Color(0.6f, 0.8f, 1f, 0.45f));
            TouchButton("Exit", flightTouchRoot.transform, new Vector2(1f, 1f), new Vector2(-80f, -60f), 72f, "<Gamepad>/select", new Color(1f, 1f, 1f, 0.35f));
            flightTouchRoot.SetActive(false);
        }

        /// <summary>Flying or on foot: swaps the readouts and the touch layout.</summary>
        public void SetFlight(PlaneController p)
        {
            flight = p;
            bool touch = InputBridge.Instance != null && InputBridge.Instance.IsTouch;
            if (flightTouchRoot != null) flightTouchRoot.SetActive(touch && p != null);
            if (touchRoot != null) touchRoot.SetActive(touch && p == null);
            if (boostFill != null) boostFill.enabled = p != null;
            if (speedText != null) speedText.text = "";
        }

        public void SetModeLine(string text) { if (modeText != null) modeText.text = text; }

        public void Announce(string text, float seconds) { message.text = text; messageT = seconds; }

        void TouchButton(string label, Transform parent, Vector2 anchor, Vector2 pos, float size, string path, Color c)
        {
            var im = Img(label, parent, anchor, pos, new Vector2(size, size), c, true);
            var b = im.gameObject.AddComponent<OnScreenButton>();
            b.controlPath = path;
            Txt("Label", im.transform, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size), Mathf.RoundToInt(size * 0.24f), TextAnchor.MiddleCenter, Color.white).text = label.ToUpper();
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

        void Refresh()
        {
            if (waveText != null && GalacticMode.Instance != null) waveText.text = "GALACTIC ASSAULT";
            else if (waveText != null) waveText.text = GameState.Wave > 0 ? "WAVE " + GameState.Wave + (GameState.Spawner != null ? "   " + GameState.Spawner.Remaining + " left" : "") : "GET READY";
            if (killText != null) killText.text = "Kills " + GameState.Kills + (GameState.Headshots > 0 ? "   Headshots " + GameState.Headshots : "");
        }

        void Update()
        {
            float dt = Time.deltaTime;
            var p = GameState.Player;
            if (flight != null && flight.Active)
            {
                healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, flight.health.Fraction, Mathf.Min(1f, 10f * dt));
                ammoText.text = "BOMBS " + flight.bombs;
                reloadText.text = flight.Overheated ? "GUNS HOT" : (flight.docked != null ? "DOCKED  ·  throttle up to launch" : "");
                reloadFill.fillAmount = flight.heat; reloadFill.enabled = true;
                reloadFill.color = flight.Overheated ? new Color(1f, 0.3f, 0.2f, 0.95f) : new Color(1f, 0.85f, 0.4f, 0.95f);
                boostFill.fillAmount = flight.boost;
                speedText.text = "SPD " + Mathf.RoundToInt(flight.speedU) + "   ALT " + Mathf.RoundToInt(flight.AltitudeU) + "   THR " + Mathf.RoundToInt(flight.thr * 100f) + "%";
                crossSpread = Mathf.Lerp(crossSpread, 9f, Mathf.Min(1f, 8f * dt));
                crossA.enabled = crossB.enabled = crossC.enabled = crossD.enabled = true;
                crossA.rectTransform.anchoredPosition = new Vector2(0f, crossSpread);
                crossB.rectTransform.anchoredPosition = new Vector2(0f, -crossSpread);
                crossC.rectTransform.anchoredPosition = new Vector2(-crossSpread, 0f);
                crossD.rectTransform.anchoredPosition = new Vector2(crossSpread, 0f);
            }
            else if (p != null && p.health != null) healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, p.health.Fraction, Mathf.Min(1f, 10f * dt));
            if ((flight == null || !flight.Active) && p != null && p.rifle != null)
            {
                ammoText.text = p.rifle.ammo + " / " + p.rifle.magSize;
                reloadText.text = p.rifle.Reloading ? "RELOADING" : (p.rifle.ammo == 0 ? "EMPTY" : "");
                reloadFill.fillAmount = p.rifle.Reloading ? p.rifle.ReloadFraction : 0f;
                reloadFill.enabled = p.rifle.Reloading;
                bool aiming = p.rifle.Aiming;
                crossSpread = Mathf.Lerp(crossSpread, aiming ? 5f : 12f + p.MoveSpeed * 1.5f, Mathf.Min(1f, 8f * dt));
                bool showCross = !(GameState.Rig != null && GameState.Rig.firstPerson && aiming);
                crossA.enabled = crossB.enabled = crossC.enabled = crossD.enabled = showCross;
                crossA.rectTransform.anchoredPosition = new Vector2(0f, crossSpread);
                crossB.rectTransform.anchoredPosition = new Vector2(0f, -crossSpread);
                crossC.rectTransform.anchoredPosition = new Vector2(-crossSpread, 0f);
                crossD.rectTransform.anchoredPosition = new Vector2(crossSpread, 0f);
            }
            if (hitT > 0f) { hitT -= dt; if (hitT <= 0f) foreach (var im in hitMarkerRoot.GetComponentsInChildren<Image>()) im.color = new Color(1f, 1f, 1f, 0f); }
            if (flashT > 0f) { flashT -= dt; damageFlash.color = new Color(0.65f, 0f, 0f, Mathf.Clamp01(flashT) * 0.45f); } else damageFlash.color = new Color(0.65f, 0f, 0f, 0f);
            if (messageT > 0f) { messageT -= dt; if (messageT <= 0f) message.text = ""; else message.color = new Color(1f, 1f, 1f, Mathf.Clamp01(messageT * 1.5f)); }
            if (InputBridge.Instance != null)
            {
                bool touch = InputBridge.Instance.IsTouch, flying = flight != null && flight.Active;
                if (touchRoot != null && touchRoot.activeSelf != (touch && !flying)) touchRoot.SetActive(touch && !flying);
                if (flightTouchRoot != null && flightTouchRoot.activeSelf != (touch && flying)) flightTouchRoot.SetActive(touch && flying);
            }
        }

        void OnDestroy() { GameState.Changed -= Refresh; }
    }
}
