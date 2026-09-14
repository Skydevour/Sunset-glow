using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

namespace SunsetGlow.Debugging
{
    /// <summary>Real-time M0 measurement and free-flight inspection; never changes Time.timeScale.</summary>
    [DisallowMultipleComponent]
    public sealed class BaselineRunner : MonoBehaviour
    {
        private const double WarmupSeconds = 15;
        private const double SamplingSeconds = 60;
        private const int Capacity = 524288;
        [SerializeField] private float moveSpeed = 8f;
        [SerializeField] private float lookSensitivity = 2f;
        public bool allowFlight = true;
        public string measurementScope = "M0 empty scene scale reference; real elapsed Update intervals. No world clock. Preallocated sampling arrays consume 12 MiB.";
        private readonly double[] frameMilliseconds = new double[Capacity];
        private readonly double[] cpuMilliseconds = new double[Capacity];
        private readonly double[] gpuMilliseconds = new double[Capacity];
        private readonly FrameTiming[] timing = new FrameTiming[1];
        private string outputDirectory;
        private bool automatic;
        private bool finishing;
        private bool sampling;
        private double startedAt;
        private double sampledAt;
        private double lastFrameAt;
        private int frameCount, cpuCount, gpuCount, droppedFrames;
        private int errors, warnings, focusLostFrames;
        private ulong lastTimingTimestamp;
        private bool hasTimingTimestamp;
        private bool movementObserved, lookObserved, escapeObserved, exitObserved;
        private float yaw, pitch;
        private long managedStart, reservedStart, allocatedStart;

        private void Awake()
        {
            Application.logMessageReceived += CountLog;
            Application.runInBackground = true;
            Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
            QualitySettings.SetQualityLevel(1, true);
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            Vector3 angles = transform.eulerAngles;
            yaw = angles.y;
            pitch = Mathf.DeltaAngle(0, angles.x);
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] != "-baselineOutput") continue;
                if (i + 1 >= args.Length || !Path.IsPathRooted(args[i + 1]))
                {
                    UnityEngine.Debug.LogError("-baselineOutput requires an absolute directory path.");
                    enabled = false;
                    Application.Quit(2);
                    return;
                }
                outputDirectory = args[++i];
                try { Directory.CreateDirectory(outputDirectory); }
                catch (Exception exception)
                {
                    UnityEngine.Debug.LogException(exception);
                    enabled = false;
                    Application.Quit(2);
                    return;
                }
                automatic = true;
            }
            startedAt = Time.realtimeSinceStartupAsDouble;
            UnityEngine.Debug.Log("M0 baseline: WASD move, Q/E down/up, Shift boost, hold RMB to look, Escape release, F10 quit. Automatic measurements use real elapsed time.");
        }

        private void Update()
        {
            if (allowFlight) UpdateFlight();
            if (!automatic || finishing) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (!sampling)
            {
                if (now - startedAt < WarmupSeconds) return;
                sampling = true;
                sampledAt = lastFrameAt = now;
                managedStart = Profiler.GetMonoUsedSizeLong();
                reservedStart = Profiler.GetTotalReservedMemoryLong();
                allocatedStart = Profiler.GetTotalAllocatedMemoryLong();
                FrameTimingManager.CaptureFrameTimings();
                return;
            }
            double frameMs = (now - lastFrameAt) * 1000.0;
            lastFrameAt = now;
            if (frameCount < Capacity) frameMilliseconds[frameCount++] = frameMs;
            else droppedFrames++;
            if (!Application.isFocused) focusLostFrames++;
            uint available = FrameTimingManager.GetLatestTimings(1, timing);
            if (available > 0 && (!hasTimingTimestamp || timing[0].frameStartTimestamp != lastTimingTimestamp))
            {
                hasTimingTimestamp = true;
                lastTimingTimestamp = timing[0].frameStartTimestamp;
                double cpu = timing[0].cpuFrameTime;
                double gpu = timing[0].gpuFrameTime;
                if (ValidTiming(cpu) && cpuCount < Capacity) cpuMilliseconds[cpuCount++] = cpu;
                if (ValidTiming(gpu) && gpuCount < Capacity) gpuMilliseconds[gpuCount++] = gpu;
            }
            FrameTimingManager.CaptureFrameTimings();
            if (now - sampledAt >= SamplingSeconds)
            {
                finishing = true;
                StartCoroutine(Finish(now - sampledAt));
            }
        }

        private static bool ValidTiming(double value)
        {
            return value > 0 && !double.IsNaN(value) && !double.IsInfinity(value);
        }

        private void UpdateFlight()
        {
#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.GetKeyDown(KeyCode.F10))
            {
                exitObserved = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                UnityEngine.Debug.Log("M0_INPUT_EXIT movement=" + movementObserved + " mouseLook=" + lookObserved + " escape=" + escapeObserved + " f10=true automaticCancelled=" + automatic);
                Application.Quit(automatic ? 2 : 0);
            }
            bool escape = Input.GetKeyDown(KeyCode.Escape);
            if (escape) escapeObserved = true;
            if (Input.GetMouseButtonDown(1) && !escape)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Input.GetMouseButtonUp(1) || escape)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            if (Input.GetMouseButton(1) && Cursor.lockState == CursorLockMode.Locked)
            {
                float dx = Input.GetAxisRaw("Mouse X"), dy = Input.GetAxisRaw("Mouse Y");
                if (dx != 0 || dy != 0) lookObserved = true;
                yaw += dx * lookSensitivity;
                pitch = Mathf.Clamp(pitch - dy * lookSensitivity, -89, 89);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0);
            }
            Vector3 direction = new Vector3(
                (Input.GetKey(KeyCode.D) ? 1 : 0) - (Input.GetKey(KeyCode.A) ? 1 : 0),
                (Input.GetKey(KeyCode.E) ? 1 : 0) - (Input.GetKey(KeyCode.Q) ? 1 : 0),
                (Input.GetKey(KeyCode.W) ? 1 : 0) - (Input.GetKey(KeyCode.S) ? 1 : 0));
            if (direction.sqrMagnitude > 0)
            {
                movementObserved = true;
                float boost = Input.GetKey(KeyCode.LeftShift) ? 3 : 1;
                transform.position += transform.TransformDirection(direction.normalized) * (moveSpeed * boost * Time.unscaledDeltaTime);
            }
