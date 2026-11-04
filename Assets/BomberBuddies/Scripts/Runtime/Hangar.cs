using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>The hangar (the original's shop): bank, upgrades, guns, craft and team on four tabs. Opened from the title or the pause menu.</summary>
    public class Hangar : MonoBehaviour
    {
        public static Hangar Instance { get; private set; }
        Font font;
        Action onClose;
        string tab = "Upgrades";
        Text bankText, title;
        RectTransform list;
        bool eraseArmed;

        public static Hangar Open(Transform parent, Action onClose)
        {
            if (Instance != null) { Instance.onClose = onClose; Instance.gameObject.SetActive(true); Instance.Render(); return Instance; }
            var go = new GameObject("Hangar");
            go.transform.SetParent(parent, false);
            var h = go.AddComponent<Hangar>();
            Instance = h; h.onClose = onClose;
            h.Build();
            h.Render();
            return h;
        }

#if UNITY_EDITOR
        void Update() { if (UnityEngine.InputSystem.Keyboard.current != null && UnityEngine.InputSystem.Keyboard.current.equalsKey.wasPressedThisFrame) { Progress.Bank += 1000; Render(); } }   // editor: = gives 1,000 credits for testing
#endif

        public static bool IsOpen { get { return Instance != null && Instance.gameObject.activeSelf; } }
        float flashT; string flashText;
        public static void Flash(string text) { if (Instance != null) { Instance.flashT = 1.6f; Instance.flashText = text; } }
        void LateUpdate() { if (flashT > 0f) { flashT -= Time.unscaledDeltaTime; if (bankText != null) bankText.text = flashT > 0f ? flashText : Cr(Progress.Bank) + " credits"; } }

        Showroom room; RawImage preview; string look, lookGun, vcat; Transform tabRow2; readonly List<Button> tabs2 = new List<Button>(); GameObject toast;
        static readonly Color Cream = new Color(239f / 255f, 233f / 255f, 214f / 255f), Gold = new Color(226f / 255f, 179f / 255f, 60f / 255f), Ink = new Color(29f / 255f, 42f / 255f, 46f / 255f), BoxC = new Color(0.08f, 0.1f, 0.13f, 0.92f), BtnC = new Color(0.12f, 0.16f, 0.2f, 1f);
        static readonly string[] Tabs2 = { "Vehicle / class", "Weapons", "Upgrades", "Teammates" };
        static readonly string[] Cats = { "Aircraft", "Ground", "Mounts", "Soldiers", "Heroes", "Creatures", "Dinosaurs" };

        void Build()
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tab = "Vehicle / class";
            var camGo = new GameObject("Hangar camera"); camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>(); cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.05f, 0.07f, 0.12f); cam.depth = 50f; cam.cullingMask = 0;
            var canvas = gameObject.AddComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30;
            var scaler = gameObject.AddComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1280f, 720f); scaler.matchWidthOrHeight = 1f;
            gameObject.AddComponent<GraphicRaycaster>();
            // the painting behind, as on the title
            var bg = Img("Background", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, Color.white); bg.raycastTarget = true;
            var bgTex = Resources.Load<Texture2D>("Art/title_bg"); if (bgTex != null) bg.sprite = Sprite.Create(bgTex, new UnityEngine.Rect(0, 0, bgTex.width, bgTex.height), new Vector2(0.5f, 0.5f)); else bg.color = new Color(0.16f, 0.22f, 0.27f, 1f);
            Img("Dim", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, new Color(0.05f, 0.08f, 0.11f, 0.45f));
            var sa = Rect("Safe", transform, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var safe = sa.GetComponent<RectTransform>(); safe.offsetMin = new Vector2(52f, 10f); safe.offsetMax = new Vector2(-52f, -8f);

            // the header bar: chevron, Hangar, credits
            var head = Rect("Head", safe, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero).GetComponent<RectTransform>(); head.offsetMin = new Vector2(0f, -78f); head.offsetMax = new Vector2(0f, -20f);
            head.gameObject.AddComponent<Image>().color = new Color(0.1f, 0.14f, 0.18f, 0.9f);
            Btn("Chevron", head, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(24f, 0f), new Vector2(44f, 44f), "‹", 28, () => Close());
            title = Txt("Title", head, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(500f, 50f), 38, TextAnchor.MiddleCenter, Cream, true); title.text = "Hangar";
            bankText = Txt("Bank", head, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-210f, 0f), new Vector2(420f, 44f), 24, TextAnchor.MiddleRight, Gold, true);
            Btn("Arrow", transform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(30f, -30f), new Vector2(44f, 44f), "←", 24, () => Close());

            // 2D game | 3D game, then Vehicle / class | Weapons | Upgrades | Teammates
            var b2d = Btn("Tab 2D", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(52f, -112f), new Vector2(104f, 40f), "2D game", 18, () => Toast("The 2D game is not in this version yet."));
            var b3d = Btn("Tab 3D", safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(164f, -112f), new Vector2(104f, 40f), "3D game", 18, () => { }); b3d.GetComponent<Image>().color = Cream; b3d.transform.Find("Label").GetComponent<Text>().color = Ink;
            float x = 0f;
            foreach (var t in Tabs2)
            {
                string tt = t; float w = 40f + t.Length * 9f;
                var b = Btn("Tab " + t, safe, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(x + w * 0.5f, -158f), new Vector2(w, 36f), t, 16, () => { tab = tt; list.anchoredPosition = Vector2.zero; Render(); list.anchoredPosition = Vector2.zero; });
                tabs2.Add(b); x += w + 8f;
            }

            // the scrolling list
            var view = Rect("View", safe, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            var vr = view.GetComponent<RectTransform>(); vr.offsetMin = new Vector2(0f, 10f); vr.offsetMax = new Vector2(0f, -182f);
            view.gameObject.AddComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            view.gameObject.AddComponent<RectMask2D>();
            var scroll = view.gameObject.AddComponent<ScrollRect>();
            var content = Rect("Content", view.transform, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            list = content.GetComponent<RectTransform>(); list.pivot = new Vector2(0.5f, 1f);
            var vl = content.gameObject.AddComponent<VerticalLayoutGroup>(); vl.spacing = 10f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true; vl.padding = new RectOffset(0, 12, 0, 0);
            var fit = content.gameObject.AddComponent<ContentSizeFitter>(); fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = list; scroll.viewport = vr; scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 30f;
        }

        public void Close()
        {
            gameObject.SetActive(false);
            var cb = onClose; onClose = null;
            if (cb != null) cb();
        }

        void Toast(string text)
        {
            if (toast != null) Destroy(toast);
            var t = Txt("Toast", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(900f, 30f), 18, TextAnchor.MiddleCenter, Cream); t.text = text; toast = t.gameObject; Destroy(toast, 3f);
        }

        static string Cr(int n) { return n.ToString("N0"); }

        void Render()
        {
            var keepScroll = list != null ? list.anchoredPosition : Vector2.zero;
            bankText.text = Cr(Progress.Bank) + " credits";
            foreach (var b in tabs2) { bool on = b.name == "Tab " + tab; b.GetComponent<Image>().color = on ? Cream : BtnC; b.transform.Find("Label").GetComponent<Text>().color = on ? Ink : Cream; }
            foreach (Transform c in list) Destroy(c.gameObject);
            if (room != null) { Destroy(room.gameObject); room = null; }
            string craft = Progress.Craft; var cd = Progress.Def(craft);
            string vname = cd != null ? cd.name : craft; bool two = cd != null && cd.seats == 2;
            switch (tab)
            {
                case "Vehicle / class":
                    {
                        if (look == null || Progress.Def(look) == null) look = craft;
                        var ld = Progress.Def(look);
                        ShowroomBox(look, false);
                        // crew: who climbs out of a vehicle
                        if (cd != null && !cd.foot)
                        {
                            var opts = new List<Progress.CraftDef>(); foreach (var c in Progress.Crafts) if (c.foot && Progress.OwnsCraft(c.id) && c.available) opts.Add(c);
                            var cur = Progress.Crew; int ci = opts.FindIndex(c => c.id == cur); if (ci < 0) ci = 0;
                            var box = Box(); var r = OptRow(box, "Crew", "Who climbs out when you leave your vehicle or bail out.");
                            Choice(r, opts.Count > 0 ? opts[ci].name + "  ▾" : "Soldier: rifleman", true, () => { if (opts.Count == 0) return; Progress.Crew = opts[(ci + 1) % opts.Count].id; PlayerPrefs.Save(); Render(); });
                        }
                        // the categories
                        if (vcat == null) { vcat = ld != null ? ld.cat : "Aircraft"; }
                        var cr = Box(); var crow = OptRow(cr, null, null);
                        foreach (var cat in Cats) { string cc = cat; Choice(crow, cat, cat == vcat, () => { vcat = cc; Render(); }); }
                        // the tiles in it
                        var grp = Box(); SecHead(grp, vcat);
                        foreach (var c in Progress.Crafts)
                        {
                            if (c.cat != vcat) continue;
                            bool own = Progress.OwnsCraft(c.id), on = c.id == craft; string cid = c.id;
                            string label = !own ? "🪙 " + Cr(c.price) : on ? (c.ground ? "Playing" : "Flying") : (c.ground ? "Play this" : "Fly this");
                            if (!c.available) label = "Coming";
                            var row = Tile(grp, (own ? "" : "🔒 ") + c.name, c.desc, label, c.available && !on, () => { if (!c.available) { Toast("Not in this version yet."); return; } if (Progress.PickCraft(cid)) { look = cid; Render(); } else Toast("Need " + Cr(c.price - Progress.Bank) + " more"); }, c.id == look);
                            row.GetComponent<Button>().onClick.AddListener(() => { look = cid; Render(); });
                        }
                    }
                    break;
                case "Weapons":
                    {
                        if (lookGun == null || !Progress.GunFits(craft, Progress.GunDef(lookGun))) lookGun = Progress.GunOf(craft);
                        ShowroomBox(lookGun, true);
                        GunList(1, two ? "Pilot's guns: " + vname : "Gun: " + vname, "Guns are bought for one vehicle at a time. What you buy here stays with the " + vname.ToLower() + ".");
                        if (craft == "jetpack")
                        {
                            var box = Box(); SecHead(box, "Launcher: " + vname); Note(box, "What the Shell button fires. Both are free; pick the one that suits the job.");
                            foreach (var o in new[] { new[] { "nade", "Grenades", "Lobbed in an arc to where you are aiming. Good against groups." }, new[] { "rpg", "Rocket launcher", "Flies straight and hits hard. Good against armor and aircraft, slower to reload." } })
                            { bool on = Progress.Sec == o[0]; string id = o[0]; Tile(box, o[1], o[2], on ? "Fitted" : "Fit", !on, () => { Progress.Sec = id; PlayerPrefs.Save(); Render(); }, false); }
                        }
                        if (two) GunList(2, "Gunner's weapon: " + vname, "What the second seat fires, whether that is you or the AI gunner.");
                    }
                    break;
                case "Upgrades":
                    {
                        var box = Box(); SecHead(box, "Upgrades: " + vname); Note(box, "Upgrades also belong to this vehicle only.");
                        string group = null;
                        foreach (var it in Progress.Catalog)
                        {
                            if (it.group != group) { group = it.group; SubHead(box, group); }
                            int lvl = Progress.Level(craft, it.id);
                            bool more = Progress.CanBuyMore(it, lvl);
                            int cost = more ? Progress.CostOf(it, lvl) : 0;
                            string lv = "Level " + lvl + (it.endless ? "" : " / " + it.max) + (lvl > it.max ? "  (past the top)" : "");
                            string bid = it.id;
                            Tile(box, it.name, it.desc + "  ·  " + lv, more ? "Buy for " + Cr(cost) : "Maxed", more && cost <= Progress.Bank, () => { if (Progress.Buy(craft, bid)) Render(); }, false);
                        }
                        Footer();
                    }
                    break;
                case "Teammates":
                    {
                        var box = Box(); SecHead(box, "Teammates");
                        Note(box, "No limit. Up to " + (Settings.Detail <= 0 ? 10 : Settings.Detail == 1 ? 14 : 20) + " fight at once at your Graphics setting (" + Progress.MatesTotal + " hired); any more wait in reserve and come in as others are lost.");
                        foreach (var m in Progress.Mates)
                        {
                            int have = Progress.MatesOf(m.id); int cost = Progress.MateCost(m.id); string mid = m.id;
                            var row = Tile(box, m.name + "   × " + have, m.desc, "Hire for " + Cr(cost), cost <= Progress.Bank, () => { if (Progress.Hire(mid)) Render(); }, false);
                            if (have > 0) { var go = Btn("Unmate", row, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-116f, 20f), new Vector2(200f, 32f), "Let one go (+" + Cr(Mathf.RoundToInt(Progress.MateCostAt(mid, have - 1) / 2f)) + ")", 13, () => { if (Progress.Fire(mid)) Render(); }); go.GetComponent<RectTransform>().pivot = new Vector2(1f, 0f); }
                        }
                    }
                    break;
            }
            Canvas.ForceUpdateCanvases();
            list.anchoredPosition = keepScroll; StartCoroutine(RestoreScroll(keepScroll));
        }
        System.Collections.IEnumerator RestoreScroll(Vector2 p) { yield return null; if (list != null) list.anchoredPosition = p; }

        /// <summary>The showroom (showroomHTML): the preview with its arrows, the name and blurb left, the stat bars and the action right.</summary>
        void ShowroomBox(string key, bool gun)
        {
            var box = Rect("Showroom", list, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var le = box.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 500f;
            box.gameObject.AddComponent<Image>().color = BoxC;
            var top = Rect("Top", box, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero).GetComponent<RectTransform>(); top.offsetMin = new Vector2(0f, -336f); top.offsetMax = Vector2.zero;
            var pv = Rect("Preview", top, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero).GetComponent<RectTransform>(); pv.offsetMin = new Vector2(40f, 0f); pv.offsetMax = new Vector2(-40f, 0f);
            preview = pv.gameObject.AddComponent<RawImage>(); preview.color = Color.white;
            room = Showroom.Create(transform.parent, preview); pv.gameObject.AddComponent<ShowroomDrag>().room = room;
            room.Show(gun ? "gun:" + key : key);
            Btn("Prev", top, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(20f, 0f), new Vector2(40f, 60f), "‹", 30, () => Step(-1, gun)).GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            Btn("Next", top, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-20f, 0f), new Vector2(40f, 60f), "›", 30, () => Step(1, gun)).GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.001f);
            string name, desc; var rows = new List<string[]>(); var bars = new List<float>();
            Transform act = null;
            var cols = Rect("Cols", box, Vector2.zero, new Vector2(1f, 1f), Vector2.zero, Vector2.zero).GetComponent<RectTransform>(); cols.offsetMin = new Vector2(12f, 8f); cols.offsetMax = new Vector2(-12f, -344f);
            if (!gun)
            {
                var c = Progress.Def(key); var st = Progress.StatsFor(key); name = c.name; desc = c.desc;
                rows.Add(new[] { "Toughness", Mathf.RoundToInt(st.maxHp).ToString() }); bars.Add(st.maxHp / 400f);
                rows.Add(new[] { "Speed", c.spd.ToString("0") }); bars.Add(c.spd / 210f);
                if (c.foot) { rows.Add(new[] { "Jump", c.jump.ToString("0") }); bars.Add(c.jump / 190f); } else { rows.Add(new[] { "Bombs", st.maxBombs.ToString() }); bars.Add(st.maxBombs / 16f); }
                float reach = c.reach > 0f ? c.reach : st.gun.range; rows.Add(new[] { "Reach", reach.ToString("0") }); bars.Add(reach / 1200f);
                rows.Add(new[] { "Weapon", Progress.GunDef(Progress.DefaultGun(key)).name }); bars.Add(0f);
                rows.Add(new[] { "Role", !c.ground ? "Aircraft" : c.foot ? "On foot" : "Ground vehicle" }); bars.Add(0f);
                if (Progress.OwnsCraft(key) && c.foot) { string gk = Progress.Craft == key ? Progress.GunOf(key) : Progress.DefaultGun(key); var fitted = new List<string>(); foreach (var a in Progress.Atts) if (Progress.AttOn(gk, a.id)) fitted.Add(a.name); rows.Add(new[] { "Loadout", Progress.GunDef(gk).name + (fitted.Count > 0 ? " + " + string.Join(", ", fitted.ToArray()) : "") }); bars.Add(0f); }
            }
            else
            {
                var g = Progress.GunDef(key); name = g.name; desc = g.desc;
                rows.Add(new[] { "Damage", g.dmg.ToString("0.#") }); bars.Add(g.dmg / 190f);
                rows.Add(new[] { "Rate", g.melee || g.interval <= 0f ? "—" : Mathf.RoundToInt(60f / g.interval) + " rpm" }); bars.Add(g.interval > 0f ? Mathf.Min(1f, 0.1f / g.interval) : 0f);
                rows.Add(new[] { "Range", g.range.ToString("0") }); bars.Add(g.range / 1900f);
                rows.Add(new[] { "Full dmg to", g.fall0.ToString("0") }); bars.Add(g.fall0 / 1700f);
                rows.Add(new[] { "Armour bite", Mathf.RoundToInt(g.pen * 100f) + "%" }); bars.Add(g.pen);
                rows.Add(new[] { "Heat per shot", g.heat.ToString("0.###") }); bars.Add(g.heat / 0.22f);
            }
            var nm = Txt("Name", cols, new Vector2(0f, 1f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, 24, TextAnchor.UpperLeft, Cream, true); nm.rectTransform.offsetMin = new Vector2(0f, -36f); nm.rectTransform.offsetMax = new Vector2(-10f, 0f); nm.text = name;
            var ds = Txt("Desc", cols, new Vector2(0f, 0f), new Vector2(0.5f, 1f), Vector2.zero, Vector2.zero, 14, TextAnchor.UpperLeft, new Color(0.8f, 0.84f, 0.88f)); ds.rectTransform.offsetMin = new Vector2(0f, 0f); ds.rectTransform.offsetMax = new Vector2(-10f, -38f); ds.horizontalOverflow = HorizontalWrapMode.Wrap; ds.text = desc;
            for (int i = 0; i < rows.Count; i++)
            {
                float y = -4f - i * 20f;
                Txt("L", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(60f, y - 9f), new Vector2(120f, 18f), 14, TextAnchor.MiddleLeft, new Color(0.8f, 0.84f, 0.88f)).text = rows[i][0];
                var track = Rect("Bar", cols, new Vector2(0.5f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero).GetComponent<RectTransform>(); track.offsetMin = new Vector2(120f, y - 12f); track.offsetMax = new Vector2(-100f, y - 6f); track.gameObject.AddComponent<Image>().color = new Color(0.25f, 0.28f, 0.33f);
                if (bars[i] > 0f) { var fillR = Rect("Fill", track, Vector2.zero, new Vector2(Mathf.Clamp01(bars[i]), 1f), Vector2.zero, Vector2.zero).GetComponent<RectTransform>(); fillR.offsetMin = Vector2.zero; fillR.offsetMax = Vector2.zero; fillR.gameObject.AddComponent<Image>().color = Gold; }
                var v = Txt("V", cols, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-48f, y - 9f), new Vector2(96f, 18f), 14, TextAnchor.MiddleRight, Cream); v.text = rows[i][1]; v.horizontalOverflow = HorizontalWrapMode.Overflow;
            }
            // the action under the bars
            float ay = -4f - rows.Count * 20f - 8f;
            if (!gun)
            {
                var c = Progress.Def(key); string k = key;
                if (!c.available) Txt("Act", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, ay - 12f), new Vector2(240f, 22f), 14, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.82f)).text = "Not in this version yet";
                else if (!Progress.OwnsCraft(key))
                {
                    var b = Btn("Unlock", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(190f, ay - 18f), new Vector2(260f, 36f), "Unlock: " + Cr(c.price) + " credits", 16, () => { if (Progress.Unlock(k)) Render(); }); b.GetComponent<Image>().color = Gold; b.transform.Find("Label").GetComponent<Text>().color = Ink;
                    Txt("Note", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(190f, ay - 46f), new Vector2(260f, 18f), 13, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.82f)).text = Progress.Bank >= c.price ? "You have " + Cr(Progress.Bank) : "Need " + Cr(c.price - Progress.Bank) + " more";
                }
                else if (Progress.Craft != key) { var b = Btn("Fly", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(130f, ay - 18f), new Vector2(140f, 36f), c.ground ? "Play this" : "Fly this", 16, () => { if (Progress.PickCraft(k)) Render(); }); b.GetComponent<Image>().color = Gold; b.transform.Find("Label").GetComponent<Text>().color = Ink; }
                else Txt("Act", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, ay - 12f), new Vector2(240f, 22f), 14, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.82f)).text = "Your pick";
            }
            else
            {
                string craft = Progress.Craft; string k = key; bool own = Progress.OwnsGun(craft, key); int cost = Progress.GunPrice(craft, key);
                if (!own) { var b = Btn("BuyGun", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(190f, ay - 18f), new Vector2(260f, 36f), "Buy: " + Cr(cost) + " credits", 16, () => { if (Progress.BuyGun(craft, k)) Render(); }); b.interactable = cost <= Progress.Bank; b.GetComponent<Image>().color = Gold; b.transform.Find("Label").GetComponent<Text>().color = Ink; }
                else if (Progress.GunOf(craft) != key) { var b = Btn("FitGun", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(130f, ay - 18f), new Vector2(140f, 36f), "Fit this", 16, () => { Progress.BuyGun(craft, k); Render(); }); b.GetComponent<Image>().color = Gold; b.transform.Find("Label").GetComponent<Text>().color = Ink; }
                else Txt("Act", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, ay - 12f), new Vector2(240f, 22f), 14, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.82f)).text = "Fitted";
                if (own && Progress.AttFits(key))
                {
                    Txt("AttH", cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(120f, ay - 52f), new Vector2(240f, 18f), 13, TextAnchor.MiddleLeft, new Color(0.75f, 0.78f, 0.82f)).text = "Attachments";
                    float ax = 60f;
                    foreach (var a in Progress.Atts)
                    {
                        bool on = Progress.AttOn(key, a.id), had = Progress.AttOwned(key, a.id); string aid = a.id;
                        var b = Btn("Att " + a.id, cols, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(ax + 62f, ay - 82f), new Vector2(120f, 40f), a.name + "\n<size=11>" + (on ? "Fitted" : had ? "Fit" : Cr(a.price)) + "</size>", 13, () => { if (Progress.ToggleAtt(k, aid)) Render(); else Toast("Not enough credits"); });
                        b.transform.Find("Label").GetComponent<Text>().supportRichText = true; b.GetComponent<Image>().color = on ? Gold : BtnC; b.transform.Find("Label").GetComponent<Text>().color = on ? Ink : Cream; b.interactable = had || a.price <= Progress.Bank;
                        ax += 126f;
                    }
                    le.preferredHeight = 560f;
                }
            }
        }

        void Step(int dir, bool gun)
        {
            if (gun)
            {
                var o = new List<string>(); foreach (var g in Progress.AllGuns) if (Progress.GunFits(Progress.Craft, g)) o.Add(g.id);
                int i = Mathf.Max(0, o.IndexOf(lookGun)); lookGun = o[(i + dir + o.Count) % o.Count];
            }
            else
            {
                var o = new List<string>(); foreach (var c in Progress.Crafts) if (!c.ground) o.Add(c.id); foreach (var c in Progress.Crafts) if (c.ground && !c.foot) o.Add(c.id); foreach (var c in Progress.Crafts) if (c.ground && c.foot) o.Add(c.id);
                int i = Mathf.Max(0, o.IndexOf(look)); look = o[(i + dir + o.Count) % o.Count]; vcat = Progress.Def(look).cat;
            }
            Sfx.Tone(520f, 0.04f, 0.04f, "triangle"); Render();
        }

        void GunList(int slot, string head, string note)
        {
            string craft = Progress.Craft; var box = Box(); SecHead(box, head); Note(box, note);
            foreach (var g in Progress.AllGuns)
            {
                if (!Progress.GunFits(craft, g)) continue;
                bool own = slot == 2 ? Progress.OwnsGun2(craft, g.id) : Progress.OwnsGun(craft, g.id); bool on = (slot == 2 ? Progress.Gun2Of(craft) : Progress.GunOf(craft)) == g.id; int cost = Progress.GunPrice(craft, g.id); string gid = g.id;
                var row = Tile(box, g.name, g.desc, on ? "Fitted" : own ? "Fit" : "Buy for " + Cr(cost), !on && (own || cost <= Progress.Bank), () => { if (slot == 2 ? Progress.BuyGun2(craft, gid) : Progress.BuyGun(craft, gid)) { lookGun = gid; Render(); } }, slot == 1 && lookGun == g.id);
                if (slot == 1) row.GetComponent<Button>().onClick.AddListener(() => { lookGun = gid; Render(); });
            }
        }

        void Footer()
        {
            var r = Rect("Erase", list, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 60f));
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 60f;
            Btn("EraseBtn", r.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(320f, 44f), eraseArmed ? "Really erase everything?" : "Erase all progress", 16, () =>
            {
                if (!eraseArmed) { eraseArmed = true; Render(); return; }
                eraseArmed = false; Progress.EraseAll(); Render();
            }).GetComponent<Image>().color = new Color(0.45f, 0.15f, 0.15f, 1f);
        }

        // ---- the original's boxes: a group with a heading, notes, tiles with a button ----
        Transform Box()
        {
            var b = Rect("Box", list, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            b.gameObject.AddComponent<Image>().color = BoxC;
            var vl = b.gameObject.AddComponent<VerticalLayoutGroup>(); vl.padding = new RectOffset(12, 12, 10, 12); vl.spacing = 6f; vl.childForceExpandHeight = false; vl.childControlHeight = true; vl.childControlWidth = true;
            b.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return b;
        }
        void SecHead(Transform box, string text) { var h = Txt("Sec", box, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 32f), 22, TextAnchor.MiddleLeft, Gold, true); h.text = text; h.gameObject.AddComponent<LayoutElement>().preferredHeight = 32f; }
        void SubHead(Transform box, string text) { var h = Txt("Sub", box, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 24f), 16, TextAnchor.MiddleLeft, new Color(0.75f, 0.85f, 1f), true); h.text = text.ToUpper(); h.gameObject.AddComponent<LayoutElement>().preferredHeight = 24f; }
        void Note(Transform box, string text) { var n = Txt("Note", box, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 20f), 13, TextAnchor.UpperLeft, new Color(0.75f, 0.78f, 0.82f)); n.text = text; n.horizontalOverflow = HorizontalWrapMode.Wrap; n.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize; }
        Transform OptRow(Transform box, string label, string note)
        {
            if (label != null) { var l = Txt("Label", box, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(0f, 22f), 16, TextAnchor.MiddleLeft, Cream, true); l.text = label; l.gameObject.AddComponent<LayoutElement>().preferredHeight = 22f; }
            if (note != null) Note(box, note);
            var seg = Rect("Seg", box, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var hl = seg.gameObject.AddComponent<HorizontalLayoutGroup>(); hl.spacing = 6f; hl.childForceExpandWidth = false; hl.childForceExpandHeight = false; hl.childControlWidth = true; hl.childControlHeight = true; hl.childAlignment = TextAnchor.MiddleLeft;
            seg.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            return seg;
        }
        void Choice(Transform seg, string label, bool on, Action a)
        {
            var b = Btn("Choice", seg, Vector2.zero, Vector2.zero, Vector2.zero, new Vector2(100f, 36f), label, 15, () => a());
            b.GetComponent<Image>().color = on ? Cream : BtnC; b.transform.Find("Label").GetComponent<Text>().color = on ? Ink : Cream;
            var le = b.gameObject.AddComponent<LayoutElement>(); le.preferredWidth = 22f + label.Length * 8.5f; le.preferredHeight = 36f; le.flexibleWidth = 0f;
        }
        Transform Tile(Transform box, string name, string desc, string buttonLabel, bool enabled, Action onClick, bool looking)
        {
            var r = Rect("Tile " + name, box, Vector2.zero, Vector2.one, Vector2.zero, new Vector2(0f, 78f));
            var le = r.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 78f;
            var im = r.gameObject.AddComponent<Image>(); im.color = looking ? new Color(0.2f, 0.26f, 0.32f, 1f) : new Color(0.12f, 0.15f, 0.19f, 1f);
            r.gameObject.AddComponent<Button>();
            var n = Txt("Name", r, new Vector2(0f, 1f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 18, TextAnchor.UpperLeft, Cream, true); n.text = name;
            n.rectTransform.offsetMin = new Vector2(12f, -30f); n.rectTransform.offsetMax = new Vector2(-230f, -6f);
            var d = Txt("Desc", r, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero, 12, TextAnchor.UpperLeft, new Color(0.8f, 0.84f, 0.9f)); d.text = desc;
            d.rectTransform.offsetMin = new Vector2(12f, 4f); d.rectTransform.offsetMax = new Vector2(-230f, -30f); d.horizontalOverflow = HorizontalWrapMode.Wrap; d.verticalOverflow = VerticalWrapMode.Truncate;
            var b = Btn("Buy", r, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(200f, 40f), buttonLabel, 15, () => { if (onClick != null) onClick(); });
            b.GetComponent<RectTransform>().pivot = new Vector2(1f, 0.5f);
            b.interactable = enabled;
            bool on = buttonLabel == "Fitted" || buttonLabel == "Flying" || buttonLabel == "Playing";
            b.GetComponent<Image>().color = on ? Gold : enabled ? BtnC : new Color(0.15f, 0.17f, 0.2f, 1f); b.transform.Find("Label").GetComponent<Text>().color = on ? Ink : Cream;
            return r;
        }

        // ---- widgets ----
        Transform Rect(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = new Vector2(0.5f, 0.5f); rt.anchoredPosition = pos; rt.sizeDelta = size;
            return go.transform;
        }
        Image Img(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, Color c)
        {
            var t = Rect(name, parent, anchorMin, anchorMax, pos, size); var im = t.gameObject.AddComponent<Image>(); im.color = c; im.raycastTarget = false; return im;
        }
        Text Txt(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, int fontSize, TextAnchor align, Color c, bool bold = false)
        {
            var t = Rect(name, parent, anchorMin, anchorMax, pos, size); var tx = t.gameObject.AddComponent<Text>();
            tx.font = font; tx.fontSize = fontSize; tx.alignment = align; tx.color = c; tx.raycastTarget = false; tx.fontStyle = bold ? FontStyle.Bold : FontStyle.Normal;
            tx.horizontalOverflow = HorizontalWrapMode.Overflow; tx.verticalOverflow = VerticalWrapMode.Overflow;
            return tx;
        }
        Button Btn(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size, string label, int fontSize, Action onClick)
        {
            var t = Rect(name, parent, anchorMin, anchorMax, pos, size);
            var im = t.gameObject.AddComponent<Image>(); im.color = new Color(0.16f, 0.2f, 0.3f, 1f);
            var b = t.gameObject.AddComponent<Button>(); if (onClick != null) b.onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); onClick(); });
            var cols = b.colors; cols.disabledColor = new Color(0.6f, 0.6f, 0.6f, 0.6f); b.colors = cols;
            var tx = Txt("Label", t, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero, fontSize, TextAnchor.MiddleCenter, Color.white, true); tx.text = label;
            return b;
        }
    }
}
