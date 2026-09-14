using System;
using System.IO;
using UnityEngine;
using SunsetGlow.EnvironmentArt;

namespace SunsetGlow.Player
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(CharacterController))]
    public sealed class FirstPersonController : MonoBehaviour
    {
        public Camera viewCamera;
        public Transform[] observationPoints;
        public Transform spawnPoint;
        public WaterContactSystem water;
        public float WaterDepth { get; private set; }
        public bool Wading { get; private set; }
        public bool IsGrounded => motor!=null && motor.isGrounded;
        public event Action<Vector3,Vector3,float> Moved;
        public event Action Teleported;
        [Min(0.1f)] public float walkSpeed = 4f;
        [Min(0.1f)] public float sprintSpeed = 6f;
        [Min(1f)] public float capsuleHeight = 1.8f;
        [Min(.8f)] public float eyeHeight = 1.65f;
        [Range(0,.2f)] public float eyeForward;
        public bool Paused { get; private set; }
        public int ResetCount { get; private set; }
        public Vector3 Velocity { get; private set; }
        public bool BobEnabled { get; set; } = true;
        public float MouseSensitivity { get; set; } = 2f;
        public bool CaptureMode { get; private set; }
        public float FieldOfView
        {
            get => viewCamera != null ? viewCamera.fieldOfView : 75f;
            set { if (viewCamera != null) viewCamera.fieldOfView = Mathf.Clamp(value, 60f, 100f); }
        }

        private CharacterController motor;
        private float yaw;
        private float pitch;
        private float verticalSpeed;
        private bool sprinting;
        private Vector3 safePosition;
        private Quaternion safeRotation;
        private float safeGroundTime;
        private GUIStyle titleStyle;
        private bool movementInputObserved;
        private bool mouseLookInputObserved;

        [Serializable]
        private struct ScreenshotState
        {
            public string timestampUtc;
            public string screenshot;
            public int frame;
            public Vector3 position;
            public Quaternion rotation;
            public Quaternion viewRotation;
            public bool paused;
            public string cursor;
            public bool cursorVisible;
            public float fov;
            public float sensitivity;
            public bool bob;
            public bool movementInputObserved;
            public bool mouseLookInputObserved;
        }

        private void Awake()
        {
            motor = GetComponent<CharacterController>();
            motor.height = capsuleHeight;
            motor.radius = 0.3f;
            motor.center = new Vector3(0f, capsuleHeight * .5f, 0f);
            motor.skinWidth = 0.03f;
            motor.stepOffset = 0.3f;
            motor.slopeLimit = 45f;
            motor.minMoveDistance = 0f;
            string[] args = Environment.GetCommandLineArgs();
            CaptureMode = Array.IndexOf(args, "-m1Capture") >= 0 || Array.IndexOf(args, "-coastalCapture") >= 0 || Array.IndexOf(args,"-interactionCapture")>=0 || Array.IndexOf(args,"-cloudMotionCapture")>=0 || Array.IndexOf(args,"-refinementCapture")>=0;
            if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>();
            if (viewCamera != null)
            {
                viewCamera.transform.localPosition = new Vector3(0f, eyeHeight, eyeForward);
                viewCamera.nearClipPlane = 0.05f;
                FieldOfView = 75f;
            }
            safePosition = spawnPoint != null ? spawnPoint.position : transform.position;
            safeRotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;
            SetLook(transform.eulerAngles.y, 0f);
            SetPaused(false);
        }

        private void Update()
        {
            if (CaptureMode) return;
            bool diagnostics = UnityEngine.Debug.isDebugBuild || Application.isEditor;
            if (Input.GetKeyDown(KeyCode.F10))
            {
                if (diagnostics)
                    UnityEngine.Debug.Log($"M1_INPUT_EXIT movement={movementInputObserved} mouseLook={mouseLookInputObserved} position={transform.position:F3}");
                Application.Quit();
                return;
            }
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SetPaused(!Paused);
                if (diagnostics) UnityEngine.Debug.Log($"M1_PAUSE paused={Paused} cursor={Cursor.lockState} visible={Cursor.visible}");
            }
            if (diagnostics && Input.GetKeyDown(KeyCode.F9)) CaptureDiagnosticScreenshot();
            if (Paused) return;

            for (int i = 0; i < 5; i++)
            {
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.F1 + i)) && observationPoints != null &&
                    i < observationPoints.Length && observationPoints[i] != null)
                    Teleport(observationPoints[i].position, observationPoints[i].rotation);
            }
            float mouseX = Input.GetAxisRaw("Mouse X");
            float mouseY = Input.GetAxisRaw("Mouse Y");
            Vector2 movementInput = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (diagnostics)
            {
                movementInputObserved |= movementInput.sqrMagnitude > 0f;
                mouseLookInputObserved |= mouseX != 0f || mouseY != 0f;
            }
            SetLook(yaw + mouseX * MouseSensitivity, pitch - mouseY * MouseSensitivity);
            sprinting = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            SimulateMove(movementInput, Time.deltaTime);
        }

        private void CaptureDiagnosticScreenshot()
        {
            try
            {
                string directory = Path.Combine(Application.persistentDataPath, "M1Screenshots");
                Directory.CreateDirectory(directory);
                DateTime now = DateTime.UtcNow;
                string stem = Path.Combine(directory, now.ToString("yyyyMMdd-HHmmss-fff") + "-" + Time.frameCount);
                ScreenshotState state = new ScreenshotState
                {
                    timestampUtc = now.ToString("O"),
                    screenshot = stem + ".png",
                    frame = Time.frameCount,
                    position = transform.position,
                    rotation = transform.rotation,
                    viewRotation = viewCamera != null ? viewCamera.transform.rotation : transform.rotation,
                    paused = Paused,
                    cursor = Cursor.lockState.ToString(),
                    cursorVisible = Cursor.visible,
                    fov = FieldOfView,
                    sensitivity = MouseSensitivity,
                    bob = BobEnabled,
                    movementInputObserved = movementInputObserved,
                    mouseLookInputObserved = mouseLookInputObserved
                };
                string json = JsonUtility.ToJson(state, true);
                File.WriteAllText(stem + ".json", json);
                ScreenCapture.CaptureScreenshot(state.screenshot);
                UnityEngine.Debug.Log("M1_SCREENSHOT_REQUEST " + json);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogError("M1_SCREENSHOT_FAILED " + exception);
            }
        }

        // The normal input path and capture tests use the same CharacterController collision path.
        public void SimulateMove(Vector2 input, float deltaSeconds)
        {
            if (Paused || deltaSeconds <= 0f || motor == null || !motor.enabled)
            {
                Velocity = Vector3.zero;
                return;
            }
            input = Vector2.ClampMagnitude(input, 1f);
            Vector3 direction = transform.right * input.x + transform.forward * input.y;
            Vector3 start = transform.position;
            float waterLevel=0;
            bool validWater=water!=null && water.TrySample(start,out waterLevel);
            if(validWater)WaterDepth=Mathf.Max(0,waterLevel-start.y);
            else WaterDepth=0;
            Wading=WaterDepth>(Wading ? .04f : .09f);
            float resistance=Mathf.Lerp(1,.4f,Mathf.InverseLerp(.05f,1.05f,WaterDepth));
            // Substeps preserve collision/gravity behavior during a long render frame.
            float remaining = deltaSeconds;
            while (remaining > 0f)
            {
                float step = Mathf.Min(remaining, 1f / 30f);
                if (motor.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
                verticalSpeed += Physics.gravity.y * step;
                CollisionFlags flags = motor.Move((direction * (sprinting ? sprintSpeed : walkSpeed) * resistance +
                    Vector3.up * verticalSpeed) * step);
                if ((flags & CollisionFlags.Above) != 0 && verticalSpeed > 0f) verticalSpeed = 0f;
                if ((flags & CollisionFlags.Below) != 0 && verticalSpeed < 0f) verticalSpeed = -2f;
                remaining -= step;
            }
            Velocity = (transform.position - start) / deltaSeconds;
            Vector3 position = transform.position;
            if (position.y < (water!=null?-3f:-.4f) || position.x * position.x + position.z * position.z > 12100f || WaterDepth>1.2f || (!validWater && water!=null && position.y<-.2f))
            {
                ResetCount++;
                Teleport(safePosition, safeRotation);
                return;
            }
            // Only stable, dry ground becomes a fallback; never remember the shoreline fall.
            if (motor.isGrounded && position.y >= 0.15f && WaterDepth<.04f)
            {
                safeGroundTime += deltaSeconds;
                if (safeGroundTime >= 0.5f)
                {
                    safePosition = position;
                    safeRotation = Quaternion.Euler(pitch, yaw, 0f);
                    safeGroundTime = 0f;
                }
            }
            else safeGroundTime = 0f;
            Moved?.Invoke(start,transform.position,deltaSeconds);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            if (motor == null) motor = GetComponent<CharacterController>();
            bool wasEnabled = motor.enabled;
            motor.enabled = false;
            transform.position = position;
            Vector3 angles = rotation.eulerAngles;
            SetLook(angles.y, Mathf.DeltaAngle(0f, angles.x));
            motor.enabled = wasEnabled;
            verticalSpeed = 0f;
            Velocity = Vector3.zero;
            safeGroundTime = 0f;
            WaterDepth=0;Wading=false;
            Teleported?.Invoke();
        }

        public void SetLook(float newYaw, float newPitch)
        {
            yaw = Mathf.Repeat(newYaw, 360f);
            pitch = Mathf.Clamp(newPitch, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (viewCamera != null) viewCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        public void SetPaused(bool paused)
        {
            Paused = paused;
            if(water!=null)water.SetPaused(paused);
            Velocity = Vector3.zero;
            sprinting = false;
            Cursor.lockState = paused || CaptureMode ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = paused || CaptureMode;
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && !CaptureMode) SetPaused(true);
        }

        private void OnDisable()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnGUI()
        {
            if (!Paused || CaptureMode) return;
            if (titleStyle == null) titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, alignment = TextAnchor.MiddleCenter };
            Rect panel = new Rect((Screen.width - 360f) * 0.5f, (Screen.height - 330f) * 0.5f, 360f, 330f);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(panel.x + 24f, panel.y + 18f, panel.width - 48f, panel.height - 36f));
            GUILayout.Label("Island sample — paused", titleStyle);
            GUILayout.Space(15f);
            if (GUILayout.Button("Resume (Escape)", GUILayout.Height(32f))) SetPaused(false);
            GUILayout.Space(12f);
            GUILayout.Label("Mouse sensitivity");
            MouseSensitivity = GUILayout.HorizontalSlider(MouseSensitivity, 0.2f, 6f);
            GUILayout.Label("Field of view (60–100)");
            FieldOfView = GUILayout.HorizontalSlider(FieldOfView, 60f, 100f);
            BobEnabled = GUILayout.Toggle(BobEnabled, "Body movement");
            GUILayout.Space(12f);
            GUILayout.Label("WASD move · Shift run · F1–F5 viewpoints");
            if (GUILayout.Button("Quit (F10)", GUILayout.Height(32f))) Application.Quit();
            GUILayout.EndArea();
        }
    }
}