#endif
        }

        private IEnumerator Finish(double elapsed)
        {
            // Capture memory before sorting, JSON serialization, and screenshot allocations.
            Report report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion,
                developmentBuild = UnityEngine.Debug.isDebugBuild,
                editor = Application.isEditor,
                operatingSystem = SystemInfo.operatingSystem,
                cpu = SystemInfo.processorType,
                cpuLogicalProcessors = SystemInfo.processorCount,
                ramMegabytes = SystemInfo.systemMemorySize,
                gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                gpuMemoryMegabytes = SystemInfo.graphicsMemorySize,
                graphicsDriver = SystemInfo.graphicsDeviceVersion,
                width = Screen.width,
                height = Screen.height,
                fullscreenMode = Screen.fullScreenMode.ToString(),
                qualityIndex = QualitySettings.GetQualityLevel(),
                qualityName = QualitySettings.names[QualitySettings.GetQualityLevel()],
                vsync = QualitySettings.vSyncCount,
                targetFrameRate = Application.targetFrameRate,
                timeScale = Time.timeScale,
                requestedWarmupSeconds = WarmupSeconds,
                requestedSamplingSeconds = SamplingSeconds,
                actualSamplingSeconds = elapsed,
                droppedFrameSamples = droppedFrames,
                unfocusedSamplingFrames = focusLostFrames,
                managedBytesAtSampleStart = managedStart,
                managedBytesAtSampleEnd = Profiler.GetMonoUsedSizeLong(),
                totalAllocatedBytesAtSampleStart = allocatedStart,
                totalAllocatedBytesAtSampleEnd = Profiler.GetTotalAllocatedMemoryLong(),
                totalReservedBytesAtSampleStart = reservedStart,
                totalReservedBytesAtSampleEnd = Profiler.GetTotalReservedMemoryLong(),
                timingFeatureEnabled = FrameTimingManager.IsFeatureEnabled(),
                movementInputObserved = movementObserved,
                mouseLookInputObserved = lookObserved,
                escapeInputObserved = escapeObserved,
                f10InputObserved = exitObserved,
                inputVerification = "Observed flags only; false means not observed, not a passing test. Automated run does not inject input.",
                measurementScope = measurementScope
            };
