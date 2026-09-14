using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using SunsetGlow.Player;

namespace SunsetGlow.EnvironmentArt
{
    public sealed class CoastalEnvironmentPresenter : MonoBehaviour
    {
        [Serializable] public sealed class SeasonalPair { public Material summer, winter; }
        public CoastalLookProfile[] looks;
        public SeasonalPair[] seasonalMaterials;
        public ComputeShader seasonalBlendCompute;
        public Volume environmentVolume;
        public Light sun;
        public FirstPersonController player;
        public WaterSurface ocean;
        public HDAdditionalReflectionData reflectionProbe;
        public ParticleSystem snow;
        public WindField wind;
        public float transitionSeconds = 8;
        public int ActiveLook { get; private set; }
        public float SnowCoverage { get; private set; }
        public float PrecipitationIntensity { get; private set; }
        public bool IsSnowing => PrecipitationIntensity > 0;

        VolumeProfile runtimeProfile;
        PhysicallyBasedSky sky;
        VolumetricClouds clouds;
        Exposure exposure;
        Fog fog;
        HDAdditionalLightData hdSun;
        float blend = 1;
        float lastSnowBlend = -1;
        float reflectionTimer;
        LookState from, to;
        readonly List<MaterialState> materials = new List<MaterialState>();
        bool capture;
        bool previouslySheltered;
        struct LookState
        {
            public float elevation, azimuth, lux, temperature, ev, density, shape, erosion, aerosol, fog, snow, snowfall, wind;
            public Color horizon, zenith;
            public static LookState Read(CoastalLookProfile p) => new LookState {
                elevation=p.sunElevation, azimuth=p.sunAzimuth, lux=p.sunLux, temperature=p.sunTemperature,
                ev=p.exposureEV,density=p.cloudDensity,shape=p.cloudShape,erosion=p.cloudErosion,aerosol=p.aerosolDensity,
                fog=p.fogDistance,snow=p.snowCoverage,snowfall=p.snowfallRate,wind=p.windSpeed,horizon=p.horizonTint,zenith=p.zenithTint };
            public static LookState Mix(LookState a, LookState b, float t) => new LookState {
                elevation=Mathf.Lerp(a.elevation,b.elevation,t),azimuth=Mathf.LerpAngle(a.azimuth,b.azimuth,t),lux=Mathf.Lerp(a.lux,b.lux,t),
                temperature=Mathf.Lerp(a.temperature,b.temperature,t),ev=Mathf.Lerp(a.ev,b.ev,t),density=Mathf.Lerp(a.density,b.density,t),
                shape=Mathf.Lerp(a.shape,b.shape,t),erosion=Mathf.Lerp(a.erosion,b.erosion,t),aerosol=Mathf.Lerp(a.aerosol,b.aerosol,t),
                fog=Mathf.Lerp(a.fog,b.fog,t),snow=Mathf.Lerp(a.snow,b.snow,t),snowfall=Mathf.Lerp(a.snowfall,b.snowfall,t),wind=Mathf.Lerp(a.wind,b.wind,t),
                horizon=Color.Lerp(a.horizon,b.horizon,t),zenith=Color.Lerp(a.zenith,b.zenith,t) };
        }
        sealed class MaterialState
        {
            public Material source, winter, instance;
            public RenderTexture texture;
        }

        void Awake()
        {
            capture = Array.IndexOf(System.Environment.GetCommandLineArgs(), "-coastalCapture") >= 0;
            runtimeProfile = environmentVolume.profile; // Unity clones the shared profile for runtime changes.
            runtimeProfile.TryGet(out sky); runtimeProfile.TryGet(out clouds);
            runtimeProfile.TryGet(out exposure); runtimeProfile.TryGet(out fog);
            hdSun = sun.GetComponent<HDAdditionalLightData>();
            CreateSeasonalInstances();
            SetLook(0, true);
        }

        public void SetLook(int index, bool immediate = false)
        {
            if (looks == null || index < 0 || index >= looks.Length || looks[index] == null) return;
            from = LookState.Mix(from, to, Mathf.SmoothStep(0,1,blend));
            to = LookState.Read(looks[index]);
            ActiveLook = index;
            if(wind!=null)wind.SetTarget(new Vector2(.848f,.53f),looks[index].windSpeed/3.6f,looks[index].windSpeed/18f);
            blend = immediate ? 1 : 0;
            if (immediate) from = to;
            Apply(LookState.Mix(from,to,blend));
            (RenderPipelineManager.currentPipeline as HDRenderPipeline)?.RequestSkyEnvironmentUpdate();
            if (reflectionProbe != null) reflectionProbe.RequestRenderNextUpdate();
        }

        void Update()
        {
            if(player!=null && player.Paused)return;
            if (!capture)
            {
                if (Input.GetKeyDown(KeyCode.Alpha1)) SetLook(0);
                if (Input.GetKeyDown(KeyCode.Alpha2)) SetLook(1);
                if (Input.GetKeyDown(KeyCode.Alpha3)) SetLook(2);
            }
            if (blend < 1)
            {
                blend = Mathf.Min(1, blend + Time.unscaledDeltaTime / Mathf.Max(.1f, transitionSeconds));
                Apply(LookState.Mix(from,to,Mathf.SmoothStep(0,1,blend)));
            }
            reflectionTimer += Time.unscaledDeltaTime;
            if (reflectionTimer > 2 && reflectionProbe != null)
            {
                reflectionTimer = 0;
                reflectionProbe.RequestRenderNextUpdate();
            }
            UpdateSnow();
        }

