using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BomberBuddies.Editor
{
    /// <summary>Remembers that the project has been set up, so it only happens once (or again when the setup changes).</summary>
    public class SetupMarker : ScriptableObject
    {
        public int version;
    }

    /// <summary>
    /// Makes a fresh checkout into a working Unity project the first time the editor opens it: the render pipeline and quality
    /// levels, the materials and textures the world is built from, the one scene, the build list and the player settings for
    /// PC and Android. Also runs on the build servers before every build, so the repository never needs the generated assets.
    /// </summary>
    [InitializeOnLoad]
    public static class ProjectBootstrap
    {
        public const int SetupVersion = 3;
        const string Root = "Assets/BomberBuddies";
        const string SettingsDir = Root + "/Settings", MaterialsDir = Root + "/Materials", ScenesDir = Root + "/Scenes";
        public const string ScenePath = ScenesDir + "/Island.unity";
        const string MarkerPath = SettingsDir + "/SetupMarker.asset";
        const string LibraryPath = SettingsDir + "/BB_Materials.asset";

        static ProjectBootstrap()
        {
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                try { EnsureSetup(false); }
                catch (System.Exception e) { Debug.LogError("Bomber Buddies setup failed: " + e); }
            };
        }

        [MenuItem("Bomber Buddies/Set up project again")]
        public static void MenuSetup() { EnsureSetup(true); }

        [MenuItem("Bomber Buddies/Open the island scene")]
        public static void MenuOpenScene()
        {
            EnsureSetup(false);
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void EnsureSetup(bool force)
        {
            Folder(Root); Folder(SettingsDir); Folder(MaterialsDir); Folder(ScenesDir);
            var marker = AssetDatabase.LoadAssetAtPath<SetupMarker>(MarkerPath);
            bool fresh = marker == null || marker.version != SetupVersion;
            if (!fresh && !force && File.Exists(ScenePath)) return;
            Debug.Log("Bomber Buddies: setting the project up" + (force ? " (forced)" : ""));

            var pipeline = SetupPipeline();
            var lib = SetupMaterials(force);
            SetupScene(lib, force || !File.Exists(ScenePath));
            SetupPlayerSettings();
            SetupIcons();
            SetupLayers();
            bool restart = SetupInputHandler();

            if (marker == null) { marker = ScriptableObject.CreateInstance<SetupMarker>(); marker.version = SetupVersion; AssetDatabase.CreateAsset(marker, MarkerPath); }
            else { marker.version = SetupVersion; EditorUtility.SetDirty(marker); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Bomber Buddies: project ready. Pipeline " + (pipeline != null ? pipeline.name : "none") + ", scene " + ScenePath);

            if (restart && !Application.isBatchMode)
            {
                if (EditorUtility.DisplayDialog("Bomber Buddies", "Unity has to restart once to switch on the new input system (one time only). Restart now?", "Restart", "Later"))
                {
                    EditorSceneManager.SaveOpenScenes();
                    EditorApplication.OpenProject(Directory.GetCurrentDirectory());
                }
            }
        }

        static void Folder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var name = Path.GetFileName(path);
            if (!AssetDatabase.IsValidFolder(parent)) Folder(parent);
            AssetDatabase.CreateFolder(parent, name);
        }

        // ---- rendering: one URP asset for every quality level, tuned for a realistic look that still runs on a phone ----
        static UniversalRenderPipelineAsset SetupPipeline()
        {
            var rendererPath = SettingsDir + "/BB_Renderer.asset";
            var rendererData = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(rendererPath);
            if (rendererData == null)
            {
                rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                ResourceReloader.ReloadAllNullIn(rendererData, UniversalRenderPipelineAsset.packagePath);
                AssetDatabase.CreateAsset(rendererData, rendererPath);
            }
            var pipelinePath = SettingsDir + "/BB_Pipeline.asset";
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(pipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, pipelinePath);
            }
            pipeline.supportsHDR = true;
            pipeline.msaaSampleCount = 4;
            pipeline.renderScale = 1f;
            pipeline.shadowDistance = 170f;
            pipeline.shadowCascadeCount = 4;
            pipeline.mainLightShadowmapResolution = 4096;
            pipeline.maxAdditionalLightsCount = 8;
            pipeline.supportsCameraDepthTexture = true;
            pipeline.supportsCameraOpaqueTexture = true;
            pipeline.colorGradingMode = ColorGradingMode.HighDynamicRange;
            pipeline.useSRPBatcher = true;
            // soft shadows have no public setter; set the serialized field directly
            var pso = new SerializedObject(pipeline);
            var soft = pso.FindProperty("m_SoftShadowsSupported"); if (soft != null) soft.boolValue = true;
            var softQ = pso.FindProperty("m_SoftShadowQuality"); if (softQ != null) softQ.enumValueIndex = 2; // medium
            pso.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
            EditorUtility.SetDirty(rendererData);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int level = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(level, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;

            EnsureGlobalSettings();
            return pipeline;
        }

        /// <summary>URP keeps a "global settings" asset beside the pipeline and the player needs it. The editor normally creates it
        /// the first time it renders, which never happens on a build server, so it is created here. The class is internal to
        /// URP, hence the reflection; if anything about it changes the pipeline still creates it itself on first render.</summary>
        static void EnsureGlobalSettings()
        {
            try
            {
                var gsType = typeof(UniversalRenderPipelineAsset).Assembly.GetType("UnityEngine.Rendering.Universal.UniversalRenderPipelineGlobalSettings");
                if (gsType == null) return;
                System.Reflection.MethodInfo method = null;
                foreach (var m in typeof(RenderPipelineGlobalSettingsUtils).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
                {
                    if (m.Name == "TryEnsure" && m.IsGenericMethodDefinition && m.GetGenericArguments().Length == 2 && m.GetParameters().Length == 3) { method = m; break; }
                }
                if (method == null) return;
                var generic = method.MakeGenericMethod(gsType, typeof(UniversalRenderPipeline));
                object current = GraphicsSettings.GetSettingsForRenderPipeline<UniversalRenderPipeline>();
                var args = new object[] { current != null && gsType.IsInstanceOfType(current) ? current : null, SettingsDir + "/UniversalRenderPipelineGlobalSettings.asset", true };
                generic.Invoke(null, args);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("Bomber Buddies: could not pre-create the URP global settings (" + e.Message + "); URP will create them on first render.");
            }
        }

        // ---- materials and textures, saved as assets so every shader variant travels with the build ----
        static Material Lit(string name, Color color, float smoothness, float metallic, Texture2D tex, bool force)
        {
            string path = MaterialsDir + "/BB_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null && !force) return mat;
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (mat == null) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, path); } else mat.shader = shader;
            mat.SetColor("_BaseColor", color);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_Metallic", metallic);
            if (tex != null) { mat.SetTexture("_BaseMap", tex); }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        static Texture2D SaveTexture(string name, Texture2D tex, bool force)
        {
            string path = MaterialsDir + "/BB_" + name + ".asset";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null && !force) { Object.DestroyImmediate(tex); return existing; }
            if (existing != null) AssetDatabase.DeleteAsset(path);
            tex.name = name;
            AssetDatabase.CreateAsset(tex, path);
            return tex;
        }

        static void MakeTransparent(Material mat, float alpha)
        {
            mat.SetFloat("_Surface", 1f);   // 0 opaque, 1 transparent
            mat.SetFloat("_Blend", 0f);     // alpha blend
            mat.SetFloat("_ZWrite", 0f);
            var c = mat.GetColor("_BaseColor"); c.a = alpha; mat.SetColor("_BaseColor", c);
            BaseShaderGUI.SetupMaterialBlendMode(mat);
            mat.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(mat);
        }

        static void MakeEmissive(Material mat, Color emission)
        {
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            EditorUtility.SetDirty(mat);
        }

        static MaterialLibrary SetupMaterials(bool force)
        {
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>(LibraryPath);
            if (lib == null) { lib = ScriptableObject.CreateInstance<MaterialLibrary>(); AssetDatabase.CreateAsset(lib, LibraryPath); }
            var entries = new System.Collections.Generic.List<MaterialLibrary.Entry>();
            void Add(string n, Material m) { entries.Add(new MaterialLibrary.Entry { name = n, material = m }); }

            var concreteTex = SaveTexture("concrete", ProceduralTextures.Concrete(3), force);
            var roofTex = SaveTexture("roof", ProceduralTextures.RoofTiles(4), force);
            var barkTex = SaveTexture("bark", ProceduralTextures.Bark(5), force);
            var rockTex = SaveTexture("rock", ProceduralTextures.Rock(6), force);

            Add("wall", Lit("wall", new Color(0.78f, 0.74f, 0.66f), 0.25f, 0f, concreteTex, force));
            Add("concrete", Lit("concrete", new Color(0.62f, 0.62f, 0.6f), 0.2f, 0f, concreteTex, force));
            Add("roof", Lit("roof", new Color(0.9f, 0.85f, 0.8f), 0.3f, 0f, roofTex, force));
            Add("wood", Lit("wood", new Color(0.6f, 0.45f, 0.28f), 0.3f, 0f, barkTex, force));
            Add("bark", Lit("bark", new Color(0.55f, 0.45f, 0.34f), 0.2f, 0f, barkTex, force));
            Add("leaves", Lit("leaves", new Color(0.22f, 0.42f, 0.16f), 0.18f, 0f, null, force));
            Add("leaves2", Lit("leaves2", new Color(0.32f, 0.5f, 0.17f), 0.18f, 0f, null, force));
            Add("rock", Lit("rock", new Color(0.6f, 0.58f, 0.55f), 0.25f, 0f, rockTex, force));
            Add("dark", Lit("dark", new Color(0.13f, 0.13f, 0.14f), 0.35f, 0.1f, null, force));
            Add("skin", Lit("skin", new Color(0.78f, 0.6f, 0.48f), 0.35f, 0f, null, force));
            Add("uniform", Lit("uniform", Color.white, 0.18f, 0f, null, force));
            Add("helmet", Lit("helmet", new Color(0.24f, 0.28f, 0.18f), 0.3f, 0.1f, null, force));
            Add("helmet2", Lit("helmet2", new Color(0.2f, 0.2f, 0.23f), 0.3f, 0.1f, null, force));
            Add("gunmetal", Lit("gunmetal", new Color(0.2f, 0.2f, 0.22f), 0.62f, 0.85f, null, force));
            var glow = Lit("glow", new Color(1f, 0.9f, 0.6f), 0.5f, 0f, null, force); MakeEmissive(glow, new Color(1f, 0.85f, 0.5f) * 2.5f); Add("glow", glow);
            var water = Lit("water", new Color(0.08f, 0.3f, 0.42f, 0.78f), 0.95f, 0.05f, null, force); MakeTransparent(water, 0.78f); Add("water", water);

            string terrainPath = MaterialsDir + "/BB_terrain.mat";
            var terrainMat = AssetDatabase.LoadAssetAtPath<Material>(terrainPath);
            if (terrainMat == null) { terrainMat = new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")); AssetDatabase.CreateAsset(terrainMat, terrainPath); }
            Add("terrain", terrainMat);

            // tracers are line renderers coloured per shot, so they need a shader that reads vertex colour and blends
            string tracerPath = MaterialsDir + "/BB_tracer.mat";
            var tracer = AssetDatabase.LoadAssetAtPath<Material>(tracerPath);
            if (tracer == null) { tracer = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(tracer, tracerPath); }
            tracer.SetFloat("_Surface", 1f); tracer.SetFloat("_Blend", 0f); tracer.SetFloat("_ZWrite", 0f);
            tracer.SetColor("_BaseColor", Color.white);
            BaseShaderGUI.SetupMaterialBlendMode(tracer);
            tracer.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(tracer);
            Add("tracer", tracer);

            string particlePath = MaterialsDir + "/BB_particle.mat";
            var particle = AssetDatabase.LoadAssetAtPath<Material>(particlePath);
            if (particle == null) { particle = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit")); AssetDatabase.CreateAsset(particle, particlePath); }
            particle.SetFloat("_Surface", 1f); particle.SetFloat("_Blend", 0f); particle.SetFloat("_ZWrite", 0f);
            particle.SetColor("_BaseColor", Color.white);
            BaseShaderGUI.SetupMaterialBlendMode(particle);
            particle.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(particle);
            Add("particle", particle);

            string skyPath = MaterialsDir + "/BB_skybox.mat";
            var sky = AssetDatabase.LoadAssetAtPath<Material>(skyPath);
            if (sky == null) { sky = new Material(Shader.Find("Skybox/Procedural")); AssetDatabase.CreateAsset(sky, skyPath); }
            sky.SetFloat("_SunSize", 0.035f); sky.SetFloat("_AtmosphereThickness", 0.95f); sky.SetColor("_SkyTint", new Color(0.55f, 0.65f, 0.8f)); sky.SetColor("_GroundColor", new Color(0.42f, 0.4f, 0.36f)); sky.SetFloat("_Exposure", 1.2f);
            EditorUtility.SetDirty(sky);
            Add("skybox", sky);

            // the original game's look: flat-shaded vertex colours, and its gradient sky
            string lpPath = MaterialsDir + "/BB_lowpoly.mat";
            var lp = AssetDatabase.LoadAssetAtPath<Material>(lpPath);
            var lpShader = Shader.Find("BomberBuddies/LowPoly");
            if (lp == null && lpShader != null) { lp = new Material(lpShader); AssetDatabase.CreateAsset(lp, lpPath); }
            if (lp != null) { if (lpShader != null && lp.shader != lpShader) lp.shader = lpShader; EditorUtility.SetDirty(lp); Add("lowpoly", lp); }
            string sky2Path = MaterialsDir + "/BB_sky.mat";
            var sky2 = AssetDatabase.LoadAssetAtPath<Material>(sky2Path);
            var skyShader = Shader.Find("BomberBuddies/Sky");
            if (sky2 == null && skyShader != null) { sky2 = new Material(skyShader); AssetDatabase.CreateAsset(sky2, sky2Path); }
            if (sky2 != null) { if (skyShader != null && sky2.shader != skyShader) sky2.shader = skyShader; EditorUtility.SetDirty(sky2); Add("sky", sky2); }

            lib.entries = entries.ToArray();
            EditorUtility.SetDirty(lib);
            return lib;
        }

        // ---- the scene: one object that builds the world when the game starts ----
        static void SetupScene(MaterialLibrary lib, bool recreate)
        {
            if (recreate || !File.Exists(ScenePath))
            {
                if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var go = new GameObject("GameBootstrap");
                var gb = go.AddComponent<GameBootstrap>();
                gb.library = lib;
                EditorSceneManager.SaveScene(scene, ScenePath);
            }
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        }

        static void SetupPlayerSettings()
        {
            PlayerSettings.companyName = "Guarrasi";
            PlayerSettings.productName = "Bomber Buddies";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.guarrasi.bomberbuddies");
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Standalone, "com.guarrasi.bomberbuddies");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = (AndroidSdkVersions)25;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.Vulkan, GraphicsDeviceType.OpenGLES3 });
            PlayerSettings.Android.forceSDCardPermission = false;
            PlayerSettings.allowedAutorotateToLandscapeLeft = true;
            PlayerSettings.allowedAutorotateToLandscapeRight = true;
            PlayerSettings.allowedAutorotateToPortrait = false;
            PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
            PlayerSettings.runInBackground = true;
        }

        /// <summary>One 1024x1024 icon, handed to every platform; Unity scales it to each size the store wants.</summary>
        static void SetupIcons()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Icons/AppIcon.png");
            if (icon == null) { Debug.LogWarning("Bomber Buddies: no app icon at " + Root + "/Icons/AppIcon.png"); return; }
            foreach (var target in new[] { NamedBuildTarget.Unknown, NamedBuildTarget.Standalone, NamedBuildTarget.Android, NamedBuildTarget.iOS })
            {
                try
                {
                    var kinds = PlayerSettings.GetSupportedIconKinds(target);
                    foreach (var kind in kinds)
                    {
                        var icons = PlayerSettings.GetPlatformIcons(target, kind);
                        for (int i = 0; i < icons.Length; i++) icons[i].SetTextures(icon);
                        PlayerSettings.SetPlatformIcons(target, kind, icons);
                    }
                    if (kinds.Length == 0)
                    {
                        var sizes = PlayerSettings.GetIconSizes(target, IconKind.Any);
                        var arr = new Texture2D[sizes.Length];
                        for (int i = 0; i < arr.Length; i++) arr[i] = icon;
                        PlayerSettings.SetIcons(target, arr, IconKind.Any);
                    }
                }
                catch (System.Exception e) { Debug.LogWarning("Bomber Buddies: icon for " + target.TargetName + ": " + e.Message); }
            }
        }

        static void SetupLayers()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (assets == null || assets.Length == 0) return;
            var so = new SerializedObject(assets[0]);
            var layers = so.FindProperty("layers");
            if (layers == null || layers.arraySize < 10) return;
            layers.GetArrayElementAtIndex(8).stringValue = "Player";
            layers.GetArrayElementAtIndex(9).stringValue = "Enemy";
            so.ApplyModifiedProperties();
        }

        /// <summary>The new Input System has to be switched on in Player settings; the setting is not in the public API, so it is set
        /// through the serialized project settings. Returns true when the editor must restart for it to take effect.</summary>
        static bool SetupInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets == null || assets.Length == 0) return false;
            var so = new SerializedObject(assets[0]);
            var prop = so.FindProperty("activeInputHandler");
            if (prop == null) return false;
            if (prop.intValue == 2 || prop.intValue == 1) return false;
            prop.intValue = 2;   // both old and new: the new system for the game, the old one still there for anything that wants it
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            return true;
        }
    }
}
