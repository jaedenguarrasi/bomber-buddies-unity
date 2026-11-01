namespace BomberBuddies
{
    /// <summary>The feel dials: how much bigger the island is than the original and how much slower everything moves.
    /// The original's numbers stay as they are; these scale them at the edges.</summary>
    public static class GameTuning
    {
        /// <summary>Movement speed multiplier for aircraft and vehicles (1 = the original's pace).</summary>
        public static float Pace = 0.75f;
        /// <summary>Island spread (1 = the original's island).</summary>
        public static float Spread { get { return World3.Spread; } set { World3.Spread = value; } }
    }
}
