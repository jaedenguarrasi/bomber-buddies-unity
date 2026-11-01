using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// One of the two capital ships, laid out exactly as in the original game (local units, x along the ship with the nose at +x,
    /// z across; the hangar mouth on the local −z side). The hull is a shell of slabs, the rooms are real walls with sliding
    /// doors, so you fly into the hangar, get out and walk the corridors.
    /// </summary>
    public class Carrier : MonoBehaviour
    {
        public struct Box { public float x0, x1, y0, y1, z0, z1; public Box(float a, float b, float c, float d, float e, float f) { x0 = a; x1 = b; y0 = c; y1 = d; z0 = e; z1 = f; } public bool Contains(Vector3 l, float pad = 0f) { return l.x >= x0 - pad && l.x <= x1 + pad && l.y >= y0 - pad && l.y <= y1 + pad && l.z >= z0 - pad && l.z <= z1 + pad; } }

        public static readonly Box[] Hull = {
            new Box(-340, 340, -45, 25, -85, 85), new Box(-260, 220, 25, 55, -55, 55), new Box(-190, -110, 55, 110, -30, 30), new Box(-385, -340, -40, 20, -70, 70), new Box(340, 425, -30, 10, -42, 42) };
        public static readonly Dictionary<string, Box> Rooms = new Dictionary<string, Box> {
            { "hangar", new Box(-40, 120, -30, 6, -90, 40) }, { "c1", new Box(-262, -40, -30, -6, -12, 12) }, { "engine", new Box(-335, -262, -30, 15, -55, 55) },
            { "c2", new Box(120, 252, -30, -6, -12, 12) }, { "comp", new Box(252, 332, -30, 6, -50, 50) } };
        public static readonly float[] DoorX = { -40, -262, 120, 252 };

        public int side;
        public bool alive = true, shield;
        public float dying = -1f, vy, home = 1f;
        public float FloorY { get { return -30f; } }
        public readonly List<ShipPart> parts = new List<ShipPart>();
        public readonly List<ShipPart> cores = new List<ShipPart>();
        public readonly List<ShipGun> guns = new List<ShipGun>();
        public readonly List<EnemySoldier> guards = new List<EnemySoldier>();
        public System.Action<Carrier> OnDestroyed;

        class Door { public float lx, open; public Transform a, b; }
        readonly List<Door> doors = new List<Door>();
        GameObject shieldWall;
        Material hullMat, hullDark, floorMat, ceilMat, wallMat, doorMat, bandMat, windowMat, panelMat, shieldMat, fireMat;
        MaterialLibrary lib;
        Vector3 basePos;
        float fireT;
        readonly List<Transform> flames = new List<Transform>();

        const float U = GalacticMode.U;

        public static Carrier Build(Transform parent, MaterialLibrary lib, int side, Vector3 worldPos)
        {
            var go = new GameObject(side == 0 ? "Your carrier" : "Enemy carrier");
            go.transform.SetParent(parent, false);
            go.transform.position = worldPos;
            go.transform.rotation = Quaternion.Euler(0f, side == 0 ? 0f : 180f, 0f);
            var c = go.AddComponent<Carrier>();
            c.side = side; c.lib = lib; c.basePos = worldPos;
            c.shield = side == 1;
            c.MakeMaterials();
            c.BuildHull();
            c.BuildRooms();
            c.BuildDoors();
            c.BuildShield();
            return c;
        }

        // ---- coordinates ----
        public Vector3 L2W(float lx, float ly, float lz) { return transform.TransformPoint(new Vector3(lx * U, ly * U, -lz * U)); }
        public Vector3 W2L(Vector3 w) { var l = transform.InverseTransformPoint(w); return new Vector3(l.x / U, l.y / U, -l.z / U); }
        /// <summary>Local yaw (radians, 0 = nose, π/2 = towards the hangar mouth) to a world-space heading.</summary>
        public Vector3 LocalDir(float lx, float ly, float lz) { return transform.TransformDirection(new Vector3(lx, ly, -lz)).normalized; }
        public Vector3 MouthDirection { get { return LocalDir(0f, 0f, -1f); } }

        public bool Solid { get { return alive && !(dying >= 0f && transform.position.y < basePos.y - 5f * U); } }

        /// <summary>The docking volume: inside the hangar, shrunk 4 along the ship, past the mouth lip.</summary>
        public bool HangarAt(Vector3 w)
        {
            if (!Solid) return false;
            var l = W2L(w); var h = Rooms["hangar"];
            return l.x > h.x0 + 4f && l.x < h.x1 - 4f && l.y > h.y0 - 2f && l.y < h.y1 && l.z > -84f && l.z < h.z1;
        }

        public Box HangarBox { get { return Rooms["hangar"]; } }

        /// <summary>The height you can stand at under a world point: a room floor, a hull top, or NaN when the ship is not under it.</summary>
        public float FloorAt(Vector3 w)
        {
            if (!Solid) return float.NaN;
            var l = W2L(w);
            foreach (var kv in Rooms)
            {
                var r = kv.Value;
                if (l.x >= r.x0 && l.x <= r.x1 && l.z >= r.z0 && l.z <= r.z1 && l.y >= r.y0 - 2f && l.y <= r.y1 + 1f) return L2W(0f, r.y0, 0f).y;
            }
            float best = float.NaN;
            foreach (var b in Hull)
            {
                if (l.x >= b.x0 && l.x <= b.x1 && l.z >= b.z0 && l.z <= b.z1 && l.y >= b.y1 - 6f)
                {
                    float y = L2W(0f, b.y1, 0f).y;
                    if (float.IsNaN(best) || y > best) best = y;
                }
            }
            return best;
        }

        public bool InsideRooms(Vector3 w)
        {
            var l = W2L(w);
            foreach (var kv in Rooms) if (kv.Value.Contains(l, 1f)) return true;
            return false;
        }

        public bool NearHull(Vector3 w, float pad)
        {
            var l = W2L(w);
            return l.x > -430f - pad && l.x < 430f + pad && l.y > -50f - pad && l.y < 115f + pad && Mathf.Abs(l.z) < 95f + pad;
        }

        // ---- materials ----
        Material M(string from, Color c, float smooth = -1f) { var m = lib != null ? lib.Instance(from) : new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", null); if (smooth >= 0f && m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth); if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", from == "gunmetal" ? 0.4f : 0f); return m; }

        void MakeMaterials()
        {
            Material E(Color c, Color glow) { var m = lib != null ? lib.Instance("glow") : new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c); m.SetColor("_EmissionColor", glow); return m; }
            var hull = side == 0 ? new Color(0.5f, 0.54f, 0.6f) : new Color(0.42f, 0.4f, 0.44f);
            hullMat = M("gunmetal", hull, 0.45f);
            hullDark = M("gunmetal", hull * 0.92f, 0.45f);
            floorMat = M("concrete", new Color(44f / 255f, 48f / 255f, 54f / 255f), 0.35f);
            ceilMat = M("concrete", new Color(70f / 255f, 74f / 255f, 80f / 255f), 0.2f);
            wallMat = M("concrete", new Color(96f / 255f, 100f / 255f, 108f / 255f), 0.25f);
            doorMat = M("gunmetal", new Color(118f / 255f, 122f / 255f, 132f / 255f), 0.5f);
            bandMat = M("dark", new Color(214f / 255f, 176f / 255f, 40f / 255f), 0.3f);
            windowMat = E(new Color(0.67f, 0.86f, 0.94f), new Color(0.5f, 0.8f, 1f) * 2.2f);
            panelMat = E(new Color(0.9f, 0.93f, 0.94f), new Color(0.9f, 0.93f, 0.95f) * 3f);
            shieldMat = lib != null ? lib.Instance("water") : null;
            if (shieldMat != null) shieldMat.SetColor("_BaseColor", new Color(90f / 255f, 170f / 255f, 1f, 0.42f));
            fireMat = E(new Color(1f, 0.55f, 0.2f), new Color(1f, 0.45f, 0.1f) * 5f);
        }

        GameObject Slab(string name, Box b, Material mat, bool collide = true, Transform parent = null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent != null ? parent : transform, false);
            go.transform.localPosition = new Vector3((b.x0 + b.x1) * 0.5f * U, (b.y0 + b.y1) * 0.5f * U, -(b.z0 + b.z1) * 0.5f * U);
            go.transform.localScale = new Vector3((b.x1 - b.x0) * U, (b.y1 - b.y0) * U, (b.z1 - b.z0) * U);
            var r = go.GetComponent<Renderer>(); r.sharedMaterial = mat;
            if (!collide) Destroy(go.GetComponent<Collider>());
            return go;
        }

        void BuildHull()
        {
            var root = new GameObject("Hull").transform; root.SetParent(transform, false);
            var h = Hull[0]; const float t = 2f;
            var hg = Rooms["hangar"];
            Slab("Top", new Box(h.x0, h.x1, h.y1 - t, h.y1, h.z0, h.z1), hullMat, true, root);
            Slab("Bottom", new Box(h.x0, h.x1, h.y0, h.y0 + t, h.z0, h.z1), hullDark, true, root);
            Slab("Bow face", new Box(h.x1 - t, h.x1, h.y0, h.y1, h.z0, h.z1), hullMat, true, root);
            Slab("Stern face", new Box(h.x0, h.x0 + t, h.y0, h.y1, h.z0, h.z1), hullMat, true, root);
            Slab("Side +z", new Box(h.x0, h.x1, h.y0, h.y1, h.z1 - t, h.z1), hullMat, true, root);
            // the hangar side, cut open round the mouth
            Slab("Side -z aft", new Box(h.x0, hg.x0, h.y0, h.y1, h.z0, h.z0 + t), hullMat, true, root);
            Slab("Side -z fwd", new Box(hg.x1, h.x1, h.y0, h.y1, h.z0, h.z0 + t), hullMat, true, root);
            Slab("Side -z below mouth", new Box(hg.x0, hg.x1, h.y0, hg.y0, h.z0, h.z0 + t), hullMat, true, root);
            Slab("Side -z above mouth", new Box(hg.x0, hg.x1, hg.y1, h.y1, h.z0, h.z0 + t), hullMat, true, root);
            Slab("Upper deck", Hull[1], hullMat, true, root);
            Slab("Bridge", Hull[2], hullDark, true, root);
            Slab("Stern", Hull[3], hullMat, true, root);
            Slab("Bow", Hull[4], hullMat, true, root);
            // the bridge windows and a row of portholes along both flanks
            Slab("Bridge glass", new Box(-190.5f, -189.5f, 92, 104, -24, 24), windowMat, false, root);
            Slab("Bridge glass 2", new Box(-110.5f, -109.5f, 92, 104, -24, 24), windowMat, false, root);
            for (int i = 0; i < 24; i++)
            {
                float x = -320f + 27f * i;
                Slab("Window", new Box(x, x + 12, 14, 18, h.z1 - 0.2f, h.z1 + 0.4f), windowMat, false, root);
                if (x + 12 < hg.x0 - 10f || x > hg.x1 + 10f) Slab("Window", new Box(x, x + 12, 14, 18, h.z0 - 0.4f, h.z0 + 0.2f), windowMat, false, root);
            }
            // engine glow at the stern
            var engMat = new Material(fireMat); engMat.SetColor("_BaseColor", side == 0 ? new Color(0.5f, 0.75f, 1f) : new Color(1f, 0.45f, 0.3f)); engMat.SetColor("_EmissionColor", (side == 0 ? new Color(0.45f, 0.7f, 1f) : new Color(1f, 0.35f, 0.2f)) * 4f);
            for (int i = -1; i <= 1; i++) Slab("Engine", new Box(-388, -385, -28, 8, i * 42f - 14f, i * 42f + 14f), engMat, false, root);
            // hangar bay lights above the mouth
            for (int i = 0; i < 4; i++) Slab("Bay light", new Box(hg.x0 + 15 + i * 40, hg.x0 + 25 + i * 40, 8, 10, h.z0 - 0.5f, h.z0 + 0.5f), panelMat, false, root);
        }

        void BuildRooms()
        {
            var root = new GameObject("Rooms").transform; root.SetParent(transform, false);
            const float t = 2f;
            foreach (var kv in Rooms)
            {
                var r = kv.Value; string k = kv.Key;
                Slab(k + " floor", new Box(r.x0, r.x1, r.y0 - t, r.y0, r.z0, r.z1), floorMat, true, root);
                Slab(k + " ceiling", new Box(r.x0, r.x1, r.y1, r.y1 + t, r.z0, r.z1), ceilMat, true, root);
                Slab(k + " wall +z", new Box(r.x0, r.x1, r.y0, r.y1, r.z1, r.z1 + t), wallMat, true, root);
                if (k != "hangar") Slab(k + " wall -z", new Box(r.x0, r.x1, r.y0, r.y1, r.z0 - t, r.z0), wallMat, true, root);
                // light panels along the ceiling
                for (float x = r.x0 + 13f; x < r.x1 - 6f; x += 26f) Slab("Panel", new Box(x - 5f, x + 5f, r.y1 - 0.6f, r.y1, -3f, 3f), panelMat, false, root);
                // the walls across the ship, with a doorway where a corridor joins
                bool corridor = k == "c1" || k == "c2";
                if (corridor) continue;
                EndWall(root, k, r, r.x0, k == "hangar" || k == "comp");
                EndWall(root, k, r, r.x1, k == "hangar" || k == "engine");
            }
            // the hangar deck: two yellow runway lines down to the mouth and hazard blocks along the lip, like the original
            var hg = Rooms["hangar"];
            var yellow = M("dark", new Color(0.92f, 0.78f, 0.1f), 0.3f);
            var black = M("dark", new Color(0.08f, 0.08f, 0.09f), 0.2f);
            for (int i = -1; i <= 1; i += 2) Slab("Runway line", new Box(hg.x0 + 60f + i * 22f - 2f, hg.x0 + 60f + i * 22f + 2f, hg.y0, hg.y0 + 0.4f, hg.z0 + 6f, hg.z1 - 10f), yellow, false, root);
            for (int i = 0; i < 8; i++) Slab("Hazard", new Box(hg.x0 + 4f + i * 20f, hg.x0 + 14f + i * 20f, hg.y0, hg.y0 + 2.2f, hg.z0 + 1f, hg.z0 + 5f), i % 2 == 0 ? yellow : black, false, root);
            // real lights in every room, where the original put them
            Lamp("hangar", 0, 2, -30, 80, new Color(1f, 0.96f, 0.88f)); Lamp("hangar", 70, 2, -30, 80, new Color(1f, 0.96f, 0.88f)); Lamp("hangar", 110, 2, 10, 70, new Color(1f, 0.96f, 0.88f));
            Lamp("c1", -90, -8, 0, 60, new Color(0.85f, 0.9f, 1f)); Lamp("c1", -200, -8, 0, 60, new Color(0.85f, 0.9f, 1f)); Lamp("c2", 190, -8, 0, 60, new Color(0.85f, 0.9f, 1f));
            Lamp("engine", -298, -10, 0, 70, new Color(1f, 0.7f, 0.35f)); Lamp("comp", 292, -10, 0, 60, new Color(0.55f, 1f, 0.9f));
        }

        void EndWall(Transform root, string k, Box r, float x, bool opening)
        {
            const float t = 2f;
            bool atLow = Mathf.Approximately(x, r.x0);
            float x0 = atLow ? x - t : x, x1 = atLow ? x : x + t;
            if (!opening) { Slab(k + " end wall", new Box(x0, x1, r.y0, r.y1, r.z0, r.z1), wallMat, true, root); return; }
            Slab(k + " end wall a", new Box(x0, x1, r.y0, r.y1, r.z0, -12f), wallMat, true, root);
            Slab(k + " end wall b", new Box(x0, x1, r.y0, r.y1, 12f, r.z1), wallMat, true, root);
            Slab(k + " end wall top", new Box(x0, x1, -6f, r.y1, -12f, 12f), wallMat, true, root);
            // the corridor beyond: walls are its own, but the hull void between needs closing where the corridor is lower than the room
        }

        void Lamp(string room, float lx, float ly, float lz, float range, Color c)
        {
            var go = new GameObject("Lamp " + room);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(lx * U, ly * U, -lz * U);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = c; l.range = range * U; l.intensity = 2.2f; l.shadows = LightShadows.None;
        }

        void BuildDoors()
        {
            var root = new GameObject("Doors").transform; root.SetParent(transform, false);
            foreach (float lx in DoorX)
            {
                var d = new Door { lx = lx };
                d.a = DoorHalf(root, lx, 1f); d.b = DoorHalf(root, lx, -1f);
                doors.Add(d);
                // hazard stripes on the deck either side of the door
                for (int i = 0; i < 6; i++) Slab("Stripe", new Box(lx - 6f + i * 2f, lx - 4f + i * 2f, -30f, -29.8f, -12f, 12f), i % 2 == 0 ? bandMat : hullDark, false, root);
            }
        }

        Transform DoorHalf(Transform root, float lx, float sign)
        {
            var half = new GameObject("Door half").transform; half.SetParent(root, false);
            half.localPosition = new Vector3(lx * U, 0f, 0f);
            Slab("Leaf", new Box(-1.3f, 1.3f, -30f, -6f, sign > 0 ? 0f : -12f, sign > 0 ? 12f : 0f), doorMat, true, half);
            Slab("Band", new Box(-1.4f, 1.4f, -20.5f, -19.5f, sign > 0 ? 0.5f : -11.5f, sign > 0 ? 11.5f : -0.5f), bandMat, false, half);
            Slab("Glass", new Box(-1.4f, 1.4f, -15f, -11f, sign > 0 ? 2f : -10f, sign > 0 ? 10f : -2f), windowMat, false, half);
            return half;
        }

        void BuildShield()
        {
            var hg = Rooms["hangar"];
            shieldWall = Slab("Hangar shield", new Box(hg.x0, hg.x1, hg.y0, hg.y1, -86f, -84.5f), shieldMat, true);
            shieldWall.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            shieldWall.SetActive(shield);
        }

        public bool IsShield(Collider c) { return shieldWall != null && c.gameObject == shieldWall; }

        public void DropShield()
        {
            shield = false;
            if (shieldWall != null) shieldWall.SetActive(false);
        }

        // ---- things aboard ----
        public ShipPart AddPart(string kind, float lx, float ly, float lz, float hp, float armor, float bombMult, GameObject model)
        {
            model.transform.SetParent(transform, false);
            model.transform.localPosition = new Vector3(lx * U, ly * U, -lz * U);
            var p = model.AddComponent<ShipPart>();
            p.Setup(kind, hp, armor, bombMult, this);
            parts.Add(p);
            if (kind == "score") cores.Add(p);
            return p;
        }

        public void HullHit(Vector3 point, Projectiles.Shot s)
        {
            if (side == 0 && s.team == 1 && alive && dying < 0f) home = Mathf.Max(0f, home - (s.bolt ? 0.0045f : 0.0008f));
        }

        public void StartDying()
        {
            if (dying >= 0f || !alive) return;
            dying = 0f; vy = 0f;
            if (shieldWall != null) shieldWall.SetActive(false);
        }

        /// <summary>Doors open for anyone on foot standing near them; the ship burns and falls once it is finished.</summary>
        public void Tick(float dt, List<Vector3> feet)
        {
            foreach (var d in doors)
            {
                bool near = false;
                foreach (var f in feet)
                {
                    var l = W2L(f);
                    if (Mathf.Abs(l.x - d.lx) < 26f && Mathf.Abs(l.z) < 20f && Mathf.Abs(l.y + 30f) < 20f) { near = true; break; }
                }
                float want = near ? d.open + dt / 0.3f : d.open - dt / 0.5f;
                d.open = Mathf.Clamp01(want);
                d.a.localPosition = new Vector3(d.lx * U, 0f, -12f * d.open * U);
                d.b.localPosition = new Vector3(d.lx * U, 0f, 12f * d.open * U);
            }
            if (dying < 0f) return;
            dying += dt;
            if (dying < 3.5f)
            {
                fireT -= dt;
                if (fireT <= 0f)
                {
                    fireT = 0.08f;
                    var b = Hull[Random.Range(0, Hull.Length)];
                    var at = L2W(Random.Range(b.x0, b.x1), Random.Range(b.y0, b.y1), Random.Range(b.z0, b.z1));
                    if (Projectiles.Instance != null) Projectiles.Instance.Explode(at, 0f, 2f, 0f, gameObject, side);
                    var f = GameObject.CreatePrimitive(PrimitiveType.Sphere); Destroy(f.GetComponent<Collider>()); f.GetComponent<Renderer>().sharedMaterial = fireMat;
                    f.transform.SetParent(transform, true); f.transform.position = at; f.transform.localScale = Vector3.one * Random.Range(1.5f, 4f);
                    flames.Add(f.transform);
                }
                return;
            }
            vy -= 90f * U * dt;
            transform.position += Vector3.up * vy * dt;
            float ground = GalacticMode.Instance != null ? GalacticMode.Instance.TerrainHeight(transform.position.x, transform.position.z) : 0f;
            if (transform.position.y - 45f * U <= ground && alive)
            {
                alive = false;
                for (int q = -3; q <= 3; q++)
                {
                    var at = L2W(q * 110f, 0f, 0f); at.y = ground + 1f;
                    if (Projectiles.Instance != null) Projectiles.Instance.Explode(at, 60f * U, 220f * U, 900f * GalacticMode.K, gameObject, side);
                }
                if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(1f);
                if (OnDestroyed != null) OnDestroyed(this);
                foreach (var g in guards) if (g != null) Destroy(g.gameObject);
                Destroy(gameObject, 0.2f);
            }
        }
    }
}
