using System;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The original's "Fly together" sheet (oNet): your call sign, Nearby / Online, the room code, Host a game /
    /// Join a game, the list of who is in (or the nearby hosts), Start flying and the game-mode pick for the host.
    /// Opened from the title (Play together) and from the pause menu (Invite friends: the host goes live mid-battle).</summary>
    public class NetScreen : MonoBehaviour
    {
        public static NetScreen Instance { get; private set; }
        static readonly Color SheetC = new Color(0.07f, 0.1f, 0.14f, 0.96f), GoC = new Color(0.93f, 0.76f, 0.2f, 1f), GhostC = new Color(1f, 1f, 1f, 0.1f), Ink = new Color(0.94f, 0.91f, 0.84f, 1f), Sub = new Color(0.94f, 0.91f, 0.84f, 0.7f), Dark = new Color(0.1f, 0.08f, 0.04f, 1f);
        Font font; NetSession net; Action onBack; bool inGame; bool online;
        Text status, codeLabel; InputField nameField, codeField; GameObject nameFld, codeFld, choose, list, startRow; Button kNear, kOnline, bHost, bJoin, bStart, bMode, bLeave;

        public static NetScreen Open(Transform parent, bool inGame, Action onBack)
        {
            if (Instance != null) { Instance.onBack = onBack; Instance.inGame = inGame; Instance.gameObject.SetActive(true); Instance.Render(); return Instance; }
            var go = new GameObject("Fly together", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var s = go.AddComponent<NetScreen>(); Instance = s; s.onBack = onBack; s.inGame = inGame; s.Build(); s.Render(); return s;
        }
        public bool IsOpen { get { return gameObject.activeSelf; } }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            net = NetSession.Get(); net.Changed += Render;
            online = PlayerPrefs.GetInt("bb_netkind", 1) == 1;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.02f, 0.03f, 0.05f, 0.55f); bg.raycastTarget = true;
            var sheet = new GameObject("Sheet", typeof(RectTransform)); sheet.transform.SetParent(transform, false);
            var sr = sheet.GetComponent<RectTransform>(); sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0.5f); sr.sizeDelta = new Vector2(560f, 0f);
            sheet.AddComponent<Image>().color = SheetC;
            var vl = sheet.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(22, 22, 18, 18); vl.spacing = 8f; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
            sheet.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var col = sheet.transform;
            Txt("Title", col, 34, "Fly together", true, Ink);
            status = Txt("Status", col, 14, "One of you hosts and reads out the room code. The others type it in and join, from anywhere.", false, Sub);
            nameFld = Field(col, "Your call sign", net.callSign, 12, false, out nameField);
            nameField.onEndEdit.AddListener(v => { net.callSign = string.IsNullOrEmpty(v.Trim()) ? net.callSign : v.Trim(); PlayerPrefs.SetString("bb_name", net.callSign); nameField.text = net.callSign; });
            var kinds = Row(col, 2);
            kNear = Btn(kinds, "Nearby, same room", GhostC, Ink, () => { online = false; PlayerPrefs.SetInt("bb_netkind", 0); net.StartListening(); Render(); });
            kOnline = Btn(kinds, "Online, anywhere", GhostC, Ink, () => { online = true; PlayerPrefs.SetInt("bb_netkind", 1); net.StopListening(); Render(); });
            codeFld = Field(col, "Room code from the host", "", 5, true, out codeField); codeLabel = codeFld.GetComponentInChildren<Text>();
            choose = Row(col, 2).gameObject;
            bHost = Btn(choose.transform, "Host a game", GoC, Dark, () => { if (online) net.HostOnline(); else net.HostNearby(); });
            bJoin = Btn(choose.transform, "Join a game", GhostC, Ink, () => { if (online) net.JoinOnline(codeField.text); else { if (net.nearby.Count > 0) net.JoinNearby(net.nearby[0].Key); else status.text = "No host nearby yet. Ask them to tap Host a game on the same Wi-Fi."; } });
            list = new GameObject("List", typeof(RectTransform)); list.transform.SetParent(col, false);
            var lv = list.AddComponent<VerticalLayoutGroup>(); lv.spacing = 4f; lv.childForceExpandHeight = false; lv.childForceExpandWidth = true; lv.childControlHeight = true; lv.childControlWidth = true;
            list.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            startRow = Row(col, 3).gameObject;
            bStart = Btn(startRow.transform, "Start flying", GoC, Dark, () => { net.StartBattle(net.lobbyMode); });
            bMode = Btn(startRow.transform, "Mode", GhostC, Ink, () => { var keys = new System.Collections.Generic.List<string>(); foreach (var kv in ModeDefs.All) if (kv.Value.playable) keys.Add(kv.Key); int i = keys.IndexOf(net.lobbyMode); net.SetMode(keys[(i + 1) % keys.Count]); });
            bLeave = Btn(startRow.transform, "Back", GhostC, Ink, () => { Close(); });
            Txt("Note", col, 12, "Nearby uses Wi-Fi between phones in the same room, with no internet needed. Online works from anywhere: the host gets a room code and reads it to the others. Everyone must be on the same game version.", false, Sub);
            if (!online) net.StartListening();
        }

        void Close()
        {
            // leaving the sheet while linked keeps the link (the original's Back); leaving before a link is made drops the attempt
            if (!net.Active) net.Leave();
            net.StopListening();
            gameObject.SetActive(false);
            var cb = onBack; if (cb != null) cb();
        }
        void OnDestroy() { if (net != null) net.Changed -= Render; if (Instance == this) Instance = null; }

        public void Render()
        {
            if (status == null) return;
            bool linked = net.Active; bool host = net.IsHost;
            kNear.GetComponent<Image>().color = !online ? Ink : GhostC; kNear.GetComponentInChildren<Text>().color = !online ? Dark : Ink;
            kOnline.GetComponent<Image>().color = online ? Ink : GhostC; kOnline.GetComponentInChildren<Text>().color = online ? Dark : Ink;
            nameFld.SetActive(!linked); kNear.transform.parent.gameObject.SetActive(!linked);
            codeFld.SetActive(!linked && online);
            choose.SetActive(!linked);
            if (!string.IsNullOrEmpty(net.status)) status.text = net.status;
            else status.text = online ? "One of you hosts and reads out the room code. The others type it in and join, from anywhere." : "Everyone on the same Wi-Fi. One of you hosts; the others pick them from the list.";
            foreach (Transform t in list.transform) Destroy(t.gameObject);
            if (linked)
            {
                Item(list.transform, host ? (online ? "Room code  " + net.joinCode : "Hosting on " + net.joinCode) : "Linked to the host", host && online ? "Read this out to your buddies" : (net.rtt >= 0f ? "Link " + Mathf.RoundToInt(net.rtt) + " ms" : ""), null, null);
                foreach (var p in net.peers.Values) Item(list.transform, p.name + (p.id == net.MyId ? " (you)" : "") + (p.id == 0 ? " · host" : ""), ModeName(net.lobbyMode) + (p.kills + p.deaths > 0 ? " · " + p.kills + " kills" : ""), null, null);
            }
            else if (!online)
            {
                if (net.nearby.Count == 0) Item(list.transform, "Looking for a host on this Wi-Fi…", "Ask them to tap Host a game", null, null);
                foreach (var h in net.nearby) { string addr = h.Key; Item(list.transform, h.Value, addr, "Join", () => net.JoinNearby(addr)); }
            }
            startRow.SetActive(true);
            bStart.gameObject.SetActive(linked && host && !inGame); bStart.GetComponentInChildren<Text>().text = net.started ? "Start again" : "Start flying";
            bMode.gameObject.SetActive(linked && host && !inGame); bMode.GetComponentInChildren<Text>().text = "Mode: " + ModeName(net.lobbyMode);
            bLeave.GetComponentInChildren<Text>().text = linked ? (inGame ? "Back to the battle" : "Leave") : "Back";
            bLeave.onClick.RemoveAllListeners(); bLeave.onClick.AddListener(() => { if (linked && !inGame) { net.Leave(); Render(); } else Close(); });
        }
        static string ModeName(string id) { ModeDefs.Mode m; return ModeDefs.All.TryGetValue(id, out m) ? m.name : id; }

        // ---- widgets ----
        Transform Row(Transform parent, int cols)
        {
            var go = new GameObject("Row", typeof(RectTransform)); go.transform.SetParent(parent, false);
            var g = go.AddComponent<GridLayoutGroup>(); g.cellSize = new Vector2(cols == 3 ? 160f : 250f, 42f); g.spacing = new Vector2(8f, 8f); g.constraint = GridLayoutGroup.Constraint.FixedColumnCount; g.constraintCount = cols;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return go.transform;
        }
        GameObject Field(Transform parent, string label, string value, int maxLen, bool caps, out InputField field)
        {
            var go = new GameObject("Field " + label, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var vl = go.AddComponent<VerticalLayoutGroup>(); vl.spacing = 4f; vl.childForceExpandHeight = false; vl.childForceExpandWidth = true; vl.childControlHeight = true; vl.childControlWidth = true;
            go.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            Txt("L", go.transform, 13, label, false, Sub);
            var box = new GameObject("Input", typeof(RectTransform)); box.transform.SetParent(go.transform, false);
            box.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f); box.AddComponent<LayoutElement>().preferredHeight = 42f;
            var ol = box.AddComponent<Outline>(); ol.effectColor = new Color(1f, 1f, 1f, 0.25f); ol.effectDistance = new Vector2(1f, 1f);
            var tgo = new GameObject("Text", typeof(RectTransform)); tgo.transform.SetParent(box.transform, false);
            var tr = tgo.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(12f, 4f); tr.offsetMax = new Vector2(-12f, -4f);
            var t = tgo.AddComponent<Text>(); t.font = font; t.fontSize = 18; t.color = Ink; t.alignment = TextAnchor.MiddleLeft; t.supportRichText = false;
            var pgo = new GameObject("Placeholder", typeof(RectTransform)); pgo.transform.SetParent(box.transform, false);
            var pr = pgo.GetComponent<RectTransform>(); pr.anchorMin = Vector2.zero; pr.anchorMax = Vector2.one; pr.offsetMin = new Vector2(12f, 4f); pr.offsetMax = new Vector2(-12f, -4f);
            var p = pgo.AddComponent<Text>(); p.font = font; p.fontSize = 18; p.color = new Color(1f, 1f, 1f, 0.35f); p.alignment = TextAnchor.MiddleLeft; p.text = caps ? "5 letters or numbers" : "";
            var f = box.AddComponent<InputField>(); f.textComponent = t; f.placeholder = p; f.characterLimit = maxLen; f.text = value;
            f.contentType = InputField.ContentType.Alphanumeric; f.caretColor = Ink; f.selectionColor = new Color(0.9f, 0.75f, 0.2f, 0.4f);
            if (caps) f.onValueChanged.AddListener(v => { string u = v.ToUpperInvariant(); if (u != v) f.text = u; });
            field = f;
            return go;
        }
        void Item(Transform parent, string name, string sub, string button, Action a)
        {
            var go = new GameObject("Item", typeof(RectTransform)); go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f); go.AddComponent<LayoutElement>().preferredHeight = 48f;
            var n = new GameObject("N", typeof(RectTransform)); n.transform.SetParent(go.transform, false); var nr = n.GetComponent<RectTransform>(); nr.anchorMin = new Vector2(0f, 0.5f); nr.anchorMax = new Vector2(1f, 1f); nr.offsetMin = new Vector2(12f, 0f); nr.offsetMax = new Vector2(-130f, -4f);
            var nt = n.AddComponent<Text>(); nt.font = font; nt.fontSize = 16; nt.fontStyle = FontStyle.Bold; nt.color = Ink; nt.alignment = TextAnchor.MiddleLeft; nt.text = name;
            var s = new GameObject("S", typeof(RectTransform)); s.transform.SetParent(go.transform, false); var sr = s.GetComponent<RectTransform>(); sr.anchorMin = new Vector2(0f, 0f); sr.anchorMax = new Vector2(1f, 0.5f); sr.offsetMin = new Vector2(12f, 4f); sr.offsetMax = new Vector2(-130f, 0f);
            var st = s.AddComponent<Text>(); st.font = font; st.fontSize = 12; st.color = Sub; st.alignment = TextAnchor.MiddleLeft; st.text = sub;
            if (button != null)
            {
                var b = Btn(go.transform, button, GoC, Dark, a); var br = b.GetComponent<RectTransform>(); br.anchorMin = new Vector2(1f, 0.5f); br.anchorMax = new Vector2(1f, 0.5f); br.pivot = new Vector2(1f, 0.5f); br.anchoredPosition = new Vector2(-8f, 0f); br.sizeDelta = new Vector2(110f, 34f);
            }
        }
        Text Txt(string name, Transform parent, int fs, string text, bool bold, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = TextAnchor.MiddleLeft; t.color = c; t.text = text; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            go.AddComponent<LayoutElement>().minHeight = fs + 8f;
            return t;
        }
        Button Btn(Transform parent, string label, Color bg, Color ink, Action onClick)
        {
            var go = new GameObject(label, typeof(RectTransform)); go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = bg;
            var b = go.AddComponent<Button>(); if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); onClick(); });
            var tg = new GameObject("Label", typeof(RectTransform)); tg.transform.SetParent(go.transform, false);
            var tr = tg.GetComponent<RectTransform>(); tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            var t = tg.AddComponent<Text>(); t.font = font; t.fontSize = 15; t.alignment = TextAnchor.MiddleCenter; t.color = ink; t.text = label; t.raycastTarget = false; t.fontStyle = FontStyle.Bold; t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Truncate;
            return b;
        }
    }
}
