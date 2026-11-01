using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace BomberBuddies
{
    /// <summary>The Commander view (cmdEnter / CMD in the original): run the battle from above. Tap your units to select them,
    /// tap the ground to send them, tap an enemy to set them on it, and spend credits on squads, jeeps, tanks, helicopters
    /// and air strikes. You are untouchable while you command; "Back to the fight" drops you back in.</summary>
    public class Commander : MonoBehaviour
    {
        public static Commander Instance { get; private set; }
        public static bool On;
        static readonly Dictionary<string, int> Cost = new Dictionary<string, int> { { "squad", 300 }, { "car", 500 }, { "tank", 900 }, { "heli", 1000 }, { "strike", 400 } };
        Camera cam, mainCam; Vector3 look; float yaw, zoom = 1f; readonly List<Health> sel = new List<Health>();
        GameObject bar; Text info; Font font; string buying; MaterialLibrary lib; Transform world;
        readonly List<GameObject> rings = new List<GameObject>();

        public static Commander Create(Transform parent, Transform hudCanvas, MaterialLibrary lib)
        {
            var go = new GameObject("Commander"); go.transform.SetParent(parent, false);
            var c = go.AddComponent<Commander>(); Instance = c; c.lib = lib; c.world = parent; c.BuildBar(hudCanvas); On = false;
            return c;
        }

        void BuildBar(Transform hudCanvas)
        {
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bar = new GameObject("Command bar", typeof(RectTransform)); bar.transform.SetParent(hudCanvas, false);
            var rt = bar.GetComponent<RectTransform>(); rt.anchorMin = new Vector2(0.5f, 0f); rt.anchorMax = new Vector2(0.5f, 0f); rt.anchoredPosition = new Vector2(0f, 46f); rt.sizeDelta = new Vector2(1100f, 70f);
            bar.AddComponent<Image>().color = new Color(0.03f, 0.05f, 0.09f, 0.75f);
            float x = -480f;
            foreach (var id in new[] { "squad", "car", "tank", "heli", "strike" })
            {
                string cid = id; Btn(id + "  " + Cost[id], new Vector2(x, 0f), 150f, () => { buying = buying == cid ? null : cid; Refresh(); }); x += 160f;
            }
            Btn("Back to the fight", new Vector2(440f, 0f), 200f, () => Leave());
            info = new GameObject("Info", typeof(RectTransform)).AddComponent<Text>(); info.transform.SetParent(bar.transform, false);
            var ir = info.rectTransform; ir.anchorMin = new Vector2(0.5f, 1f); ir.anchorMax = new Vector2(0.5f, 1f); ir.anchoredPosition = new Vector2(0f, 22f); ir.sizeDelta = new Vector2(1100f, 30f);
            info.font = font; info.fontSize = 18; info.alignment = TextAnchor.MiddleCenter; info.color = new Color(0.9f, 0.93f, 1f); info.raycastTarget = false;
            bar.SetActive(false);
        }
        void Btn(string label, Vector2 pos, float w, UnityEngine.Events.UnityAction a)
        {
            var go = new GameObject(label, typeof(RectTransform)); go.transform.SetParent(bar.transform, false);
            var rt = go.GetComponent<RectTransform>(); rt.anchoredPosition = pos; rt.sizeDelta = new Vector2(w, 50f);
            go.AddComponent<Image>().color = new Color(0.16f, 0.2f, 0.3f, 1f);
            go.AddComponent<Button>().onClick.AddListener(() => { Sfx.Play("ui_click", 0.5f); a(); });
            var t = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>(); t.transform.SetParent(go.transform, false);
            var tr = t.rectTransform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = tr.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = 18; t.alignment = TextAnchor.MiddleCenter; t.color = Color.white; t.text = label; t.raycastTarget = false;
        }
        void Refresh()
        {
            if (info == null) return;
            info.text = "● " + Progress.Fmt(Progress.Bank) + " credits   ·   " + sel.Count + " selected   ·   " + (buying != null ? "tap the ground to place the " + buying : "tap a unit to select, the ground to send it, an enemy to attack it");
        }

        public void Enter()
        {
            if (On) return;
            On = true; buying = null; sel.Clear();
            var pm = PlayerManager.Instance; mainCam = pm != null ? pm.cam : Camera.main;
            var at = pm != null && GameState.Player != null ? (pm.OnFoot ? GameState.Player.transform.position : pm.plane != null && pm.plane.Active ? pm.plane.transform.position : GameState.Player.transform.position) : Vector3.zero;
            look = at; yaw = mainCam != null ? mainCam.transform.eulerAngles.y : 0f; zoom = 1f;
            var go = new GameObject("Commander camera"); go.transform.SetParent(transform, false);
            cam = go.AddComponent<Camera>(); cam.fieldOfView = 55f; cam.nearClipPlane = 1f; cam.farClipPlane = 6000f; cam.depth = 25f;
            if (mainCam != null) { mainCam.enabled = false; cam.clearFlags = mainCam.clearFlags; cam.backgroundColor = mainCam.backgroundColor; cam.cullingMask = mainCam.cullingMask; }
            InputBridge.Hijacked = true;
            foreach (var h in Health.All) if (h != null && h.team == 0 && (h.GetComponent<FootController>() != null || h.GetComponent<PlaneController>() != null || h.GetComponent<GroundVehicle>() != null)) h.invulnerable = true;
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            bar.SetActive(true); Refresh();
            if (GameState.Hud != null) { GameState.Hud.Announce("Commander view", 2f); GameState.Hud.ShowTouch(false); }
            Sfx.Play("ui_click", 0.5f, 0.8f);
        }

        public void Leave()
        {
            if (!On) return;
            On = false; buying = null; sel.Clear(); ClearRings();
            if (cam != null) Destroy(cam.gameObject); cam = null;
            if (mainCam != null) mainCam.enabled = true;
            InputBridge.Hijacked = false;
            foreach (var h in Health.All) if (h != null && h.team == 0) h.invulnerable = false;
            bar.SetActive(false);
            if (GameState.Hud != null && InputBridge.Instance != null && InputBridge.Instance.IsTouch) GameState.Hud.ShowTouch(true);
        }

        void ClearRings() { foreach (var r in rings) if (r != null) Destroy(r); rings.Clear(); }

        void Update()
        {
            if (!On || cam == null) return;
            float dt = Time.unscaledDeltaTime;
            var ib = InputBridge.Instance;
            // pan with the stick / WASD, zoom with the throttle, turn with the right stick
            if (ib != null)
            {
                var mv = ib.RawMove; var rot = Quaternion.Euler(0f, yaw, 0f);
                look += rot * new Vector3(mv.x, 0f, mv.y) * 120f * zoom * dt;
                zoom = Mathf.Clamp(zoom - ib.Throttle * dt * 1.2f, 0.35f, 3f);
                yaw += ib.RawLook.x * 0.3f;
            }
            if (Mouse.current != null) { var sc = Mouse.current.scroll.ReadValue().y; if (Mathf.Abs(sc) > 0.1f) zoom = Mathf.Clamp(zoom - Mathf.Sign(sc) * 0.12f, 0.35f, 3f); if (Mouse.current.rightButton.isPressed) { var d = Mouse.current.delta.ReadValue(); yaw += d.x * 0.2f; } }
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) { Leave(); return; }
            float h = 520f * GalacticMode.U * zoom * 1.6f, pit = 60f;
            float gy = GameState.GroundHeight != null ? GameState.GroundHeight(look.x, look.z) : 0f;
            var back = Quaternion.Euler(0f, yaw, 0f) * Vector3.back * h / Mathf.Tan(pit * Mathf.Deg2Rad);
            cam.transform.position = new Vector3(look.x, gy + h, look.z) + back;
            cam.transform.rotation = Quaternion.Euler(pit, yaw, 0f);
            // taps
            bool tap = (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame) || (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame);
            if (!tap || (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())) return;
            Vector2 sp = Mouse.current != null && Touchscreen.current == null ? Mouse.current.position.ReadValue() : (Touchscreen.current != null ? Touchscreen.current.primaryTouch.position.ReadValue() : Mouse.current.position.ReadValue());
            var ray = cam.ScreenPointToRay(sp);
            Vector3 hitP; Health hitH = Pick(ray, out hitP);
            if (buying != null) { Buy(buying, hitP); buying = null; Refresh(); return; }
            if (hitH != null && hitH.team == 0) { if (Keyboard.current == null || !Keyboard.current.shiftKey.isPressed) sel.Clear(); if (!sel.Contains(hitH)) sel.Add(hitH); Mark(); Refresh(); return; }
            if (sel.Count == 0) return;
            // send them, or set them on it
            foreach (var u in sel)
            {
                if (u == null || !u.alive) continue;
                var es = u.GetComponent<EnemySoldier>(); if (es != null) { es.objective = hitP; es.Alert(hitP); }
                var ev = u.GetComponent<EnemyVehicle>(); if (ev != null) ev.convoyGoal = hitP;
                var ep = u.GetComponent<EnemyPlane>(); if (ep != null) ep.patrol = hitP;
            }
            sel.RemoveAll(u => u == null || !u.alive);
            if (GameState.Hud != null) GameState.Hud.Comms(hitH != null ? "On it, sir" : "Moving out");
            Sfx.Play("ui_click", 0.4f, 1.2f);
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(ring.GetComponent<Collider>()); ring.transform.position = hitP + Vector3.up * 0.3f; ring.transform.localScale = new Vector3(6f, 0.1f, 6f); ring.GetComponent<Renderer>().material.color = hitH != null ? new Color(1f, 0.4f, 0.3f) : new Color(0.4f, 1f, 0.6f); Destroy(ring, 1.5f);
        }

        Health Pick(Ray ray, out Vector3 point)
        {
            point = Vector3.zero; Health best = null; float bd = 1e9f;
            // the ground first
            RaycastHit hit; bool ground = Physics.Raycast(ray, out hit, 6000f, ~0, QueryTriggerInteraction.Ignore);
            point = ground ? hit.point : ray.origin + ray.direction * 1000f;
            if (ground) { var hz = hit.collider.GetComponentInParent<Health>(); if (hz != null && hz.alive) return hz; }
            // units are small from up here: the nearest one to the ray within a few metres
            foreach (var h in Health.All)
            {
                if (h == null || !h.alive) continue;
                var p = h.transform.position + Vector3.up; var to = p - ray.origin; float along = Vector3.Dot(to, ray.direction); if (along < 0f) continue;
                float d = (to - ray.direction * along).magnitude; float tol = 4f * zoom + 2f;
                if (d < tol && d < bd) { bd = d; best = h; }
            }
            if (best != null) point = best.transform.position;
            return best;
        }

        void Mark()
        {
            ClearRings();
            foreach (var u in sel)
            {
                if (u == null) continue;
                var r = GameObject.CreatePrimitive(PrimitiveType.Cylinder); Destroy(r.GetComponent<Collider>()); r.transform.SetParent(u.transform, false); r.transform.localPosition = Vector3.up * 0.2f; r.transform.localScale = new Vector3(3f, 0.08f, 3f); r.GetComponent<Renderer>().material.color = new Color(0.4f, 0.9f, 1f); rings.Add(r);
            }
        }

        void Buy(string id, Vector3 at)
        {
            int cost = Cost[id];
            if (Progress.Bank < cost) { if (GameState.Hud != null) GameState.Hud.Comms("Not enough credits"); return; }
            Progress.Bank -= cost; PlayerPrefs.Save();
            if (id == "strike") { if (Support.Instance != null) Support.Instance.StrikeAt(at); return; }
            var tm = Teammates.Instance; if (tm == null) return;
            if (id == "squad") { for (int i = 0; i < 4; i++) tm.SpawnMate("squad", at); }
            else tm.SpawnMate(id, at);
            if (GameState.Hud != null) GameState.Hud.Comms(id == "squad" ? "Squad deployed" : id == "car" ? "Jeep coming in" : id == "tank" ? "Tank rolling" : "Helicopter inbound");
            Sfx.Play("ui_click", 0.5f);
        }
    }
}
