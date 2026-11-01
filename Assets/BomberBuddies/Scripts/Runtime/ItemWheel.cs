using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The original's item wheel (ITEMS): eight slots round a ring, for when you are on foot. Cigarette, med kit, energy
    /// drink, airstrike flare, cymbal monkey. I / d-pad down / the 🎒 button opens it, 1–5 quick-pick, tap a slot to use it.</summary>
    public class ItemWheel : MonoBehaviour
    {
        public static ItemWheel Instance { get; private set; }
        public class Item { public string id, name, icon, note; public float dur, cd; }
        public static readonly Item[] Items =
        {
            new Item { id = "smoke", name = "Cigarette", icon = "🚬", note = "20 s: move 30% faster, music 30% faster", dur = 20f, cd = 30f },
            new Item { id = "med", name = "Med kit", icon = "🩹", note = "Heals 60% of your health", cd = 45f },
            new Item { id = "drink", name = "Energy drink", icon = "🥤", note = "20 s: jump much higher, land softly", dur = 20f, cd = 40f },
            new Item { id = "flare", name = "Airstrike", icon = "🧨", note = "Five bombs on the spot you are aiming at", cd = 90f },
            new Item { id = "monkey", name = "Cymbal monkey", icon = "🐒", note = "Wind it up and throw it: everything nearby goes for it, then it blows", cd = 25f },
            new Item { id = "", name = "Empty" }, new Item { id = "", name = "Empty" }, new Item { id = "", name = "Empty" },
        };
        readonly Dictionary<string, float> cdUntil = new Dictionary<string, float>();
        public float smokeT, drinkT;
        Font font; GameObject root; Text mid; readonly List<Button> slots = new List<Button>();
        MaterialLibrary lib;

        public static ItemWheel Create(Transform parent, MaterialLibrary lib, Transform hudCanvas)
        {
            var go = new GameObject("Item wheel"); go.transform.SetParent(parent, false);
            var w = go.AddComponent<ItemWheel>(); Instance = w; w.lib = lib; w.Build(hudCanvas); return w;
        }

        public bool IsOpen { get { return root != null && root.activeSelf; } }

        void Build(Transform hudCanvas)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            root = new GameObject("Wheel", typeof(RectTransform)); root.transform.SetParent(hudCanvas, false);
            var rt = root.GetComponent<RectTransform>(); rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f); rt.sizeDelta = new Vector2(520f, 520f);
            var bg = root.AddComponent<Image>(); bg.color = new Color(0f, 0f, 0f, 0.001f); bg.raycastTarget = true;
            for (int i = 0; i < Items.Length; i++)
            {
                var it = Items[i]; float a = i / (float)Items.Length * Mathf.PI * 2f - Mathf.PI / 2f;
                var pos = new Vector2(Mathf.Cos(a), -Mathf.Sin(a)) * 190f;
                var go = new GameObject("Slot " + i, typeof(RectTransform)); go.transform.SetParent(root.transform, false);
                var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = new Vector2(118f, 118f);
                var im = go.AddComponent<Image>(); im.color = it.id != "" ? new Color(0.1f, 0.14f, 0.2f, 0.92f) : new Color(0.08f, 0.09f, 0.11f, 0.6f);
                var b = go.AddComponent<Button>(); b.interactable = it.id != ""; string id = it.id; b.onClick.AddListener(() => Use(id));
                Txt("Icon", go.transform, new Vector2(0f, 18f), new Vector2(118f, 60f), 40, it.icon ?? "·", Color.white);
                Txt("Name", go.transform, new Vector2(0f, -30f), new Vector2(118f, 24f), 13, it.name, new Color(0.85f, 0.9f, 1f));
                slots.Add(b);
            }
            mid = Txt("Mid", root.transform, Vector2.zero, new Vector2(200f, 80f), 18, "Items", Color.white);
            root.SetActive(false);
        }

        Text Txt(string name, Transform parent, Vector2 pos, Vector2 size, int fs, string text, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>(); r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f); r.anchoredPosition = pos; r.sizeDelta = size;
            var t = go.AddComponent<Text>(); t.font = font; t.fontSize = fs; t.alignment = TextAnchor.MiddleCenter; t.color = c; t.text = text; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        FootController Foot { get { var pm = PlayerManager.Instance; return pm != null && pm.OnFoot ? GameState.Player : null; } }

        public void Toggle()
        {
            if (!IsOpen && Foot == null) { if (GameState.Hud != null) GameState.Hud.Comms("Items are for when you are on foot"); return; }
            bool open = !IsOpen;
            root.SetActive(open);
            if (open)
            {
                mid.text = "Items\n<size=12>Tap one to use it · 1-5 quick pick</size>";
                for (int i = 0; i < Items.Length; i++) { var it = Items[i]; if (it.id == "") continue; float left = Mathf.Max(0f, (cdUntil.ContainsKey(it.id) ? cdUntil[it.id] : 0f) - Time.time); slots[i].transform.Find("Name").GetComponent<Text>().text = left > 0f ? it.name + " · " + Mathf.CeilToInt(left) + " s" : it.name; }
                Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            }
            Time.timeScale = open ? 0.15f : 1f;
            if (!open && PauseMenu.Instance != null && PauseMenu.Instance.IsOpen) Time.timeScale = 0f;
        }

        void Update()
        {
            var ib = InputBridge.Instance; if (ib == null) return;
            if (ib.ItemsPressed) Toggle();
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && Foot != null) { if (kb.digit1Key.wasPressedThisFrame) Use("smoke"); else if (kb.digit2Key.wasPressedThisFrame) Use("med"); else if (kb.digit3Key.wasPressedThisFrame) Use("drink"); else if (kb.digit4Key.wasPressedThisFrame) Use("flare"); else if (kb.digit5Key.wasPressedThisFrame) Use("monkey"); }
            float dt = Time.deltaTime;
            if (smokeT > 0f) { smokeT -= dt; if (Foot == null) smokeT = 0f; }
            if (drinkT > 0f) { drinkT -= dt; if (Foot == null) drinkT = 0f; }
            Sfx.MusicRate = smokeT > 0f ? 1.3f : 1f;
        }

        public void Use(string id)
        {
            var foot = Foot; if (foot == null || !foot.health.alive) return;
            Item it = null; foreach (var q in Items) if (q.id == id) it = q; if (it == null) return;
            float until; if (cdUntil.TryGetValue(id, out until) && until > Time.time) { if (GameState.Hud != null) GameState.Hud.Comms(it.name + " in " + Mathf.CeilToInt(until - Time.time) + " s"); return; }
            cdUntil[id] = Time.time + it.cd;
            if (IsOpen) Toggle();
            var hud = GameState.Hud;
            switch (id)
            {
                case "smoke": smokeT = it.dur; if (hud != null) hud.Comms("Lit up"); Sfx.Tone(180f, 0.08f, 0.05f, "triangle"); break;
                case "med": foot.health.current = Mathf.Min(foot.health.max, foot.health.current + foot.health.max * 0.6f); if (hud != null) hud.Comms("Patched up"); Sfx.Tone(660f, 0.08f, 0.06f, "sine"); break;
                case "drink": drinkT = it.dur; if (hud != null) hud.Comms("Buzzing"); Sfx.Tone(880f, 0.06f, 0.05f, "square"); break;
                case "monkey": StartCoroutine(Monkey(foot)); if (hud != null) hud.Comms("Monkey away"); Sfx.Tone(1400f, 0.06f, 0.06f, "square"); break;
                case "flare": StartCoroutine(Flare(foot)); if (hud != null) hud.Comms("Airstrike coming in"); break;
            }
        }

        /// <summary>Red smoke on the spot, then the bombs walk across it.</summary>
        IEnumerator Flare(FootController foot)
        {
            var rig = foot.rig; Vector3 at;
            RaycastHit hit;
            if (Physics.Raycast(rig.AimOrigin, rig.AimDirection, out hit, 400f, ~(1 << 8), QueryTriggerInteraction.Ignore)) at = hit.point; else { at = rig.AimOrigin + rig.AimDirection * 120f; at.y = GameState.GroundHeight != null ? GameState.GroundHeight(at.x, at.z) : at.y; }
            for (int i = 0; i < 12; i++) if (Gunfire.Instance != null) Gunfire.Instance.Impact(at + new Vector3(Random.Range(-1f, 1f), 0.3f, Random.Range(-1f, 1f)), Vector3.up, false);
            yield return new WaitForSeconds(2.5f);
            for (int n = 0; n < 5; n++)
            {
                var p = at + new Vector3(Random.Range(-9f, 9f), 60f, Random.Range(-9f, 9f));
                if (Projectiles.Instance != null) Projectiles.Instance.DropBomb(p, Vector3.down * 30f, 4f, 10f, 160f * GalacticMode.K, gameObject);
                yield return new WaitForSeconds(0.35f);
            }
        }

        /// <summary>Wound up and lobbed: it clatters where it lands, everything near goes for it, then it blows.</summary>
        IEnumerator Monkey(FootController foot)
        {
            var rig = foot.rig;
            var go = new GameObject("Cymbal monkey"); go.layer = 8;
            var mb = new MeshBuilder(); mb.DBox(0f, 0f, 0f, 3f, 4f, 2.5f, MeshBuilder.Rgb(150, 90, 60)); mb.DBox(0f, 3.5f, 0f, 2.5f, 2.5f, 2.5f, MeshBuilder.Rgb(190, 130, 90)); mb.DBox(-2f, 1f, 0f, 1f, 1.5f, 2f, MeshBuilder.Rgb(220, 200, 80)); mb.DBox(2f, 1f, 0f, 1f, 1.5f, 2f, MeshBuilder.Rgb(220, 200, 80));
            mb.Spawn("Body", go.transform, lib != null ? lib.Get("lowpoly") : null, false);
            var hp = go.AddComponent<Health>(); hp.team = 0; hp.max = 60f; hp.current = 60f; hp.aimHeight = 0.3f;
            go.AddComponent<SphereCollider>().radius = 0.35f;
            var pos = rig.AimOrigin + rig.AimDirection * 0.6f; var vel = rig.AimDirection * 14f + Vector3.up * 6f;
            float t = 0f; bool landed = false;
            while (t < 6f && hp.alive)
            {
                float dt = Time.deltaTime; t += dt;
                if (!landed)
                {
                    vel.y -= 20f * dt; pos += vel * dt;
                    float g = GameState.GroundHeight != null ? GameState.GroundHeight(pos.x, pos.z) : 0f;
                    if (pos.y <= g + 0.1f) { pos.y = g + 0.1f; landed = true; Sfx.Play("thud", 0.4f, 1f, pos); }
                }
                else if (Mathf.Repeat(t, 0.5f) < dt) Sfx.Play("ui_click", 0.35f, 1.6f, pos);   // the cymbals
                go.transform.position = pos; go.transform.Rotate(0f, 360f * dt, 0f);
                yield return null;
            }
            if (Projectiles.Instance != null) Projectiles.Instance.Explode(pos, 4f, 12f, 160f * GalacticMode.K, go, 0);
            Destroy(go);
        }
    }
}
