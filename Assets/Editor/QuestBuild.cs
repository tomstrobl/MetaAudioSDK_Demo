using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MetaAudioSDKDemo.Editor
{
    /// <summary>
    /// Builds the demo as an Android APK for Meta Quest into the Builds/ folder at the project root.
    /// Install it afterwards with scripts/install-quest.sh (or plain "adb install -r").
    /// Also usable headless: Unity -batchmode -quit -projectPath . -executeMethod MetaAudioSDKDemo.Editor.QuestBuild.BuildApk
    /// </summary>
    public static class QuestBuild
    {
        public const string ApkFileName = "MetaXRAudioDemo.apk";

        public static string BuildsDirectory => Path.Combine(Directory.GetCurrentDirectory(), "Builds");
        public static string ApkPath => Path.Combine(BuildsDirectory, ApkFileName);

        [MenuItem("Tools/MetaAudioSDK Demo/Build Quest APK")]
        static void BuildFromMenu()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUtility.DisplayDialog("Build Quest APK",
                    "The active platform is not Android. Switch to Android now? This reimports assets and can take a while.",
                    "Switch and build", "Cancel"))
                return;

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            var report = Build();
            if (report.summary.result == BuildResult.Succeeded)
                EditorUtility.RevealInFinder(ApkPath);
        }

        /// <summary>Entry point for -executeMethod. Exits with a non-zero code on failure.</summary>
        public static void BuildApk()
        {
            var report = Build();
            if (Application.isBatchMode)
                EditorApplication.Exit(report.summary.result == BuildResult.Succeeded ? 0 : 1);
        }

        static BuildReport Build()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Could not switch the active build target to Android. Is Android Build Support installed?");

            var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
            if (scenes.Length == 0)
                throw new InvalidOperationException("No scenes are enabled in the build settings.");

            Directory.CreateDirectory(BuildsDirectory);
            EditorUserBuildSettings.buildAppBundle = false;

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = ApkPath,
                target = BuildTarget.Android,
                targetGroup = BuildTargetGroup.Android,
                options = BuildOptions.None,
            });

            var summary = report.summary;
            if (summary.result == BuildResult.Succeeded)
                Debug.Log($"[QuestBuild] Built {ApkPath} ({new FileInfo(ApkPath).Length / (1024f * 1024f):F0} MB) in {summary.totalTime.TotalSeconds:F0} s");
            else
                Debug.LogError($"[QuestBuild] Build {summary.result} with {summary.totalErrors} error(s). See the Console for details.");
            return report;
        }
    }
}
