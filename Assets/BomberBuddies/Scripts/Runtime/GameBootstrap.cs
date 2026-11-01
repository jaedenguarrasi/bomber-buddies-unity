using System.Collections;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BomberBuddies
{
    /// <summary>
    /// The one object in the scene. When the game starts it builds everything else: light and sky, the island and its village,
    /// the navigation mesh, the player and camera, the HUD, and the waves of enemies. Nothing is hand-placed, so the island is
    /// the same on every machine and can be regenerated from a seed.
    /// </summary>
    public class GameBootstrap : MonoBehaviour
    {
        public enum Mode { Galactic, Island }
        public MaterialLibrary library;
        public Mode mode = Mode.Galactic;
        /// <summary>Which battle to fight on the island (a key of ModeDefs.All): battle, horde, infantry, lightarmor, air.</summary>
        public string battleMode = "battle";
        public int seed = 7;
        public float islandSize = 1100f, maxHeight = 85f, seaLevel = 9f;
        public int treeCount = 420, rockCount = 90;
        public int waveBase = 4, wavePerWave = 2;

        World3 world;
        Material skyMat;
        float seaY;
        FootController player;
        Health playerHealth;
        Camera cam;
        CameraRig rig;
        Vector3 spawnPoint;
        float spawnYaw;

        /// <summary>The mode chosen in the menu survives the scene reload that starts a game.</summary>
        public static string ChosenMode;
        public bool showMenu = true;

        void Awake()
        {
            Application.targetFrameRate = Application.isMobilePlatform ? 60 : -1;
            GameState.Reset();
            GameState.Library = library;
            gameObject.AddComponent<InputBridge>();
            if (ChosenMode != null) { string m = ChosenMode; ChosenMode = null; Begin(m); return; }
            if (showMenu) { MainMenu.Create(transform, library, Begin); return; }
            Begin(mode == Mode.Galactic ? "galactic" : battleMode);
        }

        /// <summary>Builds the world and starts the chosen mode (a key of ModeDefs.All).</summary>
        public void Begin(string modeId)
        {
            mode = modeId == "galactic" ? Mode.Galactic : Mode.Island;
            battleMode = modeId;
            Night = battleMode == "scary";
            Random.InitState(seed);
            bool mobile = Application.isMobilePlatform;

            Gunfire.Create(transform, library);
            Projectiles.Create(transform, library);
            BuildLighting();
            world = World3.Build(transform, seed, library, mobile);
            seaY = World3.SEA * World3.U; seaLevel = seaY;
            GameState.GroundHeight = world.GroundMetres;
            if (mode == Mode.Island) BuildNavMesh();
            BuildPlayer();
            GameState.Hud = Hud.Create(transform);
            Support.Create(transform, library, GameState.Hud.transform);
            var pm = PlayerManager.Create(transform, library, player, rig, cam);
            var spawner = new GameObject("Enemies").AddComponent<EnemySpawner>();
            spawner.transform.SetParent(transform, false);
            spawner.library = library; spawner.player = player; spawner.baseCount = waveBase; spawner.perWave = wavePerWave;
            spawner.villageCenter = world.villageCentres.Count > 0 ? world.villageCentres[0] : world.spawn; spawner.villageRadius = 60f;
            spawner.modeId = battleMode; spawner.seaLevel = seaLevel;
            if (mode == Mode.Galactic)
            {
                spawner.enabled = false;   // no waves: the ships' guards come from the mode itself
                cam.farClipPlane = 4000f;
                RenderSettings.fogEndDistance = 2600f;
                GalacticMode.Create(transform, library, world, player, rig, cam, spawner);
                return;
            }
            GameState.Spawner = spawner;
            IslandMode.Create(transform, library, world, spawner, battleMode);
            // like the original: you start in your plane over the island, and come back the same way
            var team = Teammates.Create(transform, library, spawner, seaLevel);
            System.Action<Vector3> spawn = at => { if (Progress.IsFoot(Progress.Craft)) pm.SpawnOnFoot(at); else if (Progress.IsRide(Progress.Craft)) pm.SpawnDriving(at); else pm.SpawnInAir(at); team.Deploy(at); };
            var deploy = DeployScreen.Create(GameState.Hud.transform);
            // the deploy screen before every spawn: pick what you come back as, then Deploy (the original's oDeploy)
            pm.placeOnRespawn = () => { deploy.Open(() => spawn(world.spawn + new Vector3(Random.Range(-40f, 40f), 0f, Random.Range(-40f, 40f)))); return true; };
            deploy.Open(() => spawn(world.spawn));
            GameState.Hud.Announce(spawner.Mode.name);
            GameState.Touch();
        }

        // the original's day sky: SKY.day, SUNDIRS.day, SUNTINT.day, fog 520..1750 units, vignette 0.26, grain 0.035
        void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            var dir = new Vector3(0.42f, 0.78f, 0.46f).normalized;
            sunGo.transform.rotation = Quaternion.LookRotation(-dir);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.6f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.56f, 0.60f, 0.68f);
            RenderSettings.ambientEquatorColor = new Color(0.48f, 0.49f, 0.51f);
            RenderSettings.ambientGroundColor = new Color(0.40f, 0.38f, 0.34f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 520f * World3.U * World3.Spread;
            RenderSettings.fogEndDistance = 1750f * World3.U * World3.Spread;
            RenderSettings.fogColor = new Color(0.80f, 0.87f, 0.86f);
            skyMat = library != null ? library.Get("sky") : null;
            if (skyMat != null && skyMat.shader != null && skyMat.shader.name == "BomberBuddies/Sky")
            {
                skyMat = new Material(skyMat);
                skyMat.SetVector("_SunDir", dir);
                RenderSettings.skybox = skyMat;
            }
            else { var sky = library != null ? library.Get("skybox") : null; if (sky != null) RenderSettings.skybox = sky; skyMat = null; }
            SetSky(0f);

            var volGo = new GameObject("PostProcessing");
            volGo.transform.SetParent(transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.26f); vig.smoothness.Override(0.6f);
            var grain = profile.Add<FilmGrain>(true); grain.type.Override(FilmGrainLookup.Thin1); grain.intensity.Override(0.12f);
            var lens = profile.Add<LensDistortion>(true); lens.intensity.Override(-0.18f); lens.scale.Override(1.0f);
            vol.profile = profile;
        }

        /// <summary>The sky at an altitude: day on the ground, blending to space (black overhead, a thin blue band, stars) high up.</summary>
        /// <summary>Night (the Stalkers mode): the sky and fog go near-black, everything is dimmed through the shader, and the player carries a torch.</summary>
        public static bool Night;
        public void SetSky(float altU)
        {
            Color zen = Color.Lerp(new Color(0.36f, 0.62f, 0.78f), new Color(0.005f, 0.01f, 0.04f), altU);
            Color hor = Color.Lerp(new Color(0.87f, 0.91f, 0.87f), new Color(0.28f, 0.44f, 0.7f), altU);
            Color fog = Color.Lerp(new Color(0.80f, 0.87f, 0.86f), new Color(0.62f, 0.74f, 0.86f), altU);
            if (Night) { zen = new Color(0.01f, 0.012f, 0.03f); hor = new Color(0.05f, 0.06f, 0.1f); fog = new Color(0.03f, 0.035f, 0.06f); }
            if (skyMat != null)
            {
                skyMat.SetColor("_Zen", zen); skyMat.SetColor("_Hor", hor); skyMat.SetColor("_Low", fog);
                skyMat.SetFloat("_Stars", Night ? 0.8f : Mathf.Clamp01((altU - 0.45f) * 1.6f) * 0.9f);
                skyMat.SetFloat("_CloudCover", Night ? 0.2f : 1f - altU);
            }
            RenderSettings.fogColor = fog;
            if (cam != null) cam.backgroundColor = fog;
        }

        void BuildNavMesh()
        {
            var nav = new GameObject("Navigation");
            nav.transform.SetParent(transform, false);
            // only the part of the island the fight happens on, at a coarse voxel size: a bake of the whole island would take
            // a long time on a phone and the enemies never go far from the village
            var surface = nav.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            float reach = 420f;
            surface.center = new Vector3(world.spawn.x, 15f, world.spawn.z);
            surface.size = new Vector3(reach, 60f, reach);
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~((1 << 8) | (1 << 9));
            surface.overrideVoxelSize = true;
            surface.voxelSize = 0.3f;
            surface.overrideTileSize = true;
            surface.tileSize = 128;
            surface.BuildNavMesh();
        }

        void BuildPlayer()
        {
            spawnPoint = world.spawn + Vector3.up * 0.1f;   // the original's spawn point
            spawnYaw = 0f;

            var pgo = new GameObject("Player");
            pgo.layer = 8;
            pgo.transform.SetParent(transform, false);
            pgo.transform.position = spawnPoint;
            playerHealth = pgo.AddComponent<Health>(); playerHealth.max = 100f; playerHealth.current = 100f; playerHealth.team = 0; playerHealth.aimHeight = 1.3f;
            var zone = pgo.AddComponent<HitZone>(); zone.owner = playerHealth; zone.multiplier = 1f;
            player = pgo.AddComponent<FootController>();
            player.health = playerHealth;
            player.groundHeight = world.GroundMetres;

            var model = SoldierModelBase.Create(pgo.transform, library, 0, 8);
            player.model = model;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 16000f * World3.U;
            cam.fieldOfView = 58f;
            camGo.AddComponent<AudioListener>();
            var extra = cam.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = true;
            extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            extra.renderShadows = true;
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.SetParent(transform, false);
            rig = rigGo.AddComponent<CameraRig>();
            rig.cam = cam; rig.player = player; rig.yaw = spawnYaw; rig.pitch = 6f; rig.baseFov = 58f; rig.fpFov = 92f; rig.aimFov = 48f;
            player.rig = rig;

            var rifle = pgo.AddComponent<Rifle>();
            rifle.owner = player; rifle.rig = rig;
            rifle.BuildViewModel(library);
            player.rifle = rifle;
            player.ApplyKit();

            playerHealth.OnDamaged += info => { lastHurt = Time.time; if (GameState.Hud != null) GameState.Hud.Damaged(info.amount); };
            playerHealth.OnDied += info => { Debug.Log("Player down: " + info.amount + " damage from " + (info.source != null ? info.source.name : "?") + " at " + player.transform.position); };
            GameState.Player = player;
            GameState.Rig = rig;
        }

        float lastHurt = -99f;

        void Update()
        {
            // health comes back after a few seconds out of fire, like most modern shooters
            if (playerHealth != null && playerHealth.alive)
            {
                if (Time.time - lastHurt > 4f && playerHealth.current < playerHealth.max) playerHealth.Heal(22f * Time.deltaTime);
            }
            if (cam != null && world != null)
            {
                float ey = cam.transform.position.y / World3.U, hi = Mathf.Max(0f, ey - 260f);
                SetSky(Mathf.Clamp01((ey - 1500f) / 2300f));
                RenderSettings.fogStartDistance = (520f + hi * 0.9f) * World3.U * World3.Spread * (Night ? 0.18f : 1f);
                RenderSettings.fogEndDistance = (1750f + hi * 3f) * World3.U * World3.Spread * (Night ? 0.3f : 1f);
                Shader.SetGlobalVector("_NightLight", Night ? new Vector4(0.16f, 0.18f, 0.28f, 1f) : new Vector4(1f, 1f, 1f, 0f));
                // the torch: from the camera, along the look, 60 m, only on foot or in a vehicle at night
                bool torch = Night && !(PlayerManager.Instance != null && PlayerManager.Instance.plane != null && PlayerManager.Instance.plane.Active);
                Shader.SetGlobalVector("_TorchPos", cam.transform.position);
                Shader.SetGlobalVector("_TorchDir", new Vector4(cam.transform.forward.x, cam.transform.forward.y, cam.transform.forward.z, torch ? 70f : 0f));
            }
        }
    }
}
