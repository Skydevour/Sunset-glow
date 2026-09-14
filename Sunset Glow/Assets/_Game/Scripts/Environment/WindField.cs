using UnityEngine;
using SunsetGlow.Player;

namespace SunsetGlow.EnvironmentArt
{
    /// <summary>Shared horizontal wind. Direction means towards; velocity is metres per real second.</summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public sealed class WindField : MonoBehaviour
    {
        public FirstPersonController player;
        public Vector2 initialDirection = new Vector2(1f, .35f);
        [Min(0)] public float initialSpeed = 5f;
        [Min(0)] public float initialGust = 1.4f;
        [Min(.1f)] public float responseSeconds = 5f;
        [Min(1)] public float spatialScale = 24f;
        [Min(.1f)] public float gustPeriodSeconds = 7f;
        public bool debugKeys = true;

        public Vector2 CurrentDirection => DirectionFromAngle(currentAngle);
        public float CurrentSpeed { get; private set; }
        public float CurrentGust { get; private set; }
        public float CurrentAngle => currentAngle;
        public double PresentationTime { get; private set; }
        public bool IsPaused => player != null && player.Paused;
        public Vector3 MeanVelocity => new Vector3(CurrentDirection.x, 0, CurrentDirection.y) * CurrentSpeed;

        float currentAngle, targetAngle, targetSpeed, targetGust;
        bool initialized;

        void Awake() => Initialize();

        void Initialize()
        {
            if (initialized) return;
            initialized = true;
            currentAngle = targetAngle = AngleFromDirection(initialDirection);
            CurrentSpeed = targetSpeed = Mathf.Max(0, initialSpeed);
            CurrentGust = targetGust = Mathf.Max(0, initialGust);
        }

        /// <summary>A zero direction retains the previous heading; a zero speed and gust yields calm.</summary>
        public void SetTarget(Vector2 direction, float speed, float gust)
        {
            Initialize();
            if (direction.sqrMagnitude > .0001f) targetAngle = AngleFromDirection(direction);
            targetSpeed = Mathf.Max(0, speed);
            targetGust = Mathf.Max(0, gust);
        }

        void Update()
        {
            if (IsPaused) return;
            if (debugKeys && (player == null || !player.CaptureMode))
            {
                if (Input.GetKeyDown(KeyCode.Alpha7)) SetDebugPreset(0);
                if (Input.GetKeyDown(KeyCode.Alpha8)) SetDebugPreset(1);
                if (Input.GetKeyDown(KeyCode.Alpha9)) SetDebugPreset(2);
            }
            Advance(Time.unscaledDeltaTime);
        }

        /// <summary>Testable presentation tick. Never pass world/calendar delta time.</summary>
        public void Advance(float realSeconds)
        {
            Initialize();
            if (IsPaused || realSeconds <= 0 || float.IsNaN(realSeconds) || float.IsInfinity(realSeconds)) return;
            PresentationTime += realSeconds;
            float blend = 1f - Mathf.Exp(-realSeconds / Mathf.Max(.1f, responseSeconds));
            currentAngle = Mathf.Repeat(Mathf.LerpAngle(currentAngle, targetAngle, blend), 360f);
            CurrentSpeed = Mathf.Lerp(CurrentSpeed, targetSpeed, blend);
            CurrentGust = Mathf.Lerp(CurrentGust, targetGust, blend);
        }

        public void SetDebugPreset(int index)
        {
            switch (index)
            {
                case 0: SetTarget(CurrentDirection, 0, 0); break;
                case 1: SetTarget(new Vector2(1, .35f), 2, .45f); break;
                default: SetTarget(new Vector2(.35f, 1), 7, 3); break;
            }
        }

        /// <summary>Deterministic, allocation-free, spatially phased gust; pause freezes its phase.</summary>
        public Vector3 SampleVelocity(Vector3 position)
        {
            float phase = (float)(PresentationTime % 100000d) * (2f * Mathf.PI / Mathf.Max(.1f, gustPeriodSeconds));
            float space = (position.x * .81f + position.z * .59f) / Mathf.Max(1, spatialScale);
            float gust = .65f * Mathf.Sin(phase - space) + .35f * Mathf.Sin(phase * .473f + space * 1.83f + 1.2f);
            return new Vector3(CurrentDirection.x, 0, CurrentDirection.y) * Mathf.Max(0, CurrentSpeed + CurrentGust * gust);
        }

        public static float AngleFromDirection(Vector2 direction) => direction.sqrMagnitude < .0001f ? 0 : Mathf.Repeat(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, 360);
        public static Vector2 DirectionFromAngle(float degrees) => new Vector2(Mathf.Cos(degrees * Mathf.Deg2Rad), Mathf.Sin(degrees * Mathf.Deg2Rad));
    }
}
