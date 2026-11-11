using System.IO;
using UnityEditor;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.XR.Management;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features;

namespace BomberBuddies.Editor
{
    /// <summary>Turns on PC headsets (OpenXR: Valve Index, Vive, Quest Link through SteamVR) for the Windows build and the editor,
    /// with the hand-controller profiles, so nobody has to tick boxes in Project Settings. Phones are left alone.</summary>
    public static class XrSetup
    {
        public static void Run()
        {
            try
            {
                const BuildTargetGroup group = BuildTargetGroup.Standalone;
                XRGeneralSettingsPerBuildTarget per;
                if (!EditorBuildSettings.TryGetConfigObject(XRGeneralSettings.k_SettingsKey, out per) || per == null)
                {
                    Directory.CreateDirectory("Assets/XR");
                    per = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
                    AssetDatabase.CreateAsset(per, "Assets/XR/XRGeneralSettingsPerBuildTarget.asset");
                    EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, per, true);
                }
                if (!per.HasSettingsForBuildTarget(group)) per.CreateDefaultSettingsForBuildTarget(group);
                if (!per.HasManagerSettingsForBuildTarget(group)) per.CreateDefaultManagerSettingsForBuildTarget(group);
                var general = per.SettingsForBuildTarget(group);
                var manager = per.ManagerSettingsForBuildTarget(group);
                if (general != null) { general.InitManagerOnStart = true; EditorUtility.SetDirty(general); }
                if (manager != null)
                {
                    bool has = false; foreach (var l in manager.activeLoaders) if (l is OpenXRLoader) has = true;
                    if (!has) XRPackageMetadataStore.AssignLoader(manager, typeof(OpenXRLoader).FullName, group);
                    EditorUtility.SetDirty(manager);
                }
                var oxr = OpenXRSettings.GetSettingsForBuildTargetGroup(group);
                if (oxr != null)
                {
                    // the game's own shaders draw one eye at a time
                    oxr.renderMode = OpenXRSettings.RenderMode.MultiPass;
                    foreach (var f in oxr.GetFeatures<OpenXRInteractionFeature>())
                    { string n = f.GetType().Name; if (n == "ValveIndexControllerProfile" || n == "HTCViveControllerProfile" || n == "OculusTouchControllerProfile" || n == "KHRSimpleControllerProfile") { f.enabled = true; EditorUtility.SetDirty(f); } }
                    EditorUtility.SetDirty(oxr);
                }
                EditorUtility.SetDirty(per);
                AssetDatabase.SaveAssets();
                Debug.Log("Bomber Buddies: PC headsets (OpenXR) are on for the Windows build.");
            }
            catch (System.Exception e) { Debug.LogWarning("Bomber Buddies: could not set up PC headsets: " + e.Message); }
        }
    }
}
