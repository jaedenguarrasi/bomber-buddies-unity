using System.IO;
using UnityEditor;
using UnityEngine;

namespace BomberBuddies.Editor
{
    /// <summary>
    /// Every model dropped under Assets/BomberBuddies/Characters is set up the same way without anyone touching the
    /// Inspector: Humanoid rig (so any animation plays on any body), textures and materials pulled out of the file,
    /// animations marked loopable where it makes sense, and a sensible scale.
    /// </summary>
    public class CharacterImporter : AssetPostprocessor
    {
        const string Root = "Assets/BomberBuddies/Characters/";

        bool Mine { get { return assetPath.StartsWith(Root); } }

        void OnPreprocessModel()
        {
            if (!Mine) return;
            var mi = (ModelImporter)assetImporter;
            mi.animationType = ModelImporterAnimationType.Human;
            mi.avatarSetup = IsAnimationOnly() ? ModelImporterAvatarSetup.CreateFromThisModel : ModelImporterAvatarSetup.CreateFromThisModel;
            mi.importBlendShapes = true;
            mi.importVisibility = false;
            mi.importCameras = false;
            mi.importLights = false;
            mi.isReadable = false;
            mi.meshCompression = ModelImporterMeshCompression.Off;
            mi.optimizeGameObjects = false;
            mi.globalScale = 1f;
            mi.useFileScale = true;
            mi.materialImportMode = IsAnimationOnly() ? ModelImporterMaterialImportMode.None : ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            mi.materialLocation = ModelImporterMaterialLocation.InPrefab;
            if (!IsAnimationOnly()) mi.ExtractTextures(Path.GetDirectoryName(assetPath) + "/Textures");
        }

        void OnPreprocessAnimation()
        {
            if (!Mine) return;
            var mi = (ModelImporter)assetImporter;
            var clips = mi.defaultClipAnimations;
            if (clips == null || clips.Length == 0) return;
            string name = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            bool loop = !(name.Contains("death") || name.Contains("dying") || name.Contains("hit") || name.Contains("jump") || name.Contains("land") || name.Contains("reload"));
            bool inPlace = name.Contains("inplace") || name.Contains("in place") || name.Contains("idle") || name.Contains("aim") || name.Contains("death") || name.Contains("reload") || name.Contains("jump");
            for (int i = 0; i < clips.Length; i++)
            {
                var c = clips[i];
                c.name = Path.GetFileNameWithoutExtension(assetPath);
                c.loopTime = loop;
                c.loopPose = loop;
                c.lockRootRotation = true;
                c.lockRootHeightY = true;
                c.lockRootPositionXZ = true;           // the game moves the body; the clip only poses it
                c.keepOriginalOrientation = true;
                c.keepOriginalPositionY = true;
                c.keepOriginalPositionXZ = inPlace;
                c.heightFromFeet = !name.Contains("jump");
                clips[i] = c;
            }
            mi.clipAnimations = clips;
        }

        bool IsAnimationOnly()
        {
            // Mixamo animation downloads ("without skin") have no mesh; character downloads do
            string dir = Path.GetDirectoryName(assetPath).Replace('\\', '/');
            return dir.EndsWith("/Animations") || dir.Contains("/Animations/");
        }

        void OnPreprocessTexture()
        {
            if (!Mine) return;
            var ti = (TextureImporter)assetImporter;
            string n = Path.GetFileNameWithoutExtension(assetPath).ToLowerInvariant();
            if (n.Contains("normal") || n.EndsWith("_n") || n.Contains("nrm")) ti.textureType = TextureImporterType.NormalMap;
            ti.maxTextureSize = 2048;
            ti.mipmapEnabled = true;
            ti.anisoLevel = 4;
            ti.streamingMipmaps = true;
        }
    }
}
