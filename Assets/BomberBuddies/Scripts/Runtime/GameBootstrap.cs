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
        public MaterialLibrary library;
        public int seed = 7;
        public float islandSize = 1100f, maxHeight = 85f, seaLevel = 9f;
        public int treeCount = 420, rockCount = 90;
        public int waveBase = 4, wavePerWave = 2;

        Terrain terrain;
        WorldProps.WorldInfo world;
        FootController player;
        Health playerHealth;
        Vector3 spawnPoint;
        float spawnYaw;
        bool respawning;

        void Awake()
        {
            Application.targetFrameRate = Application.isMobilePlatform ? 60 : -1;
            GameState.Reset();
            GameState.Library = library;
            Random.InitState(seed);

            gameObject.AddComponent<InputBridge>();
            Gunfire.Create(transform, library);
            BuildLighting();
            terrain = IslandBuilder.Build(transform, seed, islandSize, maxHeight, seaLevel, library);
            BuildWater();
            world = WorldProps.Build(transform, terrain, seed, islandSize, seaLevel, library, treeCount, rockCount);
            BuildNavMesh();
            BuildPlayer();
            GameState.Hud = Hud.Create(transform);
            var spawner = new GameObject("Enemies").AddComponent<EnemySpawner>();
            spawner.transform.SetParent(transform, false);
            spawner.library = library; spawner.player = player; spawner.baseCount = waveBase; spawner.perWave = wavePerWave;
            spawner.villageCenter = world.villageCenter; spawner.villageRadius = world.villageRadius;
            GameState.Spawner = spawner;
            GameState.Hud.Announce("Hold the village");
            GameState.Touch();
        }

        void BuildLighting()
        {
            var sunGo = new GameObject("Sun");
            sunGo.transform.SetParent(transform, false);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.88f);
            sun.intensity = 1.7f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.92f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.5f;
            RenderSettings.sun = sun;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.52f, 0.62f, 0.82f);
            RenderSettings.ambientEquatorColor = new Color(0.46f, 0.47f, 0.44f);
            RenderSettings.ambientGroundColor = new Color(0.22f, 0.2f, 0.18f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 180f;
            RenderSettings.fogEndDistance = 1500f;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
            var sky = library != null ? library.Get("skybox") : null;
            if (sky != null) RenderSettings.skybox = sky;

            var volGo = new GameObject("PostProcessing");
            volGo.transform.SetParent(transform, false);
            var vol = volGo.AddComponent<Volume>();
            vol.isGlobal = true;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var bloom = profile.Add<Bloom>(true); bloom.intensity.Override(0.32f); bloom.threshold.Override(1.05f); bloom.scatter.Override(0.6f);
            var tone = profile.Add<Tonemapping>(true); tone.mode.Override(TonemappingMode.ACES);
            var adj = profile.Add<ColorAdjustments>(true); adj.postExposure.Override(0.12f); adj.contrast.Override(9f); adj.saturation.Override(6f);
            var vig = profile.Add<Vignette>(true); vig.intensity.Override(0.2f); vig.smoothness.Override(0.5f);
            vol.profile = profile;
        }

        void BuildWater()
        {
            var water = GameObject.CreatePrimitive(PrimitiveType.Plane);
            water.name = "Sea";
            water.transform.SetParent(transform, false);
            water.transform.position = new Vector3(0f, seaLevel, 0f);
            water.transform.localScale = new Vector3(islandSize * 0.3f, 1f, islandSize * 0.3f);
            var col = water.GetComponent<Collider>(); if (col != null) Destroy(col);
            var r = water.GetComponent<Renderer>();
            var m = library != null ? library.Get("water") : null;
            if (m != null) r.sharedMaterial = m;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        void BuildNavMesh()
        {
            var nav = new GameObject("Navigation");
            nav.transform.SetParent(transform, false);
            // only the part of the island the fight happens on, at a coarse voxel size: a bake of the whole island would take
            // a long time on a phone and the enemies never go far from the village
            var surface = nav.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Volume;
            float reach = Mathf.Min(islandSize * 0.9f, world.villageRadius * 2f + 320f);
            surface.center = new Vector3(world.villageCenter.x, maxHeight * 0.5f, world.villageCenter.z);
            surface.size = new Vector3(reach, maxHeight + 40f, reach);
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
            float vr = world.villageRadius;
            spawnPoint = new Vector3(0f, 0f, -vr * 0.12f);   // the open square in the middle of the village
            spawnPoint.y = IslandBuilder.GroundHeight(terrain, spawnPoint.x, spawnPoint.z) + 0.1f;
            spawnYaw = 0f;

            var pgo = new GameObject("Player");
            pgo.layer = 8;
            pgo.transform.SetParent(transform, false);
            pgo.transform.position = spawnPoint;
            playerHealth = pgo.AddComponent<Health>(); playerHealth.max = 100f; playerHealth.current = 100f; playerHealth.team = 0;
            var zone = pgo.AddComponent<HitZone>(); zone.owner = playerHealth; zone.multiplier = 1f;
            player = pgo.AddComponent<FootController>();
            player.health = playerHealth;

            var model = SoldierModelBase.Create(pgo.transform, library, 0, 8);
            player.model = model;

            var camGo = new GameObject("Main Camera");
            camGo.tag = "MainCamera";
            camGo.transform.SetParent(transform, false);
            var cam = camGo.AddComponent<Camera>();
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 2600f;
            cam.fieldOfView = 70f;
            camGo.AddComponent<AudioListener>();
            var extra = cam.GetUniversalAdditionalCameraData();
            extra.renderPostProcessing = true;
            extra.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            extra.renderShadows = true;
            var rigGo = new GameObject("CameraRig");
            rigGo.transform.SetParent(transform, false);
            var rig = rigGo.AddComponent<CameraRig>();
            rig.cam = cam; rig.player = player; rig.yaw = spawnYaw; rig.pitch = 6f;
            player.rig = rig;

            var rifle = pgo.AddComponent<Rifle>();
            rifle.owner = player; rifle.rig = rig;
            rifle.BuildViewModel(library);
            player.rifle = rifle;

            playerHealth.OnDamaged += info => { lastHurt = Time.time; if (GameState.Hud != null) GameState.Hud.Damaged(info.amount); };
            playerHealth.OnDied += info => { Debug.Log("Player down: " + info.amount + " damage from " + (info.source != null ? info.source.name : "?") + " at " + player.transform.position); if (!respawning) StartCoroutine(Respawn()); };
            GameState.Player = player;
            GameState.Rig = rig;
        }

        IEnumerator Respawn()
        {
            respawning = true;
            GameState.Deaths++;
            player.ControlsLocked = true;
            if (player.model != null) player.model.Die(Vector3.forward);
            if (GameState.Hud != null) GameState.Hud.Announce("You're down");
            yield return new WaitForSeconds(3.2f);
            // a fresh figure: the old one has fallen over
            var old = player.model;
            var model = SoldierModelBase.Create(player.transform, library, 0, 8);
            player.model = model;
            if (old != null) Destroy(old.gameObject);
            player.Teleport(spawnPoint, spawnYaw);
            playerHealth.ResetFull();
            player.rifle.ammo = player.rifle.magSize;
            player.ControlsLocked = false;
            if (GameState.Hud != null) GameState.Hud.Announce("Back in the fight");
            respawning = false;
        }

        float lastHurt = -99f;

        void Update()
        {
            // health comes back after a few seconds out of fire, like most modern shooters
            if (playerHealth != null && playerHealth.alive && !respawning)
            {
                if (Time.time - lastHurt > 4f && playerHealth.current < playerHealth.max) playerHealth.Heal(22f * Time.deltaTime);
            }
            if (player != null && playerHealth != null && playerHealth.alive && player.transform.position.y < seaLevel - 1.4f && !respawning)
            {
                playerHealth.TakeDamage(new DamageInfo { amount = 9999f, point = player.transform.position, direction = Vector3.down, source = gameObject });
            }
        }
    }
}
