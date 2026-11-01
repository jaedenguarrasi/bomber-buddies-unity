using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// Textures painted in code at start-up: ground, rock, sand, bark, concrete, roof tiles, camouflage. Everything tiles.
    /// They stand in until photographed PBR textures are added; the materials that use them keep working either way.
    /// </summary>
    public static class ProceduralTextures
    {
        public static bool mobile;
        public delegate Color Painter(float u, float v);

        public static Texture2D Make(int size, Painter paint, bool linear = false)
        {
            if (mobile) size = Mathf.Max(64, size / 2);
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, linear);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++) px[y * size + x] = paint(x / (float)size, y / (float)size);
            }
            tex.SetPixels(px);
            tex.wrapMode = TextureWrapMode.Repeat;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.Apply(true, false);
            return tex;
        }

        // tiling noise: sample on a torus so the edges meet
        static float TNoise(float u, float v, float scale, int seed, int octaves = 4)
        {
            float a = u * Mathf.PI * 2f, b = v * Mathf.PI * 2f;
            float x = Mathf.Cos(a) * scale + 100f, y = Mathf.Sin(a) * scale + 100f, z = Mathf.Cos(b) * scale + 200f, w = Mathf.Sin(b) * scale + 200f;
            return (Noise.Fbm(x, z, octaves, 2f, 0.5f, seed) + Noise.Fbm(y, w, octaves, 2f, 0.5f, seed + 11)) * 0.5f;
        }

        public static Texture2D Grass(int seed)
        {
            return Make(256, (u, v) =>
            {
                float n = TNoise(u, v, 3f, seed), d = TNoise(u, v, 11f, seed + 3, 3);
                var a = new Color(0.30f, 0.46f, 0.18f);
                var b = new Color(0.52f, 0.60f, 0.26f);
                var c = Color.Lerp(a, b, n) * (0.82f + 0.36f * d);
                return new Color(c.r, c.g, c.b, 1f);
            });
        }

        public static Texture2D Rock(int seed)
        {
            return Make(256, (u, v) =>
            {
                float n = TNoise(u, v, 2.2f, seed), d = TNoise(u, v, 9f, seed + 5, 3);
                float g = 0.38f + 0.3f * n + 0.14f * d;
                return new Color(g * 1.02f, g, g * 0.94f, 1f);
            });
        }

        public static Texture2D Sand(int seed)
        {
            return Make(256, (u, v) =>
            {
                float n = TNoise(u, v, 4f, seed, 3), d = TNoise(u, v, 14f, seed + 7, 2);
                float g = 0.78f + 0.12f * n + 0.08f * d;
                return new Color(g, g * 0.92f, g * 0.74f, 1f);
            });
        }

        public static Texture2D Bark(int seed)
        {
            return Make(128, (u, v) =>
            {
                float n = TNoise(u * 3f, v, 2f, seed, 3);
                float g = 0.30f + 0.22f * n;
                return new Color(g * 1.1f, g * 0.86f, g * 0.62f, 1f);
            });
        }

        public static Texture2D Concrete(int seed)
        {
            return Make(256, (u, v) =>
            {
                float n = TNoise(u, v, 3f, seed, 4), d = TNoise(u, v, 15f, seed + 2, 2);
                float g = 0.55f + 0.18f * n + 0.08f * d;
                float line = (Mathf.Repeat(v * 4f, 1f) < 0.03f || Mathf.Repeat(u * 2f, 1f) < 0.015f) ? 0.78f : 1f;
                g *= line;
                return new Color(g, g * 0.99f, g * 0.96f, 1f);
            });
        }

        public static Texture2D RoofTiles(int seed)
        {
            return Make(256, (u, v) =>
            {
                float row = Mathf.Floor(v * 12f);
                float uu = Mathf.Repeat(u * 8f + (row % 2f) * 0.5f, 1f), vv = Mathf.Repeat(v * 12f, 1f);
                float edge = (uu < 0.06f || vv < 0.12f) ? 0.72f : 1f;
                float n = TNoise(u, v, 5f, seed, 3);
                var c = new Color(0.55f, 0.27f, 0.2f) * (0.85f + 0.3f * n) * edge;
                return new Color(c.r, c.g, c.b, 1f);
            });
        }

        /// <summary>Four-tone camouflage. Tones are given dark to light; the pattern is the same for every soldier of a team.</summary>
        public static Texture2D Camo(int seed, Color t0, Color t1, Color t2, Color t3)
        {
            return Make(256, (u, v) =>
            {
                float a = TNoise(u, v, 2.6f, seed, 3), b = TNoise(u, v, 4.1f, seed + 21, 3);
                float d = TNoise(u, v, 16f, seed + 8, 2);
                Color c = a < 0.42f ? t0 : a < 0.55f ? t1 : b < 0.5f ? t2 : t3;
                c *= 0.9f + 0.2f * d;
                return new Color(c.r, c.g, c.b, 1f);
            });
        }

        public static Texture2D Flat(Color c)
        {
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            var px = new Color[16];
            for (int i = 0; i < 16; i++) px[i] = c;
            tex.SetPixels(px);
            tex.Apply();
            return tex;
        }
    }
}
