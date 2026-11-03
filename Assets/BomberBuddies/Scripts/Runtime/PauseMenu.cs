using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The pause screen: resume, a few of the original's settings (look sensitivity, invert, big buttons, left-handed),
    /// and quit to the title. Time stops while it is up.</summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static float Sensitivity = 1f;
        public static bool InvertY, BigButtons, Lefty, Tilt;
        /// <summary>Graphics detail (the original's opt.detail): 0 low (no shadows, 70 % render scale), 1 normal, 2 high.</summary>
        public static int Detail = 1;
        public static void ApplyDetail()
        {
            var urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline as UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset;
            if (urp != null) { urp.renderScale = Detail == 0 ? 0.7f : Detail == 2 ? 1f : (Application.isMobilePlatform ? 0.85f : 1f); urp.shadowDistance = Detail == 0 ? 0f : Detail == 2 ? 120f : 60f; urp.supportsCameraDepthTexture = Detail != 0; }
            QualitySettings.shadows = Detail == 0 ? ShadowQuality.Disable : ShadowQuality.All;
        }
        GameObject root;
        Font font;
        Text sensText;

        public static PauseMenu Create(Transform hudCanvas)
        {
            var go = new GameObject("Pause menu", typeof(RectTransform));
            go.transform.SetParent(hudCanvas, false);
            var pm = go.AddComponent<PauseMenu>();
            Instance = pm;
            pm.Build();
            return pm;
        }

        public bool IsOpen { get { return root != null && root.activeSelf; } }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            Settings.Load();
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            root = gameObject;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.82f); bg.raycastTarget = true;
            Txt("Title", new Vector2(0f, 250f), new Vector2(800f, 60f), 40, "Paused");
            Button("Resume", new Vector2(0f, 150f), () => Close());
            Button("Options", new Vector2(-180f, 60f), () => { root.SetActive(false); OptionsScreen.Open(transform.parent, () => root.SetActive(true)); });
            Button("Controls", new Vector2(180f, 60f), () => { root.SetActive(false); ControlsScreen.Open(transform.parent, () => root.SetActive(true)); ControlsScreen.Instance.transform.SetAsLastSibling(); });
            Button("Commander view", new Vector2(-180f, -30f), () => { if (Commander.Instance == null) return; Close(); if (Commander.On) Commander.Instance.Leave(); else Commander.Instance.Enter(); });
            Button("Hangar", new Vector2(180f, -30f), () => { root.SetActive(false); Hangar.Open(transform.parent.parent, () => { root.SetActive(true); if (PlayerManager.Instance != null) PlayerManager.Instance.ApplyLoadout(); }); });
            Button("Quit to title", new Vector2(0f, -140f), () => { Time.timeScale = 1f; GameBootstrap.ChosenMode = null; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); });
            sensText = Txt("SensLabel", new Vector2(0f, -210f), new Vector2(1000f, 30f), 15, "");
            Refresh();
            root.SetActive(false);
        }

        void Save() { Settings.LookSens = Sensitivity; Settings.InvertY = InvertY; Settings.BigButtons = BigButtons; Settings.Lefty = Lefty; Settings.Steer = Tilt ? "tilt" : "stick"; Settings.Detail = Detail; Settings.Save(); Refresh(); }

        void Refresh()
        {
            if (sensText != null) sensText.text = "Look sensitivity " + Mathf.RoundToInt(Sensitivity * 100f) + "%   ·   invert " + (InvertY ? "on" : "off") + "   ·   big buttons " + (BigButtons ? "on" : "off") + "   ·   left-handed " + (Lefty ? "on" : "off") + "   ·   tilt " + (Tilt ? "on" : "off") + "   ·   detail " + (Detail == 0 ? "low" : Detail == 2 ? "high" : "normal");
        }

        public void Open() { root.SetActive(true); Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void Close() { root.SetActive(false); Time.timeScale = 1f; }
        public void Toggle() { if (IsOpen) Close(); else Open(); }

        Text Txt(string name, Vector2 pos, Vector2 size, int fontSize, string text)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fontSize; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            return t;
        }

        void Button(string label, Vector2 pos, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(320f, 70f);
            var im = go.AddComponent<Image>(); im.color = new Color(0.16f, 0.2f, 0.3f, 0.95f);
            var b = go.AddComponent<UnityEngine.UI.Button>(); b.onClick.AddListener(onClick);
            var t = Txt("Label", Vector2.zero, new Vector2(320f, 70f), 26, label); t.transform.SetParent(go.transform, false);
        }
    }
}
