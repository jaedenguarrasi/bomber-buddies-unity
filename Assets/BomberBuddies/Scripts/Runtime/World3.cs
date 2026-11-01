using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The original game's island, generated the way genWorld3 did it (same noise, same seeded random, same order): a 224×224
    /// heightmap, four skyscraper districts, five towns of hollow homes on streets, halls, villages with cover, fuel tanks,
    /// 520 trees; drawn as flat-shaded vertex-coloured chunks with the terrain's own colouring rules. World units are the
    /// original's (1 unit = 0.12 m); the island is centred on the Unity origin.
    /// </summary>
    public class World3 : MonoBehaviour
    {
        public const int N3 = 224, BS = 14, CH = 16;
        public const float WORLD3 = N3 * BS, MID3 = WORLD3 * 0.5f, SEA = 28f, FLOOR3 = 21f, FLOOR_H = 13f;
        public const float U = GalacticMode.U;
        public static World3 Instance { get; private set; }
        /// <summary>How much wider the island is than the original (buildings keep their size, the land between them grows).</summary>
        public static float Spread = 1.8f;

        public int seed;
        public Material lowPoly, water;
        public bool mobile;
        // per-corner / per-cell data
        public float[] hm, hm0;
        public byte[] road3, tree3, house3, bfl, burn;
        public float[] bhw;
        public int[] own3;
        public readonly List<Vector3> villageCentres = new List<Vector3>();   // world metres
        public readonly List<Vector3> towerCells = new List<Vector3>();
        public Vector3 spawn;   // world metres: the original's spawnC
        uint rngState;
        MeshBuilder mb = new MeshBuilder();

        // ---- the original's maths ----
        static float Noise3(float x, float z, int seed)
        {
            int xi = Mathf.FloorToInt(x), zi = Mathf.FloorToInt(z);
            float xf = x - xi, zf = z - zi;
            float u = xf * xf * (3f - 2f * xf), v = zf * zf * (3f - 2f * zf);
            float H(int a, int b)
            {
                unchecked
                {
                    int n = (int)((uint)a * 374761393u + (uint)b * 668265263u + (uint)seed * 1442695041u);
                    n = (int)((uint)(n ^ (int)((uint)n >> 13)) * 1274126177u);
                    return ((uint)(n ^ (int)((uint)n >> 16))) / 4294967296f;
                }
            }
            return Mathf.Lerp(Mathf.Lerp(H(xi, zi), H(xi + 1, zi), u), Mathf.Lerp(H(xi, zi + 1), H(xi + 1, zi + 1), u), v);
        }

        static float Hash(float x) { float v = Mathf.Sin(x * 12.9898f) * 43758.5453f; return v - Mathf.Floor(v); }

        float Rng()
        {
            unchecked
            {
                rngState += 0x6D2B79F5u;
                uint t = rngState;
                t = (t ^ (t >> 15)) * (t | 1u);
                t ^= t + (t ^ (t >> 7)) * (t | 61u);
                return ((t ^ (t >> 14)) >> 0) / 4294967296f;
            }
        }

        static float Clamp01(float v) { return v < 0f ? 0f : v > 1f ? 1f : v; }
        static float Smooth(float t) { return t * t * (3f - 2f * t); }

        // ---- heights ----
        /// <summary>Terrain height in original units at original-unit coordinates.</summary>
        public float HAt0(float x, float z)
        {
            float fx = x / BS, fz = z / BS;   // x, z in original units, unspread
            int ix = Mathf.FloorToInt(fx), iz = Mathf.FloorToInt(fz);
            if (ix < 0 || iz < 0 || ix > N3 - 2 || iz > N3 - 2) return FLOOR3;
            float u = fx - ix, v = fz - iz; int k = iz * N3 + ix;
            float h00 = hm[k], h10 = hm[k + 1], h01 = hm[k + N3], h11 = hm[k + N3 + 1];
            return u > v ? h00 + (h10 - h00) * u + (h11 - h10) * v : h00 + (h11 - h01) * u + (h01 - h00) * v;
        }

        /// <summary>Ground height in metres at world metres (x, z); the sea counts as ground.</summary>
        public float GroundMetres(float x, float z)
        {
            float h = HAt0(x / (U * Spread) + MID3, z / (U * Spread) + MID3);
            return Mathf.Max(SEA, h) * U;
        }

        /// <summary>The highest thing under a point in metres: a tower roof, a house, or the ground (the original's obstH).</summary>
        public float ObstacleMetres(float x, float z)
        {
            float h = GroundMetres(x, z);
            foreach (var t in towerCells)
            {
                int k = (int)t.z * N3 + (int)t.x;
                var c = ToWorld((int)t.x * BS, hm[k], (int)t.z * BS);
                float hw = bhw[k] * U;
                if (Mathf.Abs(x - c.x) <= hw && Mathf.Abs(z - c.z) <= hw) h = Mathf.Max(h, c.y + bfl[k] * FLOOR_H * U);
            }
            return h;
        }

        public static Vector3 ToWorld(float ox, float oy, float oz) { return new Vector3((ox - MID3) * U * Spread, oy * U, (oz - MID3) * U * Spread); }

        bool Flat(int k) { return k > N3 && k < N3 * N3 - N3 - 1 && Mathf.Abs(hm[k + 1] - hm[k - 1]) + Mathf.Abs(hm[k + N3] - hm[k - N3]) < 7f; }

        // ---- generation ----
        public static World3 Build(Transform parent, int seed, MaterialLibrary lib, bool mobile)
        {
            var go = new GameObject("World");
            go.transform.SetParent(parent, false);
            var w = go.AddComponent<World3>();
            Instance = w;
            w.seed = seed; w.mobile = mobile;
            w.lowPoly = lib != null ? lib.Get("lowpoly") : null;
            if (w.lowPoly == null) { var sh = Shader.Find("BomberBuddies/LowPoly"); w.lowPoly = new Material(sh != null ? sh : Shader.Find("Universal Render Pipeline/Lit")); }
            w.water = lib != null ? lib.Get("water") : null;
            w.Generate();
            w.BuildMeshes();
            return w;
        }

        void Generate()
        {
            int n = N3 * N3;
            hm = new float[n]; hm0 = new float[n]; road3 = new byte[n]; tree3 = new byte[n]; house3 = new byte[n]; bfl = new byte[n]; burn = new byte[n]; bhw = new float[n]; own3 = new int[n];
            rngState = (uint)seed;
            // the heightmap
            float c = N3 / 2f;
            for (int z = 0; z < N3; z++)
                for (int x = 0; x < N3; x++)
                {
                    float dx = (x - c) / c, dz = (z - c) / c, d = Mathf.Sqrt(dx * dx + dz * dz);
                    float m0 = Clamp01((0.98f - d) / 0.34f), m = Smooth(m0);
                    float plains = 24f + 9f * Noise3(x / 38f, z / 38f, seed) + 3.5f * Noise3(x / 13f, z / 13f, seed + 7);
                    float hk = Clamp01((Noise3(x / 58f, z / 58f, seed + 21) - 0.6f) / 0.2f);
                    float hills = Smooth(hk) * (34f + 110f * Noise3(x / 24f, z / 24f, seed + 13));
                    hm[z * N3 + x] = 14f + m * (SEA - 14f + plains + hills);
                }
            Districts();
            Towns();
            Trees();
            Halls();
            Villages();
            StrayTanks();
            System.Array.Copy(hm, hm0, n);
            int sx = Mathf.RoundToInt((MID3 + 60f) / BS), sz = Mathf.RoundToInt((MID3 + 520f) / BS);
            spawn = ToWorld(sx * BS, Mathf.Max(SEA, hm[sz * N3 + sx]), sz * BS);
        }

        void Level(int x0, int z0, int W2, int D2, int grow, int blend, float lvl, bool paveCore)
        {
            for (int z = z0 - grow; z < z0 + D2 + grow; z++)
                for (int x = x0 - grow; x < x0 + W2 + grow; x++)
                {
                    if (x < 0 || z < 0 || x >= N3 || z >= N3) continue;
                    int ex = Mathf.Max(0, Mathf.Max(x0 - x, x - (x0 + W2 - 1))), ez = Mathf.Max(0, Mathf.Max(z0 - z, z - (z0 + D2 - 1)));
                    float t = Clamp01(Mathf.Max(ex, ez) / (float)blend);
                    int k = z * N3 + x;
                    hm[k] = Mathf.Lerp(lvl, hm[k], Smooth(t));
                    if (paveCore && t == 0f) road3[k] = 1;
                }
        }

        bool RectOk(int x0, int z0, int W2, int D2, float landFrac, float maxRange, int noRoadGrow, out float mean)
        {
            int land = 0, tot = 0; float lo = 1e9f, hi = -1e9f, sum = 0f;
            for (int z = z0; z < z0 + D2; z++)
                for (int x = x0; x < x0 + W2; x++)
                {
                    int k = z * N3 + x; tot++;
                    if (hm[k] > SEA + 6f) { land++; sum += hm[k]; lo = Mathf.Min(lo, hm[k]); hi = Mathf.Max(hi, hm[k]); }
                }
            mean = land > 0 ? sum / land : SEA + 14f;
            if (land < tot * landFrac || hi - lo > maxRange) return false;
            for (int z = z0 - noRoadGrow; z < z0 + D2 + noRoadGrow; z += 3)
                for (int x = x0 - noRoadGrow; x < x0 + W2 + noRoadGrow; x += 3)
                {
                    if (x < 0 || z < 0 || x >= N3 || z >= N3) continue;
                    int k = z * N3 + x;
                    if (road3[k] != 0 || own3[k] != 0 || house3[k] != 0) return false;
                }
            return true;
        }

        void Districts()
        {
            int c = 112;
            for (int cN = 0, tries = 0; cN < 4 && tries < 1400; tries++)
            {
                bool big = cN == 0;
                int W2 = Mathf.FloorToInt((big ? 58 : 38) + Rng() * 12), D2 = Mathf.FloorToInt((big ? 54 : 36) + Rng() * 12);
                int x0 = Mathf.FloorToInt(20 + Rng() * (N3 - 40 - W2)), z0 = Mathf.FloorToInt(20 + Rng() * (N3 - 40 - D2));
                float range = tries < 500 ? 44f : tries < 900 ? 80f : 120f;
                float mean;
                if (!RectOk(x0, z0, W2, D2, 0.8f, range, 10, out mean)) continue;
                // not in the take-off corridor
                if (x0 + W2 > c - 20 && x0 < c + 36 && z0 + D2 > c - 30 && z0 < c + 84) continue;
                float lvl = Mathf.Max(SEA + 14f, mean);
                Level(x0, z0, W2, D2, 7, 7, lvl, true);
                int lot = big ? 13 : 11;
                float cx = x0 + W2 * 0.5f, cz = z0 + D2 * 0.5f;
                for (int gz = z0 + 5; gz < z0 + D2 - 4; gz += lot)
                    for (int gx = x0 + 5; gx < x0 + W2 - 4; gx += lot)
                    {
                        if (Rng() < (big ? 0.12f : 0.22f)) continue;
                        int jx = gx + Mathf.FloorToInt(Rng() * 3) - 1, jz = gz + Mathf.FloorToInt(Rng() * 3) - 1;
                        float mid = 1f - Mathf.Sqrt(Mathf.Pow((jx - cx) / (W2 / 2f), 2) + Mathf.Pow((jz - cz) / (D2 / 2f), 2)) * 0.8f;
                        float r = Rng();
                        float half = big ? (r < 0.15f ? 24 : r < 0.4f ? 34 : r < 0.68f ? 44 : r < 0.88f ? 52 : 60) : (r < 0.2f ? 16 : r < 0.45f ? 22 : r < 0.7f ? 30 : r < 0.88f ? 38 : 46);
                        bool squat = half >= 44 && Rng() < (big ? 0.3f : 0.55f);
                        int k = jz * N3 + jx;
                        house3[k] = (byte)(20 + Mathf.FloorToInt(Rng() * 4));
                        bhw[k] = half;
                        int floors = squat ? 4 + Mathf.FloorToInt(Rng() * 7) : Mathf.Clamp(Mathf.RoundToInt((big ? 14 : 8) + Rng() * 10 + mid * (big ? 34 : 24)), 6, big ? 60 : 44);
                        bfl[k] = (byte)floors;
                        int cells = Mathf.CeilToInt((half + 7f) / BS);
                        for (int oz = -cells; oz <= cells; oz++) for (int ox = -cells; ox <= cells; ox++) { int kk = (jz + oz) * N3 + jx + ox; if (jx + ox >= 0 && jx + ox < N3 && jz + oz >= 0 && jz + oz < N3) { own3[kk] = k + 1; tree3[kk] = 0; } }
                        towerCells.Add(new Vector3(jx, floors, jz));
                    }
                cN++;
            }
        }

        void PlaceHome(int k, int door, bool big)
        {
            int lx = big ? 22 : 15 + Mathf.FloorToInt(Rng() * 4), lz = big ? 18 : 13 + Mathf.FloorToInt(Rng() * 4);
            int wc = Mathf.FloorToInt(Rng() * 4), rf = Mathf.FloorToInt(Rng() * 4); Rng();   // furniture
            house3[k] = 30;
            homes[k] = new Home { lx = lx, lz = lz, door = door, wc = wc, rf = rf, big = big };
            int cx = k % N3, cz = k / N3;
            int cxs = Mathf.CeilToInt((lx + 7f) / BS), czs = Mathf.CeilToInt((lz + 7f) / BS);
            for (int oz = -czs; oz <= czs; oz++) for (int ox = -cxs; ox <= cxs; ox++) { int x = cx + ox, z = cz + oz; if (x < 0 || z < 0 || x >= N3 || z >= N3) continue; int kk = z * N3 + x; if (kk != k) own3[kk] = k + 1; tree3[kk] = 0; }
        }
        public class Home { public int lx, lz, door, wc, rf; public bool big, hall; }
        public readonly Dictionary<int, Home> homes = new Dictionary<int, Home>();

        void Towns()
        {
            int spawnCx = Mathf.RoundToInt((MID3 + 60f) / BS), spawnCz = Mathf.RoundToInt((MID3 + 520f) / BS);
            for (int t = 0, tries = 0; t < 5 && tries < 900; tries++)
            {
                int W2 = Mathf.FloorToInt(24 + Rng() * 12), D2 = Mathf.FloorToInt(16 + Rng() * 6);
                int x0 = Mathf.FloorToInt(14 + Rng() * (N3 - 28 - W2)), z0 = Mathf.FloorToInt(14 + Rng() * (N3 - 28 - D2));
                float mean;
                if (!RectOk(x0, z0, W2, D2, 0.9f, tries < 600 ? 40f : 70f, 3, out mean)) continue;
                if (Mathf.Abs(x0 + W2 / 2 - spawnCx) < 22 && Mathf.Abs(z0 + D2 / 2 - spawnCz) < 22) continue;
                Level(x0, z0, W2, D2, 5, 5, mean, false);
                int mz0 = z0 + (D2 >> 1);
                for (int x = x0; x < x0 + W2; x++) { road3[mz0 * N3 + x] = 1; road3[(mz0 + 1) * N3 + x] = 1; }
                var cross = new List<int>();
                for (int x = x0 + 4; x < x0 + W2 - 3; x += 8) { cross.Add(x); for (int z = z0; z < z0 + D2; z++) road3[z * N3 + x] = 1; }
                for (int x = x0 + 1; x < x0 + W2 - 1; x += 3)
                {
                    bool nearCross = false; foreach (var cx in cross) if (Mathf.Abs(x - cx) <= 2) nearCross = true;
                    if (nearCross) continue;
                    PlaceHome((mz0 - 2) * N3 + x, 2, Rng() < 0.12f);
                    PlaceHome((mz0 + 3) * N3 + x, 3, Rng() < 0.12f);
                }
                foreach (var c2 in cross)
                    for (int z = z0 + 1; z < z0 + D2 - 1; z += 3)
                    {
                        if (Mathf.Abs(z - mz0) < 3) continue;
                        if (c2 - 2 > x0) PlaceHome(z * N3 + c2 - 2, 0, Rng() < 0.12f);
                        if (c2 + 2 < x0 + W2 - 1) PlaceHome(z * N3 + c2 + 2, 1, Rng() < 0.12f);
                    }
                t++;
            }
        }

        void Trees()
        {
            int count = 0;
            for (int tries = 0; tries < 9000 && count < 520; tries++)
            {
                int x = 2 + Mathf.FloorToInt(Rng() * (N3 - 5)), z = 2 + Mathf.FloorToInt(Rng() * (N3 - 5));
                int k = z * N3 + x;
                if (hm[k] <= SEA + 6f || tree3[k] != 0 || road3[k] != 0 || own3[k] != 0 || house3[k] != 0) continue;
                if (Noise3(x / 17f, z / 17f, seed + 31) < 0.6f && Rng() > 0.07f) continue;
                tree3[k] = (byte)(1 + Mathf.FloorToInt(Rng() * 2));
                count++;
            }
        }

        void Halls()
        {
            for (int h = 0, tries = 0; h < 4 && tries < 800; tries++)
            {
                int x = 8 + Mathf.FloorToInt(Rng() * (N3 - 16)), z = 8 + Mathf.FloorToInt(Rng() * (N3 - 16));
                int k = z * N3 + x;
                if (hm[k] <= SEA + 12f || !Flat(k) || road3[k] != 0 || own3[k] != 0) continue;
                bool ok = true;
                for (int oz = -4; oz <= 4 && ok; oz++) for (int ox = -3; ox <= 3; ox++) { int kk = (z + oz) * N3 + x + ox; if (Mathf.Abs(hm[kk] - hm[k]) > 7f || road3[kk] != 0 || own3[kk] != 0 || house3[kk] != 0) { ok = false; break; } }
                if (!ok) continue;
                for (int oz = -4; oz <= 4; oz++) for (int ox = -3; ox <= 3; ox++) { int kk = (z + oz) * N3 + x + ox; hm[kk] = hm[k]; }
                PlaceHome(k, 0, true);
                homes[k].lx = 44; homes[k].lz = 30; homes[k].hall = true;
                h++;
            }
        }

        void Villages()
        {
            for (int v = 0, tries = 0; v < 13 && tries < 600; tries++)
            {
                int x = 14 + Mathf.FloorToInt(Rng() * (N3 - 28)), z = 14 + Mathf.FloorToInt(Rng() * (N3 - 28));
                int k = z * N3 + x;
                if (hm[k] <= SEA + 12f || !Flat(k) || road3[k] != 0 || own3[k] != 0) continue;
                villageCentres.Add(ToWorld(x * BS, hm[k], z * BS));
                for (int i = 0; i < 5; i++)
                {
                    float cx = x + (Rng() * 14f - 7f), cz = z + (Rng() * 14f - 7f);
                    float r = Rng(); string kind = r < 0.5f ? "car" : Rng() < 0.5f ? "barrier" : "bags";
                    float yaw = Mathf.FloorToInt(Rng() * 4) * Mathf.PI * 0.5f + (Rng() - 0.5f) * 0.5f;
                    cover.Add(new Cover { x = cx * BS, z = cz * BS, kind = kind, yaw = yaw });
                }
                int nB = 4 + Mathf.FloorToInt(Rng() * 5);
                for (int i = 0; i < nB; i++)
                {
                    int bx = x + Mathf.FloorToInt(Rng() * 11) - 5, bz = z + Mathf.FloorToInt(Rng() * 11) - 5;
                    int kb = bz * N3 + bx;
                    if (hm[kb] <= SEA + 10f || !Flat(kb)) continue;
                    bool ok = true;
                    for (int oz = -2; oz <= 2 && ok; oz++) for (int ox = -2; ox <= 2; ox++) { int kk = (bz + oz) * N3 + bx + ox; if (house3[kk] != 0 || own3[kk] != 0 || road3[kk] != 0) { ok = false; break; } }
                    if (!ok) continue;
                    if (Rng() < 0.45f) PlaceHome(kb, Mathf.FloorToInt(Rng() * 4), false);
                    else { house3[kb] = (byte)(1 + Mathf.FloorToInt(Rng() * 4)); tree3[kb] = 0; }
                }
                if (v % 2 == 0) for (int i = 0; i < 2; i++) { int fx = x + Mathf.FloorToInt(Rng() * 15) - 7, fz = z + Mathf.FloorToInt(Rng() * 15) - 7; int kf = fz * N3 + fx; if (house3[kf] == 0 && own3[kf] == 0 && hm[kf] > SEA + 8f) { house3[kf] = 10; tree3[kf] = 0; } }
                v++;
            }
        }
        public class Cover { public float x, z, yaw; public string kind; }
        public readonly List<Cover> cover = new List<Cover>();

        void StrayTanks()
        {
            for (int i = 0; i < 70; i++)
            {
                int x = 6 + Mathf.FloorToInt(Rng() * (N3 - 12)), z = 6 + Mathf.FloorToInt(Rng() * (N3 - 12));
                int k = z * N3 + x;
                if (i % 3 != 0) continue;
                if (hm[k] <= SEA + 9f || hm[k] > SEA + 60f || !Flat(k) || house3[k] != 0 || own3[k] != 0 || road3[k] != 0) continue;
                house3[k] = 10; tree3[k] = 0;
            }
        }

        // ---- meshes ----
        static readonly Color32[] HOUSE_WALL = { MeshBuilder.Rgb(226, 216, 190), MeshBuilder.Rgb(214, 200, 170), MeshBuilder.Rgb(232, 226, 208), MeshBuilder.Rgb(205, 188, 160) };
        static readonly Color32[] HOUSE_ROOF = { MeshBuilder.Rgb(181, 69, 47), MeshBuilder.Rgb(150, 62, 48), MeshBuilder.Rgb(74, 85, 96), MeshBuilder.Rgb(120, 86, 60) };
        static readonly Color32[] TOWER_COL = { MeshBuilder.Rgb(222, 210, 184), MeshBuilder.Rgb(196, 138, 104), MeshBuilder.Rgb(150, 172, 180), MeshBuilder.Rgb(206, 194, 150) };

        Color32 CornerColour(int x, int z)
        {
            int k = z * N3 + x;
            float h = hm[k], a = h - SEA;
            float hL = hm[Mathf.Max(0, k - 1)], hR = hm[Mathf.Min(N3 * N3 - 1, k + 1)], hU = hm[Mathf.Max(0, k - N3)], hD = hm[Mathf.Min(N3 * N3 - 1, k + N3)];
            float nx = (hL - hR) / (2f * BS), nz = (hU - hD) / (2f * BS), ny = 1f / Mathf.Sqrt(nx * nx + 1f + nz * nz);
            float v = Noise3(x / 9f, z / 9f, 77);
            var col = MeshBuilder.Rgb(Mathf.RoundToInt(Mathf.Lerp(94, 118, v)), Mathf.RoundToInt(Mathf.Lerp(125, 150, v)), Mathf.RoundToInt(Mathf.Lerp(58, 72, v)));
            if (a > 70f) col = MeshBuilder.Mix(col, MeshBuilder.Rgb(86, 110, 62), Clamp01((a - 70f) / 60f));
            if (a < 11f) col = MeshBuilder.Mix(col, MeshBuilder.Rgb(222, 205, 150), Clamp01((11f - a) / 6f));
            if (ny < 0.86f) col = MeshBuilder.Mix(col, MeshBuilder.Rgb(143, 103, 59), Clamp01((0.86f - ny) / 0.18f));
            if (road3[k] != 0) { int nn = Mathf.RoundToInt(Noise3(x / 3f, z / 3f, 5) * 10f); col = MeshBuilder.Rgb(86 + nn, 88 + nn, 90 + nn); }
            if (burn[k] != 0) col = MeshBuilder.Mix(col, MeshBuilder.Rgb(43, 36, 31), burn[k] / 255f * 0.8f);
            return col;
        }

        void BuildMeshes()
        {
            int nch = N3 / CH;
            for (int cz = 0; cz < nch; cz++)
                for (int cx = 0; cx < nch; cx++)
                {
                    mb.Clear();
                    BuildChunk(cx, cz);
                    if (mb.VertexCount == 0) continue;
                    mb.Spawn("Chunk " + cx + "," + cz, transform, lowPoly, true);
                }
            BuildWater();
            BuildRidges();
            BuildClouds();
        }

        void BuildChunk(int cx, int cz)
        {
            for (int z = cz * CH; z < (cz + 1) * CH && z < N3 - 1; z++)
                for (int x = cx * CH; x < (cx + 1) * CH && x < N3 - 1; x++)
                {
                    int k = z * N3 + x;
                    float h00 = hm[k], h10 = hm[k + 1], h01 = hm[k + N3], h11 = hm[k + N3 + 1];
                    if (h00 < SEA - 1f && h10 < SEA - 1f && h01 < SEA - 1f && h11 < SEA - 1f) continue;
                    float x0 = (x * BS - MID3) * Spread, z0 = (z * BS - MID3) * Spread, x1 = x0 + BS * Spread, z1 = z0 + BS * Spread;
                    var c00 = CornerColour(x, z); var c10 = CornerColour(x + 1, z); var c01 = CornerColour(x, z + 1); var c11 = CornerColour(x + 1, z + 1);
                    // two triangles per cell, each with the average of its corner colours (flat shaded)
                    mb.Tri(new Vector3(x0, h00, z0), new Vector3(x1, h11, z1), new Vector3(x1, h10, z0), Avg(c00, c10, c11));
                    mb.Tri(new Vector3(x0, h00, z0), new Vector3(x0, h01, z1), new Vector3(x1, h11, z1), Avg(c00, c11, c01));
                    // what stands on the cell
                    float px = x0, pz = z0, ph = h00;
                    if (tree3[k] == 1 || tree3[k] == 2) Tree(k, px, ph, pz, tree3[k]);
                    if (house3[k] >= 20 && house3[k] < 24) Tower(k, px, ph, pz);
                    else if (house3[k] == 30) HomeMesh(k, px, ph, pz);
                    else if (house3[k] >= 1 && house3[k] <= 4) SimpleHouse(k, px, ph, pz);
                    else if (house3[k] == 10) FuelTank(px, ph, pz);
                }
            // village cover on this chunk
            foreach (var c in cover)
            {
                int ccx = Mathf.FloorToInt(c.x / BS / CH), ccz = Mathf.FloorToInt(c.z / BS / CH);
                if (ccx != cx || ccz != cz) continue;
                CoverMesh(c);
            }
        }

        static Color32 Avg(Color32 a, Color32 b, Color32 c) { return new Color32((byte)((a.r + b.r + c.r) / 3), (byte)((a.g + b.g + c.g) / 3), (byte)((a.b + b.b + c.b) / 3), 255); }

        void Tree(int k, float x, float h, float z, int type)
        {
            float s = 0.85f + 0.5f * Hash(k + 0.5f), rot = Hash(k + 2.2f) * Mathf.PI * 2f;
            var trunk = MeshBuilder.Rgb(74, 58, 42);
            mb.Box(x - 1.3f, x + 1.3f, h - 2f, h + 9f * s, z - 1.3f, z + 1.3f, trunk, true);
            if (type == 1)
            {
                mb.Prism(x, h + 6f * s, h + 19f * s, z, 8f * s, 2.6f * s, 7, rot, MeshBuilder.Rgb(63, 97, 54));
                mb.Prism(x, h + 16f * s, h + 31f * s, z, 5.6f * s, 0f, 7, rot + 0.4f, MeshBuilder.Rgb(70, 108, 58));
            }
            else mb.Blob(x, h + 17f * s, z, 9f * s, 10f * s, 9f * s, 7, MeshBuilder.Rgb(74, 118, 60));
            AddCollider(WM(x, h + 4.5f * s, z), new Vector3(2.6f * U, 9f * s * U, 2.6f * U), "Tree");
        }

        static Vector3 WM(float x, float y, float z) { return new Vector3(x * U, y * U, z * U); }   // spread units (chunk space) to metres

        void AddCollider(Vector3 worldCentre, Vector3 size, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.position = worldCentre;
            var bc = go.AddComponent<BoxCollider>(); bc.size = size;
        }

        void Tower(int k, float x, float h, float z)
        {
            int look = (house3[k] - 20) % 4; var col = TOWER_COL[look];
            float hw = bhw[k] - 1f; int floors = bfl[k]; float top = floors * FLOOR_H;
            mb.Box(x - hw, x + hw, h - 3f, h, z - hw, z + hw, col, true);
            mb.Box(x - hw - 0.4f, x + hw + 0.4f, h, h + 5f, z - hw - 0.4f, z + hw + 0.4f, MeshBuilder.Mul(col, 0.8f), true);
            mb.Box(x - hw, x + hw, h, h + top, z - hw, z + hw, col, true);
            // window bands
            int stride = mobile ? 2 : 1;
            var glass = MeshBuilder.Rgb(104, 128, 142); var lit = MeshBuilder.Emissive(MeshBuilder.Rgb(240, 214, 150));
            for (int f = 0; f < floors; f += stride)
            {
                float y0 = h + f * FLOOR_H + 4.5f, y1 = y0 + 5f;
                var gc = Hash(k + f * 3.3f) > 0.78f ? lit : glass;
                mb.Box(x - hw + 2f, x + hw - 2f, y0, y1, z - hw - 0.25f, z + hw + 0.25f, gc);
                mb.Box(x - hw - 0.25f, x + hw + 0.25f, y0, y1, z - hw + 2f, z + hw - 2f, gc);
            }
            // cornices every 6th floor, parapet, roof furniture
            for (int f = 6; f < floors; f += 6) { float y = h + f * FLOOR_H; mb.Box(x - hw - 0.8f, x + hw + 0.8f, y, y + 1.2f, z - hw - 0.8f, z + hw + 0.8f, MeshBuilder.Mul(col, 0.86f)); }
            float ty = h + top;
            mb.Box(x - hw, x + hw, ty, ty + 1.4f, z - hw, z - hw + 1f, col); mb.Box(x - hw, x + hw, ty, ty + 1.4f, z + hw - 1f, z + hw, col);
            mb.Box(x - hw, x - hw + 1f, ty, ty + 1.4f, z - hw, z + hw, col); mb.Box(x + hw - 1f, x + hw, ty, ty + 1.4f, z - hw, z + hw, col);
            mb.Box(x - 3.5f, x + 3.5f, ty, ty + 4f, z - 3.5f, z + 3.5f, MeshBuilder.Mul(col, 0.9f), true);
            if (!mobile)
            {
                mb.Box(x + hw * 0.4f - 2.5f, x + hw * 0.4f + 2.5f, ty, ty + 6f, z - hw * 0.4f - 2.5f, z - hw * 0.4f + 2.5f, MeshBuilder.Rgb(150, 146, 138), true);
                mb.Box(x - 0.3f, x + 0.3f, ty, ty + 18f, z - 0.3f, z + 0.3f, MeshBuilder.Rgb(60, 60, 64));
            }
            var wc = WM(x, h + top * 0.5f, z);
            AddCollider(wc, new Vector3(hw * 2f * U, (top + 3f) * U, hw * 2f * U), "Tower");
        }

        void HomeMesh(int k, float x, float h, float z)
        {
            var H = homes[k];
            float lx = H.lx, lz = H.lz, wh = H.hall ? 16f * 1.55f : 16f, t = 1.3f, rh = 7f, o = 1.5f;
            var wall = HOUSE_WALL[H.wc]; var roof = HOUSE_ROOF[H.rf];
            mb.Box(x - lx, x + lx, h - 3f, h + 0.35f, z - lz, z + lz, MeshBuilder.Rgb(156, 126, 96), true);
            // four walls with a doorway in the door wall (0 +x, 1 -x, 2 +z, 3 -z) and a window in each of the others
            float dw = 4.5f, dh = 13f;
            void WallX(float wx, bool door)
            {
                if (door)
                {
                    mb.Box(wx - t, wx + t, h, h + wh, z - lz, z - dw, wall, true); mb.Box(wx - t, wx + t, h, h + wh, z + dw, z + lz, wall, true);
                    mb.Box(wx - t, wx + t, h + dh, h + wh, z - dw, z + dw, wall, true);
                }
                else
                {
                    mb.Box(wx - t, wx + t, h, h + wh, z - lz, z + lz, wall, true);
                    mb.Box(wx - t - 0.2f, wx + t + 0.2f, h + 6f, h + 11f, z - 2.3f, z + 2.3f, MeshBuilder.Rgb(118, 146, 164));
                }
            }
            void WallZ(float wz, bool door)
            {
                if (door)
                {
                    mb.Box(x - lx, x - dw, h, h + wh, wz - t, wz + t, wall, true); mb.Box(x + dw, x + lx, h, h + wh, wz - t, wz + t, wall, true);
                    mb.Box(x - dw, x + dw, h + dh, h + wh, wz - t, wz + t, wall, true);
                }
                else
                {
                    mb.Box(x - lx, x + lx, h, h + wh, wz - t, wz + t, wall, true);
                    mb.Box(x - 2.3f, x + 2.3f, h + 6f, h + 11f, wz - t - 0.2f, wz + t + 0.2f, MeshBuilder.Rgb(118, 146, 164));
                }
            }
            WallX(x + lx, H.door == 0); WallX(x - lx, H.door == 1); WallZ(z + lz, H.door == 2); WallZ(z - lz, H.door == 3);
            // pitched roof, ridge along the door's axis
            bool ridgeX = H.door >= 2;
            float ry = h + wh;
            if (ridgeX)
            {
                mb.Quad(new Vector3(x - lx - o, ry, z - lz - o), new Vector3(x - lx - o, ry + rh, z), new Vector3(x + lx + o, ry + rh, z), new Vector3(x + lx + o, ry, z - lz - o), roof);
                mb.Quad(new Vector3(x + lx + o, ry, z + lz + o), new Vector3(x + lx + o, ry + rh, z), new Vector3(x - lx - o, ry + rh, z), new Vector3(x - lx - o, ry, z + lz + o), roof);
                mb.Tri(new Vector3(x + lx, ry, z - lz), new Vector3(x + lx, ry + rh, z), new Vector3(x + lx, ry, z + lz), wall);
                mb.Tri(new Vector3(x - lx, ry, z + lz), new Vector3(x - lx, ry + rh, z), new Vector3(x - lx, ry, z - lz), wall);
            }
            else
            {
                mb.Quad(new Vector3(x + lx + o, ry, z - lz - o), new Vector3(x, ry + rh, z - lz - o), new Vector3(x, ry + rh, z + lz + o), new Vector3(x + lx + o, ry, z + lz + o), roof);
                mb.Quad(new Vector3(x - lx - o, ry, z + lz + o), new Vector3(x, ry + rh, z + lz + o), new Vector3(x, ry + rh, z - lz - o), new Vector3(x - lx - o, ry, z - lz - o), roof);
                mb.Tri(new Vector3(x - lx, ry, z - lz), new Vector3(x, ry + rh, z - lz), new Vector3(x + lx, ry, z - lz), wall);
                mb.Tri(new Vector3(x + lx, ry, z + lz), new Vector3(x, ry + rh, z + lz), new Vector3(x - lx, ry, z + lz), wall);
            }
            mb.Box(x + lx * 0.5f - 1.2f, x + lx * 0.5f + 1.2f, ry + 2f, ry + rh + 2f, z - 1.2f, z + 1.2f, MeshBuilder.Rgb(120, 90, 80), true);
            // colliders: the four walls (with the doorway left open) and the roof slab
            var c = new Vector3(x, 0f, z);
            float wy = h + wh * 0.5f;
            if (H.door != 0) AddCollider(WM(c.x + lx, wy, c.z), new Vector3(2f * t * U, wh * U, 2f * lz * U), "Wall"); else SplitWall(c.x + lx, wy, c.z, lz, dw, dh, wh, t, true);
            if (H.door != 1) AddCollider(WM(c.x - lx, wy, c.z), new Vector3(2f * t * U, wh * U, 2f * lz * U), "Wall"); else SplitWall(c.x - lx, wy, c.z, lz, dw, dh, wh, t, true);
            if (H.door != 2) AddCollider(WM(c.x, wy, c.z + lz), new Vector3(2f * lx * U, wh * U, 2f * t * U), "Wall"); else SplitWall(c.x, wy, c.z + lz, lx, dw, dh, wh, t, false);
            if (H.door != 3) AddCollider(WM(c.x, wy, c.z - lz), new Vector3(2f * lx * U, wh * U, 2f * t * U), "Wall"); else SplitWall(c.x, wy, c.z - lz, lx, dw, dh, wh, t, false);
            AddCollider(WM(c.x, ry + rh * 0.5f, c.z), new Vector3((2f * lx + 2f * o) * U, rh * U, (2f * lz + 2f * o) * U), "Roof");
            AddCollider(WM(c.x, h - 1.3f, c.z), new Vector3(2f * lx * U, 3.3f * U, 2f * lz * U), "Floor");
        }

        void SplitWall(float wx, float wy, float wz, float half, float dw, float dh, float wh, float t, bool alongZ)
        {
            // a wall with a doorway: two side pieces and a lintel
            float side = (half - dw) * 0.5f;
            if (alongZ)
            {
                AddCollider(WM(wx, wy, wz - dw - side), new Vector3(2f * t * U, wh * U, 2f * side * U), "Wall");
                AddCollider(WM(wx, wy, wz + dw + side), new Vector3(2f * t * U, wh * U, 2f * side * U), "Wall");
                AddCollider(WM(wx, wy + (wh * 0.5f - (wh - dh) * 0.5f) + 0f, wz), new Vector3(2f * t * U, (wh - dh) * U, 2f * dw * U), "Lintel");
            }
            else
            {
                AddCollider(WM(wx - dw - side, wy, wz), new Vector3(2f * side * U, wh * U, 2f * t * U), "Wall");
                AddCollider(WM(wx + dw + side, wy, wz), new Vector3(2f * side * U, wh * U, 2f * t * U), "Wall");
                AddCollider(WM(wx, wy + (wh * 0.5f - (wh - dh) * 0.5f), wz), new Vector3(2f * dw * U, (wh - dh) * U, 2f * t * U), "Lintel");
            }
        }

        void SimpleHouse(int k, float x, float h, float z)
        {
            int kind = (house3[k] - 1) % 4;
            float lx = (kind & 1) != 0 ? 13f : 9f, lz = (kind & 1) != 0 ? 9f : 13f, y0 = h - 3f, y1 = h + 11f, rh = 8f, o = 1.6f;
            var wall = HOUSE_WALL[kind]; var roof = HOUSE_ROOF[kind];
            mb.Box(x - lx, x + lx, y0, y1, z - lz, z + lz, wall, true);
            bool ridgeX = lx > lz;
            if (ridgeX)
            {
                mb.Quad(new Vector3(x - lx - o, y1, z - lz - o), new Vector3(x - lx - o, y1 + rh, z), new Vector3(x + lx + o, y1 + rh, z), new Vector3(x + lx + o, y1, z - lz - o), roof);
                mb.Quad(new Vector3(x + lx + o, y1, z + lz + o), new Vector3(x + lx + o, y1 + rh, z), new Vector3(x - lx - o, y1 + rh, z), new Vector3(x - lx - o, y1, z + lz + o), roof);
                mb.Tri(new Vector3(x + lx, y1, z - lz), new Vector3(x + lx, y1 + rh, z), new Vector3(x + lx, y1, z + lz), wall);
                mb.Tri(new Vector3(x - lx, y1, z + lz), new Vector3(x - lx, y1 + rh, z), new Vector3(x - lx, y1, z - lz), wall);
                mb.Box(x + lx, x + lx + 0.3f, h, h + 9.5f, z - 1.6f, z + 1.6f, MeshBuilder.Rgb(74, 58, 42));
            }
            else
            {
                mb.Quad(new Vector3(x + lx + o, y1, z - lz - o), new Vector3(x, y1 + rh, z - lz - o), new Vector3(x, y1 + rh, z + lz + o), new Vector3(x + lx + o, y1, z + lz + o), roof);
                mb.Quad(new Vector3(x - lx - o, y1, z + lz + o), new Vector3(x, y1 + rh, z + lz + o), new Vector3(x, y1 + rh, z - lz - o), new Vector3(x - lx - o, y1, z - lz - o), roof);
                mb.Tri(new Vector3(x - lx, y1, z - lz), new Vector3(x, y1 + rh, z - lz), new Vector3(x + lx, y1, z - lz), wall);
                mb.Tri(new Vector3(x + lx, y1, z + lz), new Vector3(x, y1 + rh, z + lz), new Vector3(x - lx, y1, z + lz), wall);
                mb.Box(x - 1.6f, x + 1.6f, h, h + 9.5f, z + lz, z + lz + 0.3f, MeshBuilder.Rgb(74, 58, 42));
            }
            AddCollider(WM(x, (y0 + y1 + rh) * 0.5f, z), new Vector3(2f * lx * U, (y1 + rh - y0) * U, 2f * lz * U), "House");
        }

        void FuelTank(float x, float h, float z)
        {
            mb.Prism(x, h - 2f, h + 12f, z, 8f, 8f, 10, 0f, MeshBuilder.Rgb(226, 226, 214));
            mb.Prism(x, h + 12f, h + 15.5f, z, 8f, 0f, 10, 0f, MeshBuilder.Rgb(206, 206, 196));
            mb.Prism(x, h + 5f, h + 7.2f, z, 8.15f, 8.15f, 10, 0f, MeshBuilder.Rgb(181, 69, 47));
            mb.Box(x + 8f, x + 9f, h, h + 12f, z - 1f, z + 1f, MeshBuilder.Rgb(74, 85, 96));
            AddCollider(WM(x, h + 5f, z), new Vector3(16f * U, 14f * U, 16f * U), "Tank");
        }

        void CoverMesh(Cover c)
        {
            float h = HAt0(c.x, c.z), x = (c.x - MID3) * Spread, z = (c.z - MID3) * Spread;
            switch (c.kind)
            {
                case "car":
                    {
                        float L = 13f, W = 6f; var tone = MeshBuilder.Rgb(96, 52, 40);
                        mb.BoxYaw(x, h + 2f + 1.3f, z, L, 2.6f, W, c.yaw, tone, true);
                        mb.BoxYaw(x, h + 2f + 2.6f + 1.15f, z, L * 0.46f, 2.3f, W * 0.86f, c.yaw, MeshBuilder.Mul(tone, 0.7f), true);
                        mb.BoxYaw(x, h + 2f + 2.6f + 1.15f, z, L * 0.47f, 1.2f, W * 0.9f, c.yaw, MeshBuilder.Rgb(16, 16, 20));
                        float cs = Mathf.Cos(c.yaw), sn = Mathf.Sin(c.yaw);
                        for (int i = 0; i < 4; i++) { float dx = (i < 2 ? 1f : -1f) * 0.32f * L, dz = (i % 2 == 0 ? 1f : -1f) * 0.48f * W; mb.BoxYaw(x + dx * cs - dz * sn, h + 1.1f, z + dx * sn + dz * cs, 2.4f, 2.2f, 0.9f, c.yaw, MeshBuilder.Rgb(28, 28, 30)); }
                        AddColliderYaw(x, h + 3f, z, L, 6.5f, W, c.yaw);
                    }
                    break;
                case "barrier":
                    mb.BoxYaw(x, h + 1f, z, 15f, 2f, 3f, c.yaw, MeshBuilder.Rgb(150, 148, 140), true);
                    mb.BoxYaw(x, h + 2f + 1.6f, z, 15f, 3.2f, 1.5f, c.yaw, MeshBuilder.Rgb(158, 156, 148), true);
                    AddColliderYaw(x, h + 2.75f, z, 15f, 5.5f, 3f, c.yaw);
                    break;
                default:
                    {
                        float L = 15f, W = 4f, bl = L / 5f - 0.3f; var bag = MeshBuilder.Rgb(122, 110, 78);
                        float cs = Mathf.Cos(c.yaw), sn = Mathf.Sin(c.yaw);
                        for (int row = 0; row < 3; row++)
                            for (int i = 0; i < 5; i++)
                            {
                                float dx = (i - 2) * (L / 5f) + (row % 2 == 1 ? L / 10f : 0f);
                                mb.BoxYaw(x + dx * cs, h + 0.95f + row * 1.9f, z + dx * sn, bl, 1.9f, W, c.yaw, MeshBuilder.Mul(bag, 1f - row * 0.05f), true);
                            }
                        AddColliderYaw(x, h + 3f, z, L, 6f, W, c.yaw);
                    }
                    break;
            }
        }

        void AddColliderYaw(float x, float y, float z, float len, float h, float wid, float yaw)
        {
            var go = new GameObject("Cover");
            go.transform.SetParent(transform, false);
            go.transform.position = WM(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, -yaw * Mathf.Rad2Deg, 0f);
            var bc = go.AddComponent<BoxCollider>(); bc.size = new Vector3(len * U, h * U, wid * U);
        }

        void BuildWater()
        {
            var w = GameObject.CreatePrimitive(PrimitiveType.Plane);
            w.name = "Sea";
            w.transform.SetParent(transform, false);
            w.transform.position = new Vector3(0f, SEA * U, 0f);
            w.transform.localScale = new Vector3(12000f * U / 10f, 1f, 12000f * U / 10f);
            Destroy(w.GetComponent<Collider>());
            var r = w.GetComponent<Renderer>();
            if (water != null) { r.sharedMaterial = water; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
        }

        void BuildRidges()
        {
            mb.Clear();
            Ridge(13500f, 150f, 900f, 5f, 13f, 1.1f, MeshBuilder.Emissive(MeshBuilder.Rgb(168, 188, 186)));
            Ridge(11000f, 90f, 520f, 7f, 19f, 4.2f, MeshBuilder.Emissive(MeshBuilder.Rgb(140, 165, 148)));
            var go = mb.Spawn("Ridges", transform, lowPoly, false, false);
        }

        void Ridge(float R, float b, float amp, float f1, float f2, float ph, Color32 col)
        {
            const int segs = 120;
            for (int i = 0; i < segs; i++)
            {
                float a0 = i / (float)segs * Mathf.PI * 2f, a1 = (i + 1) / (float)segs * Mathf.PI * 2f;
                float h0 = b + amp * (0.55f + 0.3f * Mathf.Sin(a0 * f1 + ph) + 0.15f * Mathf.Sin(a0 * f2 + ph * 2.3f));
                float h1 = b + amp * (0.55f + 0.3f * Mathf.Sin(a1 * f1 + ph) + 0.15f * Mathf.Sin(a1 * f2 + ph * 2.3f));
                var p0 = new Vector3(Mathf.Cos(a0) * R, -200f, Mathf.Sin(a0) * R); var p1 = new Vector3(Mathf.Cos(a1) * R, -200f, Mathf.Sin(a1) * R);
                // facing inward
                mb.Quad(p1, new Vector3(p1.x, h1, p1.z), new Vector3(p0.x, h0, p0.z), p0, col);
            }
        }

        void BuildClouds()
        {
            mb.Clear();
            int lowN = mobile ? 14 : 26, hiN = mobile ? 10 : 22;
            for (int i = 0; i < lowN; i++)
            {
                float x = (Hash(i + 1.1f) * (WORLD3 + 2400f) - 1200f - MID3) * Spread, z = (Hash(i + 7.7f) * (WORLD3 + 2400f) - 1200f - MID3) * Spread, y = 560f + Hash(i + 3.3f) * 220f;
                float sx = 90f + Hash(i + 5f) * 130f, sz = 60f + Hash(i + 9f) * 90f;
                mb.Blob(x, y, z, sx, 30f, sz, 9, MeshBuilder.Emissive(MeshBuilder.Rgb(250, 252, 251)));
                mb.Blob(x + sx * 0.55f, y - 6f, z + sz * 0.2f, sx * 0.62f, 22f, sz * 0.62f, 9, MeshBuilder.Emissive(MeshBuilder.Rgb(246, 249, 248)));
                mb.Blob(x - sx * 0.5f, y - 8f, z - sz * 0.15f, sx * 0.5f, 18f, sz * 0.55f, 9, MeshBuilder.Emissive(MeshBuilder.Rgb(244, 247, 246)));
            }
            for (int i = 0; i < hiN; i++)
            {
                float x = (Hash(i + 41.1f) * (WORLD3 + 3600f) - 1800f - MID3) * Spread, z = (Hash(i + 47.7f) * (WORLD3 + 3600f) - 1800f - MID3) * Spread, y = 1400f + Hash(i + 43.1f) * 420f;
                float sx = 130f + Hash(i + 45f) * 190f, sz = 90f + Hash(i + 49f) * 140f;
                mb.Blob(x, y, z, sx, 40f, sz, 9, MeshBuilder.Emissive(MeshBuilder.Rgb(252, 253, 252)));
                mb.Blob(x + sx * 0.5f, y - 8f, z - sz * 0.25f, sx * 0.6f, 28f, sz * 0.6f, 9, MeshBuilder.Emissive(MeshBuilder.Rgb(247, 250, 249)));
            }
            mb.Spawn("Clouds", transform, lowPoly, false, false);
        }
    }
}
