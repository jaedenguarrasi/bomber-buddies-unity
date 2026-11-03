using UnityEngine;

namespace BomberBuddies
{
    /// <summary>Every option the original kept in save.opt, in one place, saved to PlayerPrefs. The Options screen edits these;
    /// the systems read them. Difficulty presets fill the four multipliers the way the original's did.</summary>
    public static class Settings
    {
        // sound and look
        public static float Volume = 0.7f, LookSens = 1f, AimSens = 1f;
        public static bool InvertY, AimInvert, CamBank = true, SlowAim = true;
        public static string PadPitch = "pull";          // pull: stick back = nose up (the original's default) · push: stick forward = nose up
        // touch
        public static string FireMode = "hold";          // hold · toggle (tap the gun button to start/stop) · drag (holding a look-finger down fires)
        public static bool FixedStick, Lefty, BigButtons, LookPad = true, TwoFingerAim = true, AutoHide = true;
        public static string Steer = "stick";            // stick · tilt
        public static string TiltRole = "fly";           // fly · aim
        // flying and crews
        public static string FlightModel = "arcade";     // arcade · aero
        public static string HeliScheme = "a";           // a: throttle climbs, stick drives · b: stick climbs, throttle drives
        public static string CrewAI = "ai";              // ai: the other seat is crewed · solo: nobody else aboard
        public static string Turret = "auto";            // auto: a single-seat chin gun tracks targets on its own · manual
        // difficulty
        public static string Difficulty = "normal";      // easy · normal · hard · custom
        public static float EnemyHp = 1f, EnemyDmg = 1f, EnemyAcc = 1f, YouHp = 1f;
        public static string Side = "good";              // good · bad (Beast side everywhere: your squad comes as greys)
        public static int Detail = 1;

        public static void Load()
        {
            Volume = PlayerPrefs.GetFloat("opt_vol", 0.7f); LookSens = PlayerPrefs.GetFloat("bb_sens", 1f); AimSens = PlayerPrefs.GetFloat("opt_aimsens", 1f);
            InvertY = PlayerPrefs.GetInt("bb_inv", 0) == 1; AimInvert = PlayerPrefs.GetInt("opt_aiminv", 0) == 1; CamBank = PlayerPrefs.GetInt("opt_cambank", 1) == 1; SlowAim = PlayerPrefs.GetInt("opt_slowaim", 1) == 1;
            PadPitch = PlayerPrefs.GetString("opt_padpitch", "pull"); FireMode = PlayerPrefs.GetString("opt_firemode", "hold");
            FixedStick = PlayerPrefs.GetInt("opt_fixedstick", 0) == 1; Lefty = PlayerPrefs.GetInt("bb_lefty", 0) == 1; BigButtons = PlayerPrefs.GetInt("bb_big", 0) == 1;
            LookPad = PlayerPrefs.GetInt("opt_lookpad", 1) == 1; TwoFingerAim = PlayerPrefs.GetInt("opt_twof", 1) == 1; AutoHide = PlayerPrefs.GetInt("opt_autohide", 1) == 1;
            Steer = PlayerPrefs.GetInt("bb_tilt", 0) == 1 ? "tilt" : "stick"; TiltRole = PlayerPrefs.GetString("opt_tiltrole", "fly");
            FlightModel = PlayerPrefs.GetString("opt_flight", "arcade"); HeliScheme = PlayerPrefs.GetString("opt_heli", "a"); CrewAI = PlayerPrefs.GetString("opt_crew", "ai"); Turret = PlayerPrefs.GetString("opt_turret", "auto");
            Difficulty = PlayerPrefs.GetString("opt_diff", "normal"); EnemyHp = PlayerPrefs.GetFloat("opt_dEnemyHp", 1f); EnemyDmg = PlayerPrefs.GetFloat("opt_dEnemyDmg", 1f); EnemyAcc = PlayerPrefs.GetFloat("opt_dEnemyAcc", 1f); YouHp = PlayerPrefs.GetFloat("opt_dYouHp", 1f);
            Side = PlayerPrefs.GetString("opt_side", "good"); Detail = PlayerPrefs.GetInt("bb_detail", 1);
            Apply();
        }
        public static void Save()
        {
            PlayerPrefs.SetFloat("opt_vol", Volume); PlayerPrefs.SetFloat("bb_sens", LookSens); PlayerPrefs.SetFloat("opt_aimsens", AimSens);
            PlayerPrefs.SetInt("bb_inv", InvertY ? 1 : 0); PlayerPrefs.SetInt("opt_aiminv", AimInvert ? 1 : 0); PlayerPrefs.SetInt("opt_cambank", CamBank ? 1 : 0); PlayerPrefs.SetInt("opt_slowaim", SlowAim ? 1 : 0);
            PlayerPrefs.SetString("opt_padpitch", PadPitch); PlayerPrefs.SetString("opt_firemode", FireMode);
            PlayerPrefs.SetInt("opt_fixedstick", FixedStick ? 1 : 0); PlayerPrefs.SetInt("bb_lefty", Lefty ? 1 : 0); PlayerPrefs.SetInt("bb_big", BigButtons ? 1 : 0);
            PlayerPrefs.SetInt("opt_lookpad", LookPad ? 1 : 0); PlayerPrefs.SetInt("opt_twof", TwoFingerAim ? 1 : 0); PlayerPrefs.SetInt("opt_autohide", AutoHide ? 1 : 0);
            PlayerPrefs.SetInt("bb_tilt", Steer == "tilt" ? 1 : 0); PlayerPrefs.SetString("opt_tiltrole", TiltRole);
            PlayerPrefs.SetString("opt_flight", FlightModel); PlayerPrefs.SetString("opt_heli", HeliScheme); PlayerPrefs.SetString("opt_crew", CrewAI); PlayerPrefs.SetString("opt_turret", Turret);
            PlayerPrefs.SetString("opt_diff", Difficulty); PlayerPrefs.SetFloat("opt_dEnemyHp", EnemyHp); PlayerPrefs.SetFloat("opt_dEnemyDmg", EnemyDmg); PlayerPrefs.SetFloat("opt_dEnemyAcc", EnemyAcc); PlayerPrefs.SetFloat("opt_dYouHp", YouHp);
            PlayerPrefs.SetString("opt_side", Side); PlayerPrefs.SetInt("bb_detail", Detail);
            PlayerPrefs.Save();
            Apply();
        }
        /// <summary>Push the ones other systems do not poll: volume, the legacy statics, the layout, the graphics.</summary>
        public static void Apply()
        {
            AudioListener.volume = Volume;
            PauseMenu.Sensitivity = LookSens; PauseMenu.InvertY = InvertY; PauseMenu.BigButtons = BigButtons; PauseMenu.Lefty = Lefty; PauseMenu.Tilt = Steer == "tilt"; PauseMenu.Detail = Detail;
            PauseMenu.ApplyDetail();
            if (GameState.Hud != null) GameState.Hud.ApplyLayoutOptions();
        }
        /// <summary>The presets: easy / normal / hard set the four multipliers; anything else is custom.</summary>
        public static void SetDifficulty(string d)
        {
            Difficulty = d;
            if (d == "easy") { EnemyHp = 0.7f; EnemyDmg = 0.6f; EnemyAcc = 0.7f; YouHp = 1.5f; }
            else if (d == "normal") { EnemyHp = 1f; EnemyDmg = 1f; EnemyAcc = 1f; YouHp = 1f; }
            else if (d == "hard") { EnemyHp = 1.5f; EnemyDmg = 1.5f; EnemyAcc = 1.3f; YouHp = 0.75f; }
        }
    }
}
