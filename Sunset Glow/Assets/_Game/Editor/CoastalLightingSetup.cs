using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using Object = UnityEngine.Object;

namespace SunsetGlow.Editor
{
    /// <summary>Independent HDRP lighting assets for the coastal visual sample.</summary>
    public static class CoastalLightingSetup
    {
        const string ArtRoot = "Assets/_Game/Art";
        const string PipelinePath = ArtRoot + "/CoastalHDRP.asset";
        const string ProfilePath = ArtRoot + "/CoastalSky.asset";

        public static void Apply()
        {
            Directory.CreateDirectory(ArtRoot);
            AssetDatabase.Refresh();
            ConfigurePipeline();
            ConfigureCameras();
            ConfigureEnvironment();
            ConfigureSun();
            ConfigureReflection();
            foreach (var oldSky in Object.FindObjectsByType<StaticLightingSky>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                oldSky.enabled = false;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        static void ConfigurePipeline()
        {
            var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                var original = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/Settings/HDRP Balanced.asset");
                if (original == null) throw new InvalidOperationException("HDRP Balanced source asset is missing.");
                pipeline = Object.Instantiate(original);
                pipeline.name = "CoastalHDRP";
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }
            var settings = pipeline.currentPlatformRenderPipelineSettings;
            settings.supportWater = true;
            settings.supportSSR = true;
            settings.supportSSRTransparent = true;
            settings.supportTransparentDepthPrepass = true;
            settings.supportVolumetricClouds = true;
            settings.supportVolumetrics = true;
            settings.supportSSAO = true;
            settings.supportSSGI = true;
            pipeline.currentPlatformRenderPipelineSettings = settings;
            int balanced = Array.FindIndex(QualitySettings.names, name => name.IndexOf("Balanced", StringComparison.OrdinalIgnoreCase) >= 0);
            if (balanced < 0) throw new InvalidOperationException("Balanced quality level is missing.");
            QualitySettings.SetQualityLevel(balanced, false);
            QualitySettings.renderPipeline = pipeline;
            GraphicsSettings.defaultRenderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
        }

        static void ConfigureCameras()
        {
            foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                var data = camera.GetComponent<HDAdditionalCameraData>();
                if (data == null) data = camera.gameObject.AddComponent<HDAdditionalCameraData>();
                data.customRenderingSettings = true;
                data.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
                camera.allowMSAA = false;
                foreach (var field in new[] {
                    FrameSettingsField.Water, FrameSettingsField.TransparentObjects, FrameSettingsField.Refraction,
                    FrameSettingsField.SSR, FrameSettingsField.TransparentSSR, FrameSettingsField.TransparentPrepass,
                    FrameSettingsField.VolumetricClouds, FrameSettingsField.Volumetrics, FrameSettingsField.AtmosphericScattering,
                    FrameSettingsField.SSAO, FrameSettingsField.SSGI, FrameSettingsField.ExposureControl, FrameSettingsField.Postprocess,
                    FrameSettingsField.Bloom, FrameSettingsField.Tonemapping, FrameSettingsField.Antialiasing })
                    OverrideFrame(data, field, true);
                OverrideFrame(data, FrameSettingsField.MSAA, false);
                EditorUtility.SetDirty(data);
            }
        }