#if ENABLE_LEGACY_INPUT_MANAGER
            report.legacyInputEnabled = true;
#endif
            report.frame = Summarize(frameMilliseconds, frameCount);
            report.cpuFrameTiming = Summarize(cpuMilliseconds, cpuCount);
            report.gpuFrameTiming = Summarize(gpuMilliseconds, gpuCount);
            string stem = "baseline-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff");
            string screenshotPath = Path.Combine(outputDirectory, stem + ".png");
            report.screenshot = screenshotPath;
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(screenshotPath);
            double deadline = Time.realtimeSinceStartupAsDouble + 20;
            while ((!File.Exists(screenshotPath) || new FileInfo(screenshotPath).Length == 0) && Time.realtimeSinceStartupAsDouble < deadline)
                yield return null;
            report.screenshotWritten = File.Exists(screenshotPath) && new FileInfo(screenshotPath).Length > 0;
            if (!report.screenshotWritten) UnityEngine.Debug.LogError("Baseline screenshot was not written before timeout.");
            report.errorCount = errors;
            report.warningCount = warnings;
            string reportPath = Path.Combine(outputDirectory, stem + ".json");
            bool written = false;
            try
            {
                File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                written = true;
                UnityEngine.Debug.Log("Baseline report written: " + reportPath);
            }
            catch (Exception exception) { UnityEngine.Debug.LogException(exception); }
            Application.Quit(written && report.screenshotWritten && errors == 0 && droppedFrames == 0 ? 0 : 1);
        }

        private static Distribution Summarize(double[] values, int count)
        {
            if (count == 0) return new Distribution { status = "unavailable", meanMs = -1, p50Ms = -1, p95Ms = -1, p99Ms = -1, maxMs = -1 };
            double sum = 0;
            for (int i = 0; i < count; i++) sum += values[i];
            Array.Sort(values, 0, count);
            return new Distribution
            {
                status = "valid", sampleCount = count, meanMs = sum / count,
                p50Ms = values[Math.Max(0, (int)Math.Ceiling(count * 0.50) - 1)],
                p95Ms = values[Math.Max(0, (int)Math.Ceiling(count * 0.95) - 1)],
                p99Ms = values[Math.Max(0, (int)Math.Ceiling(count * 0.99) - 1)],
                maxMs = values[count - 1]
            };
        }

        private void CountLog(string message, string stackTrace, LogType type)
        {
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) errors++;
            else if (type == LogType.Warning) warnings++;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= CountLog;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        [Serializable]
        public sealed class Distribution
        {
            public string status;
            public int sampleCount;
            public double meanMs, p50Ms, p95Ms, p99Ms, maxMs;
        }

        [Serializable]
        public sealed class Report
        {
            public string utc, unityVersion, operatingSystem, cpu, gpu, graphicsApi, graphicsDriver;
            public bool developmentBuild, editor;
            public int cpuLogicalProcessors, ramMegabytes, gpuMemoryMegabytes;
            public int width, height, qualityIndex, vsync, targetFrameRate;
            public string fullscreenMode, qualityName;
            public float timeScale;
            public double requestedWarmupSeconds, requestedSamplingSeconds, actualSamplingSeconds;
            public int droppedFrameSamples, unfocusedSamplingFrames, errorCount, warningCount;
            public long managedBytesAtSampleStart, managedBytesAtSampleEnd;
            public long totalAllocatedBytesAtSampleStart, totalAllocatedBytesAtSampleEnd;
            public long totalReservedBytesAtSampleStart, totalReservedBytesAtSampleEnd;
            public bool timingFeatureEnabled;
            public Distribution frame, cpuFrameTiming, gpuFrameTiming;
            public bool legacyInputEnabled, movementInputObserved, mouseLookInputObserved, escapeInputObserved, f10InputObserved;
            public string inputVerification, measurementScope, screenshot;
            public bool screenshotWritten;
        }
    }
}
