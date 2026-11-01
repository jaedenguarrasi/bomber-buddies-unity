using System.Collections.Generic;
using UnityEngine;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace BomberBuddies
{
    /// <summary>The underground (buildUnder / UND / HELL in the original), built as its own place well below the island:
    /// the subway (a long dark tunnel with platforms, pillars and stopped trains), the bunker complex (rooms off a corridor
    /// behind blast doors, with ammo dumps that clear the room when hit) and hell (a red cavern of lava and fire with room
    /// for a dragon to fly). You go down on foot; the ladder at the far end is the way out.</summary>
    public class Underground : MonoBehaviour
    {
        public static Underground Instance { get; private set; }
        public string layer; public Vector3 spawn, ladder; public float floorY; Bounds bounds;
        MaterialLibrary lib; readonly List<Vector3> lava = new List<Vector3>();
        public readonly List<Health> doors = new List<Health>(), dumps = new List<Health>();
        Material mat;

        public static Underground Build(Transform parent, MaterialLibrary lib, string layer, Vector3 islandCentre)
        {
            var go = new GameObject("Underground " + layer); go.transform.SetParent(parent, false);
            var u = go.AddComponent<Underground>(); Instance = u; u.lib = lib; u.layer = layer;
            u.mat = lib != null ? lib.Get("lowpoly") : null;
            u.floorY = -80f;
            var c = new Vector3(islandCentre.x, u.floorY, islandCentre.z);
            if (layer == "subway") u.Subway(c); else if (layer == "bunkers") u.Bunkers(c); else u.Hell(c);
            u.Nav();
            return u;
        }

        public bool Contains(float x, float z) { return x >= bounds.min.x && x <= bounds.max.x && z >= bounds.min.z && z <= bounds.max.z; }
        public float Ground(float x, float z) { return floorY; }
        /// <summary>A spot on the floor the enemies can stand on, at least 40 m from the player.</summary>
        public Vector3 RandomSpot()
        {
            var pl = GameState.Player != null ? GameState.Player.transform.position : spawn;
            for (int i = 0; i < 40; i++)
            {
                var p = new Vector3(Random.Range(bounds.min.x + 2f, bounds.max.x - 2f), floorY, Random.Range(bounds.min.z + 2f, bounds.max.z - 2f));
                if (Vector3.Distance(p, pl) < 40f) continue;
                UnityEngine.AI.NavMeshHit hit; if (UnityEngine.AI.NavMesh.SamplePosition(p, out hit, 6f, UnityEngine.AI.NavMesh.AllAreas) && Mathf.Abs(hit.position.y - floorY) < 3f) return hit.position;
            }
            return ladder + Vector3.up * 0.2f;
        }
        public bool InLava(Vector3 p) { foreach (var l in lava) if (Mathf.Abs(p.x - l.x) < 9f && Mathf.Abs(p.z - l.z) < 9f && p.y < floorY + 1f) return true; return false; }

        GameObject Piece(MeshBuilder mb, string name, bool collider) { var g = mb.Spawn(name, transform, mat, collider, false); return g; }
        static Color32 C(int r, int g, int b) { return new Color32((byte)r, (byte)g, (byte)b, 255); }
        static Color32 E(int r, int g, int b) { return MeshBuilder.Emissive(new Color32((byte)r, (byte)g, (byte)b, 255)); }

        /// <summary>A rectangular space: floor, ceiling and four thick walls, with the given openings left out (gaps along a wall).</summary>
        void Room(Vector3 min, Vector3 max, Color32 wall, Color32 floor, Color32 ceil, float thick = 2f)
        {
            var mb = new MeshBuilder { scale = 1f };
            mb.Box(new Vector3(min.x - thick, min.y - thick, min.z - thick), new Vector3(max.x + thick, min.y, max.z + thick), floor, true);
            mb.Box(new Vector3(min.x - thick, max.y, min.z - thick), new Vector3(max.x + thick, max.y + thick, max.z + thick), ceil, true);
            Piece(mb, "Slab", true);
        }
        void Wall(Vector3 a, Vector3 b, float h, float y0, Color32 col, float thick = 2f)
        {
            var mb = new MeshBuilder { scale = 1f };
            var min = Vector3.Min(a, b); var max = Vector3.Max(a, b);
            if (Mathf.Approximately(min.x, max.x)) { min.x -= thick * 0.5f; max.x += thick * 0.5f; } else { min.z -= thick * 0.5f; max.z += thick * 0.5f; }
            mb.Box(new Vector3(min.x, y0, min.z), new Vector3(max.x, y0 + h, max.z), col, true);
            Piece(mb, "Wall", true);
        }
        void Lamp(Vector3 at, Color32 col, float size = 0.6f)
        {
            var mb = new MeshBuilder { scale = 1f }; mb.Box(at - Vector3.one * size * 0.5f, at + Vector3.one * size * 0.5f, col); Piece(mb, "Lamp", false);
        }
        Health Breakable(string name, Vector3 min, Vector3 max, Color32 col, float hp, System.Action<Health> onDied)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false); go.layer = 9;
            var mb = new MeshBuilder { scale = 1f }; mb.Box(min, max, col, true); var g = mb.Spawn("Body", go.transform, mat, true, false); g.layer = 9;
            var h = go.AddComponent<Health>(); h.max = hp; h.current = hp; h.team = 1; h.aimHeight = (max.y - min.y) * 0.5f; go.transform.position = Vector3.zero;
            var hz = g.AddComponent<HitZone>(); hz.owner = h; hz.multiplier = 1f;
            h.OnDied += info => { onDied(h); Destroy(go); };
            return h;
        }

        // ---------- the subway ----------
        void Subway(Vector3 c)
        {
            float L = 420f, W = 14f, H = 6f;
            bounds = new Bounds(c + new Vector3(0f, H * 0.5f, 0f), new Vector3(W + 40f, H + 10f, L + 40f));
            var rock = C(70, 66, 64); var tile = C(120, 118, 110); var ceil = C(50, 48, 46);
            Room(c + new Vector3(-W * 0.5f, 0f, -L * 0.5f), c + new Vector3(W * 0.5f, H, L * 0.5f), rock, C(40, 38, 36), ceil);
            Wall(c + new Vector3(-W * 0.5f, 0f, -L * 0.5f), c + new Vector3(-W * 0.5f, 0f, L * 0.5f), H, c.y, rock);
            Wall(c + new Vector3(W * 0.5f, 0f, -L * 0.5f), c + new Vector3(W * 0.5f, 0f, L * 0.5f), H, c.y, rock);
            Wall(c + new Vector3(-W * 0.5f, 0f, -L * 0.5f), c + new Vector3(W * 0.5f, 0f, -L * 0.5f), H, c.y, rock);
            Wall(c + new Vector3(-W * 0.5f, 0f, L * 0.5f), c + new Vector3(W * 0.5f, 0f, L * 0.5f), H, c.y, rock);
            // the rails down the middle, platforms either side at two stations, pillars, stopped trains
            var mb = new MeshBuilder { scale = 1f };
            for (int s = -1; s <= 1; s += 2) mb.Box(c + new Vector3(s * 1.4f - 0.08f, 0f, -L * 0.5f), c + new Vector3(s * 1.4f + 0.08f, 0.15f, L * 0.5f), C(140, 140, 150));
            for (float z = -L * 0.5f; z < L * 0.5f; z += 1.2f) mb.Box(c + new Vector3(-1.8f, 0f, z), c + new Vector3(1.8f, 0.1f, z + 0.3f), C(90, 70, 50));
            Piece(mb, "Rails", false);
            foreach (float sz in new[] { -120f, 90f })
            {
                var pm = new MeshBuilder { scale = 1f };
                pm.Box(c + new Vector3(-W * 0.5f, 0f, sz - 40f), c + new Vector3(-2.6f, 1.1f, sz + 40f), tile, true);
                pm.Box(c + new Vector3(2.6f, 0f, sz - 40f), c + new Vector3(W * 0.5f, 1.1f, sz + 40f), tile, true);
                for (float z = sz - 36f; z <= sz + 36f; z += 12f) { pm.Box(c + new Vector3(-4.6f, 1.1f, z - 0.4f), c + new Vector3(-3.8f, H, z + 0.4f), C(100, 100, 108), true); pm.Box(c + new Vector3(3.8f, 1.1f, z - 0.4f), c + new Vector3(4.6f, H, z + 0.4f), C(100, 100, 108), true); }
                Piece(pm, "Platform", true);
                for (float z = sz - 36f; z <= sz + 36f; z += 18f) { Lamp(c + new Vector3(-3f, H - 0.4f, z), E(255, 240, 200)); Lamp(c + new Vector3(3f, H - 0.4f, z), E(255, 240, 200)); }
            }
            for (float z = -L * 0.5f + 20f; z < L * 0.5f; z += 30f) Lamp(c + new Vector3(0f, H - 0.3f, z), E(200, 60, 40), 0.4f);
            // three stopped trains: cars you fight through, doors open on the platform side
            foreach (float tz in new[] { -150f, -20f, 150f })
            {
                for (int car = 0; car < 2; car++)
                {
                    float z0 = tz + car * 19f; var tm = new MeshBuilder { scale = 1f }; var body = C(60, 90, 140);
                    tm.Box(c + new Vector3(-1.5f, 0.4f, z0), c + new Vector3(1.5f, 0.7f, z0 + 18f), C(40, 40, 44), true);   // the floor of the car
                    tm.Box(c + new Vector3(-1.5f, 0.7f, z0), c + new Vector3(-1.35f, 3.4f, z0 + 18f), body, true);          // left side
                    for (float z = z0; z < z0 + 18f; z += 6f) tm.Box(c + new Vector3(1.35f, 0.7f, z), c + new Vector3(1.5f, 3.4f, z + 3.5f), body, true);   // right side with door gaps
                    tm.Box(c + new Vector3(-1.5f, 3.4f, z0), c + new Vector3(1.5f, 3.7f, z0 + 18f), C(50, 70, 110), true);  // roof
                    tm.Box(c + new Vector3(-1.5f, 0.7f, z0 + 17.8f), c + new Vector3(1.5f, 3.4f, z0 + 18f), body, true);  // ends
                    if (car == 0) tm.Box(c + new Vector3(-1.5f, 0.7f, z0), c + new Vector3(1.5f, 3.4f, z0 + 0.2f), body, true);
                    for (float z = z0 + 1f; z < z0 + 17f; z += 2.5f) tm.Box(c + new Vector3(-1.3f, 0.7f, z), c + new Vector3(-0.7f, 1.3f, z + 1.6f), C(140, 60, 50), true);   // seats
                    Piece(tm, "Train car", true);
                    Lamp(c + new Vector3(0f, 3.3f, z0 + 9f), E(200, 230, 255), 0.3f);
                }
            }
            spawn = c + new Vector3(-5f, 0.2f, -L * 0.5f + 30f);
            ladder = c + new Vector3(5f, 0f, L * 0.5f - 12f);
            LadderShaft(ladder, H);
        }

        void LadderShaft(Vector3 at, float H)
        {
            var mb = new MeshBuilder { scale = 1f };
            mb.Box(at + new Vector3(-0.5f, 0f, -0.2f), at + new Vector3(0.5f, H + 1f, 0.2f), C(160, 140, 90), true);
            for (float y = 0.4f; y < H; y += 0.5f) mb.Box(at + new Vector3(-0.5f, y, -0.3f), at + new Vector3(0.5f, y + 0.08f, 0.3f), C(190, 170, 110));
            Piece(mb, "Ladder", false);
            Lamp(at + Vector3.up * (H - 0.3f), E(120, 255, 160), 0.5f);
        }

        // ---------- the bunker complex ----------
        void Bunkers(Vector3 c)
        {
            float L = 240f, CW = 6f, H = 4f, RW = 30f, RD = 26f;
            bounds = new Bounds(c + new Vector3(0f, H * 0.5f, 0f), new Vector3(2f * RD + CW + 40f, H + 10f, L + 40f));
            var conc = C(118, 118, 112); var fl = C(70, 70, 66); var ce = C(90, 90, 86);
            // the corridor
            Room(c + new Vector3(-CW * 0.5f, 0f, -L * 0.5f), c + new Vector3(CW * 0.5f, H, L * 0.5f), conc, fl, ce);
            Wall(c + new Vector3(0f, 0f, -L * 0.5f) + Vector3.left * CW * 0.5f, c + new Vector3(0f, 0f, -L * 0.5f) + Vector3.right * CW * 0.5f, H, c.y, conc);
            Wall(c + new Vector3(0f, 0f, L * 0.5f) + Vector3.left * CW * 0.5f, c + new Vector3(0f, 0f, L * 0.5f) + Vector3.right * CW * 0.5f, H, c.y, conc);
            for (float z = -L * 0.5f + 8f; z < L * 0.5f; z += 16f) Lamp(c + new Vector3(0f, H - 0.3f, z), E(255, 230, 170), 0.4f);
            // six rooms, three a side, each behind a blast door with an ammo dump inside
            int n = 0;
            for (int i = 0; i < 3; i++) for (int side = -1; side <= 1; side += 2)
            {
                float rz = -L * 0.5f + 45f + i * 75f; float rx = side * (CW * 0.5f + RD * 0.5f);
                var rmin = c + new Vector3(rx - RD * 0.5f, 0f, rz - RW * 0.5f); var rmax = c + new Vector3(rx + RD * 0.5f, H, rz + RW * 0.5f);
                Room(rmin, rmax, conc, fl, ce);
                // walls of the room, the corridor side with a doorway
                Wall(new Vector3(rmin.x, 0f, rmin.z), new Vector3(rmax.x, 0f, rmin.z), H, c.y, conc);
                Wall(new Vector3(rmin.x, 0f, rmax.z), new Vector3(rmax.x, 0f, rmax.z), H, c.y, conc);
                float far = side < 0 ? rmin.x : rmax.x; Wall(new Vector3(far, 0f, rmin.z), new Vector3(far, 0f, rmax.z), H, c.y, conc);
                float near = side < 0 ? rmax.x : rmin.x;
                Wall(new Vector3(near, 0f, rmin.z), new Vector3(near, 0f, c.z + rz - 2.5f), H, c.y, conc);
                Wall(new Vector3(near, 0f, c.z + rz + 2.5f), new Vector3(near, 0f, rmax.z), H, c.y, conc);
                // corridor wall segments between doorways
                float cx = c.x + side * CW * 0.5f;
                Wall(new Vector3(cx, 0f, i == 0 ? c.z - L * 0.5f : c.z + rz - 75f + RW * 0.5f), new Vector3(cx, 0f, c.z + rz - RW * 0.5f), H, c.y, conc);
                if (i == 2) Wall(new Vector3(cx, 0f, c.z + rz + RW * 0.5f), new Vector3(cx, 0f, c.z + L * 0.5f), H, c.y, conc);
                Wall(new Vector3(cx, 0f, c.z + rz - RW * 0.5f), new Vector3(cx, 0f, c.z + rz - 2.5f), H, c.y, conc);
                Wall(new Vector3(cx, 0f, c.z + rz + 2.5f), new Vector3(cx, 0f, c.z + rz + RW * 0.5f), H, c.y, conc);
                // the blast door: thick, grey, 600 hp; blow it and the room is open
                var dmin = new Vector3(Mathf.Min(near, cx), c.y, c.z + rz - 2.5f); var dmax = new Vector3(Mathf.Max(near, cx) + 0.01f, c.y + H, c.z + rz + 2.5f);
                if (Mathf.Abs(dmax.x - dmin.x) < 0.5f) { dmin.x = cx - 0.4f; dmax.x = cx + 0.4f; }
                doors.Add(Breakable("Blast door", dmin, dmax, C(90, 95, 100), 600f * GalacticMode.K, h => { Sfx.Play("expl_big", 0.8f, 0.7f, h.transform.position); if (GameState.Hud != null) GameState.Hud.Comms("Door breached"); GameState.Earn(60); if (Projectiles.Instance != null) Projectiles.Instance.Explode((dmin + dmax) * 0.5f, 1f, 4f, 30f, null, 2); }));
                // the ammo dump: crates in the back corner; one stray round and the whole room goes
                var amin = new Vector3(far + (side < 0 ? 1f : -5f), c.y, rmax.z - 6f); var amax = amin + new Vector3(4f, 1.6f, 4f);
                var room = (rmin + rmax) * 0.5f;
                dumps.Add(Breakable("Ammo dump", amin, amax, C(110, 90, 50), 30f, h => { Sfx.Play("expl_huge", 1f, 0.8f, room); if (GalacticMode.Instance != null) GalacticMode.Instance.Shake(1f); if (Projectiles.Instance != null) Projectiles.Instance.Explode(room + Vector3.up, 12f, 22f, 400f * GalacticMode.K, null, 2); if (GameState.Hud != null) GameState.Hud.Comms("The ammo dump went up"); }));
                Lamp(room + Vector3.up * (H - 0.3f), E(255, 200, 150), 0.4f);
                for (int k = 0; k < 3; k++) { var cb = new MeshBuilder { scale = 1f }; var p = room + new Vector3(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f)); cb.Box(p + new Vector3(-0.8f, 0f, -0.8f), p + new Vector3(0.8f, 1.2f, 0.8f), C(100, 80, 55), true); Piece(cb, "Crate", true); }
                n++;
            }
            spawn = c + new Vector3(0f, 0.2f, -L * 0.5f + 8f);
            ladder = c + new Vector3(0f, 0f, L * 0.5f - 6f);
            LadderShaft(ladder, H);
        }

        // ---------- hell ----------
        void Hell(Vector3 c)
        {
            float S = 260f, H = 70f;
            bounds = new Bounds(c + new Vector3(0f, H * 0.5f, 0f), new Vector3(S + 40f, H + 20f, S + 40f));
            var rock = C(80, 30, 24); var fl = C(60, 22, 18); var ce = C(40, 14, 12);
            Room(c + new Vector3(-S * 0.5f, 0f, -S * 0.5f), c + new Vector3(S * 0.5f, H, S * 0.5f), rock, fl, ce, 4f);
            Wall(c + new Vector3(-S * 0.5f, 0f, -S * 0.5f), c + new Vector3(-S * 0.5f, 0f, S * 0.5f), H, c.y, rock, 4f);
            Wall(c + new Vector3(S * 0.5f, 0f, -S * 0.5f), c + new Vector3(S * 0.5f, 0f, S * 0.5f), H, c.y, rock, 4f);
            Wall(c + new Vector3(-S * 0.5f, 0f, -S * 0.5f), c + new Vector3(S * 0.5f, 0f, -S * 0.5f), H, c.y, rock, 4f);
            Wall(c + new Vector3(-S * 0.5f, 0f, S * 0.5f), c + new Vector3(S * 0.5f, 0f, S * 0.5f), H, c.y, rock, 4f);
            // lava pools that glow, pillars of rock, fire
            var lv = new MeshBuilder { scale = 1f };
            for (int i = 0; i < 14; i++)
            {
                var p = c + new Vector3(Random.Range(-S * 0.42f, S * 0.42f), 0f, Random.Range(-S * 0.42f, S * 0.42f));
                if (Vector3.Distance(p, c + new Vector3(0f, 0f, -S * 0.45f)) < 30f) continue;
                lava.Add(p); lv.Box(p + new Vector3(-9f, 0.02f, -9f), p + new Vector3(9f, 0.25f, 9f), E(255, 110, 20));
            }
            Piece(lv, "Lava", false);
            for (int i = 0; i < 18; i++)
            {
                var p = c + new Vector3(Random.Range(-S * 0.45f, S * 0.45f), 0f, Random.Range(-S * 0.45f, S * 0.45f));
                var pb = new MeshBuilder { scale = 1f }; float r = Random.Range(2f, 5f), h = Random.Range(12f, H);
                pb.Prism(p.x, p.y, p.y + h, p.z, r, r * 0.6f, 6, Random.Range(0f, 1f), rock); Piece(pb, "Pillar", true);
            }
            for (int i = 0; i < 24; i++) Lamp(c + new Vector3(Random.Range(-S * 0.45f, S * 0.45f), Random.Range(2f, H - 4f), Random.Range(-S * 0.45f, S * 0.45f)), E(255, 150, 40), 0.8f);
            spawn = c + new Vector3(0f, 0.2f, -S * 0.45f + 8f);
            ladder = c + new Vector3(0f, 0f, -S * 0.45f + 2f);
            LadderShaft(ladder, 12f);
        }

        void Nav()
        {
            var nav = new GameObject("Underground navigation"); nav.transform.SetParent(transform, false);
            var surface = nav.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume; surface.center = bounds.center; surface.size = bounds.size;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders; surface.layerMask = ~((1 << 8) | (1 << 9));
            surface.overrideVoxelSize = true; surface.voxelSize = 0.25f; surface.overrideTileSize = true; surface.tileSize = 128;
            surface.BuildNavMesh();
        }

        void Update()
        {
            // lava burns; the ladder is the way out
            var p = GameState.Player; if (p == null || !p.health.alive || PlayerManager.Instance == null || !PlayerManager.Instance.OnFoot) return;
            if (InLava(p.transform.position) && Mathf.Repeat(Time.time, 0.4f) < Time.deltaTime) { p.health.TakeDamage(new DamageInfo { amount = 18f * GalacticMode.K, point = p.transform.position, direction = Vector3.up, source = gameObject }); if (GameState.Hud != null) GameState.Hud.Comms("You are in the lava!"); }
        }
    }
}
