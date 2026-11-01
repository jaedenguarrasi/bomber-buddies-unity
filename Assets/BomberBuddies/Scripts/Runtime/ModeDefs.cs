using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The modes table from the original (MODES, line 5042) — the part the Unity build can play so far — and the wave arithmetic
    /// from startWave3/waveMix: how many of each enemy type the n-th wave brings. Types we have not built yet simply spawn nothing.
    /// </summary>
    public static class ModeDefs
    {
        public class Mode
        {
            public string id, name, group, desc;
            public string[] comp;          // allowed enemy types (null = everything)
            public float infK = 1f;
            public bool horde;
            public System.Func<int, Dictionary<string, int>, Dictionary<string, int>> force;
        }

        public static readonly Dictionary<string, Mode> All = new Dictionary<string, Mode>
        {
            { "battle", new Mode { id = "battle", name = "Battle", group = "Battles", desc = "Waves of the enemy army with a breather between each." } },
            { "horde", new Mode { id = "horde", name = "Horde", group = "Battles", desc = "Endless waves with no breather, each one bigger.", horde = true } },
            { "infantry", new Mode { id = "infantry", name = "Infantry", group = "Battles", desc = "Soldiers only: no vehicles, no aircraft.", comp = new[] { "inf", "medic", "egun", "vest", "bunker" }, infK = 2.2f } },
            { "lightarmor", new Mode { id = "lightarmor", name = "Light armour", group = "Battles", desc = "Soldiers, scout jeeps and gunships, with machine-gun nests. No tanks.", comp = new[] { "inf", "jeep", "eheli", "medic", "egun", "ejet", "bunker", "aa" }, infK = 1.6f } },
            { "air", new Mode { id = "air", name = "Air Superiority", group = "Battles", desc = "The sky is full of enemy fighters and gunships, nothing on the ground. Shoot down forty.", comp = new[] { "eplane", "eheli" } } },
            { "conquest", new Mode { id = "conquest", name = "Conquest", group = "Battles", desc = "Five command posts across the island. Take them by standing on them. Every post you hold drains the enemy's reinforcements; run them to zero." } },
            { "defend", new Mode { id = "defend", name = "Hold the Tower", group = "Battles", desc = "Keep the marked tower standing through eight waves. If it falls, you lose." } },
            { "strike", new Mode { id = "strike", name = "Strike", group = "Missions", desc = "Five enemy fuel depots, each ringed with flak and missiles. Destroy all five in six minutes." } },
            { "assassin", new Mode { id = "assassin", name = "Assassination", group = "Missions", desc = "An enemy general is somewhere on the island with his bodyguards. He will try to get to his escape helicopter. Kill him in five minutes." } },
            { "convoy", new Mode { id = "convoy", name = "Convoy Escort", group = "Missions", desc = "Three of your trucks have to cross the island. Keep at least two of them alive to the far side." } },
            { "galactic", new Mode { id = "galactic", name = "Galactic Assault", group = "Battles", desc = "Take the posts, kill the shield generator, board their carrier." } },
        };

        /// <summary>What wave n brings (startWave3 + waveMix), filtered by the mode.</summary>
        public static Dictionary<string, int> WaveCounts(Mode mode, int n)
        {
            var c = new Dictionary<string, int>
            {
                { "inf", Mathf.Min(26, 6 + 2 * n) },
                { "jeep", Mathf.Min(7, 1 + Mathf.CeilToInt(n / 2f)) },
                { "tank", Mathf.Min(6, n / 2) },
                { "aa", Mathf.Min(6, Mathf.CeilToInt(n / 2f)) },
                { "eheli", n >= 3 ? Mathf.Min(3, (n - 1) / 2) : 0 },
                { "eplane", n >= 4 ? Mathf.Min(3, (n - 2) / 2) : 0 },
                { "vest", n >= 2 ? Mathf.Min(4, Mathf.CeilToInt(n / 2f)) : 0 },
                { "medic", n >= 3 ? Mathf.Min(3, n / 3) : 0 },
                { "egun", n >= 3 ? Mathf.Min(4, n / 2) : 0 },
                { "ejet", n >= 4 ? Mathf.Min(3, (n - 2) / 2) : 0 },
                { "sam", n >= 4 ? Mathf.Min(4, (n - 2) / 2) : 0 },
                { "bunker", n >= 2 ? Mathf.Min(3, 1 + (n - 2) / 3) : 0 },
            };
            if (mode != null)
            {
                if (mode.comp != null) { var keys = new List<string>(c.Keys); foreach (var k in keys) if (System.Array.IndexOf(mode.comp, k) < 0) c[k] = 0; }
                if (mode.infK != 1f) c["inf"] = Mathf.RoundToInt(c["inf"] * mode.infK);
                if (mode.force != null) c = mode.force(n, c);
                if (mode.horde) { var keys = new List<string>(c.Keys); foreach (var k in keys) c[k] = Mathf.RoundToInt(c[k] * (1.25f + n * 0.04f)); }
                if (mode.id == "air") { c["eplane"] = 3 + n; c["eheli"] = 1 + n / 2; }
            }
            return c;
        }
    }
}
