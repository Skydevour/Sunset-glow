using System.IO;
using SunsetGlow.EnvironmentArt;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace SunsetGlow.Editor
{
    /// <summary>Run after CoastalWaterSetup. Retains the existing texture-based shoreline mask.</summary>
    public static class WaterInteractionSetup
    {
        public static WaterContactSystem Apply(Transform anchor)
        {
            var ocean = Object.FindFirstObjectByType<WaterSurface>();
            if (ocean == null) throw new System.InvalidOperationException("Create Coastal Water before interaction setup.");
            ocean.scriptInteractions = true;
            ocean.cpuEvaluateRipples = false;
            ocean.deformation = true;
            ocean.deformationRes = WaterSurface.WaterDecalRegionResolution.Resolution512;
            ocean.decalRegionSize = new Vector2(32f, 32f);
            ocean.decalRegionAnchor = anchor;
            // Do NOT enable m_EnableMaskAndCurrentWaterDecals: deformation works independently,
            // while that setting would switch off the authored waterMask texture path.
            var pipeline = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>("Assets/_Game/Art/CoastalHDRP.asset");
            if (pipeline != null)
            {
                var settings = pipeline.currentPlatformRenderPipelineSettings;
                settings.supportWaterDecals = true;
                pipeline.currentPlatformRenderPipelineSettings = settings;
                EditorUtility.SetDirty(pipeline);
            }
            foreach (var camera in Object.FindObjectsByType<HDAdditionalCameraData>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                camera.customRenderingSettings = true;
                camera.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.WaterDecals, true);
                camera.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.WaterDecals] = true;
                EditorUtility.SetDirty(camera);
            }
            const string path = "Assets/_Game/Art/Water/FootContactRipple.mat";
            Directory.CreateDirectory("Assets/_Game/Art/Water");
            var shader = Shader.Find("SunsetGlow/WaterContactRipple");
            if (shader == null) throw new System.InvalidOperationException("WaterContactRipple shader missing.");
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader) { name = "FootContactRipple" };
                AssetDatabase.CreateAsset(material, path);
            }
            material.shader = shader;
            material.SetFloat("_AffectDeformation", 1f);
            material.SetFloat("_AffectsFoam", 0f);
            var system = Object.FindFirstObjectByType<WaterContactSystem>();
            if (system == null) system = new GameObject("Water contact and local ripples").AddComponent<WaterContactSystem>();
            system.ocean = ocean;
            system.followTarget = anchor;
            system.rippleMaterial = material;
            EditorUtility.SetDirty(material);
            EditorUtility.SetDirty(ocean);
            EditorUtility.SetDirty(system);
            return system;
        }
    }
}
