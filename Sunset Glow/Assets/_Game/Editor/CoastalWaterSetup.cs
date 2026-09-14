using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace SunsetGlow.Editor
{
    /// <summary>Authored HDRP ocean for the sample island; all generated assets survive player builds.</summary>
    public static class CoastalWaterSetup
    {
        const string Root = "Assets/_Game/Art/Water";

        public static void Apply()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var oldWater = GameObject.Find("Sea level 0 — opaque M1 water");
            if (oldWater != null) Object.DestroyImmediate(oldWater);
            var parent = GameObject.Find("M1SampleRoot");
            var ocean = GameObject.Find("Coastal Ocean — HDRP Water");
            if (ocean == null) ocean = new GameObject("Coastal Ocean — HDRP Water");
            if (parent != null) ocean.transform.SetParent(parent.transform, false);
            ocean.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var water = ocean.GetComponent<WaterSurface>();
            if (water == null) water = ocean.AddComponent<WaterSurface>();
            water.surfaceType = WaterSurfaceType.OceanSeaLake;
            water.geometryType = WaterGeometryType.Infinite;
            // Visual waves run at real speed; world clock acceleration must never change Time.timeScale.
            water.timeMultiplier = 1f;
            water.scriptInteractions = false;
            water.cpuEvaluateRipples = false;
            water.repetitionSize = 250f;
            water.largeWindSpeed = 18f;
            water.largeOrientationValue = 32f;
            water.largeChaos = .65f;
            water.largeBand0Multiplier = .34f;
            water.largeBand1Multiplier = .55f;
            water.ripples = true;
            water.ripplesMotionMode = WaterPropertyOverrideMode.Custom;
            water.ripplesWindSpeed = 6.5f;
            water.ripplesOrientationValue = 48f;
            water.ripplesChaos = .72f;
            water.startSmoothness = .96f;
            water.endSmoothness = .88f;
            water.smoothnessFadeStart = 80f;
            water.smoothnessFadeDistance = 700f;
            water.refractionColor = new Color(.18f, .79f, .84f).linear;
            water.scatteringColor = new Color(.035f, .55f, .59f).linear;
            water.absorptionDistance = 18f;
            // This caps distortion, not visibility depth. Keep it moderate to avoid shoreline smearing.
            water.maxRefractionDistance = 1.1f;
            water.ambientScattering = .32f;
            water.heightScattering = .22f;
            water.displacementScattering = .16f;
            water.directLightTipScattering = .55f;
            water.directLightBodyScattering = .34f;
            water.caustics = true;
            water.causticsBand = 2;
            water.causticsIntensity = .32f;
            water.causticsResolution = WaterSurface.WaterCausticsResolution.Caustics256;
            water.causticsPlaneBlendDistance = 1.5f;
            water.virtualPlaneDistance = 4f;
            water.foam = true;
            water.foamResolution = WaterSurface.WaterDecalRegionResolution.Resolution512;
            water.foamTextureTiling = .3f;
            water.foamSmoothness = .25f;
            water.simulationFoamAmount = .18f;
            water.foamPersistenceMultiplier = .35f;
            water.simulationFoamWindCurve = AnimationCurve.Linear(0f, .1f, 1f, 1f);
            water.decalRegionSize = new Vector2(240, 240);
            water.tessellation = true;
            water.maxTessellationFactor = 3f;
            water.tessellationFactorFadeStart = 35f;
            water.tessellationFactorFadeRange = 180f;
            water.underWater = true;
            water.volumeDepth = 60f;
            water.waterMask = CreateMask();
            water.waterMaskExtent = new Vector2(220, 220);
            water.waterMaskOffset = Vector2.zero;
            water.waterMaskRemap = new Vector2(0, 1);
            CreateSeabed(parent != null ? parent.transform : null);
            EditorUtility.SetDirty(water);
            AssetDatabase.SaveAssets();
        }

        static Texture2D CreateMask()
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true)
            { name = "Coastal wave attenuation", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float px = ((float)x / (size - 1) - .5f) * 220;
                    float pz = ((float)y / (size - 1) - .5f) * 220;
                    float r = Mathf.Sqrt(px * px / 3600f + pz * pz / 2500f);
                    float large = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.94f, 1.65f, r));
                    float ripples = Mathf.SmoothStep(.08f, 1, Mathf.InverseLerp(.91f, 1.08f, r));
                    pixels[y * size + x] = new Color(large, large, ripples, 1);
                }
            texture.SetPixels(pixels);
            texture.Apply();
            return Save(texture, Root + "/CoastalWaveMask.asset");
        }

        static void CreateSeabed(Transform parent)
        {
            // A broad shallow shelf creates a real depth-driven turquoise gradient. The old mesh
            // drops to -2m in only a few metres, which reads as black water at a beach-level angle.
            const int count = 150;
            var vertices = new Vector3[(count + 1) * (count + 1)];
            var uv = new Vector2[vertices.Length];
            var indices = new int[count * count * 6];
            for (int z = 0; z <= count; z++)
                for (int x = 0; x <= count; x++)
                {
                    int i = z * (count + 1) + x;
                    float px = x * 4 - 300, pz = z * 4 - 300;
                    float r = Mathf.Sqrt(px * px / 3600f + pz * pz / 2500f);
                    float shoreDistance = Mathf.Max(0, r - .916f);
                    float h = -.06f - shoreDistance * 2.4f;
                    if (r > 1.5f) h -= (r - 1.5f) * (r - 1.5f) * 14f;
                    h = Mathf.Max(-47f, h);
                    vertices[i] = new Vector3(px, h, pz);
                    uv[i] = new Vector2(px / 6f, pz / 6f);
                    if (x == count || z == count) continue;
                    int t = (z * count + x) * 6;
                    indices[t] = i; indices[t + 1] = i + count + 1; indices[t + 2] = i + 1;
                    indices[t + 3] = i + 1; indices[t + 4] = i + count + 1; indices[t + 5] = i + count + 2;
                }
            var mesh = new Mesh { name = "600m coastal shelf", vertices = vertices, uv = uv, triangles = indices };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh = Save(mesh, Root + "/CoastalShelf.asset");
            var material = AssetDatabase.LoadAssetAtPath<Material>(Root + "/Seabed.mat");
            if (material == null)
            {
                material = new Material(Shader.Find("HDRP/Lit")) { name = "Submerged sand" };
                AssetDatabase.CreateAsset(material, Root + "/Seabed.mat");
            }
            material.SetColor("_BaseColor", new Color(.76f, .71f, .51f));
            material.SetFloat("_Smoothness", .16f);
            material.SetFloat("_Metallic", 0f);
            EditorUtility.SetDirty(material);
            var bed = GameObject.Find("Coastal shelf — submerged sand");
            if (bed == null) bed = new GameObject("Coastal shelf — submerged sand");
            bed.transform.SetParent(parent, false);
            bed.transform.localPosition = Vector3.zero;
            var filter = bed.GetComponent<MeshFilter>();
            if (filter == null) filter = bed.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            var renderer = bed.GetComponent<MeshRenderer>();
            if (renderer == null) renderer = bed.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            bed.isStatic = true;
        }

        static T Save<T>(T generated, string path) where T : Object
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(generated, path);
                return generated;
            }
            EditorUtility.CopySerialized(generated, existing);
            Object.DestroyImmediate(generated);
            EditorUtility.SetDirty(existing);
            return existing;
        }
    }
}
