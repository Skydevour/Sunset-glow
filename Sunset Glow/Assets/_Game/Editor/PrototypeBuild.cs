using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using SunsetGlow.Debugging;

namespace SunsetGlow.Editor
{
    public static class PrototypeBuild
    {
        public const string ScenePath = "Assets/_Game/Scenes/IslandPrototype.unity";

        [MenuItem("Sunset Glow/M0/Create baseline scene")]
        public static void CreateBaseline()
        {
            if (File.Exists(ScenePath))
                throw new InvalidOperationException("Development scene already exists; refusing to overwrite authored work.");
            Directory.CreateDirectory("Assets/_Game/Scenes");
            Directory.CreateDirectory("Assets/_Game/Data/Environment");
            Directory.CreateDirectory("Assets/_Game/Materials");
            AssetDatabase.Refresh();
            if (!AssetDatabase.CopyAsset("Assets/OutdoorsScene.unity", ScenePath))
                throw new IOException("Could not copy template scene.");
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var volume in UnityEngine.Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.sharedProfile == null) continue;
                string source = AssetDatabase.GetAssetPath(volume.sharedProfile);
                string destination = "Assets/_Game/Data/Environment/" + volume.name.Replace(" ", "") + "Profile.asset";
                if (!AssetDatabase.CopyAsset(source, destination)) throw new IOException("Could not isolate environment profile.");
                volume.sharedProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(destination);
            }

            var ground = new Material(Shader.Find("HDRP/Lit")) { name = "Baseline stone" };
            ground.SetColor("_BaseColor", new Color(0.32f, 0.37f, 0.35f));
            AssetDatabase.CreateAsset(ground, "Assets/_Game/Materials/BaselineStone.mat");
            var marker = new Material(Shader.Find("HDRP/Lit")) { name = "Scale marker" };
            marker.SetColor("_BaseColor", new Color(0.65f, 0.24f, 0.09f));
            AssetDatabase.CreateAsset(marker, "Assets/_Game/Materials/BaselineMarker.mat");
            Block("Baseline ground 40m", new Vector3(0, -0.25f, 0), new Vector3(40, 0.5f, 40), ground);
            Block("Human scale 1.8m", new Vector3(0, 0.9f, 4), new Vector3(0.5f, 1.8f, 0.4f), marker);
            Block("North +Z marker", new Vector3(0, 0.025f, 9), new Vector3(0.15f, 0.05f, 8), marker);
            Block("East +X marker", new Vector3(9, 0.025f, 0), new Vector3(8, 0.05f, 0.15f), marker);
            var observations = new GameObject("ObservationPoints (+Z North, +X East, metres)");
            string[] names = { "EastCoast", "WestCoast", "Forest", "Hilltop", "CabinWindow" };
            foreach (string name in names)
            {
                var point = new GameObject("Reserved_" + name);
                point.transform.SetParent(observations.transform);
            }
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("Template has no main camera.");
            camera.transform.SetPositionAndRotation(new Vector3(7, 1.65f, -9), Quaternion.Euler(6, -25, 0));
            camera.nearClipPlane = 0.05f;
            camera.farClipPlane = 1500;
            camera.fieldOfView = 75;
            camera.gameObject.AddComponent<BaselineRunner>();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("M0_SCENE_CREATED " + ScenePath);
        }

        static void Block(string name, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.position = position;
            block.transform.localScale = scale;
            block.GetComponent<Renderer>().sharedMaterial = material;
        }

        [MenuItem("Sunset Glow/M0/Build Windows development player")]
        public static void BuildBaseline()
        {
            if (!File.Exists(ScenePath)) CreateBaseline();
            // Once M1 replaces the development scene, rebuild M0 from its preserved snapshot.
            const string baselineSnapshot = "Assets/_Game/Scenes/M0Baseline.unity";
            string buildScene = File.Exists(baselineSnapshot) ? baselineSnapshot : ScenePath;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.fullScreenMode = FullScreenMode.Windowed;
            PlayerSettings.runInBackground = true;
            PlayerSettings.resizableWindow = false;
            PlayerSettings.enableFrameTimingStats = true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset")[0]);
            settings.FindProperty("activeInputHandler").intValue = 0;
            settings.ApplyModifiedPropertiesWithoutUndo();
            QualitySettings.SetQualityLevel(1, true);
            QualitySettings.vSyncCount = 0;
            EditorBuildSettings.RemoveConfigObject("com.unity.input.settings.actions");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Builds/M0/IslandPrototype.exe"));
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { buildScene }, locationPathName = output,
                target = BuildTarget.StandaloneWindows64, options = BuildOptions.Development
            });
            string summary = JsonUtility.ToJson(new BuildEvidence
            {
                result = report.summary.result.ToString(), errors = report.summary.totalErrors,
                warnings = report.summary.totalWarnings, seconds = report.summary.totalTime.TotalSeconds,
                bytes = report.summary.totalSize, output = output, unity = Application.unityVersion
            }, true);
            string evidence = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/m0"));
            Directory.CreateDirectory(evidence);
            File.WriteAllText(Path.Combine(evidence, "build.json"), summary);
            Debug.Log("M0_BUILD " + summary);
            if (report.summary.result != BuildResult.Succeeded) throw new Exception("M0 build failed.");
        }

        [Serializable]
        sealed class BuildEvidence
        {
            public string result, output, unity;
            public int errors, warnings;
            public double seconds;
            public ulong bytes;
        }
    }
}
