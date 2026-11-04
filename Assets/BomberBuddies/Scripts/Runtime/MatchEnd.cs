using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The original's over sheet (oOver): "Shot down", "Earned N credits", the info line (wave reached, best flight,
    /// bank), the scoreboard table (Player K D K/D Heads Limbs Veh Bldg Run) with the career line, and
    /// Take off again / Hangar / Main menu.</summary>
    public class MatchEnd : MonoBehaviour
    {
        public static MatchEnd Instance { get; private set; }
        public static bool Over;
        static readonly Color SheetC = new Color(0.07f, 0.1f, 0.14f, 0.96f);
        static readonly Color GoC = new Color(0.93f, 0.76f, 0.2f, 1f);
        static readonly Color GhostC = new Color(1f, 1f, 1f, 0.1f);
        static readonly Color Ink = new Color(0.94f, 0.91f, 0.84f, 1f);
        static readonly Color Sub = new Color(0.94f, 0.91f, 0.84f, 0.7f);
        static readonly Color Me = new Color(1f, 0.82f, 0.4f, 1f);
        Font font; GameObject root, sheet; Text title, score, info, career; Transform table;
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
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.02f, 0.03f, 0.05f, 0.55f); bg.raycastTarget = true;
            sheet = new GameObject("Sheet", typeof(RectTransform)); sheet.transform.SetParent(transform, false);
            var sr = sheet.GetComponent<RectTransform>(); sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.sizeDelta = new Vector2(700f, 0f);
            sheet.AddComponent<Image>().color = SheetC;
            var vl = sheet.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(22, 22, 18, 18); vl.spacing = 6f; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
            sheet.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            title = Txt("Title", sheet.transform, 34, "Shot down", true, Ink, TextAnchor.MiddleLeft);
            score = Txt("Score", sheet.transform, 22, "", true, Ink, TextAnchor.MiddleLeft);
            info = Txt("Info", sheet.transform, 15, "", false, Sub, TextAnchor.MiddleLeft);
            table = new GameObject("Board", typeof(RectTransform)).transform; table.SetParent(sheet.transform, false);
            var tv = table.gameObject.AddComponent<VerticalLayoutGroup>(); tv.spacing = 2f; tv.childForceExpandHeight = false; tv.childForceExpandWidth = true; tv.childControlHeight = true; tv.childControlWidth = true;
            table.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            career = Txt("Career", sheet.transform, 13, "", false, Sub, TextAnchor.MiddleLeft);
            var row = new GameObject("Row", typeof(RectTransform)); row.transform.SetParent(sheet.transform, false);
            var g = row.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(160f, 42f); g.spacing = new Vector2(8f, 8f); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 4;
            row.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Btn(row.transform, "Take off again", GoC, new Color(0.1f, 0.08f, 0.04f, 1f), () => { GameBootstrap.ChosenMode = GameBootstrap.LastMode; Reload(); });
            Btn(row.transform, "Hangar", GhostC, Ink, () => { root.SetActive(false); Hangar.Open(transform.parent.parent, () => root.SetActive(true)); });
            Btn(row.transform, "Main menu", GhostC, Ink, () => { GameBootstrap.ChosenMode = null; Reload(); });
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
            int earned = GameState.Credits;
            if (earned > Progress.Best) Progress.Best = earned;
            title.text = won ? "Victory" : "Shot down";
            score.text = "Earned " + Progress.Fmt(earned) + " credits";
            info.text = (won ? why + " " : "You reached wave " + GameState.Wave + ". ") + "Best flight: " + Progress.Fmt(Progress.Best) + ". Bank: " + Progress.Fmt(Progress.Bank) + ".";
            // the scoreboard, the original's scoreHTML
            foreach (Transform t in table) Destroy(t.gameObject);
            string kd = GameState.Deaths > 0 ? ((float)GameState.Kills / GameState.Deaths).ToString("0.0") : GameState.Kills.ToString();
            BoardRow(new[] { "Player", "K", "D", "K/D", "Heads", "Limbs", "Veh", "Bldg", "Run" }, Sub, false);
            BoardRow(new[] { "You", GameState.Kills.ToString(), GameState.Deaths.ToString(), kd, GameState.Headshots.ToString(), GameState.Limbs.ToString(), GameState.Vehicles.ToString(), GameState.Buildings.ToString(), GameState.Wave.ToString() }, Me, true);
            int Lk = Progress.Life("k") + GameState.Kills, Ld = Progress.Life("d") + GameState.Deaths, Lh = Progress.Life("hs") + GameState.Headshots, Ll = Progress.Life("lb") + GameState.Limbs, Lb = Progress.Life("bld") + GameState.Buildings, Lg = Progress.Life("games") + 1;
            career.text = Lg > 1 ? "Your career: " + Lk + " kills, " + Ld + " deaths, " + Lh + " headshots, " + Ll + " limbs, " + Lb + " buildings over " + Lg + " battles. Best run " + Mathf.Max(Progress.Life("best"), GameState.Wave) + "." : "";
            Progress.LifeAdd(GameState.Kills, GameState.Deaths, GameState.Headshots, GameState.Limbs, GameState.Buildings, GameState.Wave);
            PlayerPrefs.Save();
            root.SetActive(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            Sfx.Play(won ? "sting" : "thud", 0.7f, won ? 1.2f : 0.7f);
            if (won) Sfx.Tone(880f, 0.3f, 0.1f, "triangle");
        }

        void BoardRow(string[] cells, Color c, bool line)
        {
            var go = new GameObject("Row", typeof(RectTransform)); go.transform.SetParent(table, false);
            var hl = go.AddComponent<HorizontalLayoutGroup>(); hl.childForceExpandWidth = false; hl.childForceExpandHeight = true; hl.childControlWidth = true; hl.childControlHeight = true;
            go.AddComponent<LayoutElement>().preferredHeight = 22f;
            if (line) { var im = go.AddComponent<Image>(); im.color = new Color(1f, 1f, 1f, 0.04f); }
            for (int i = 0; i < cells.Length; i++)
            {
                var t = Txt("C" + i, go.transform, 13, cells[i], true, c, i == 0 ? TextAnchor.MiddleLeft : TextAnchor.MiddleRight);
                var le = t.GetComponent<LayoutElement>(); le.preferredWidth = i == 0 ? 180f : 56f; le.flexibleWidth = i == 0 ? 1f : 0f;
            }
        }

        Text Txt(string name, Transform parent, int fs, string text, bool bold, Color c, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = align; t.color = c; t.text = text; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<LayoutElement>().minHeight = fs + 8f;
            return t;
        }
        void Btn(Transform parent, string label, Color bg, Color ink, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(label, typeof(RectTransform)); go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            var b = go.AddComponent<Button>(); b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); onClick(); });
            var tg = new GameObject("Label", typeof(RectTransform)); tg.transform.SetParent(go.transform, false);
            var tr = tg.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = tg.AddComponent<Text>(); t.font = font; t.fontSize = 16; t.alignment = TextAnchor.MiddleCenter; t.color = ink; t.text = label; t.raycastTarget = false; t.fontStyle = FontStyle.Bold;
        }
    }
}
