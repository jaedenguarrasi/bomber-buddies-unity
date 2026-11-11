using UnityEngine;

namespace BomberBuddies
{
    public static class VectorExt
    {
        /// <summary>The vector with its height dropped (for scattering things on the ground).</summary>
        public static Vector3 Flat(this Vector3 v) { v.y = 0f; return v; }
    }

    /// <summary>The feel dials: how much bigger the island is than the original and how much slower everything moves.
    /// The original's numbers stay as they are; these scale them at the edges.</summary>
    public static class GameTuning
    {
        /// <summary>Movement speed multiplier for aircraft and vehicles (1 = the original's pace).</summary>
        public static float Pace = 1f;
        /// <summary>Island spread (1 = the original's island).</summary>
        public static float Spread { get { return World3.Spread; } set { World3.Spread = value; } }
    }
}
