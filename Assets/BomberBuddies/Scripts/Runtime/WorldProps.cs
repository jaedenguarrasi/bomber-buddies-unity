using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>What the island is scattered with: a village on the plateau, towers, cover, woods and boulders. Built from primitives
    /// for now; every piece is a plain GameObject so it can be swapped for a modelled prefab later without touching the rest.</summary>
    public static class WorldProps
    {
        public class WorldInfo
        {
            public Vector3 villageCenter;
            public float villageRadius;
            public List<Vector3> coverSpots = new List<Vector3>();
        }

        static Material M(MaterialLibrary lib, string name) { return lib != null ? lib.Get(name) : null; }

        static GameObject Prim(PrimitiveType t, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool keepCollider = true)
        {
            var go = GameObject.CreatePrimitive(t);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            if (mat != null) r.sharedMaterial = mat;
            if (!keepCollider) { var c = go.GetComponent<Collider>(); if (c != null) Object.DestroyImmediate(c); }
            return go;
        }

        public static WorldInfo Build(Transform parent, Terrain terrain, int seed, float size, float seaLevel, MaterialLibrary lib, int treeCount, int rockCount)
        {
            var info = new WorldInfo { villageCenter = Vector3.zero, villageRadius = size * 0.06f };
            var rng = new System.Random(seed);
            float Rnd(float a, float b) { return a + (float)rng.NextDouble() * (b - a); }

            var village = new GameObject("Village").transform; village.SetParent(parent, false);
            var woods = new GameObject("Woods").transform; woods.SetParent(parent, false);
            var rocks = new GameObject("Rocks").transform; rocks.SetParent(parent, false);

            // ---- the village: houses on a loose grid round a square, two towers, cover between them ----
            float vr = info.villageRadius;
            var wall = M(lib, "wall"); var roof = M(lib, "roof"); var dark = M(lib, "dark"); var glow = M(lib, "glow"); var wood = M(lib, "wood"); var concrete = M(lib, "concrete");
            int houseCount = 0;
            for (int gx = -2; gx <= 2; gx++)
            {
                for (int gz = -2; gz <= 2; gz++)
                {
                    if (Mathf.Abs(gx) <= 0 && Mathf.Abs(gz) <= 0) continue;   // the square in the middle stays open
                    float px = gx * vr * 0.42f + Rnd(-4f, 4f), pz = gz * vr * 0.42f + Rnd(-4f, 4f);
                    if (Mathf.Abs(gx) == 2 && Mathf.Abs(gz) == 2 && rng.NextDouble() < 0.5) continue;
                    float w = Rnd(8f, 12f), d = Rnd(8f, 12f), h = Rnd(5f, 7f);
                    float gy = IslandBuilder.GroundHeight(terrain, px, pz);
                    var house = new GameObject("House " + houseCount++).transform; house.SetParent(village, false); house.position = new Vector3(px, gy, pz);
                    house.rotation = Quaternion.Euler(0f, Rnd(-8f, 8f) + (gx % 2 == 0 ? 0f : 90f), 0f);
                    Prim(PrimitiveType.Cube, "Walls", house, house.position + Vector3.up * (h * 0.5f - 0.3f), new Vector3(w, h + 0.6f, d), wall);
                    var slab = Prim(PrimitiveType.Cube, "RoofSlab", house, house.position + Vector3.up * (h + 0.3f), new Vector3(w + 1.2f, 0.5f, d + 1.2f), roof);
                    slab.transform.rotation = house.rotation;
                    var gable = Prim(PrimitiveType.Cube, "Gable", house, house.position + Vector3.up * (h + 0.3f), new Vector3(w * 0.7f, w * 0.7f, d + 1.0f), roof, false);
                    gable.transform.rotation = house.rotation * Quaternion.Euler(0f, 0f, 45f);
                    house.GetChild(0).rotation = house.rotation;
                    // a door and lit windows on the long sides
                    var fwd = house.forward; var right = house.right;
                    Prim(PrimitiveType.Cube, "Door", house, house.position + fwd * (d * 0.5f + 0.05f) + Vector3.up * 1.2f, new Vector3(1.4f, 2.4f, 0.12f), dark, false).transform.rotation = house.rotation;
                    for (int s = -1; s <= 1; s += 2)
                    {
                        for (int k = -1; k <= 1; k += 2)
                        {
                            var win = Prim(PrimitiveType.Cube, "Window", house, house.position + right * (w * 0.5f * s + 0.06f * s) + fwd * (d * 0.28f * k) + Vector3.up * (h * 0.55f), new Vector3(0.12f, 1.1f, 1.3f), glow, false);
                            win.transform.rotation = house.rotation;
                        }
                    }
                }
            }
            // towers at two corners of the square
            for (int i = 0; i < 2; i++)
            {
                float px = (i == 0 ? -1f : 1f) * vr * 0.95f, pz = (i == 0 ? 1f : -1f) * vr * 0.95f;
                float gy = IslandBuilder.GroundHeight(terrain, px, pz);
                var tower = new GameObject("Tower " + i).transform; tower.SetParent(village, false); tower.position = new Vector3(px, gy, pz);
                Prim(PrimitiveType.Cube, "Shaft", tower, tower.position + Vector3.up * 11f, new Vector3(7f, 23f, 7f), concrete);
                Prim(PrimitiveType.Cube, "Ledge", tower, tower.position + Vector3.up * 22.6f, new Vector3(9f, 0.8f, 9f), dark);
                Prim(PrimitiveType.Cube, "Parapet", tower, tower.position + Vector3.up * 23.6f, new Vector3(9f, 1.2f, 0.4f), concrete).transform.position += Vector3.forward * 4.3f;
                Prim(PrimitiveType.Cube, "Parapet", tower, tower.position + Vector3.up * 23.6f, new Vector3(9f, 1.2f, 0.4f), concrete).transform.position += Vector3.back * 4.3f;
                Prim(PrimitiveType.Cube, "Parapet", tower, tower.position + Vector3.up * 23.6f, new Vector3(0.4f, 1.2f, 9f), concrete).transform.position += Vector3.right * 4.3f;
                Prim(PrimitiveType.Cube, "Parapet", tower, tower.position + Vector3.up * 23.6f, new Vector3(0.4f, 1.2f, 9f), concrete).transform.position += Vector3.left * 4.3f;
            }
            // cover: low walls and crates in the square and the streets
            for (int i = 0; i < 26; i++)
            {
                float a = Rnd(0f, Mathf.PI * 2f), rr = Rnd(6f, vr * 1.1f);
                float px = Mathf.Cos(a) * rr, pz = Mathf.Sin(a) * rr, gy = IslandBuilder.GroundHeight(terrain, px, pz);
                var pos = new Vector3(px, gy, pz);
                if (rng.NextDouble() < 0.55)
                {
                    var g = Prim(PrimitiveType.Cube, "LowWall", village, pos + Vector3.up * 0.6f, new Vector3(Rnd(3f, 5f), 1.2f, 0.5f), concrete);
                    g.transform.rotation = Quaternion.Euler(0f, Rnd(0f, 180f), 0f);
                }
                else
                {
                    float cs = Rnd(1.1f, 1.5f);
                    Prim(PrimitiveType.Cube, "Crate", village, pos + Vector3.up * (cs * 0.5f), Vector3.one * cs, wood).transform.rotation = Quaternion.Euler(0f, Rnd(0f, 90f), 0f);
                    if (rng.NextDouble() < 0.4) Prim(PrimitiveType.Cube, "Crate", village, pos + Vector3.up * (cs * 1.5f) + Vector3.right * 0.2f, Vector3.one * cs * 0.9f, wood).transform.rotation = Quaternion.Euler(0f, Rnd(0f, 90f), 0f);
                }
                info.coverSpots.Add(pos);
            }

            // ---- woods: trees where the ground is gentle and dry, outside the village ----
            var bark = M(lib, "bark"); var leaves = M(lib, "leaves"); var leaves2 = M(lib, "leaves2");
            int placed = 0, tries = 0;
            while (placed < treeCount && tries < treeCount * 12)
            {
                tries++;
                float px = Rnd(-size * 0.47f, size * 0.47f), pz = Rnd(-size * 0.47f, size * 0.47f);
                if (Mathf.Sqrt(px * px + pz * pz) < vr * 1.35f) continue;
                float gy = IslandBuilder.GroundHeight(terrain, px, pz);
                if (gy < seaLevel + 2.5f || IslandBuilder.Steepness(terrain, px, pz) > 27f) continue;
                if (Noise.Fbm(px * 0.012f + 50f, pz * 0.012f + 50f, 3, 2f, 0.5f, seed + 4) < 0.45f) continue;   // woods come in clumps, with clearings
                float th = Rnd(3.2f, 5.5f), tr = Rnd(0.22f, 0.4f), cr = Rnd(1.6f, 2.7f);
                var tree = new GameObject("Tree").transform; tree.SetParent(woods, false); tree.position = new Vector3(px, gy, pz);
                Prim(PrimitiveType.Cylinder, "Trunk", tree, tree.position + Vector3.up * (th * 0.5f), new Vector3(tr * 2f, th * 0.5f, tr * 2f), bark);
                var lm = rng.NextDouble() < 0.5 ? leaves : leaves2;
                Prim(PrimitiveType.Sphere, "Canopy", tree, tree.position + Vector3.up * (th + cr * 0.6f), new Vector3(cr * 2f, cr * 1.7f, cr * 2f), lm, false);
                Prim(PrimitiveType.Sphere, "Canopy", tree, tree.position + Vector3.up * (th + cr * 1.3f) + new Vector3(Rnd(-0.6f, 0.6f), 0f, Rnd(-0.6f, 0.6f)), new Vector3(cr * 1.4f, cr * 1.3f, cr * 1.4f), lm, false);
                placed++;
            }

            // ---- boulders ----
            var rockMat = M(lib, "rock");
            for (int i = 0; i < rockCount; i++)
            {
                float px = Rnd(-size * 0.47f, size * 0.47f), pz = Rnd(-size * 0.47f, size * 0.47f);
                if (Mathf.Sqrt(px * px + pz * pz) < vr * 1.3f) continue;
                float gy = IslandBuilder.GroundHeight(terrain, px, pz);
                if (gy < seaLevel + 0.5f) continue;
                float rs = Rnd(1.2f, 4f);
                var rock = Prim(PrimitiveType.Sphere, "Boulder", rocks, new Vector3(px, gy + rs * 0.25f, pz), new Vector3(rs * Rnd(0.8f, 1.3f), rs * 0.7f, rs * Rnd(0.8f, 1.3f)), rockMat);
                rock.transform.rotation = Quaternion.Euler(Rnd(-15f, 15f), Rnd(0f, 360f), Rnd(-15f, 15f));
            }

            // batch what never moves: far fewer draw calls on a phone
            StaticBatchingUtility.Combine(village.gameObject);
            StaticBatchingUtility.Combine(woods.gameObject);
            StaticBatchingUtility.Combine(rocks.gameObject);
            return info;
        }
    }
}
