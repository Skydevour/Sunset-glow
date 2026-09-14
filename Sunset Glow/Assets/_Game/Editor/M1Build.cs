using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using SunsetGlow.Debugging;
using SunsetGlow.Player;

namespace SunsetGlow.Editor
{
    public static class M1Build
    {
        public static void RefreshBodyAndBuild()
        {
            IslandSampleBuilder.RefreshBody();
            Build();
        }
        [MenuItem("Sunset Glow/M1/Create sample and build")]
        public static void CreateAndBuild()
        {
            IslandSampleBuilder.CreateSample();
            Build();
        }

        [MenuItem("Sunset Glow/M1/Build Windows development player")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeBuild.ScenePath, OpenSceneMode.Single);
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            if (player == null) throw new InvalidOperationException("Create M1 sample before building.");
            var validation = UnityEngine.Object.FindFirstObjectByType<M1ValidationRunner>();
            if (validation == null) validation = new GameObject("M1 Validation").AddComponent<M1ValidationRunner>();
            validation.player = player;
            validation.observationPoints = player.observationPoints;
            var baseline = player.viewCamera.GetComponent<BaselineRunner>();
            if (baseline == null) baseline = player.viewCamera.gameObject.AddComponent<BaselineRunner>();
            baseline.allowFlight = false;
            baseline.measurementScope = "M1 island sample, forest observation; real elapsed Update intervals, preallocated arrays 12 MiB. Compare M0 same machine/quality, inspect focus counts.";
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.enableFrameTimingStats = true;
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(PrototypeBuild.ScenePath, true) };
            string output = Path.GetFullPath("../Builds/M1/IslandPrototype.exe");
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { PrototypeBuild.ScenePath }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            Directory.CreateDirectory("../artifacts/m1");
            File.WriteAllText("../artifacts/m1/build.json", JsonUtility.ToJson(new Summary
            {
                result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds,
                bytes = report.summary.totalSize, output = output
            }, true));
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("M1 build failed.");
        }
        [Serializable] sealed class Summary { public string result, output; public int errors, warnings; public double seconds; public ulong bytes; }
    }
}
