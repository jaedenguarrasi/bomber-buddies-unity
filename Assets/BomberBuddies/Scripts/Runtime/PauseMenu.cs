using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The original's pause sheet (oPause), button for button: Resume, Invite friends, Controls for this, Hangar,
    /// Settings, Respawn now, Add enemies, AI crew, Restart from wave 1, Quit to menu (tap again), then Commander view /
    /// Tilt: Off / Level tilt on a second row. "Add enemies" opens the original's Send-in list. Time stops while it is up.</summary>
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

        static readonly Color SheetC = new Color(0.07f, 0.1f, 0.14f, 0.96f);
        static readonly Color GoC = new Color(0.93f, 0.76f, 0.2f, 1f);
        static readonly Color GhostC = new Color(1f, 1f, 1f, 0.1f);
        static readonly Color Ink = new Color(0.94f, 0.91f, 0.84f, 1f);
        static readonly Color Sub = new Color(0.94f, 0.91f, 0.84f, 0.7f);

        GameObject root, pauseSheet, callSheet;
        Font font;
        Text info, callInfo;
        Button bCrew, bCmd, bTiltT, bTiltP, bQuit, bRestart;
        float quitArm = -10f;

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
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.02f, 0.03f, 0.05f, 0.55f); bg.raycastTarget = true;

            // ---- the pause sheet ----
            pauseSheet = Sheet("Sheet", 720f);
            var col = pauseSheet.transform;
            Txt("Title", col, 34, "Paused", true, Ink, TextAnchor.MiddleLeft);
            info = Txt("Info", col, 15, "", false, Sub, TextAnchor.MiddleLeft);
            var row = Row("Row", col);
            Go(row, "Resume", () => Close());
            Ghost(row, "Invite friends", () => { info.text = "Online play is not in this build yet: the room code lobby from the original is next on the list."; });
            Ghost(row, "Controls for this", () => { root.SetActive(false); ControlsScreen.Open(transform.parent, () => root.SetActive(true)); ControlsScreen.Instance.transform.SetAsLastSibling(); });
            Ghost(row, "Hangar", () => { root.SetActive(false); Hangar.Open(transform.parent.parent, () => { root.SetActive(true); if (PlayerManager.Instance != null) PlayerManager.Instance.ApplyLoadout(); Refresh(); }); });
            Ghost(row, "Settings", () => { root.SetActive(false); OptionsScreen.Open(transform.parent, () => { root.SetActive(true); Refresh(); }); });
            Ghost(row, "Respawn now", ForceRespawn);
            Ghost(row, "Add enemies", () => { pauseSheet.SetActive(false); RenderCall(); callSheet.SetActive(true); });
            bCrew = Ghost(row, "AI crew: on", () => { Settings.Set("crewAI", Settings.CrewAI == "solo" ? "ai" : "solo"); Settings.Save(); Refresh(); info.text = Settings.CrewAI == "solo" ? "The other seat is now empty: no AI gunner, and no AI pilot when you swap seats." : "An AI copilot now fills the other seat."; });
            bRestart = Ghost(row, "Restart from wave 1", () => { Time.timeScale = 1f; GameBootstrap.ChosenMode = GameBootstrap.LastMode; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); });
            bQuit = Ghost(row, "Quit to menu", () => QuitTap());
            var row2 = Row("Row2", col);
            bCmd = Ghost(row2, "Commander view", () => { if (Commander.Instance == null) return; Close(); if (Commander.On) Commander.Instance.Leave(); else Commander.Instance.Enter(); });
            bTiltT = Ghost(row2, "Tilt: Off", () => { bool on = Settings.Steer != "tilt"; Settings.Set("steer", on ? "tilt" : "stick"); Settings.Save(); Settings.Apply(); if (on) InputBridge.LevelTilt(); Refresh(); });
            bTiltP = Ghost(row2, "Level tilt", () => { InputBridge.LevelTilt(); info.text = "Tilt levelled: hold the phone the way you are holding it now."; });

            // ---- the Add enemies sheet (oCall) ----
            callSheet = Sheet("Call", 640f);
            var cc = callSheet.transform;
            Txt("Title", cc, 30, "Add enemies", true, Ink, TextAnchor.MiddleLeft);
            callInfo = Txt("Info", cc, 15, "They arrive straight away and join the current wave.", false, Sub, TextAnchor.MiddleLeft);
            callList = new GameObject("List", typeof(RectTransform)); callList.transform.SetParent(cc, false);
            var vl = callList.AddComponent<VerticalLayoutGroup>(); vl.spacing = 4f; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
            callList.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var foot = Row("Foot", cc);
            Go(foot, "Back to the battle", () => Close());
            callSheet.SetActive(false);

            Refresh();
            root.SetActive(false);
        }
        GameObject callList;

        /// <summary>The original's ENEMY_NAMES list with its +1/+3 (big or air) or +4/+12 buttons.</summary>
        static readonly string[][] CallRows = { new[] { "inf", "Infantry" }, new[] { "jeep", "Jeeps" }, new[] { "tank", "Tanks" }, new[] { "aa", "Anti-aircraft guns" }, new[] { "sam", "Missile launchers" }, new[] { "bunker", "Bunkers" }, new[] { "eheli", "Helicopters" }, new[] { "eplane", "Fighter planes" }, new[] { "ufo", "Flying saucers" }, new[] { "ape", "Apes (3D game)" }, new[] { "gob", "Goblin bands (3D game)" } };
        void RenderCall()
        {
            foreach (Transform t in callList.transform) Destroy(t.gameObject);
            foreach (var r in CallRows)
            {
                string k = r[0];
                bool big = k == "bunker" || k == "sam" || k == "aa" || k == "tank" || k == "eheli" || k == "eplane" || k == "ufo";
                int a = big ? 1 : 4, b = big ? 3 : 12;
                var line = new GameObject("Opt " + k, typeof(RectTransform)); line.transform.SetParent(callList.transform, false);
                var hl = line.AddComponent<HorizontalLayoutGroup>(); hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true; hl.spacing = 6f; hl.childAlignment = TextAnchor.MiddleLeft;
                line.AddComponent<LayoutElement>().preferredHeight = 36f;
                var lab = Txt("Name", line.transform, 16, r[1], false, Ink, TextAnchor.MiddleLeft); lab.GetComponent<LayoutElement>().flexibleWidth = 1f;
                Seg(line.transform, "+" + a, () => { Reinforce(k, a); });
                Seg(line.transform, "+" + b, () => { Reinforce(k, b); });
            }
        }
        void Reinforce(string k, int n)
        {
            if (GameState.Spawner == null) return;
            int made = GameState.Spawner.Reinforce(k, n);
            callInfo.text = made > 0 ? "Sent in " + made + ". They join the current wave." : "They could not be placed just now.";
        }
        Button Seg(Transform parent, string label, UnityEngine.Events.UnityAction a)
        {
            var b = Btn("Seg", parent, label, 15, a, GhostC, Ink);
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 64f; le.preferredHeight = 32f;
            return b;
        }

        /// <summary>The original's forceRespawn: no life lost, for switching vehicle or getting unstuck.</summary>
        void ForceRespawn()
        {
            var pm = PlayerManager.Instance; if (pm == null || pm.Respawning) { Close(); return; }
            Close();
            Health h = pm.OnFoot ? (pm.foot != null ? pm.foot.health : null) : (pm.plane != null ? pm.plane.health : null);
            if (h != null && h.alive) { GameState.Deaths--; h.TakeDamage(new DamageInfo { amount = 1e9f, point = h.transform.position, direction = Vector3.up, source = null }); }
        }

        void QuitTap()
        {
            float now = Time.unscaledTime;
            if (now - quitArm < 3f) { Time.timeScale = 1f; GameBootstrap.ChosenMode = null; UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex); return; }
            quitArm = now; SetLabel(bQuit, "Tap again to quit");
            info.text = "Your credits and hangar are kept. Tap again to go to the menu.";
            StartCoroutine(Rearm());
        }
        System.Collections.IEnumerator Rearm() { yield return new WaitForSecondsRealtime(3.05f); if (Time.unscaledTime - quitArm >= 2.95f) SetLabel(bQuit, "Quit to menu"); }

        void Refresh()
        {
            if (info == null) return;
            info.text = "Wave " + GameState.Wave + ". " + Progress.Bank.ToString("N0") + " credits in the bank.";
            var pm = PlayerManager.Instance;
            bool twoSeat = pm != null && !pm.OnFoot && pm.plane != null && pm.plane.Seats == 2;
            bCrew.gameObject.SetActive(twoSeat); SetLabel(bCrew, Settings.CrewAI == "solo" ? "AI crew: off" : "AI crew: on");
            bool command = GameState.Spawner != null && GameState.Spawner.modeId == "command";
            bCmd.gameObject.SetActive(command && Commander.Instance != null); SetLabel(bCmd, Commander.On ? "Back to the fight" : "Commander view");
            bool tilt = Settings.Steer == "tilt";
            SetLabel(bTiltT, tilt ? "Tilt: On" : "Tilt: Off"); bTiltP.gameObject.SetActive(tilt);
            SetLabel(bQuit, "Quit to menu");
        }

        public void Open() { pauseSheet.SetActive(true); callSheet.SetActive(false); Refresh(); root.SetActive(true); Time.timeScale = 0f; Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        public void Close() { root.SetActive(false); Time.timeScale = 1f; }
        public void Toggle() { if (IsOpen) Close(); else if (!(DeployScreen.Instance != null && DeployScreen.Instance.IsOpen) && !MatchEnd.Over && !Hangar.IsOpen && !(OptionsScreen.Instance != null && OptionsScreen.Instance.IsOpen)) Open(); }

        // ---- widgets in the original's sheet style ----
        GameObject Sheet(string name, float width)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.sizeDelta = new Vector2(width, 0f);
            go.AddComponent<Image>().color = SheetC;
            var vl = go.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(22, 22, 18, 18); vl.spacing = 8f; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go;
        }
        Transform Row(string name, Transform parent)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var g = go.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(160f, 42f); g.spacing = new Vector2(8f, 8f); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = 4;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }
        Button Go(Transform row, string label, UnityEngine.Events.UnityAction a) { return Btn(label, row, label, 16, a, GoC, new Color(0.1f, 0.08f, 0.04f, 1f)); }
        Button Ghost(Transform row, string label, UnityEngine.Events.UnityAction a) { return Btn(label, row, label, 15, a, GhostC, Ink); }
        static void SetLabel(Button b, string s) { var t = b.GetComponentInChildren<Text>(); if (t != null) t.text = s; }

        Text Txt(string name, Transform parent, int fontSize, string text, bool bold, Color c, TextAnchor align)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fontSize; t.alignment = align; t.color = c; t.text = text; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            var le = go.AddComponent<LayoutElement>(); le.minHeight = fontSize + 8f;
            return t;
        }
        Button Btn(string name, Transform parent, string label, int fs, UnityEngine.Events.UnityAction onClick, Color bg, Color ink)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            var b = go.AddComponent<Button>(); b.onClick.AddListener(onClick);
            var tg = new GameObject("Label", typeof(RectTransform)); tg.transform.SetParent(go.transform, false);
            var tr = tg.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = tg.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = TextAnchor.MiddleCenter; t.color = ink; t.text = label; t.raycastTarget = false; t.fontStyle = FontStyle.Bold; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return b;
        }
    }
}
