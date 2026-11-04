using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>The original's save.opt: every option from the OPTS table, stored under the original's keys (opt_&lt;k&gt;) with the
    /// original's defaults. The typed properties below are what the systems read; the Controls screen edits the raw values.</summary>
    public static class Settings
    {
        static readonly Dictionary<string, object> vals = new Dictionary<string, object>();
        static readonly Dictionary<string, OptionDefs.Opt> defs = new Dictionary<string, OptionDefs.Opt>();
        public static OptionDefs.Opt Def(string k) { OptionDefs.Opt d; return defs.TryGetValue(k, out d) ? d : null; }

        public static object Get(string k) { object v; if (vals.TryGetValue(k, out v)) return v; var d = Def(k); return d != null ? d.def : null; }
        public static string Str(string k) { var v = Get(k); return v == null ? "" : v is bool ? ((bool)v ? "true" : "false") : v is float ? ((float)v).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture) : v.ToString(); }
        public static bool Bool(string k) { var v = Get(k); return v is bool ? (bool)v : v is string ? (string)v == "true" : v is float ? (float)v != 0f : false; }
        public static float Num(string k) { var v = Get(k); return v is float ? (float)v : v is bool ? ((bool)v ? 1f : 0f) : 0f; }
        public static bool Is(string k, object c) { var v = Get(k); return v != null && c != null && v.Equals(c); }

        public static void Set(string k, object v)
        {
            vals[k] = v;
            var d = Def(k);
            if (v is bool) PlayerPrefs.SetString("opt_" + k, (bool)v ? "b:true" : "b:false");
            else if (v is float) PlayerPrefs.SetString("opt_" + k, "f:" + ((float)v).ToString("R", System.Globalization.CultureInfo.InvariantCulture));
            else PlayerPrefs.SetString("opt_" + k, "s:" + v);
            // the difficulty presets fill the four multipliers; touching a multiplier makes it Custom (the original's rule)
            if (k == "diff") ApplyPreset((string)v);
            if ((k == "dEnemyHp" || k == "dEnemyDmg" || k == "dEnemyAcc" || k == "dYouHp") && !Is("diff", "custom")) { vals["diff"] = "custom"; PlayerPrefs.SetString("opt_diff", "s:custom"); }
            PlayerPrefs.Save();
            Apply();
        }

        static void ApplyPreset(string d)
        {
            float[] p = d == "easy" ? new[] { 0.8f, 0.6f, 1.4f, 1.6f } : d == "normal" ? new[] { 1.6f, 1f, 1f, 1f } : d == "hard" ? new[] { 2.4f, 1.4f, 0.7f, 0.8f } : d == "brutal" ? new[] { 3.5f, 2f, 0.45f, 0.6f } : null;
            if (p == null) return;
            string[] ks = { "dEnemyHp", "dEnemyDmg", "dEnemyAcc", "dYouHp" };
            for (int i = 0; i < 4; i++) { vals[ks[i]] = p[i]; PlayerPrefs.SetString("opt_" + ks[i], "f:" + p[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture)); }
        }

        public static void Load()
        {
            defs.Clear(); vals.Clear();
            foreach (var d in OptionDefs.All)
            {
                defs[d.k] = d;
                string raw = PlayerPrefs.GetString("opt_" + d.k, "");
                if (raw.Length < 2) continue;
                if (raw.StartsWith("b:")) vals[d.k] = raw == "b:true";
                else if (raw.StartsWith("f:")) { float f; if (float.TryParse(raw.Substring(2), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out f)) vals[d.k] = f; }
                else vals[d.k] = raw.Substring(2);
            }
            Apply();
        }
        public static void Save() { PlayerPrefs.Save(); Apply(); }
        public static void ResetAll() { foreach (var d in OptionDefs.All) PlayerPrefs.DeleteKey("opt_" + d.k); Load(); }

        // ---- what the systems read (the original's names, typed) ----
        public static float Volume { get { return Num("vol"); } }
        public static bool Music { get { return Bool("music"); } }
        public static float LookSens { get { return Num("sens"); } }
        public static float PitchSens { get { return Num("sensP"); } }
        public static float AimSens { get { return Num("aimSens"); } }
        public static float MouseSens { get { return Num("mouseSens"); } }
        public static bool InvertY { get { return Bool("invertY"); } }
        public static bool HeliInvert { get { return Bool("heliInvert"); } }
        public static bool AimInvert { get { return Bool("aimInvert"); } }
        public static bool CamBank { get { return Bool("camBank"); } }
        public static bool SlowAim { get { return Bool("slowAim"); } }
        public static bool AutoLevel { get { return Bool("autoLevel"); } }
        public static bool Fine { get { return Bool("fine"); } }
        public static bool Deploy { get { return Bool("deploy"); } }
        public static string PadPitch { get { return Str("padPitch"); } }
        public static string PadLayout { get { return Str("padLayout"); } }
        public static float PadDead { get { return Num("padDead"); } }
        public static float PadSens { get { return Num("padSens"); } }
        public static bool PadRumble { get { return Bool("padRumble"); } }
        public static string FireMode { get { return Str("fireMode"); } }
        public static bool AimToggle { get { return Bool("aimToggle"); } }
        public static bool FixedStick { get { return Bool("fixedStick"); } }
        public static bool Lefty { get { return Bool("lefty"); } }
        public static bool BigButtons { get { return Bool("bigButtons"); } }
        public static float CtlAlpha { get { return Num("ctlAlpha"); } }
        public static string SliderPos { get { return Str("sliderPos"); } }
        public static bool LookPad { get { return Bool("lookPad"); } }
        public static bool GunDrag { get { return Bool("gunDrag"); } }
        public static bool GunTilt { get { return Bool("gunTilt"); } }
        public static bool TwoFingerAim { get { return true; } }      // the original's drag-on-the-view aim with a second finger is always on
        public static bool AutoHide { get { return true; } }
        public static string StickSide { get { return Str("stickSide"); } }
        public static float LookZoom { get { return Num("lookZoom"); } }
        public static float TiltZoom { get { return Num("tiltZoom"); } }
        public static string Steer { get { return Str("steer"); } }
        public static string TiltRole { get { return Str("tiltRole"); } }
        public static string StickRole { get { return Str("stickRole"); } }
        public static string TiltAxis { get { return Str("tiltAxis"); } }
        public static float TiltRangeX { get { return Num("tiltRangeX"); } }
        public static float TiltRangeY { get { return Num("tiltRangeY"); } }
        public static float TiltDead { get { return Num("tiltDead"); } }
        public static string TiltCurve { get { return Str("tiltCurve"); } }
        public static float TiltSmooth { get { return Num("tiltSmooth"); } }
        public static bool TiltFlipX { get { return Bool("tiltFlipX"); } }
        public static bool TiltFlipY { get { return Bool("tiltFlipY"); } }
        public static bool TiltBtn { get { return Bool("tiltBtn"); } }
        public static float TiltAimK { get { return Num("tiltAimK"); } }
        public static float TiltSensFor(string craft, bool aim) { var d = Def((aim ? "ta_" : "ts_") + craft); return d != null ? Num(d.k) : 1f; }
        public static string FlightModel { get { return Str("flightModel"); } }
        public static string HeliModel { get { return Str("heliModel"); } }
        public static string HeliScheme { get { return Str("heliScheme"); } }
        public static string CrewAI { get { return Str("crewAI"); } }
        public static string Turret { get { return Str("turret"); } }
        public static string DroneMode { get { return Str("droneMode"); } }
        public static float DroneCam { get { return Num("droneCam"); } }
        public static float DroneRates { get { return Num("droneRates"); } }
        public static string Difficulty { get { return Str("diff"); } }
        public static float EnemyHp { get { return Num("dEnemyHp"); } }
        public static float EnemyDmg { get { return Num("dEnemyDmg"); } }
        public static float EnemyAcc { get { return Num("dEnemyAcc"); } }
        public static float YouHp { get { return Num("dYouHp"); } }
        public static string Side { get { return Str("side"); } }
        public static bool Under { get { return Bool("under"); } }
        public static string Gfx { get { return Str("gfx"); } }
        public static bool Shadows { get { return Is("shadows", "on"); } }
        public static bool Film { get { return Is("film", "on"); } }
        public static float Pix { get { return Num("pix"); } }
        public static float Dist { get { return Num("dist"); } }
        public static float Bodies { get { return Num("bodies"); } }
        public static string SfxPack { get { return Str("sfxPack"); } }
        public static int Detail { get { string g = Gfx; return g == "low" ? 0 : g == "med" ? 1 : g == "high" ? 2 : 3; } }

        /// <summary>Push the ones other systems do not poll: volume, the legacy statics, the layout, the graphics.</summary>
        public static void Apply()
        {
            AudioListener.volume = Volume;
            Sfx.MusicOn = Music;
            PauseMenu.Sensitivity = LookSens; PauseMenu.InvertY = InvertY; PauseMenu.BigButtons = BigButtons; PauseMenu.Lefty = Lefty; PauseMenu.Tilt = Steer == "tilt"; PauseMenu.Detail = Mathf.Min(2, Detail);
            PauseMenu.ApplyDetail();
            if (GameState.Hud != null) GameState.Hud.ApplyLayoutOptions();
        }
    }
}
