using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace BomberBuddies.Editor
{
    /// <summary>
    /// Command-line builds (used by the GitHub Actions workflow, and from the menu for a local build). Reads the arguments the
    /// GameCI builder passes (-customBuildPath, -customBuildName, -customBuildTarget, -buildVersion, -androidVersionCode,
    /// keystore details), makes sure the project is set up, then builds the one scene.
    /// </summary>
    public static class BuildScript
    {
        [MenuItem("Bomber Buddies/Build/Windows (to Builds folder)")]
        public static void MenuBuildWindows() { Run(BuildTarget.StandaloneWindows64, "Builds/Windows/BomberBuddies.exe", "0.1.0", 1); }

        [MenuItem("Bomber Buddies/Build/Android APK (to Builds folder)")]
        public static void MenuBuildAndroid() { Run(BuildTarget.Android, "Builds/Android/BomberBuddies.apk", "0.1.0", 1); }

        /// <summary>Entry point for the command line: Unity -executeMethod BomberBuddies.Editor.BuildScript.Build</summary>
        public static void Build()
        {
            var args = ParseArgs();
            string targetName = Get(args, "customBuildTarget", Get(args, "buildTarget", "StandaloneWindows64"));
            BuildTarget target;
            if (!Enum.TryParse(targetName, true, out target)) { Debug.LogError("Unknown build target " + targetName); EditorApplication.Exit(120); return; }
            string path = Get(args, "customBuildPath", null);
            string name = Get(args, "customBuildName", "BomberBuddies");
            if (string.IsNullOrEmpty(path)) path = Path.Combine("Builds", targetName);
            if (target != BuildTarget.iOS && !Path.HasExtension(path)) path = Path.Combine(path, name + ExtensionFor(target));   // iOS output is a folder (an Xcode project)
            string version = Get(args, "buildVersion", "0.1.0");
            int code; if (!int.TryParse(Get(args, "androidVersionCode", "1"), out code)) code = 1;

            string keystoreName = Get(args, "androidKeystoreName", "");
            if (target == BuildTarget.Android)
            {
                if (!string.IsNullOrEmpty(keystoreName) && File.Exists(keystoreName))
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = keystoreName;
                    PlayerSettings.Android.keystorePass = Get(args, "androidKeystorePass", "");
                    PlayerSettings.Android.keyaliasName = Get(args, "androidKeyaliasName", "");
                    PlayerSettings.Android.keyaliasPass = Get(args, "androidKeyaliasPass", "");
                }
                else PlayerSettings.Android.useCustomKeystore = false;
                string exportType = Get(args, "androidExportType", "androidPackage");
                EditorUserBuildSettings.buildAppBundle = exportType == "androidAppBundle";
                EditorUserBuildSettings.exportAsGoogleAndroidProject = exportType == "androidStudioProject";
                string sdk = Get(args, "androidTargetSdkVersion", "");
                int sdkInt; if (!string.IsNullOrEmpty(sdk) && int.TryParse(sdk, out sdkInt) && sdkInt > 0) PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)sdkInt;
            }

            if (target == BuildTarget.iOS)
            {
                string bundle = Environment.GetEnvironmentVariable("IOS_BUNDLE_ID"), teamId = Environment.GetEnvironmentVariable("APPLE_TEAM_ID"), buildNo = Get(args, "iosBuildNumber", "");
                if (!string.IsNullOrEmpty(bundle)) PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.iOS, bundle);
                if (!string.IsNullOrEmpty(teamId)) { PlayerSettings.iOS.appleDeveloperTeamID = teamId; PlayerSettings.iOS.appleEnableAutomaticSigning = false; }
                if (!string.IsNullOrEmpty(buildNo)) PlayerSettings.iOS.buildNumber = buildNo;
                PlayerSettings.iOS.targetOSVersionString = "15.0";
                PlayerSettings.iOS.requiresPersistentWiFi = false;
            }
            var report = Run(target, path, version, code);
            if (report == null) { EditorApplication.Exit(121); return; }
            var summary = report.summary;
            Debug.Log("Build " + summary.result + ": " + summary.totalSize / (1024 * 1024) + " MB, " + summary.totalErrors + " errors, " + summary.totalWarnings + " warnings, " + summary.totalTime.TotalMinutes.ToString("0.0") + " min");
            if (summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
            else if (Application.isBatchMode) EditorApplication.Exit(0);
        }

        public static BuildReport Run(BuildTarget target, string path, string version, int versionCode)
        {
            ProjectBootstrap.EnsureSetup(false);
            PlayerSettings.bundleVersion = version;
            PlayerSettings.Android.bundleVersionCode = versionCode;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            if (EditorUserBuildSettings.activeBuildTarget != target)
            {
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(group, target)) { Debug.LogError("Cannot switch to build target " + target + " (is the platform module installed?)"); return null; }
            }
            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            var options = new BuildPlayerOptions
            {
                scenes = new[] { ProjectBootstrap.ScenePath },
                locationPathName = path,
                target = target,
                targetGroup = group,
                options = BuildOptions.None,
            };
            Debug.Log("Building " + target + " -> " + path);
            return BuildPipeline.BuildPlayer(options);
        }

        static string ExtensionFor(BuildTarget target)
        {
            switch (target)
            {
                case BuildTarget.StandaloneWindows:
                case BuildTarget.StandaloneWindows64: return ".exe";
                case BuildTarget.Android: return EditorUserBuildSettings.buildAppBundle ? ".aab" : ".apk";
                case BuildTarget.StandaloneOSX: return ".app";
                default: return "";
            }
        }

        static Dictionary<string, string> ParseArgs()
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (!args[i].StartsWith("-", StringComparison.Ordinal) || args[i].Length < 2) continue;
                string key = args[i].Substring(1);
                string value = i + 1 < args.Length && !args[i + 1].StartsWith("-", StringComparison.Ordinal) ? args[i + 1] : "";
                result[key] = value;
            }
            return result;
        }

        static string Get(Dictionary<string, string> args, string key, string fallback)
        {
            string v;
            return args.TryGetValue(key, out v) && !string.IsNullOrEmpty(v) ? v : fallback;
        }
    }
}
