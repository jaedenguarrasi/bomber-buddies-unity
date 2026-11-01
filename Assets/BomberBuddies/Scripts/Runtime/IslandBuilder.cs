using UnityEngine;

namespace BomberBuddies
{
    /// <summary>Builds the island as a Unity Terrain from noise: a central plateau for the village, hills behind it, beaches all round.</summary>
    public static class IslandBuilder
    {
        public static bool mobile;
        static int HeightRes { get { return mobile ? 257 : 513; } }
        static int SplatRes { get { return mobile ? 128 : 256; } }

        public static Terrain Build(Transform parent, int seed, float size, float maxHeight, float seaLevel, MaterialLibrary lib)
        {
            var td = new TerrainData();
            td.heightmapResolution = HeightRes;
            td.size = new Vector3(size, maxHeight, size);

            var heights = new float[HeightRes, HeightRes];
            for (int y = 0; y < HeightRes; y++)
            {
                for (int x = 0; x < HeightRes; x++)
                {
                    float u = x / (float)(HeightRes - 1), v = y / (float)(HeightRes - 1);
                    heights[y, x] = HeightAt01(u, v, seed, seaLevel / maxHeight);
                }
            }
            td.SetHeights(0, 0, heights);

            // three ground types painted by height and slope: sand by the water, grass on the flats, rock on the steep bits
            var grass = new TerrainLayer { diffuseTexture = ProceduralTextures.Grass(seed), tileSize = new Vector2(9f, 9f) };
            var rock = new TerrainLayer { diffuseTexture = ProceduralTextures.Rock(seed + 1), tileSize = new Vector2(7f, 7f) };
            var sand = new TerrainLayer { diffuseTexture = ProceduralTextures.Sand(seed + 2), tileSize = new Vector2(6f, 6f) };
            td.terrainLayers = new[] { grass, rock, sand };
            td.alphamapResolution = SplatRes;
            var splat = new float[SplatRes, SplatRes, 3];
            for (int y = 0; y < SplatRes; y++)
            {
                for (int x = 0; x < SplatRes; x++)
                {
                    float u = x / (float)(SplatRes - 1), v = y / (float)(SplatRes - 1);
                    float h = td.GetInterpolatedHeight(u, v);
                    float steep = td.GetSteepness(u, v);
                    float rockK = Mathf.InverseLerp(22f, 40f, steep);
                    float sandK = Mathf.Clamp01(1f - Mathf.InverseLerp(seaLevel + 0.5f, seaLevel + 4f, h)) * (1f - rockK);
                    float grassK = Mathf.Max(0f, 1f - rockK - sandK);
                    float n = Noise.Fbm(u * 40f, v * 40f, 2, 2f, 0.5f, seed + 9) - 0.5f;   // a little dapple so the borders are not clean lines
                    rockK = Mathf.Clamp01(rockK + n * 0.2f);
                    grassK = Mathf.Clamp01(grassK - n * 0.2f);
                    float sum = rockK + sandK + grassK;
                    if (sum <= 0f) { grassK = 1f; sum = 1f; }
                    splat[y, x, 0] = grassK / sum;
                    splat[y, x, 1] = rockK / sum;
                    splat[y, x, 2] = sandK / sum;
                }
            }
            td.SetAlphamaps(0, 0, splat);

            var go = Terrain.CreateTerrainGameObject(td);
            go.name = "Island";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-size * 0.5f, 0f, -size * 0.5f);
            var terrain = go.GetComponent<Terrain>();
            var mat = lib != null ? lib.Get("terrain") : null;
            if (mat != null) terrain.materialTemplate = mat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 6f;
            terrain.basemapDistance = 800f;
            terrain.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            return terrain;
        }

        /// <summary>The island's shape, 0..1 of the terrain height. The plateau sits at a fixed height so the village is level.</summary>
        public static float HeightAt01(float u, float v, int seed, float sea01)
        {
            float dx = u - 0.5f, dy = v - 0.5f, r = Mathf.Sqrt(dx * dx + dy * dy) * 2f;   // 0 at the centre, 1 at the edge of the map
            float hills = Noise.Fbm(u * 6f, v * 6f, 5, 2f, 0.5f, seed) * 0.6f + Noise.Ridged(u * 3.5f, v * 3.5f, 4, seed + 1) * 0.4f;
            float edge = Noise.Fbm(u * 9f, v * 9f, 3, 2f, 0.5f, seed + 2) * 0.16f;   // a ragged coastline
            float island = Mathf.Clamp01(1f - Mathf.Pow(Mathf.Max(0f, r - 0.1f) / (0.8f + edge), 2.2f));
            float h = Mathf.Pow(hills, 1.25f) * island * 0.9f;
            // the plateau: a level shelf near the middle for the village, blending out over 60 m or so
            float plateau = sea01 + 0.14f;
            float pk = Mathf.Clamp01(1f - Mathf.InverseLerp(0.16f, 0.30f, r));
            h = Mathf.Lerp(h, Mathf.Max(h * 0.35f + plateau * 0.65f, plateau), pk * pk);
            // the beaches: everything just above sea level is pulled flat, so the water meets a gentle shore
            float shore = Mathf.InverseLerp(sea01 - 0.02f, sea01 + 0.06f, h);
            h = Mathf.Lerp(h, sea01 + (h - sea01) * 0.6f, (1f - shore) * 0.5f);
            return Mathf.Clamp01(h);
        }

        public static float GroundHeight(Terrain terrain, float x, float z)
        {
            var p = new Vector3(x, 0f, z);
            return terrain.SampleHeight(p) + terrain.transform.position.y;
        }

        public static float Steepness(Terrain terrain, float x, float z)
        {
            var td = terrain.terrainData;
            var local = terrain.transform.InverseTransformPoint(new Vector3(x, 0f, z));
            return td.GetSteepness(Mathf.Clamp01(local.x / td.size.x), Mathf.Clamp01(local.z / td.size.z));
        }
    }
}
