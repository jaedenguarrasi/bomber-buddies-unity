using System;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>What everyone needs to know about the match: who the player is, the score, the wave, and the events the HUD listens to.</summary>
    public static class GameState
    {
        public static FootController Player;
        public static CameraRig Rig;
        public static Hud Hud;
        public static EnemySpawner Spawner;
        public static MaterialLibrary Library;
        /// <summary>The walkable ground height at (x, z), for anything that moves without a navmesh.</summary>
        public static Func<float, float, float> GroundHeight;
        public static int Kills, Headshots, Wave, Deaths, Credits, Grenades = 8;
        public static event Action Changed;
        public static event Action<bool> Killed;

        public static void Reset()
        {
            Kills = 0; Headshots = 0; Wave = 0; Deaths = 0; Credits = 0; Grenades = 8;
            Player = null; Rig = null; Hud = null; Spawner = null;
        }

        public static void Earn(int credits) { Credits += credits; Touch(); }

        public static void EnemyKilled(bool headshot, int score = 10)
        {
            Kills++; Credits += score;
            if (headshot) Headshots++;
            if (Killed != null) Killed(headshot);
            Touch();
        }

        public static void Touch()
        {
            if (Changed != null) Changed();
        }
    }
}
