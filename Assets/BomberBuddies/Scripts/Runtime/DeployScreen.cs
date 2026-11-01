using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The deploy screen (the original's oDeploy): before each spawn, pick what you come back as from what you own,
    /// then Deploy. The battle carries on behind it. Hangar opens the shop from here too.</summary>
    public class DeployScreen : MonoBehaviour
    {
        public static DeployScreen Instance { get; private set; }
        Font font; Action onDeploy; Transform list; Text title; GameObject root;

        public static DeployScreen Create(Transform hudCanvas)
        {
            var go = new GameObject("Deploy", typeof(RectTransform)); go.transform.SetParent(hudCanvas, false);
            var d = go.AddComponent<DeployScreen>(); Instance = d; d.Build(); return d;
        }

        public bool IsOpen { get { return root != null && root.activeSelf; } }

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = gameObject;
            var rt = GetComponent<RectTransform>(); rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
            var bg = gameObject.AddComponent<Image>(); bg.color = new Color(0.03f, 0.05f, 0.09f, 0.7f); bg.raycastTarget = true;
            title = Txt("Title", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -60f), new Vector2(900f, 50f), 36, TextAnchor.MiddleCenter, Color.white, true);
            var panel = new GameObject("Panel", typeof(RectTransform)); panel.transform.SetParent(transform, false);
            var pr = panel.GetComponent<RectTransform>(); pr.anchorMin = new Vector2(0.5f, 0.5f); pr.anchorMax = new Vector2(0.5f, 0.5f); pr.sizeDelta = new Vector2(1000f, 420f); pr.anchoredPosition = new Vector2(0f, 10f);
            var grid = panel.AddComponent<GridLayoutGroup>(); grid.cellSize = new Vector2(236f, 92f); grid.spacing = new Vector2(10f, 10f); grid.childAlignment = TextAnchor.UpperCenter;
            list = panel.transform;
            Btn("Deploy", transform, new Vector2(0.5f, 0f), new Vector2(0f, 60f), new Vector2(320f, 64f), "DEPLOY  (Space)", 26, () => Deploy()).GetComponent<Image>().color = new Color(0.2f, 0.55f, 0.3f, 1f);
            Btn("Hangar", transform, new Vector2(0.5f, 0f), new Vector2(260f, 60f), new Vector2(160f, 64f), "Hangar", 20, () => { root.SetActive(false); Hangar.Open(transform.parent.parent, () => { root.SetActive(true); Render(); }); });
            root.SetActive(false);
        }

        public void Open(Action onDeploy)
        {
            this.onDeploy = onDeploy;
            root.SetActive(true);
            Render();
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        void Update()
        {
            if (!IsOpen) return;
            var kb = UnityEngine.InputSystem.Keyboard.current; var gp = UnityEngine.InputSystem.Gamepad.current;
            if ((kb != null && kb.spaceKey.wasPressedThisFrame) || (gp != null && gp.startButton.wasPressedThisFrame)) Deploy();
        }

        void Deploy()
        {
            root.SetActive(false);
            var cb = onDeploy; onDeploy = null;
            if (cb != null) cb();
        }

        void Render()
        {
            title.text = "DEPLOY AS  ·  " + (Progress.Def(Progress.Craft) != null ? Progress.Def(Progress.Craft).name : Progress.Craft);
            foreach (Transform c in list) Destroy(c.gameObject);
            foreach (var c in Progress.Crafts)
            {
                if (!c.available || !Progress.OwnsCraft(c.id)) continue;
                string id = c.id; bool chosen = c.id == Progress.Craft;
                var b = Btn("Tile " + c.id, list, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(236f, 92f), "", 18, () => { Progress.PickCraft(id); Render(); });
                b.GetComponent<Image>().color = chosen ? new Color(0.3f, 0.45f, 0.65f, 1f) : new Color(0.14f, 0.18f, 0.26f, 0.95f);
                var n = b.transform.Find("Label").GetComponent<Text>(); n.text = c.name; n.alignment = TextAnchor.UpperLeft; n.rectTransform.offsetMin = new Vector2(10f, 30f); n.rectTransform.offsetMax = new Vector2(-10f, -8f);
                var d = Txt("Desc", b.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, 12, TextAnchor.UpperLeft, new Color(0.8f, 0.85f, 0.92f)); d.rectTransform.offsetMin = new Vector2(10f, 6f); d.rectTransform.offsetMax = new Vector2(-10f, -34f); d.horizontalOverflow = HorizontalWrapMode.Wrap; d.verticalOverflow = VerticalWrapMode.Truncate; d.text = c.desc;
            }
        }

        Text Txt(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fs, TextAnchor align, Color c, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = align; t.color = c; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        Button Btn(string name, Transform parent, Vector2 anchor, Vector2 pos, Vector2 size, string label, int fs, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchor; rt.anchorMax = anchor; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.16f, 0.2f, 0.3f, 1f);
            var b = go.AddComponent<Button>(); b.onClick.AddListener(onClick);
            var t = Txt("Label", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fs, TextAnchor.MiddleCenter, Color.white, true); t.text = label;
            return b;
        }
    }
}
