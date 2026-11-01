using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BomberBuddies
{
    /// <summary>Battle setup (MATCH_DEF): which enemies come in Battle and Horde, how many, how many lives, friendly fire.
    /// Saved in PlayerPrefs and applied to the waves.</summary>
    public static class BattleSetup
    {
        public static readonly string[] Types = { "inf", "jeep", "tank", "aa", "sam", "bunker", "eheli", "eplane", "gob", "ape" };
        public static readonly string[] Names = { "Infantry", "Jeeps", "Tanks", "Anti-aircraft guns", "Missile launchers", "Bunkers", "Helicopters", "Fighter planes", "Goblin bands", "Apes" };
        public static bool On(string t) { return PlayerPrefs.GetInt("bb_ms_" + t, t == "gob" || t == "ape" ? 0 : 1) == 1; }
        public static void Set(string t, bool on) { PlayerPrefs.SetInt("bb_ms_" + t, on ? 1 : 0); }
        public static float Amount { get { return PlayerPrefs.GetFloat("bb_ms_amount", 1f); } set { PlayerPrefs.SetFloat("bb_ms_amount", value); } }
        public static int Lives { get { return PlayerPrefs.GetInt("bb_ms_lives", 3); } set { PlayerPrefs.SetInt("bb_ms_lives", value); } }
        public static bool FriendlyFire { get { return PlayerPrefs.GetInt("bb_ms_ff", 0) == 1; } set { PlayerPrefs.SetInt("bb_ms_ff", value ? 1 : 0); } }

        /// <summary>Applies the setup to a wave's counts for the modes it governs (Battle and Horde, like the original).</summary>
        public static Dictionary<string, int> Apply(string modeId, Dictionary<string, int> c, int n)
        {
            if (modeId != "battle" && modeId != "horde") return c;
            var keys = new List<string>(c.Keys);
            foreach (var k in keys)
            {
                bool governed = System.Array.IndexOf(Types, k) >= 0;
                if (governed && !On(k)) c[k] = 0;
                if (k == "gob" && On("gob")) c[k] = 4 + 2 * n;
                if (k == "ape" && On("ape")) c[k] = 1 + n / 2;
                c[k] = Mathf.RoundToInt(c[k] * Amount);
            }
            return c;
        }
    }
}
