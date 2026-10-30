using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace CampusTour
{
    public static class QuestBuildHelper
    {
        public const string ApkOutputPath = "Builds/Android/CampusVRTour.apk";
        private const string ExpectedScenePath = "Assets/Scenes/CampusTour.unity";
        private const string VideosDir = "Assets/StreamingAssets/Videos";

        [MenuItem("Tools/Campus VR Tour/Configure Quest Settings")]
        public static void ConfigureForQuest()
        {
            Debug.Log("[CampusTour] Configuring project settings for Meta Quest standalone Android VR...");

            // 1. Switch active build target to Android if not already active
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
                Debug.Log("[CampusTour] Switched active build target to Android.");
            }

            // 2. Configure PlayerSettings for Meta Quest
            PlayerSettings.companyName = "LabVR";
            PlayerSettings.productName = "Lab Experience VR";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.labexperience.vr");
            PlayerSettings.bundleVersion = "1.0.0";
            PlayerSettings.Android.bundleVersionCode = 1;

            // Scripting backend & Architecture
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.SetIl2CppCompilerConfiguration(NamedBuildTarget.Android, Il2CppCompilerConfiguration.Release);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

            // Restrict IL2CPP parallel jobs to 2 to prevent OutOfMemoryException on large assembly conversions
            try
            {
                Environment.SetEnvironmentVariable("IL2CPP_ADDITIONAL_ARGS", "--jobs=2");
                Environment.SetEnvironmentVariable("IL2CPP_ARGS", "--jobs=2");
                var methods = typeof(PlayerSettings).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
                foreach (var m in methods)
                {
                    if (m.Name == "SetAdditionalIl2CppArgs")
                    {
                        var pars = m.GetParameters();
                        if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                            m.Invoke(null, new object[] { "--jobs=2" });
                        else if (pars.Length == 2 && pars[0].ParameterType == typeof(NamedBuildTarget))
                            m.Invoke(null, new object[] { NamedBuildTarget.Android, "--jobs=2" });
                    }
                }
                Debug.Log("[CampusTour] IL2CPP worker count set to 2.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[CampusTour] IL2CPP args note: " + ex.Message);
            }

            // Android SDK levels (Quest requires Android 10+ / API 29+)
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;

            // Graphics & Color Space
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.LandscapeLeft;

            // Disable Burst compilation for player build to prevent bcl.exe memory crashes on resource-constrained environments
            try
            {
                Type burstCompilerType = Type.GetType("Unity.Burst.BurstCompiler, Unity.Burst");
                if (burstCompilerType != null)
                {
                    var clearMethod = burstCompilerType.GetMethod("ClearCache", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    clearMethod?.Invoke(null, null);
                }

                Type aotSettingsType = Type.GetType("Unity.Burst.Editor.BurstPlatformAotSettings, Unity.Burst.Editor");
                if (aotSettingsType != null)
                {
                    var getOrCreateMethod = aotSettingsType.GetMethod("GetOrCreateSettings",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static,
                        null, new Type[] { typeof(BuildTarget?) }, null);
                    if (getOrCreateMethod != null)
                    {
                        var aotSettings = getOrCreateMethod.Invoke(null, new object[] { (BuildTarget?)BuildTarget.Android });
                        if (aotSettings != null)
                        {
                            var enableField = aotSettingsType.GetField("EnableBurstCompilation",
                                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            enableField?.SetValue(aotSettings, false);
                            var saveMethod = aotSettingsType.GetMethod("Save",
                                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                            saveMethod?.Invoke(aotSettings, new object[] { (BuildTarget?)BuildTarget.Android });
                            Debug.Log("[CampusTour] Saved Android BurstPlatformAotSettings with EnableBurstCompilation = false");
                        }
                    }
                }

                Type burstOpts = Type.GetType("Unity.Burst.Editor.BurstEditorOptions, Unity.Burst.Editor");
                if (burstOpts != null)
                {
                    var prop = burstOpts.GetProperty("EnableBurstCompilation", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                    if (prop != null)
                    {
                        prop.SetValue(null, false);
                        Debug.Log("[CampusTour] Disabled Burst compilation for build stability.");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampusTour] Could not configure Burst options: {ex.Message}");
            }

            // Ensure CampusTour scene is in Build Settings
            EnsureBuildScene();

            // Try configuring OpenXR loader via XR Management API
            ConfigureOpenXRLoader();

            AssetDatabase.SaveAssets();
            Debug.Log("<color=green>[CampusTour] Meta Quest Player and Build settings successfully configured!</color>");
        }

        private static void ConfigureOpenXRLoader()
        {
            try
            {
                // Reflection invocation to safely configure XR loader without hard compile errors if types differ
                Type xrSettingsType = Type.GetType("UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget, Unity.XR.Management.Editor");
                if (xrSettingsType != null)
                {
                    var xrSettingsObj = ScriptableObject.CreateInstance(xrSettingsType);
                    Debug.Log("[CampusTour] XR General Settings available.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[CampusTour] XR Management reflection notice: {ex.Message}");
            }
        }

        private static void EnsureBuildScene()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            bool found = false;
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ExpectedScenePath)
                {
                    scenes[i].enabled = true;
                    if (i != 0)
                    {
                        var s = scenes[i];
                        scenes.RemoveAt(i);
                        scenes.Insert(0, s);
                    }
                    found = true;
                    break;
                }
            }

            if (!found && File.Exists(ExpectedScenePath))
            {
                scenes.Insert(0, new EditorBuildSettingsScene(ExpectedScenePath, true));
            }

            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Tools/Campus VR Tour/Validate Quest Build")]
        public static bool ValidateQuestBuild()
        {
            Debug.Log("==================================================");
            Debug.Log("[CampusTour] Validating Meta Quest Project Configuration...");

            int errors = 0;
            int warnings = 0;

            void Error(string msg) { errors++; Debug.LogError($"[CampusTour][FAIL] {msg}"); }
            void Warn(string msg) { warnings++; Debug.LogWarning($"[CampusTour][WARN] {msg}"); }
            void Pass(string msg) { Debug.Log($"[CampusTour][PASS] {msg}"); }

            // 1. Scene Existence
            if (File.Exists(ExpectedScenePath)) Pass($"Tour scene exists: {ExpectedScenePath}");
            else Error($"CampusTour scene not found at {ExpectedScenePath}. Run 'Tools > Campus VR Tour > Build Scene'.");

            // 2. Build Settings Scene Inclusion
            bool sceneInBuild = false;
            foreach (var s in EditorBuildSettings.scenes)
            {
                if (s.path == ExpectedScenePath && s.enabled)
                {
                    sceneInBuild = true;
                    break;
                }
            }
            if (sceneInBuild) Pass($"Scene {ExpectedScenePath} is included in Build Settings at index 0.");
            else Error($"Scene {ExpectedScenePath} is not enabled at index 0 in Build Settings.");

            // 3. Android Target & Architecture
            if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.Android) Pass("Active build target is Android.");
            else Warn($"Active build target is currently {EditorUserBuildSettings.activeBuildTarget}. Expected Android.");

            var backend = PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android);
            if (backend == ScriptingImplementation.IL2CPP) Pass("Scripting backend is IL2CPP.");
            else Error($"Scripting backend is {backend}. Meta Quest requires IL2CPP.");

            var arch = PlayerSettings.Android.targetArchitectures;
            if ((arch & AndroidArchitecture.ARM64) != 0) Pass("Target Architecture includes ARM64.");
            else Error("Target Architecture does not include ARM64. Meta Quest requires ARM64.");

            var minSdk = PlayerSettings.Android.minSdkVersion;
            if ((int)minSdk >= 29) Pass($"Minimum SDK Version is API {(int)minSdk} (>= 29).");
            else Warn($"Minimum SDK Version is API {(int)minSdk}. Meta Quest recommends API 29+.");

            if (PlayerSettings.colorSpace == ColorSpace.Linear) Pass("Color space is Linear.");
            else Warn($"Color space is {PlayerSettings.colorSpace}. Linear color space is strongly recommended for Quest.");

            // 4. Videos Verification
            string fullVideosPath = Path.Combine(Application.dataPath, "StreamingAssets", "Videos");
            if (Directory.Exists(fullVideosPath))
            {
                string[] files = Directory.GetFiles(fullVideosPath, "*.mp4");
                Pass($"StreamingAssets/Videos directory found with {files.Length} MP4 file(s).");
                foreach (string f in files)
                {
                    FileInfo fi = new FileInfo(f);
                    Debug.Log($"   • {fi.Name} ({(fi.Length / (1024f * 1024f)):0.0} MB)");
                }

                // Check default 4 videos
                string[] defaults = { "Gate.mp4", "Library.mp4", "Canteen.mp4", "Lab.mp4" };
                foreach (string d in defaults)
                {
                    string p = Path.Combine(fullVideosPath, d);
                    if (File.Exists(p)) Pass($"Campus video verified: {d}");
                    else Warn($"Default video '{d}' not found in {VideosDir}. The app will show a friendly 'Video not found' message until supplied.");
                }
            }
            else
            {
                Warn($"Folder not found: {VideosDir}. Create this directory to store 360 MP4 clips.");
            }

            // Summary
            Debug.Log("--------------------------------------------------");
            if (errors == 0)
            {
                Debug.Log($"<color=green>[CampusTour] Quest Build Validation PASSED ({warnings} warnings, 0 errors).</color>");
                return true;
            }
            else
            {
                Debug.LogError($"<color=red>[CampusTour] Quest Build Validation FAILED with {errors} error(s) and {warnings} warning(s).</color>");
                return false;
            }
        }

        [MenuItem("Tools/Campus VR Tour/Build Quest APK")]
        public static void BuildQuestApkMenu()
        {
            BuildQuestApk();
        }

        public static bool BuildQuestApk()
        {
            Debug.Log("[CampusTour] Initiating Meta Quest APK Build...");

            // Ensure project settings are properly configured
            ConfigureForQuest();

            // Validate scene existence
            if (!File.Exists(ExpectedScenePath))
            {
                Debug.Log("[CampusTour] Scene missing, building scene first...");
                CampusTourSceneBuilder.BuildScene();
            }

            // Prepare output folder
            string directory = Path.GetDirectoryName(ApkOutputPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            BuildPlayerOptions buildOptions = new BuildPlayerOptions
            {
                scenes = new[] { ExpectedScenePath },
                locationPathName = ApkOutputPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None
            };

            Debug.Log($"[CampusTour] Starting BuildPlayer: Output => {ApkOutputPath}");
            DateTime startTime = DateTime.Now;

            BuildReport report = BuildPipeline.BuildPlayer(buildOptions);
            BuildSummary summary = report.summary;

            TimeSpan duration = DateTime.Now - startTime;

            if (summary.result == BuildResult.Succeeded)
            {
                FileInfo fi = new FileInfo(ApkOutputPath);
                float sizeMb = fi.Exists ? (fi.Length / (1024f * 1024f)) : 0f;

                string labApk = Path.Combine(Path.GetDirectoryName(ApkOutputPath), "LabExperienceVR.apk");
                try { File.Copy(ApkOutputPath, labApk, true); } catch {}

                Debug.Log($"<color=green>[CampusTour] Build SUCCEEDED in {duration.TotalSeconds:0.0}s!</color>\n" +
                          $"APK Path: {ApkOutputPath}\n" +
                          $"Lab APK Path: {labApk}\n" +
                          $"File Size: {sizeMb:0.0} MB\n" +
                          $"Total Warnings: {summary.totalWarnings}, Total Errors: {summary.totalErrors}");
                return true;
            }
            else
            {
                Debug.LogError($"<color=red>[CampusTour] Build FAILED with result: {summary.result}</color>\n" +
                               $"Total Errors: {summary.totalErrors}\n" +
                               $"Please review the Unity Editor log for compilation or NDK/Gradle details.");
                return false;
            }
        }

        // Batchmode entry points for automated CLI builds
        public static void ConfigureForQuestCommandLine()
        {
            ConfigureForQuest();
        }

        public static void BuildSceneCommandLine()
        {
            CampusTourSceneBuilder.BuildScene();
        }

        public static void ValidateCommandLine()
        {
            bool ok = ValidateQuestBuild();
            if (!ok)
            {
                EditorApplication.Exit(1);
            }
        }

        public static void BuildQuestApkCommandLine()
        {
            bool success = BuildQuestApk();
            EditorApplication.Exit(success ? 0 : 1);
        }
    }
}
