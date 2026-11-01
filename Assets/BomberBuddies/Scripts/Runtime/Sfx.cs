using System.Collections.Generic;
using UnityEngine;

namespace BomberBuddies
{
    /// <summary>
    /// The original's sounds, pulled straight out of its SFX_DATA (41 sounds, several variants each) and its music loop.
    /// sfx(): one of a sound's variants, never the same one twice running, with a little random pitch, faded by distance like att3.
    /// Loops for engines and rotors, the music with the boost treatment (bass up, top rolled off, a touch louder), and the
    /// original's little synth tones (sTone) for the menus.
    /// </summary>
    public class Sfx : MonoBehaviour
    {
        public static Sfx Instance { get; private set; }
        public static bool Muted { get { return PlayerPrefs.GetInt("bb_mute", 0) == 1; } set { PlayerPrefs.SetInt("bb_mute", value ? 1 : 0); } }
        public static bool MusicOn { get { return PlayerPrefs.GetInt("bb_music", 1) == 1; } set { PlayerPrefs.SetInt("bb_music", value ? 1 : 0); } }
        public static bool Boosting;   // set by whatever is boosting this frame

        readonly Dictionary<string, List<AudioClip>> bank = new Dictionary<string, List<AudioClip>>();
        readonly Dictionary<string, int> last = new Dictionary<string, int>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        readonly Dictionary<string, AudioSource> loops = new Dictionary<string, AudioSource>();
        readonly Dictionary<string, AudioClip> tones = new Dictionary<string, AudioClip>();
        AudioSource music; AudioLowPassFilter musicLp; float boostK;
        AudioClip musicClip;

        public static Sfx Create(Transform parent)
        {
            if (Instance != null) return Instance;
            var go = new GameObject("Sfx"); go.transform.SetParent(parent, false);
            var s = go.AddComponent<Sfx>(); Instance = s; s.Load(); return s;
        }

        void Load()
        {
            foreach (var c in Resources.LoadAll<AudioClip>("Sfx"))
            {
                if (c.name == "music_loop") { musicClip = c; continue; }
                // gun_rifle_1_0 → gun_rifle
                int cut = c.name.Length; for (int k = 0; k < 2; k++) { int i = c.name.LastIndexOf('_', cut - 1); if (i > 0) cut = i; }
                string key = c.name.Substring(0, cut);
                List<AudioClip> l; if (!bank.TryGetValue(key, out l)) { l = new List<AudioClip>(); bank[key] = l; }
                l.Add(c);
            }
            for (int i = 0; i < 24; i++) pool.Add(NewSource("voice " + i));
            music = NewSource("music"); music.loop = true; music.spatialBlend = 0f; music.clip = musicClip; music.volume = 0f;
            musicLp = music.gameObject.AddComponent<AudioLowPassFilter>(); musicLp.cutoffFrequency = 22000f;
            if (musicClip != null) music.Play();
        }

        AudioSource NewSource(string name)
        {
            var go = new GameObject(name); go.transform.SetParent(transform, false);
            var a = go.AddComponent<AudioSource>(); a.playOnAwake = false; a.rolloffMode = AudioRolloffMode.Linear; a.minDistance = 6f; a.maxDistance = 180f; a.dopplerLevel = 0f; a.spread = 60f;
            return a;
        }

        static Transform Ear { get { var c = Camera.main; return c != null ? c.transform : null; } }

        /// <summary>sfx(name, vol, pitch, x, y, z): play a variant at a place (or in your ear when pos is null).</summary>
        public static void Play(string name, float vol = 0.6f, float pitch = 1f, Vector3? pos = null)
        {
            if (Instance == null || Muted) return;
            Instance.PlayNow(name, vol, pitch, pos);
        }

        void PlayNow(string name, float vol, float pitch, Vector3? pos)
        {
            List<AudioClip> l; if (!bank.TryGetValue(name, out l) || l.Count == 0) return;
            int i = Random.Range(0, l.Count); int prev; if (l.Count > 1 && last.TryGetValue(name, out prev) && prev == i) i = (i + 1) % l.Count; last[name] = i;
            float at = 1f;
            if (pos.HasValue && Ear != null) { at = Mathf.Clamp(1f - Vector3.Distance(pos.Value, Ear.position) / 180f, 0.1f, 1f); if (at < 0.11f) return; }
            AudioSource src = null; foreach (var a in pool) if (!a.isPlaying) { src = a; break; }
            if (src == null) src = pool[Random.Range(0, pool.Count)];
            src.transform.position = pos.HasValue ? pos.Value : (Ear != null ? Ear.position : Vector3.zero);
            src.spatialBlend = pos.HasValue ? 0.8f : 0f;
            src.pitch = pitch * Random.Range(0.94f, 1.06f);
            src.volume = Mathf.Clamp01(vol * at);
            src.clip = l[i]; src.Play();
        }