        static void OverrideFrame(HDAdditionalCameraData data, FrameSettingsField field, bool enabled)
        {
            data.renderingPathCustomFrameSettings.SetEnabled(field, enabled);
            data.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)field] = true;
        }

        static T Component<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out var component)) return component;
            component = profile.Add<T>(true);
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        static void ConfigureEnvironment()
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "CoastalSky";
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }
            var environment = Component<VisualEnvironment>(profile);
            environment.skyType.Override(SkySettings.GetUniqueID<PhysicallyBasedSky>());
            environment.skyAmbientMode.Override(SkyAmbientMode.Dynamic);
            var sky = Component<PhysicallyBasedSky>(profile);
            sky.exposure.Override(0f);
            sky.updateMode.Override(EnvironmentUpdateMode.Realtime);
            sky.updatePeriod.Override(1f);
            var clouds = Component<VolumetricClouds>(profile);
            clouds.enable.Override(true);
            clouds.cloudControl.Override(VolumetricClouds.CloudControl.Simple);
            clouds.cloudSimpleMode.Override(VolumetricClouds.CloudSimpleMode.Quality);
            clouds.cloudPreset = VolumetricClouds.CloudPresets.Sparse;
            clouds.bottomAltitude.Override(1900f);
            clouds.altitudeRange.Override(1000f);
            // More, smaller cloud forms with softer internal shadows instead of a low ceiling.
            clouds.shapeScale.Override(10f);
            clouds.multiScattering.Override(.8f);
            var exposure = Component<Exposure>(profile);
            exposure.mode.Override(ExposureMode.Fixed);
            exposure.fixedExposure.Override(13.2f);
            Component<Tonemapping>(profile).mode.Override(TonemappingMode.ACES);
            Component<Bloom>(profile).intensity.Override(.15f);
            var fog = Component<Fog>(profile);
            fog.enabled.Override(true);
            fog.enableVolumetricFog.Override(true);
            fog.meanFreePath.Override(2000f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(100f);
            var ssr = Component<ScreenSpaceReflection>(profile);
            ssr.enabled.Override(true);
            ssr.enabledTransparent.Override(true);
            ssr.reflectSky.Override(true);
            var ao = Component<ScreenSpaceAmbientOcclusion>(profile);
            ao.intensity.Override(.3f);
            ao.radius.Override(.45f);
            // HDRP owns the dynamic sky ambient probe. Give that diffuse contribution
            // readable weight, then use screen-space GI for actual visible ground bounce.
            Component<IndirectLightingController>(profile).indirectDiffuseLightingMultiplier.Override(2f);
            var gi = Component<GlobalIllumination>(profile);
            gi.enable.Override(true);
            gi.tracing.Override(RayCastingMode.RayMarching);
            gi.fullResolutionSS.Override(false);
            gi.rayMiss.Override(RayMarchingFallbackHierarchy.ReflectionProbesAndSky);
            bool assigned = false;
            foreach (var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!volume.isGlobal) continue;
                volume.sharedProfile = profile;
                volume.weight = 1f;
                volume.enabled = true;
                EditorUtility.SetDirty(volume);
                assigned = true;
            }
            if (!assigned)
            {
                var volume = new GameObject("Coastal Environment").AddComponent<Volume>();
                volume.isGlobal = true;
                volume.sharedProfile = profile;
            }
            foreach (var component in profile.components) EditorUtility.SetDirty(component);
            EditorUtility.SetDirty(profile);
        }

        static void ConfigureSun()
        {
            var sun = RenderSettings.sun;
            if (sun == null)
                foreach (var light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                    if (light.type == LightType.Directional) { sun = light; break; }
            if (sun == null) sun = new GameObject("Coastal Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(45f, 90f, 0f);
            sun.lightUnit = LightUnit.Lux;
            sun.intensity = 85000f;
            sun.color = Color.white;
            sun.useColorTemperature = true;
            sun.colorTemperature = 5700f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;
            var data = sun.GetComponent<HDAdditionalLightData>();
            if (data == null) data = sun.gameObject.AddComponent<HDAdditionalLightData>();
            data.interactsWithSky = true;
            data.angularDiameter = .65f;
            data.diameterMultiplerMode = false;
            data.diameterOverride = .65f;
            EditorUtility.SetDirty(sun);
            EditorUtility.SetDirty(data);
        }

        static void ConfigureReflection()
        {
            var go = GameObject.Find("Coastal Reflection");
            if (go == null) go = new GameObject("Coastal Reflection");
            go.transform.position = new Vector3(0f, 8f, 0f);
            var probe = go.GetComponent<HDAdditionalReflectionData>();
            if (probe == null) probe = go.AddComponent<HDAdditionalReflectionData>();
            probe.mode = ProbeSettings.Mode.Realtime;
            probe.realtimeMode = ProbeSettings.RealtimeMode.OnDemand;
            probe.timeSlicing = true;
            probe.influenceVolume.boxSize = new Vector3(1200f, 400f, 1200f);
            probe.weight = 1f;
            probe.multiplier = 1f;
            EditorUtility.SetDirty(probe);
        }
    }
}
