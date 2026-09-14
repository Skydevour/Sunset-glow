using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SunsetGlow.EnvironmentArt
{
    /// <summary>Bounded HDRP query gateway and pooled native water displacement. No world-clock dependency.</summary>
    public sealed class WaterContactSystem : MonoBehaviour
    {
        public WaterSurface ocean;
        public Transform followTarget;
        public Material rippleMaterial;
        [Range(1, 16)] public int maxQueriesPerFrame = 4;
        [Range(1, 32)] public int rippleCapacity = 32;
        public float trustworthyCacheSeconds = .4f;
        public float rippleLifetime = 2.8f;
        public int QueryCount { get; private set; }
        public int CacheHitCount { get; private set; }
        public int FailedQueryCount { get; private set; }
        public int BudgetExceededCount { get; private set; }
        public int ActiveRippleCount { get; private set; }
        public int EmittedRippleCount { get; private set; }
        public float LastSuccessfulSampleTime { get; private set; } = -100f;
        public float LastSampleError { get; private set; }
        public bool LastSampleWasCached { get; private set; }
        public bool Paused { get; private set; }
        public bool ForceQueryFailure { get; set; }
        public bool QueryHealthy => Time.unscaledTime - LastSuccessfulSampleTime < trustworthyCacheSeconds;

        struct Sample { public Vector3 position; public float height, time; public bool valid; }
        struct Ripple { public WaterDecal decal; public float age, strength; public bool active; }
        readonly Sample[] samples = new Sample[16];
        Ripple[] ripples;
        int frame = -1, queriesThisFrame, sampleCursor, rippleCursor;

        void Awake()
        {
            if (ocean == null) ocean = FindFirstObjectByType<WaterSurface>();
            // Allocated once, not per footstep. Every decal shares one static atlas entry.
            ripples = new Ripple[Mathf.Clamp(rippleCapacity, 1, 32)];
            if (rippleMaterial == null) return;
            for (int i = 0; i < ripples.Length; i++)
            {
                var go = new GameObject("Foot ripple " + i);
                go.SetActive(false);
                go.transform.SetParent(transform, false);
                var decal = go.AddComponent<WaterDecal>();
                decal.material = rippleMaterial;
                decal.resolution = new Vector2Int(128, 128);
                decal.updateMode = CustomRenderTextureUpdateMode.OnLoad;
                decal.surfaceFoamDimmer = 0;
                decal.deepFoamDimmer = 0;
                ripples[i].decal = decal;
            }
        }

        /// <summary>False means no trustworthy nearby height. Output then is nominal sea level, not a valid sample.</summary>
        public bool TrySample(Vector3 position, out float surfaceHeight)
        {
            surfaceHeight = ocean != null ? ocean.transform.position.y : 0f;
            LastSampleWasCached = false;
            float now = Time.unscaledTime;
            int cached = -1;
            float nearest = .75f * .75f;
            for (int i = 0; i < samples.Length; i++)
            {
                if (!samples[i].valid || now - samples[i].time > trustworthyCacheSeconds) continue;
                Vector3 delta = samples[i].position - position;
                float sqr = delta.x * delta.x + delta.z * delta.z;
                if (sqr < nearest) { nearest = sqr; cached = i; }
            }
            if (cached >= 0 && nearest < .2f * .2f && now - samples[cached].time < .05f)
                return ReadCache(cached, out surfaceHeight);
            if (frame != Time.frameCount) { frame = Time.frameCount; queriesThisFrame = 0; }
            if (queriesThisFrame >= maxQueriesPerFrame)
            {
                BudgetExceededCount++;
                return cached >= 0 && ReadCache(cached, out surfaceHeight);
            }
            queriesThisFrame++;
            QueryCount++;
            var parameters = new WaterSearchParameters
            {
                targetPositionWS = position,
                startPositionWS = position,
                error = .025f,
                maxIterations = 8,
                includeDeformation = true,
                excludeSimulation = false
            };
            if (!ForceQueryFailure && ocean != null && ocean.scriptInteractions &&
                ocean.ProjectPointOnWaterSurface(parameters, out WaterSearchResult result))
            {
                float height = result.projectedPositionWS.y;
                LastSampleError = result.error;
                if (!float.IsNaN(height) && !float.IsInfinity(height) && result.error <= .1f)
                {
                    surfaceHeight = height;
                    LastSuccessfulSampleTime = now;
                    samples[sampleCursor] = new Sample { position = position, height = height, time = now, valid = true };
                    sampleCursor = (sampleCursor + 1) % samples.Length;
                    return true;
                }
            }
            FailedQueryCount++;
            return cached >= 0 && ReadCache(cached, out surfaceHeight);
        }

        bool ReadCache(int index, out float height)
        {
            CacheHitCount++;
            LastSampleWasCached = true;
            height = samples[index].height;
            return true;
        }

        public void SetPaused(bool paused) { Paused = paused; }

        /// <summary>Called once by the shared contact event. Water shader consumes height, so there is no floating quad.</summary>
        public void EmitRipple(Vector3 contact, float strength = 1f)
        {
            if (Paused || ripples == null || rippleMaterial == null || ocean == null || !ocean.deformation) return;
            if (!TrySample(contact, out float height)) return;
            ref Ripple ripple = ref ripples[rippleCursor];
            rippleCursor = (rippleCursor + 1) % ripples.Length;
            ripple.age = 0;
            ripple.strength = Mathf.Clamp(strength, .2f, 2f);
            ripple.active = true;
            ripple.decal.transform.position = new Vector3(contact.x, height, contact.z);
            ripple.decal.regionSize = Vector2.one * .3f;
            ripple.decal.amplitude = .035f * ripple.strength;
            ripple.decal.gameObject.SetActive(true);
            EmittedRippleCount++;
        }

        void Update()
        {
            if (Paused || ripples == null) return;
            ActiveRippleCount = 0;
            for (int i = 0; i < ripples.Length; i++)
            {
                ref Ripple ripple = ref ripples[i];
                if (!ripple.active) continue;
                ripple.age += Time.unscaledDeltaTime;
                bool far = followTarget != null && (ripple.decal.transform.position - followTarget.position).sqrMagnitude > 28f * 28f;
                if (ripple.age >= rippleLifetime || far)
                {
                    ripple.active = false;
                    ripple.decal.gameObject.SetActive(false);
                    continue;
                }
                ActiveRippleCount++;
                float fade = 1f - ripple.age / rippleLifetime;
                ripple.decal.regionSize = Vector2.one * (.3f + ripple.age * 1.7f);
                ripple.decal.amplitude = .035f * ripple.strength * fade * fade;
            }
        }
    }
}