        void Apply(LookState s)
        {
            sun.transform.rotation = Quaternion.Euler(s.elevation, s.azimuth + 180, 0);
            sun.lightUnit = LightUnit.Lux; sun.intensity = s.lux;
            sun.useColorTemperature = true; sun.colorTemperature = s.temperature;
            hdSun.diameterOverride = Mathf.Lerp(.6f, .9f, Mathf.InverseLerp(30, 3, s.elevation));
            exposure.fixedExposure.Override(s.ev);
            sky.aerosolDensity.Override(s.aerosol);
            sky.horizonTint.Override(s.horizon); sky.zenithTint.Override(s.zenith);
            clouds.densityMultiplier.Override(s.density); clouds.shapeFactor.Override(s.shape); clouds.erosionFactor.Override(s.erosion);
            fog.meanFreePath.Override(s.fog);
            SnowCoverage = s.snow;
            PrecipitationIntensity = s.snowfall;
            if (wind==null && ocean != null) { ocean.largeWindSpeed = s.wind; ocean.ripplesWindSpeed = s.wind * .65f; }
            if (Mathf.Abs(lastSnowBlend-s.snow) > .005f || s.snow == 0 || s.snow == 1)
            {
                BlendMaterials(s.snow);
                lastSnowBlend = s.snow;
            }
        }

        void CreateSeasonalInstances()
        {
            if (seasonalMaterials == null) return;
            var replacements = new Dictionary<Material, Material>();
            foreach (var pair in seasonalMaterials)
            {
                if (pair.summer == null || pair.winter == null || replacements.ContainsKey(pair.summer)) continue;
                var m = new MaterialState { source=pair.summer, winter=pair.winter, instance=new Material(pair.summer) };
                Texture summer = pair.summer.GetTexture("_BaseColorMap"), winter = pair.winter.GetTexture("_BaseColorMap");
                if (summer != null && winter != null && seasonalBlendCompute != null)
                {
                    m.texture = new RenderTexture(summer.width, summer.height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
                    { name=pair.summer.name+" runtime seasonal blend", enableRandomWrite=true, useMipMap=true, autoGenerateMips=false, wrapMode=summer.wrapMode, anisoLevel=8 };
                    m.texture.Create(); m.instance.SetTexture("_BaseColorMap",m.texture);
                }
                materials.Add(m); replacements[pair.summer]=m.instance; replacements[pair.winter]=m.instance;
            }
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                Material[] slots=renderer.sharedMaterials;
                bool changed=false;
                for(int i=0;i<slots.Length;i++) if(slots[i]!=null && replacements.TryGetValue(slots[i],out var replacement)) { slots[i]=replacement;changed=true; }
                if(changed) renderer.sharedMaterials=slots;
            }
        }

        void BlendMaterials(float amount)
        {
            foreach (var m in materials)
            {
                m.instance.SetColor("_BaseColor",Color.Lerp(m.source.GetColor("_BaseColor"),m.winter.GetColor("_BaseColor"),amount));
                m.instance.SetFloat("_Smoothness",Mathf.Lerp(m.source.GetFloat("_Smoothness"),m.winter.GetFloat("_Smoothness"),amount));
                if(m.texture!=null)
                {
                    seasonalBlendCompute.SetTexture(0,"_Summer",m.source.GetTexture("_BaseColorMap"));
                    seasonalBlendCompute.SetTexture(0,"_Winter",m.winter.GetTexture("_BaseColorMap"));
                    seasonalBlendCompute.SetTexture(0,"_Result",m.texture);
                    seasonalBlendCompute.SetFloat("_Amount",amount);
                    seasonalBlendCompute.Dispatch(0,(m.texture.width+7)/8,(m.texture.height+7)/8,1);
                    m.texture.GenerateMips();
                    // Stable endpoints retain the authored texture and its original mip chain.
                    m.instance.SetTexture("_BaseColorMap",amount<=0 ? m.source.GetTexture("_BaseColorMap") : amount>=1 ? m.winter.GetTexture("_BaseColorMap") : m.texture);
                }
            }
        }

        void UpdateSnow()
        {
            if (snow==null || player==null) return;
            bool sheltered=Physics.Raycast(player.transform.position+Vector3.up*1.7f,Vector3.up,10,LayerMask.GetMask("Shelter"));
            snow.transform.position=player.transform.position+Vector3.up*7;
            var emission=snow.emission;
            emission.rateOverTime=sheltered ? 0 : PrecipitationIntensity;
            if(sheltered && !previouslySheltered) snow.Clear();
            previouslySheltered=sheltered;
        }

        public void CaptureSeasonalTextures(string directory)
        {
            var previous=RenderTexture.active;
            try { foreach(var m in materials)
            {
                if(m.texture==null)continue;
                RenderTexture.active=m.texture;
                var copy=new Texture2D(m.texture.width,m.texture.height,TextureFormat.RGBA32,false);
                copy.ReadPixels(new Rect(0,0,copy.width,copy.height),0,0);copy.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.Combine(directory,m.source.name+"-blend.png"),copy.EncodeToPNG());
                Debug.Log("SEASONAL_TEXTURE "+m.source.name+" blend="+SnowCoverage+" lower="+copy.GetPixel(copy.width/2,copy.height/4)+" upper="+copy.GetPixel(copy.width/2,copy.height*3/4));
                Destroy(copy);
            }} finally {RenderTexture.active=previous;}
        }

        void OnGUI()
        {
            if(capture || (player!=null && player.Paused)) return;
            GUI.Label(new Rect(22, Screen.height-42, 700, 24), "1  Clear coast     2  Golden sunset     3  Winter shore");
        }
        void OnDestroy()
        {
            foreach(var m in materials) { if(m.texture!=null){m.texture.Release();Destroy(m.texture);} Destroy(m.instance); }
            if(runtimeProfile!=null) {foreach(var component in runtimeProfile.components)Destroy(component);Destroy(runtimeProfile);}
        }
    }
}
