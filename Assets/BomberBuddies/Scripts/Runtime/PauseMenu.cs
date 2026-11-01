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
            Sensitivity = PlayerPrefs.GetFloat("bb_sens", 1f); InvertY = PlayerPrefs.GetInt("bb_inv", 0) == 1; BigButtons = PlayerPrefs.GetInt("bb_big", 0) == 1; Lefty = PlayerPrefs.GetInt("bb_lefty", 0) == 1; Tilt = PlayerPrefs.GetInt("bb_tilt", 0) == 1;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            root = gameObject;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.82f); bg.raycastTarget = true;
            Txt("Title", new Vector2(0f, 320f), new Vector2(800f, 70f), 44, "Paused");
            Button("Resume", new Vector2(-180f, 230f), () => Close());
            Button("Hangar", new Vector2(-180f, -280f), () => { root.SetActive(false); Hangar.Open(transform.parent.parent, () => { root.SetActive(true); if (PlayerManager.Instance != null) PlayerManager.Instance.ApplyLoadout(); }); });
            Button("Quit to title", new Vector2(180f, -280f), () => { Time.timeScale = 1f; GameBootstrap.ChosenMode = null; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); });
            Button("Controls", new Vector2(180f, 230f), () => { root.SetActive(false); ControlsScreen.Open(transform.parent, () => root.SetActive(true)); });
            sensText = Txt("SensLabel", new Vector2(0f, 150f), new Vector2(800f, 40f), 24, "");
            Button("Look slower", new Vector2(-180f, 80f), () => { Sensitivity = Mathf.Max(0.3f, Sensitivity - 0.15f); Save(); });
            Button("Look faster", new Vector2(180f, 80f), () => { Sensitivity = Mathf.Min(3f, Sensitivity + 0.15f); Save(); });
            Button("Invert look", new Vector2(-180f, -10f), () => { InvertY = !InvertY; Save(); });
            Button("Big buttons", new Vector2(180f, -10f), () => { BigButtons = !BigButtons; Save(); });
            Button("Left-handed", new Vector2(-180f, -100f), () => { Lefty = !Lefty; Save(); });
            Button("Tilt steering", new Vector2(-180f, -190f), () => { Tilt = !Tilt; if (Tilt) InputBridge.LevelTilt(); Save(); });
            Button("Level tilt", new Vector2(180f, -190f), () => { InputBridge.LevelTilt(); if (GameState.Hud != null) GameState.Hud.Comms("Tilt levelled: hold the phone like this to fly straight"); });
            Button("Commander view", new Vector2(180f, -100f), () => { if (Commander.Instance == null) return; Close(); if (Commander.On) Commander.Instance.Leave(); else Commander.Instance.Enter(); });
            Refresh();
            root.SetActive(false);
        }

        void Save()
        {
            PlayerPrefs.SetFloat("bb_sens", Sensitivity); PlayerPrefs.SetInt("bb_inv", InvertY ? 1 : 0); PlayerPrefs.SetInt("bb_big", BigButtons ? 1 : 0); PlayerPrefs.SetInt("bb_lefty", Lefty ? 1 : 0); PlayerPrefs.SetInt("bb_tilt", Tilt ? 1 : 0); PlayerPrefs.Save();
            Refresh();
            if (GameState.Hud != null) GameState.Hud.ApplyLayoutOptions();
        }

        void Refresh()
        {
            if (sensText != null) sensText.text = "Look sensitivity " + Mathf.RoundToInt(Sensitivity * 100f) + "%   ·   invert " + (InvertY ? "on" : "off") + "   ·   big buttons " + (BigButtons ? "on" : "off") + "   ·   left-handed " + (Lefty ? "on" : "off") + "   ·   tilt " + (Tilt ? "on" : "off");
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
