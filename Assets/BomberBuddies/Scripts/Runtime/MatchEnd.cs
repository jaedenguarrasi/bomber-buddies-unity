using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The original's over screen (showOver / scoreHTML): why it ended, the score, kills, headshots, the wave reached,
    /// credits earned and banked, your best, and Again / Hangar / Title. Lives run out the way they did: 3 plus spare aircraft.</summary>
    public class MatchEnd : MonoBehaviour
    {
        public static MatchEnd Instance { get; private set; }
        public static bool Over;
        Font font; GameObject root; Text title, body;
        int creditsAtStart;

        public static MatchEnd Create(Transform hudCanvas)
        {
            var go = new GameObject("Match end", typeof(RectTransform)); go.transform.SetParent(hudCanvas, false);
            var m = go.AddComponent<MatchEnd>(); Instance = m; Over = false; m.creditsAtStart = Progress.Bank - GameState.Credits; m.Build(); return m;
        }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = gameObject;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.86f); bg.raycastTarget = true;
            title = Txt("Title", new Vector2(0f, 230f), new Vector2(1000f, 60f), 44, "", Color.white, true);
            body = Txt("Body", new Vector2(0f, 40f), new Vector2(900f, 300f), 22, "", new Color(0.85f, 0.9f, 1f));
            Btn("Again", new Vector2(-240f, -220f), "Again", () => { GameBootstrap.ChosenMode = GameBootstrap.LastMode; Reload(); });
            Btn("Hangar", new Vector2(0f, -220f), "Hangar", () => { root.SetActive(false); Hangar.Open(transform.parent.parent, () => root.SetActive(true)); });
            Btn("Title", new Vector2(240f, -220f), "Title", () => { GameBootstrap.ChosenMode = null; Reload(); });
            root.SetActive(false);
        }

        static void Reload() { Time.timeScale = 1f; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); }

        /// <summary>The match is over: show the score. won: a win, a loss, or just the end of the road.</summary>
        public void Show(bool won, string why)
        {
            if (Over) return;
            Over = true;
            if (PlayerManager.Instance != null) PlayerManager.Instance.over = true;
            if (DeployScreen.Instance != null) DeployScreen.Instance.Hide();
            if (PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) PauseMenu.Instance.Close();
            Time.timeScale = 1f;
            int earned = GameState.Credits; int score = GameState.Kills * 10 + GameState.Headshots * 5 + GameState.Wave * 100 + earned;
            bool best = score > Progress.Best; if (best) Progress.Best = score;
            PlayerPrefs.Save();
            title.text = won ? "VICTORY" : "GAME OVER";
            title.color = won ? new Color(0.6f, 1f, 0.7f) : new Color(1f, 0.55f, 0.5f);
            body.text = why + "\n\n" +
                "Score  " + Progress.Fmt(score) + (best ? "   ·  a new best!" : "   ·  best " + Progress.Fmt(Progress.Best)) + "\n" +
                "Kills " + GameState.Kills + "   ·   headshots " + GameState.Headshots + "   ·   wave " + GameState.Wave + "   ·   deaths " + GameState.Deaths + "\n" +
                "Credits earned " + Progress.Fmt(earned) + "   ·   in the bank " + Progress.Fmt(Progress.Bank);
            root.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Sfx.Play(won ? "sting" : "thud", 0.7f, won ? 1.2f : 0.7f);
            if (won) Sfx.Tone(880f, 0.3f, 0.1f, "triangle");
        }

        Text Txt(string name, Vector2 pos, Vector2 size, int fs, string text, Color c, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = TextAnchor.MiddleCenter; t.color = c; t.text = text; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        void Btn(string name, Vector2 pos, string label, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(200f, 60f);
            go.AddComponent<Image>().color = new Color(0.16f, 0.2f, 0.3f, 1f);
            var b = go.AddComponent<Button>(); b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); onClick(); });
            var t = Txt("Label", Vector2.zero, new Vector2(200f, 60f), 24, label, Color.white, true); t.transform.SetParent(go.transform, false); t.rectTransform.anchoredPosition = Vector2.zero;
        }
    }
}
