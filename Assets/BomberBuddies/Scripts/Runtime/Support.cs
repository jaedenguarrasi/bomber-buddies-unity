using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>
    /// Support calls (killstreaks): kills in a row earn them, dying resets the streak. Tab / d-pad up / the Support button opens the
    /// tablet; pick a call. UAV paints every enemy on the radar, the Predator missile is steered from its own camera, the airstrike
    /// walks a stick of bombs along a line, the gunship is an AI AC-130 that circles the fight for a while.
    /// </summary>
    public class Support : MonoBehaviour
    {
        public static Support Instance { get; private set; }
        public class Call { public string id, name, desc; public int kills; }
        public static readonly Call[] Calls =
        {
            new Call { id = "uav", name = "UAV", desc = "Every enemy on the island on your radar for 40 s.", kills = 3 },
            new Call { id = "predator", name = "Predator missile", desc = "Steer it in from its own camera. Look to steer, Fire to boost.", kills = 5 },
            new Call { id = "strike", name = "Airstrike", desc = "A bomber lays a stick of bombs across whatever is in front of you.", kills = 7 },
            new Call { id = "gunship", name = "AC-130 gunship", desc = "A gunship circles the fight for 45 s and works everything it sees.", kills = 10 },
        };

        public int streak;
        public bool UavActive { get { return uavT > 0f; } }
        float uavT; int lastDeaths;
        readonly HashSet<string> earned = new HashSet<string>();
        MaterialLibrary lib; Font font;
        GameObject tablet; Transform tabletList; Text tabletTitle;
        bool busy;

        public static Support Create(Transform parent, MaterialLibrary lib, Transform hudCanvas)
        {
            var go = new GameObject("Support"); go.transform.SetParent(parent, false);
            var s = go.AddComponent<Support>(); s.lib = lib; Instance = s;
            s.BuildTablet(hudCanvas);
            GameState.Killed += s.OnKill;
            return s;
        }

        void OnDestroy() { GameState.Killed -= OnKill; if (Instance == this) Instance = null; }

        void OnKill(bool headshot)
        {
            streak++;
            foreach (var c in Calls) if (streak == c.kills && !earned.Contains(c.id)) { earned.Add(c.id); if (GameState.Hud != null) GameState.Hud.Announce(c.name + " ready — Tab / ▲", 3f); }
        }

        void Update()
        {
            if (GameState.Deaths != lastDeaths) { lastDeaths = GameState.Deaths; streak = 0; }
            if (uavT > 0f) uavT -= Time.deltaTime;
            if (InputBridge.Instance != null && InputBridge.Instance.SupportPressed && !busy) ToggleTablet();
#if UNITY_EDITOR
            if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.digit0Key.wasPressedThisFrame) foreach (var c in Calls) earned.Add(c.id);   // editor: 0 earns every call
#endif
        }

        // ---------- the tablet ----------
        void BuildTablet(Transform hudCanvas)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tablet = new GameObject("Tablet", typeof(RectTransform)); tablet.transform.SetParent(hudCanvas, false);
            var rt = tablet.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(560f, 420f);
            var bg = tablet.AddComponent<Image>(); bg.color = new Color(0.06f, 0.07f, 0.09f, 0.97f); bg.raycastTarget = true;
            var frame = new GameObject("Screen", typeof(RectTransform)); frame.transform.SetParent(tablet.transform, false);
            var fr = frame.GetComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = new Vector2(14f, 14f); fr.offsetMax = new Vector2(-14f, -14f);
            frame.AddComponent<Image>().color = new Color(0.1f, 0.16f, 0.14f, 1f);
            tabletTitle = Txt("Title", frame.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0f, -22f), new Vector2(0f, 36f), 22, TextAnchor.MiddleCenter, new Color(0.6f, 1f, 0.7f), true);
            var list = new GameObject("List", typeof(RectTransform)); list.transform.SetParent(frame.transform, false);
            var lr = list.GetComponent<RectTransform>(); lr.anchorMin = Vector2.zero; lr.anchorMax = Vector2.one; lr.offsetMin = new Vector2(16f, 56f); lr.offsetMax = new Vector2(-16f, -48f);
            var vl = list.AddComponent<VerticalLayoutGroup>(); vl.spacing = 8f; vl.childControlHeight = true; vl.childControlWidth = true; vl.childForceExpandHeight = false;
            tabletList = list.transform;
            Btn("Close", frame.transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 26f), new Vector2(200f, 40f), "Close", 16, () => ToggleTablet()).GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
            tablet.SetActive(false);
        }

        public void ToggleTablet()
        {
            if (tablet == null) return;
            bool open = !tablet.activeSelf;
            tablet.SetActive(open);
            if (open) RenderTablet();
            Time.timeScale = open ? 0.15f : 1f;   // the world slows while you choose
            if (!open && PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) Time.timeScale = 0f;
            if (open) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        }

        void RenderTablet()
        {
            tabletTitle.text = "SUPPORT  ·  streak " + streak;
            foreach (Transform c in tabletList) Destroy(c.gameObject);
            foreach (var c in Calls)
            {
                bool have = earned.Contains(c.id);
                var row = new GameObject("Row " + c.id, typeof(RectTransform)); row.transform.SetParent(tabletList, false);
                var le = row.AddComponent<LayoutElement>(); le.preferredHeight = 66f;
                var im = row.AddComponent<Image>(); im.color = have ? new Color(0.16f, 0.3f, 0.22f, 1f) : new Color(0.12f, 0.14f, 0.16f, 1f);
                var n = Txt("Name", row.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 18, TextAnchor.UpperLeft, have ? Color.white : new Color(0.6f, 0.65f, 0.7f), true);
                n.rectTransform.offsetMin = new Vector2(12f, -30f); n.rectTransform.offsetMax = new Vector2(-150f, -6f);
                n.text = c.name + (have ? "" : "   (" + c.kills + " kills)");
                var d = Txt("Desc", row.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 12, TextAnchor.UpperLeft, new Color(0.75f, 0.85f, 0.8f));
                d.rectTransform.offsetMin = new Vector2(12f, 4f); d.rectTransform.offsetMax = new Vector2(-150f, -30f); d.horizontalOverflow = HorizontalWrapMode.Wrap; d.text = c.desc;
                string id = c.id;
                var b = Btn("Use", row.transform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(120f, 44f), have ? "Call in" : "Locked", 15, () => Use(id));
                b.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f); b.interactable = have;
            }
        }

        void Use(string id)
        {
            if (!earned.Contains(id) || busy) return;
            earned.Remove(id);
            Sfx.Play("ui_click", 0.6f); Sfx.Play("sting", 0.5f);
            ToggleTablet();
            switch (id)
            {
                case "uav": uavT = 40f; if (GameState.Hud != null) GameState.Hud.Announce("UAV online", 2.5f); break;
                case "predator": StartCoroutine(Predator()); break;
                case "strike": StartCoroutine(Airstrike()); break;
                case "gunship": StartCoroutine(Gunship()); break;
            }
        }

        Vector3 PlayerPos() { var pm = PlayerManager.Instance; if (pm != null && pm.plane != null && pm.plane.Active) return pm.plane.transform.position; return GameState.Player != null ? GameState.Player.transform.position : Vector3.zero; }
        Vector3 PlayerFwd() { var pm = PlayerManager.Instance; if (pm != null && pm.plane != null && pm.plane.Active) return pm.plane.Forward; var cam = Camera.main; return cam != null ? cam.transform.forward : Vector3.forward; }
        float GroundAt(Vector3 p) { return GameState.GroundHeight != null ? GameState.GroundHeight(p.x, p.z) : 0f; }

        // ---------- Predator: a missile you fly from its nose camera ----------
        IEnumerator Predator()
        {
            busy = true;
            var mainCam = Camera.main;
            var at = PlayerPos(); var fwd = PlayerFwd(); fwd.y = 0f; fwd.Normalize();
            var start = at + fwd * 40f + Vector3.up * 150f;
            var go = new GameObject("Predator missile"); go.transform.position = start;
            var mb = new MeshBuilder(); mb.Frustum(0f, 0f, 0f, 2.4f, 0.5f, 0.5f, 1f, 1f, 0f, MeshBuilder.Rgb(200, 200, 205)); mb.DBox(0f, 0f, -0.9f, 0.05f, 1.2f, 0.4f, MeshBuilder.Rgb(80, 80, 85)); mb.DBox(0f, 0f, -0.9f, 1.2f, 0.05f, 0.4f, MeshBuilder.Rgb(80, 80, 85));
            var body = mb.Spawn("Body", go.transform, lib != null ? lib.Get("lowpoly") : null, false); body.transform.localScale = Vector3.one / GalacticMode.U;   // the builder scales by U; the missile is drawn in metres
            var cam = go.AddComponent<Camera>(); cam.fieldOfView = 62f; cam.nearClipPlane = 0.3f; cam.farClipPlane = 5000f; cam.depth = 20f;
            var dir = (fwd * 0.8f + Vector3.down * 0.6f).normalized;
            float yawD = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, pitchD = -Mathf.Asin(dir.y) * Mathf.Rad2Deg;
            float speed = 95f; float t = 0f;
            if (mainCam != null) mainCam.enabled = false;
            InputBridge.Hijacked = true;
            if (GameState.Hud != null) GameState.Hud.Announce("PREDATOR  ·  look to steer, fire to boost", 3f);
            var ib = InputBridge.Instance;
            while (t < 14f)
            {
                float dt = Time.deltaTime; t += dt;
                if (ib != null)
                {
                    yawD += ib.RawLook.x * 0.6f; pitchD = Mathf.Clamp(pitchD - ib.RawLook.y * 0.6f, -10f, 89f);
                    if (ib.RawFire) speed = Mathf.Min(260f, speed + 140f * dt);
                }
                var rot = Quaternion.Euler(pitchD, yawD, 0f);
                go.transform.rotation = rot;
                var next = go.transform.position + rot * Vector3.forward * speed * dt;
                RaycastHit hit;
                if (Physics.Linecast(go.transform.position, next, out hit, ~((1 << 8) | 0), QueryTriggerInteraction.Ignore) || next.y <= GroundAt(next) + 0.5f) { go.transform.position = Physics.Linecast(go.transform.position, next, out hit) ? hit.point : next; break; }
                go.transform.position = next;
                yield return null;
            }
            var impact = go.transform.position;
            if (Projectiles.Instance != null) Projectiles.Instance.Explode(impact, 14f, 42f, 900f * GalacticMode.K, go.gameObject, 0);
            InputBridge.Hijacked = false;
            if (mainCam != null) mainCam.enabled = true;
            Destroy(go);
            busy = false;
        }

        // ---------- Airstrike: a bomber lays a stick along the line you are facing ----------
        IEnumerator Airstrike()
        {
            busy = true;
            var at = PlayerPos(); var fwd = PlayerFwd(); fwd.y = 0f; if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward; fwd.Normalize();
            var target = at + fwd * 220f; target.y = GroundAt(target);
            if (GameState.Hud != null) GameState.Hud.Announce("Airstrike inbound", 2.5f);
            // the bomber itself, for show: a friendly fighter crossing the target at height
            var from = target - fwd * 500f + Vector3.up * 150f;
            var bomber = EnemyPlane.Build(transform, lib, from, Mathf.Atan2(fwd.x, fwd.z), 0, false);
            yield return new WaitForSeconds(2.2f);
            for (int i = 0; i < 8; i++)
            {
                var p = target + fwd * (-70f + i * 20f) + Vector3.up * 140f;
                if (Projectiles.Instance != null) Projectiles.Instance.DropBomb(p, fwd * 60f + Vector3.down * 40f, 8f, 22f, 300f * GalacticMode.K, gameObject);
                yield return new WaitForSeconds(0.22f);
            }
            yield return new WaitForSeconds(12f);
            if (bomber != null) Destroy(bomber.gameObject);
            busy = false;
        }

        // ---------- Gunship: a friendly AC-130 that circles the fight ----------
        IEnumerator Gunship()
        {
            var at = PlayerPos();
            var start = at + new Vector3(-300f, 160f, 0f);
            var g = EnemyPlane.Build(transform, lib, start, Mathf.PI * 0.5f, 0, false);
            g.transform.localScale = Vector3.one * 1.8f;
            g.health.max = 2500f; g.health.current = 2500f;
            if (GameState.Hud != null) GameState.Hud.Announce("Gunship on station", 2.5f);
            yield return new WaitForSeconds(45f);
            if (g != null) Destroy(g.gameObject);
        }

        // ---- widgets ----
        Text Txt(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, int fs, TextAnchor align, Color c, bool bold = false)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = align; t.color = c; t.raycastTarget = false; t.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
        Button Btn(string name, Transform parent, Vector2 aMin, Vector2 aMax, Vector2 pos, Vector2 size, string label, int fs, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = aMin; rt.anchorMax = aMax; rt.anchoredPosition = pos; rt.sizeDelta = size;
            go.AddComponent<Image>().color = new Color(0.2f, 0.35f, 0.28f, 1f);
            var b = go.AddComponent<Button>(); b.onClick.AddListener(onClick);
            var t = Txt("Label", go.transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fs, TextAnchor.MiddleCenter, Color.white, true); t.text = label;
            return b;
        }
    }
}