        /// <summary>A loop (engine, rotor, fire) under a key: call every frame it should run; it stops when not refreshed.</summary>
        public static void Loop(string key, string name, float vol, float pitch, Vector3? pos = null)
        {
            if (Instance == null) return;
            AudioSource a;
            if (!Instance.loops.TryGetValue(key, out a))
            {
                List<AudioClip> l; if (!Instance.bank.TryGetValue(name, out l) || l.Count == 0) return;
                a = Instance.NewSource("loop " + key); a.loop = true; a.clip = l[0]; a.spatialBlend = pos.HasValue ? 0.8f : 0f; a.Play();
                Instance.loops[key] = a;
            }
            a.volume = Mathf.Lerp(a.volume, Muted ? 0f : vol, 0.2f); a.pitch = Mathf.Lerp(a.pitch, pitch, 0.1f);
            if (pos.HasValue) a.transform.position = pos.Value; else if (Ear != null) a.transform.position = Ear.position;
            Instance.loopTouched[key] = Time.unscaledTime;
        }
        readonly Dictionary<string, float> loopTouched = new Dictionary<string, float>();

        /// <summary>sTone: the original's menu blips, made on the fly.</summary>
        public static void Tone(float freq, float dur, float vol, string type = "square")
        {
            if (Instance == null || Muted) return;
            string key = freq + "/" + dur + "/" + type;
            AudioClip c;
            if (!Instance.tones.TryGetValue(key, out c))
            {
                int sr = 22050, n = Mathf.CeilToInt(sr * (dur + 0.02f));
                var d = new float[n];
                for (int i = 0; i < n; i++)
                {
                    float t = i / (float)sr, f = freq * Mathf.Pow(0.6f, Mathf.Clamp01(t / dur)), ph = t * f * Mathf.PI * 2f;
                    float w = type == "triangle" ? Mathf.PingPong(ph / Mathf.PI, 2f) - 1f : type == "sine" ? Mathf.Sin(ph) : Mathf.Sin(ph) > 0f ? 0.6f : -0.6f;
                    d[i] = w * Mathf.Exp(-t / dur * 6.9f);
                }
                c = AudioClip.Create("tone " + key, n, 1, sr, false); c.SetData(d, 0); Instance.tones[key] = c;
            }
            AudioSource src = null; foreach (var a in Instance.pool) if (!a.isPlaying) { src = a; break; }
            if (src == null) return;
            src.transform.position = Ear != null ? Ear.position : Vector3.zero; src.spatialBlend = 0f; src.pitch = 1f; src.volume = vol * 1.4f; src.clip = c; src.Play();
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        float listenerT; AudioListener ownListener;
        /// <summary>The game camera has a listener now: ours goes at once.</summary>
        public static void CameraReady() { if (Instance != null && Instance.ownListener != null) { DestroyImmediate(Instance.ownListener); Instance.ownListener = null; } }
        void Update()
        {
            // somebody has to listen: the game camera when there is one, else us (the menus)
            listenerT -= Time.unscaledDeltaTime;
            if (listenerT <= 0f)
            {
                listenerT = 1f;
                var cam = Camera.main; bool camHas = cam != null && cam.GetComponent<AudioListener>() != null && cam.isActiveAndEnabled;
                if (camHas && ownListener != null) { Destroy(ownListener); ownListener = null; }
                else if (!camHas && ownListener == null && FindObjectOfType<AudioListener>() == null) ownListener = gameObject.AddComponent<AudioListener>();
            }
            // loops that were not refreshed this frame fade out and go
            var dead = new List<string>();
            foreach (var kv in loops) { float t; if (!loopTouched.TryGetValue(kv.Key, out t) || Time.unscaledTime - t > 0.25f) { kv.Value.volume = Mathf.Lerp(kv.Value.volume, 0f, 0.2f); if (kv.Value.volume < 0.01f) dead.Add(kv.Key); } }
            foreach (var k in dead) { Destroy(loops[k].gameObject); loops.Remove(k); loopTouched.Remove(k); }
            // the music, with the boost treatment
            if (music != null)
            {
                boostK = Mathf.Lerp(boostK, Boosting ? 1f : 0f, Mathf.Min(1f, Time.unscaledDeltaTime * 6f));
                float want = (MusicOn && !Muted) ? 0.5f + 0.22f * boostK : 0f;
                music.volume = Mathf.Lerp(music.volume, want * 0.55f, Mathf.Min(1f, Time.unscaledDeltaTime * 6f));
                musicLp.cutoffFrequency = Mathf.Lerp(22000f, 2600f, boostK);
                music.ignoreListenerPause = true;
            }
            Boosting = false;
        }

        /// <summary>The gun's sound (gunSfx).</summary>
        public static string GunSound(string id)
        {
            switch (id)
            {
                case "lmg": case "m2": case "mini": return "gun_lmg";
                case "duals": return "gun_pistol";
                case "shotgun": case "sawn": return "gun_shotgun";
                case "sniper": return "gun_sniper";
                case "laser": case "ray": return "gun_zap";
                case "volt": return "gun_bolt";
                case "m20": case "m25": case "m30": return "gun_cannon";
                case "breath": return "breath";
                case "darts": case "webs": return "thwip";
                default: return "gun_rifle";
            }
        }
    }
}
