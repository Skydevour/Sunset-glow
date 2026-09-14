using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SunsetGlow.EnvironmentArt;
using SunsetGlow.Player;

namespace SunsetGlow.Debugging
{
    /// <summary>Fixed camera and weather: a full minute of rendered cloud motion, then pause/resume.</summary>
    public sealed class CloudMotionValidationRunner : MonoBehaviour
    {
        [Serializable] sealed class Frame
        {
            public string image;
            public float realSeconds, differenceFromPrevious, differenceFromStart;
            public Vector2 cloudDisplacement;
        }
        [Serializable] sealed class Report
        {
            public bool passed;
            public string scope = "Fixed clear weather, fixed camera, 60 real seconds at normal time; pixel differences sample only upper sky (55-90% image height). No season transition.";
            public string differenceUnits = "Mean absolute RGB difference normalized to 0-1";
            public int runtimeErrors;
            public float movingSkyDifference, pausedSkyDifference, resumedSkyDifference;
            public bool cloudPhaseFrozen;
            public List<Frame> frames = new List<Frame>();
        }
        string output;
        float start;
        Color32[] first, previous;
        WindEnvironmentConsumer consumer;
        readonly Report report = new Report();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Install()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-cloudMotionCapture")
                {
                    var runner = new GameObject("Cloud motion capture").AddComponent<CloudMotionValidationRunner>();
                    runner.output = args[i + 1];
                    break;
                }
        }

        IEnumerator Start()
        {
            if (!Path.IsPathRooted(output) || (Directory.Exists(output) && Directory.GetFileSystemEntries(output).Length > 0))
            {
                Debug.LogError("-cloudMotionCapture requires a fresh absolute directory");
                Application.Quit(2); yield break;
            }
            Directory.CreateDirectory(output);
            Application.logMessageReceived += Log;
            Application.runInBackground = true;
            yield return null;
            var presenter = FindFirstObjectByType<CoastalEnvironmentPresenter>();
            var player = FindFirstObjectByType<FirstPersonController>();
            consumer = FindFirstObjectByType<WindEnvironmentConsumer>();
            if (presenter == null || player == null || consumer == null) { Finish(false); yield break; }
            player.SetPaused(false);
            player.enabled = false;
            player.BobEnabled = false;
            player.Teleport(new Vector3(45, 3, -5), Quaternion.Euler(-18, 90, 0));
            presenter.SetLook(0, true);
            // Do not transition looks or move the viewpoint anywhere in this run.
            yield return new WaitForSecondsRealtime(12);
            start = Time.realtimeSinceStartup;
            for (int i = 0; i <= 12; i++)
            {
                if (i > 0) yield return new WaitForSecondsRealtime(5);
                yield return Capture("cloud-" + (i * 5).ToString("D2") + "s");
            }
            report.movingSkyDifference = Difference(first, previous);
            player.SetPaused(true);
            // Let cloud temporal reprojection settle before evaluating a frozen image.
            yield return new WaitForSecondsRealtime(2);
            yield return Capture("pause-start");
            Color32[] pauseStart = previous;
            Vector2 phase = consumer.CloudDisplacement;
            yield return new WaitForSecondsRealtime(5);
            yield return Capture("pause-after-5s");
            report.pausedSkyDifference = Difference(pauseStart, previous);
            report.cloudPhaseFrozen = (phase - consumer.CloudDisplacement).sqrMagnitude < .000001f;
            Color32[] resumeStart = previous;
            player.SetPaused(false);
            yield return new WaitForSecondsRealtime(5);
            yield return Capture("resume-after-5s");
            report.resumedSkyDifference = Difference(resumeStart, previous);
            Finish(report.cloudPhaseFrozen && report.movingSkyDifference > report.pausedSkyDifference * 2f + .001f
                && report.resumedSkyDifference > report.pausedSkyDifference + .0001f);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            Texture2D frame = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(output, name + ".png"), frame.EncodeToPNG());
            Color32[] all = frame.GetPixels32();
            int bottom = Mathf.FloorToInt(frame.height * .55f), top = Mathf.FloorToInt(frame.height * .90f);
            Color32[] sky = new Color32[(top - bottom) * frame.width];
            Array.Copy(all, bottom * frame.width, sky, 0, sky.Length);
            if (first == null) first = sky;
            report.frames.Add(new Frame { image = name + ".png", realSeconds = Time.realtimeSinceStartup - start,
                differenceFromPrevious = Difference(previous, sky), differenceFromStart = Difference(first, sky),
                cloudDisplacement = consumer.CloudDisplacement });
            previous = sky;
            Destroy(frame);
        }

        static float Difference(Color32[] a, Color32[] b)
        {
            if (a == null || b == null || a.Length != b.Length) return 0;
            double total = 0;
            for (int i = 0; i < a.Length; i++)
                total += Math.Abs(a[i].r - b[i].r) + Math.Abs(a[i].g - b[i].g) + Math.Abs(a[i].b - b[i].b);
            return (float)(total / (a.Length * 3d * 255d));
        }

        void Log(string message, string stack, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.runtimeErrors++;
        }
        void Finish(bool passed)
        {
            report.passed = passed && report.runtimeErrors == 0;
            File.WriteAllText(Path.Combine(output, "cloud-motion.json"), JsonUtility.ToJson(report, true));
            Debug.Log("CLOUD_MOTION " + report.passed + " moving=" + report.movingSkyDifference + " paused=" + report.pausedSkyDifference);
            Application.Quit(report.passed ? 0 : 1);
        }
        void OnDestroy() => Application.logMessageReceived -= Log;
    }
}
