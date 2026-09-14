using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using SunsetGlow.Player;

namespace SunsetGlow.Debugging
{
    public sealed class M1ValidationRunner : MonoBehaviour
    {
        public FirstPersonController player;
        public Transform[] observationPoints;
        string output;
        readonly List<string> failures = new List<string>();
        readonly List<string> checks = new List<string>();
        int errors;

        IEnumerator Start()
        {
            string[] args = System.Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; i++)
                if (args[i] == "-m1Capture") output = args[i + 1];
            if (string.IsNullOrEmpty(output)) yield break;
            if (!Path.IsPathRooted(output)) { Debug.LogError("-m1Capture needs absolute output directory."); Application.Quit(2); yield break; }
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
            player.SetPaused(false);
            player.enabled = false; // Tests drive the same controller movement method, without a second Update.
            yield return new WaitForSecondsRealtime(8);
            if (Array.IndexOf(args, "-m1Benchmark") >= 0)
            {
                player.Teleport(observationPoints[2].position, observationPoints[2].rotation);
                yield break; // BaselineRunner owns the 15s warmup / 60s capture / exit.
            }

            Check("five_observation_points", observationPoints != null && observationPoints.Length == 5);
            for (int i = 0; i < observationPoints.Length; i++)
            {
                Transform point = observationPoints[i];
                player.Teleport(point.position, point.rotation);
                for (int frame = 0; frame < 40; frame++) { player.SimulateMove(Vector2.zero, 1f / 60); yield return null; }
                Check(point.name + "_ground", Physics.Raycast(player.transform.position + Vector3.up * 0.3f, Vector3.down, 0.7f, ~LayerMask.GetMask("Player")));
                Check(point.name + "_above_water", player.transform.position.y >= 0);
                bool sheltered = Physics.Raycast(player.transform.position + Vector3.up * 1.65f, Vector3.up, 8, LayerMask.GetMask("Shelter"));
                Check(point.name + "_shelter", sheltered == (i == 4));
                yield return Capture(point.name);
            }

            // Body visible from a grounded standing position, independent of head animation.
            player.Teleport(observationPoints[2].position, Quaternion.Euler(78, 0, 0));
            yield return Capture("BodyLookDown");

            Transform cabin = observationPoints[4];
            Vector3 inside = cabin.position;
            int resetsBeforeWall = player.ResetCount;
            player.Teleport(inside, Quaternion.Euler(0, 270, 0));
            for (int i = 0; i < 120; i++) { player.SimulateMove(Vector2.up, 1f / 60); yield return null; }
            Check("cabin_west_wall_blocks", player.transform.position.x < -2.35f && player.transform.position.x > -2.65f && player.ResetCount == resetsBeforeWall);
            yield return Capture("CabinWall");

            // The cabin is centred at x=0,z=8 with south doorway; move from inside through its centre.
            player.Teleport(new Vector3(0, inside.y + 0.05f, 8), Quaternion.Euler(0, 180, 0));
            for (int i = 0; i < 90; i++) { player.SimulateMove(Vector2.up, 1f / 60); yield return null; }
            Check("south_door_passable", player.transform.position.z < 4.9f && player.transform.position.z > 1.5f && player.ResetCount == resetsBeforeWall);

            // Walk continuous ground, not just teleport between known safe samples.
            player.Teleport(player.spawnPoint.position, player.spawnPoint.rotation);
            yield return WalkTo("walk_spawn_to_east", new Vector2(48, -5));
            yield return WalkTo("walk_east_to_south", new Vector2(0, -5));
            yield return WalkTo("walk_south_to_west", new Vector2(-48, -5));
            yield return WalkTo("walk_west_to_forest_approach", new Vector2(-15, 5));
            yield return WalkTo("walk_into_forest", new Vector2(-15, 15));
            yield return WalkTo("walk_out_of_forest", new Vector2(-15, 0));
            // Material test slabs occupy z=2.25..3.75; the traversal lane passes south of them.
            yield return WalkTo("walk_hill_approach", new Vector2(22, 0));
            yield return WalkTo("walk_up_hill", new Vector2(22, 25));

            player.SetPaused(true);
            Vector3 pausedPosition = player.transform.position;
            player.SimulateMove(Vector2.up, 1);
            Check("pause_stops_movement", (player.transform.position - pausedPosition).sqrMagnitude < 0.0001f);
            Check("capture_cursor_unlocked", Cursor.lockState == CursorLockMode.None);
            player.SetPaused(false);
            int resets = player.ResetCount;
            player.Teleport(new Vector3(70, -5, 0), Quaternion.identity);
            player.SimulateMove(Vector2.zero, 1f / 60);
            Check("abnormal_deep_water_resets_player", player.ResetCount > resets && player.transform.position.y >= 0);
            Check("physics_time_scale_one", Mathf.Approximately(Time.timeScale, 1));
            Check("no_runtime_errors", errors == 0);
            File.WriteAllText(Path.Combine(output, "validation.json"), JsonUtility.ToJson(new Result
            {
                utc = DateTime.UtcNow.ToString("O"), passed = failures.Count == 0,
                checks = checks.ToArray(), failures = failures.ToArray(), runtimeErrors = errors,
                scope = "Runtime controller collision/shelter smoke checks plus actual screenshots. Physical mouse feel and extended traversal still require manual review."
            }, true));
            Application.Quit(failures.Count == 0 ? 0 : 1);
        }

        IEnumerator Capture(string name)
        {
            yield return new WaitForSecondsRealtime(2);
            yield return new WaitForEndOfFrame();
            string path = Path.Combine(output, name + ".png");
            ScreenCapture.CaptureScreenshot(path);
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while ((!File.Exists(path) || new FileInfo(path).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Check(name + "_screenshot", File.Exists(path) && new FileInfo(path).Length > 0);
        }

        IEnumerator WalkTo(string name, Vector2 target)
        {
            int resetCount = player.ResetCount;
            bool reached = false;
            for (int step = 0; step < 1600; step++)
            {
                Vector3 p = player.transform.position;
                Vector2 delta = target - new Vector2(p.x, p.z);
                if (delta.magnitude < 0.3f) { reached = true; break; }
                player.SetLook(Mathf.Atan2(delta.x, delta.y) * Mathf.Rad2Deg, 0);
                player.SimulateMove(Vector2.up, 1f / 60);
                if (player.ResetCount != resetCount) break;
                yield return null;
            }
            Check(name, reached && player.ResetCount == resetCount);
            if (!reached) Debug.LogWarning(name + " stopped at " + player.transform.position + " target=" + target);
        }

        void Check(string name, bool passed)
        {
            checks.Add(name + ": " + (passed ? "PASS" : "FAIL"));
            if (!passed) failures.Add(name);
            Debug.Log("M1_CHECK " + name + " " + passed);
        }
        void OnLog(string message, string trace, LogType type) { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++; }
        void OnDestroy() { Application.logMessageReceived -= OnLog; }
        [Serializable] sealed class Result { public string utc, scope; public bool passed; public string[] checks, failures; public int runtimeErrors; }
    }
}
