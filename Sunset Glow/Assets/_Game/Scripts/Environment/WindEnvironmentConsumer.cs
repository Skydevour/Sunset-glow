using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SunsetGlow.EnvironmentArt
{
    /// <summary>The sole writer for environment wind parameters; art presets set WindField targets.</summary>
    [DefaultExecutionOrder(100)]
    public sealed class WindEnvironmentConsumer : MonoBehaviour
    {
        public WindField field;
        public CoastalEnvironmentPresenter presenter;
        [Min(0)] public float highAltitudeMultiplier = 2.8f;
        public float highAltitudeDirectionOffset = 8f;
        [Min(.1f)] public float cloudResponseSeconds = 10f;
        [Header("Visible coastal cloud layer")]
        [Min(300)] public float cloudBaseAltitude = 1400f;
        [Min(100)] public float cloudLayerDepth = 1100f;
        [Range(1, 40)] public float cloudShapeScale = 9f;
        [Min(.1f)] public float swellResponseSeconds = 18f;
        [Range(0,1)] public float snowDrag = .24f;

        VolumetricClouds clouds;
        float cloudSpeed, cloudAngle, swellSpeed, swellAngle;
        bool initialized, snowWasPaused;
        Vector3 initialCloudShapeOffset;
        double previousPresentationTime, cloudDistanceX, cloudDistanceZ;

        /// <summary>World metres travelled by cloud shapes, for fixed-weather motion validation.</summary>
        public Vector2 CloudDisplacement => new Vector2((float)cloudDistanceX, (float)cloudDistanceZ);
        public Vector2 CloudVelocity => WindField.DirectionFromAngle(cloudAngle) * cloudSpeed;

        void Start()
        {
            if (field == null) field = GetComponent<WindField>();
            if (presenter == null) presenter = FindFirstObjectByType<CoastalEnvironmentPresenter>();
            if (field == null || presenter == null) { enabled = false; return; }
            if (presenter.environmentVolume != null) presenter.environmentVolume.profile.TryGet(out clouds);
            if (clouds != null)
            {
                initialCloudShapeOffset = clouds.shapeOffset.value;
                clouds.bottomAltitude.Override(cloudBaseAltitude);
                clouds.altitudeRange.Override(cloudLayerDepth);
                clouds.shapeScale.Override(cloudShapeScale);
                // Broad masses with smaller eroded lobes, rather than many tiny smooth islands.
                clouds.numPrimarySteps.Override(96);
                clouds.numLightSteps.Override(8);
                clouds.erosionScale.Override(95f);
                clouds.microErosion.Override(true);
                clouds.microErosionFactor.Override(.45f);
                clouds.microErosionScale.Override(260f);
                clouds.multiScattering.Override(.48f);
                clouds.ambientLightProbeDimmer.Override(.8f);
                clouds.powderEffectIntensity.Override(.15f);
                clouds.erosionOcclusion.Override(.25f);
                // Own shape advection explicitly. HDRP's built-in offset only advances when
                // main-camera temporal history is valid and uses Time.time rather than our clock.
                // Leaving both paths enabled would apply the wind twice.
                clouds.shapeSpeedMultiplier.Override(0f);
                clouds.erosionSpeedMultiplier.Override(.65f);
                clouds.temporalAccumulationFactor.Override(.85f);
            }
            previousPresentationTime = field.PresentationTime;
            cloudSpeed = field.CurrentSpeed * highAltitudeMultiplier;
            swellSpeed = field.CurrentSpeed;
            cloudAngle = field.CurrentAngle + highAltitudeDirectionOffset;
            swellAngle = field.CurrentAngle;
            if (presenter.ocean != null) presenter.ocean.ripplesMotionMode = WaterPropertyOverrideMode.Custom;
            initialized = true;
        }

        void LateUpdate()
        {
            if (!initialized) return;
            bool paused = field.IsPaused;
            // Consume exactly the phase advanced by WindField, including pause and test ticks.
            float dt = paused ? 0 : (float)System.Math.Max(0, field.PresentationTime - previousPresentationTime);
            previousPresentationTime = field.PresentationTime;
            float c = 1 - Mathf.Exp(-dt / Mathf.Max(.1f, cloudResponseSeconds));
            float w = 1 - Mathf.Exp(-dt / Mathf.Max(.1f, swellResponseSeconds));
            cloudSpeed = Mathf.Lerp(cloudSpeed, field.CurrentSpeed * highAltitudeMultiplier, c);
            cloudAngle = Mathf.LerpAngle(cloudAngle, field.CurrentAngle + highAltitudeDirectionOffset, c);
            swellSpeed = Mathf.Lerp(swellSpeed, field.CurrentSpeed, w);
            swellAngle = Mathf.LerpAngle(swellAngle, field.CurrentAngle, w);
            // HDRP 17 cloud/water APIs both use km/h. Their orientation is relative to world +X.
            // Cloud shader sampling applies its own negative offset to move the visible mass towards wind.
            if (clouds != null)
            {
                Vector2 cloudVelocity = CloudVelocity;
                cloudDistanceX += cloudVelocity.x * dt;
                cloudDistanceZ += cloudVelocity.y * dt;
                // HDRP 17 samples position / 100000 * shapeScale - shapeOffset.
                // Positive offsets therefore move visible shapes towards positive world axes.
                // Keep phase in double precision; do not wrap and pop during long sessions.
                double noiseScale = clouds.shapeScale.value / 100000d;
                clouds.shapeOffset.Override(initialCloudShapeOffset + new Vector3(
                    (float)(cloudDistanceX * noiseScale),
                    (float)((cloudDistanceX / 3d + cloudDistanceZ / 7d) * noiseScale),
                    (float)(cloudDistanceZ * noiseScale)));
                SetCustom(clouds.globalWindSpeed, paused ? 0 : cloudSpeed * 3.6f);
                SetCustom(clouds.orientation, Mathf.Repeat(cloudAngle, 360));
                clouds.verticalShapeWindSpeed.Override(paused ? 0 : .06f);
                clouds.verticalErosionWindSpeed.Override(paused ? 0 : .12f);
            }
            if (presenter.ocean != null)
            {
                presenter.ocean.largeWindSpeed = swellSpeed * 3.6f;
                presenter.ocean.largeOrientationValue = Mathf.Repeat(swellAngle, 360);
                presenter.ocean.ripplesWindSpeed = field.CurrentSpeed * 3.6f * .65f;
                presenter.ocean.ripplesOrientationValue = field.CurrentAngle;
                presenter.ocean.timeMultiplier = paused ? 0 : 1;
            }
            if (presenter.snow != null)
            {
                if (paused && !snowWasPaused) presenter.snow.Pause();
                else if (!paused && snowWasPaused) presenter.snow.Play();
                snowWasPaused = paused;
                var velocity = presenter.snow.velocityOverLifetime;
                velocity.enabled = true;
                velocity.space = ParticleSystemSimulationSpace.World;
                Vector3 drift = field.SampleVelocity(presenter.snow.transform.position) * snowDrag;
                velocity.x = drift.x;
                velocity.z = drift.z;
            }
        }

        static void SetCustom(WindParameter parameter, float value)
        {
            parameter.Override(new WindParameter.WindParamaterValue
            {
                mode = WindParameter.WindOverrideMode.Custom,
                customValue = value, multiplyValue = 1
            });
        }

        void OnDisable()
        {
            if (presenter != null && presenter.ocean != null) presenter.ocean.timeMultiplier = 1;
            if (snowWasPaused && presenter != null && presenter.snow != null) presenter.snow.Play();
        }
    }
}
