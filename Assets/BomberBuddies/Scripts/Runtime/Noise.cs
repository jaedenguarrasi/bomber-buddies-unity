using UnityEngine;

namespace BomberBuddies
{
    /// <summary>Layered Perlin noise, seeded, in the 0..1 range. Used for the island, the textures and the camouflage.</summary>
    public static class Noise
    {
        public static float Fbm(float x, float y, int octaves, float lacunarity, float gain, int seed)
        {
            float ox = (seed * 37 % 1000) * 0.731f, oy = (seed * 91 % 1000) * 0.417f;
            float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + ox, y * freq + oy);
                norm += amp;
                amp *= gain;
                freq *= lacunarity;
                ox += 13.7f;
                oy += 7.3f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        public static float Ridged(float x, float y, int octaves, int seed)
        {
            float sum = 0f, amp = 0.5f, norm = 0f, freq = 1f;
            float ox = (seed * 53 % 1000) * 0.311f, oy = (seed * 17 % 1000) * 0.829f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq + ox, y * freq + oy) * 2f - 1f);
                sum += amp * n * n;
                norm += amp;
                amp *= 0.5f;
                freq *= 2.1f;
            }
            return norm > 0f ? sum / norm : 0f;
        }

        /// <summary>A deterministic pseudo-random number in 0..1 for an integer pair, for scattering things repeatably.</summary>
        public static float Hash(int x, int y, int seed)
        {
            uint h = (uint)(x * 374761393) ^ (uint)(y * 668265263) ^ (uint)(seed * 982451653);
            h = (h ^ (h >> 13)) * 1274126177u;
            h ^= h >> 16;
            return (h & 0xFFFFFF) / 16777216f;
        }
    }
}
