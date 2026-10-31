using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using BomberBuddies.Editor;

namespace BomberBuddies.Tests
{
    /// <summary>Checks that a fresh checkout turns into a working project: pipeline assigned, materials present, scene in the build list.</summary>
    public class SetupTests
    {
        [OneTimeSetUp]
        public void Setup() { ProjectBootstrap.EnsureSetup(false); }

        [Test]
        public void PipelineIsUniversal()
        {
            Assert.IsNotNull(GraphicsSettings.defaultRenderPipeline, "no render pipeline assigned");
            Assert.IsInstanceOf<UniversalRenderPipelineAsset>(GraphicsSettings.defaultRenderPipeline);
        }

        [Test]
        public void MaterialLibraryIsComplete()
        {
            var lib = AssetDatabase.LoadAssetAtPath<MaterialLibrary>("Assets/BomberBuddies/Settings/BB_Materials.asset");
            Assert.IsNotNull(lib, "material library missing");
            foreach (var name in new[] { "terrain", "water", "skybox", "uniform", "skin", "dark", "helmet", "helmet2", "gunmetal", "wood", "wall", "roof", "glow", "concrete", "bark", "leaves", "leaves2", "rock", "tracer", "particle" })
            {
                var m = lib.Get(name);
                Assert.IsNotNull(m, "material '" + name + "' missing from the library");
                Assert.IsNotNull(m.shader, "material '" + name + "' has no shader");
                Assert.IsTrue(m.shader.isSupported, "shader for '" + name + "' (" + m.shader.name + ") is not supported / did not compile");
            }
        }

        [Test]
        public void SceneIsInTheBuild()
        {
            Assert.IsTrue(System.IO.File.Exists(ProjectBootstrap.ScenePath), "island scene missing");
            Assert.IsTrue(EditorBuildSettings.scenes.Length >= 1 && EditorBuildSettings.scenes[0].path == ProjectBootstrap.ScenePath, "scene not first in the build settings");
        }

        [Test]
        public void ProceduralTexturesAreOpaque()
        {
            var tex = ProceduralTextures.Grass(1);
            Assert.AreEqual(1f, tex.GetPixel(3, 5).a, 0.001f);
            Object.DestroyImmediate(tex);
        }

        [Test]
        public void NoiseIsDeterministic()
        {
            Assert.AreEqual(Noise.Fbm(1.3f, 2.7f, 4, 2f, 0.5f, 11), Noise.Fbm(1.3f, 2.7f, 4, 2f, 0.5f, 11), 1e-6f);
            Assert.AreNotEqual(Noise.Fbm(1.3f, 2.7f, 4, 2f, 0.5f, 11), Noise.Fbm(1.3f, 2.7f, 4, 2f, 0.5f, 12));
        }
    }
}
