using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using SunsetGlow.Player;
using SunsetGlow.EnvironmentArt;
using SunsetGlow.Debugging;

namespace SunsetGlow.Editor
{
    public static class CoastalBuild
    {
        const string Root="Assets/_Game/Art";
        [MenuItem("Sunset Glow/Coastal/Upgrade environment and build")]
        public static void UpgradeAndBuild()
        {
            const string backup="Assets/_Game/Scenes/M1BeforeVisualUpgrade.unity";
            if(!File.Exists(backup)) AssetDatabase.CopyAsset(PrototypeBuild.ScenePath,backup);
            var scene=EditorSceneManager.OpenScene(PrototypeBuild.ScenePath,OpenSceneMode.Single);
            CoastalLightingSetup.Apply();
            CoastalWaterSetup.Apply();
            CoastalLandArt.Apply();
            var materialTests=GameObject.Find("Material tests — wood stone soil cloth");
            if(materialTests!=null) foreach(var renderer in materialTests.GetComponentsInChildren<Renderer>()) renderer.enabled=false;
            var presenter=UnityEngine.Object.FindFirstObjectByType<CoastalEnvironmentPresenter>();
            if(presenter==null) presenter=new GameObject("Coastal Environment Presentation").AddComponent<CoastalEnvironmentPresenter>();
            presenter.player=UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            presenter.environmentVolume=UnityEngine.Object.FindFirstObjectByType<Volume>();
            presenter.sun=RenderSettings.sun;
            presenter.ocean=UnityEngine.Object.FindFirstObjectByType<WaterSurface>();
            presenter.reflectionProbe=UnityEngine.Object.FindFirstObjectByType<HDAdditionalReflectionData>();
            presenter.seasonalBlendCompute=AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/_Game/Shaders/SeasonalBlend.compute");
            string[] names={"LandTerrain","PineNeedles","CabinRoof","MeadowGrass"};
            presenter.seasonalMaterials=new CoastalEnvironmentPresenter.SeasonalPair[names.Length];
            for(int i=0;i<names.Length;i++) presenter.seasonalMaterials[i]=new CoastalEnvironmentPresenter.SeasonalPair {
                summer=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Land/"+names[i]+"_Summer.mat"),
                winter=AssetDatabase.LoadAssetAtPath<Material>(Root+"/Land/"+names[i]+"_Winter.mat") };
            presenter.looks=new[]{Look("ClearDay",0),Look("GoldenSunset",1),Look("WinterShore",2)};
            presenter.snow=CreateSnow(presenter.transform);
            var capture=presenter.GetComponent<CoastalCaptureRunner>();
            if(capture==null) capture=presenter.gameObject.AddComponent<CoastalCaptureRunner>();
            capture.presenter=presenter; capture.player=presenter.player;
            presenter.player.viewCamera.farClipPlane=3000;
            var baseline=presenter.player.viewCamera.GetComponent<BaselineRunner>();
            baseline.measurementScope="Coastal HDRP water/clouds/snow visual preview; 1080p dedicated Coastal pipeline. Not complete P0 weather acceptance.";
            if(UnityEngine.Object.FindFirstObjectByType<WaterContactSystem>()!=null)InteractionBuild.Apply();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Build();
        }

        static CoastalLookProfile Look(string name,int index)
        {
            string path=Root+"/"+name+".asset";
            var p=AssetDatabase.LoadAssetAtPath<CoastalLookProfile>(path);
            if(p==null) {p=ScriptableObject.CreateInstance<CoastalLookProfile>();AssetDatabase.CreateAsset(p,path);}
            p.displayName=name;
            p.sunElevation=index==0?48:index==1?9:24;
            p.sunAzimuth=index==1?270:255;
            p.sunLux=index==0?100000:index==1?30000:45000;
            p.sunTemperature=index==0?6500:index==1?5700:6800;
            p.exposureEV=index==0?12.8f:index==1?10.7f:12.3f;
            p.cloudDensity=index==0?.35f:index==1?.4f:.45f;
            p.cloudShape=index==0?.95f:index==1?.925f:.86f;
            p.cloudErosion=index==2?.55f:.75f;
            p.aerosolDensity=index==1?.004f:.002f;
            p.fogDistance=index==2?1100:2400;
            p.horizonTint=index==1?new Color(1,.94f,.87f):Color.white;
            p.zenithTint=index==2?new Color(.86f,.94f,1):Color.white;
            p.snowCoverage=index==2?1:0;
            p.snowfallRate=index==2?32:0;
            p.windSpeed=index==2?24:18;
            EditorUtility.SetDirty(p);return p;
        }

        static ParticleSystem CreateSnow(Transform parent)
        {
            var existing=parent.Find("Local snowfall");
            if(existing!=null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var go=new GameObject("Local snowfall");go.transform.SetParent(parent,false);
            var ps=go.AddComponent<ParticleSystem>();ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main=ps.main;main.startLifetime=6;main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.025f,.065f);
            main.maxParticles=400;main.simulationSpace=ParticleSystemSimulationSpace.World;main.startColor=Color.white;
            var emission=ps.emission;emission.rateOverTime=0;
            var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(18,.1f,18);
            var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=-1.5f;
            var noise=ps.noise;noise.enabled=true;noise.strength=.3f;noise.frequency=.3f;
            var collision=ps.collision;collision.enabled=true;collision.type=ParticleSystemCollisionType.World;
            collision.collidesWith=LayerMask.GetMask("Shelter");collision.lifetimeLoss=1;
            var texture=new Texture2D(32,32,TextureFormat.RGBA32,true);texture.name="Snowflake";
            var pixels=new Color[1024];for(int y=0;y<32;y++)for(int x=0;x<32;x++){
                float d=Vector2.Distance(new Vector2(x,y),new Vector2(15.5f,15.5f))/15.5f;
                pixels[y*32+x]=new Color(1,1,1,Mathf.SmoothStep(1,0,Mathf.Clamp01(d)));}
            texture.SetPixels(pixels);texture.Apply();
            string texturePath=Root+"/Snowflake.asset";
            var saved=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            if(saved==null){AssetDatabase.CreateAsset(texture,texturePath);saved=texture;}else{EditorUtility.CopySerialized(texture,saved);UnityEngine.Object.DestroyImmediate(texture);}
            string matPath=Root+"/Snowflake.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(mat==null){mat=new Material(Shader.Find("HDRP/Unlit"));AssetDatabase.CreateAsset(mat,matPath);}
            mat.SetFloat("_SurfaceType",1);mat.SetFloat("_BlendMode",0);mat.SetFloat("_ZWrite",0);
            mat.SetColor("_UnlitColor",Color.white);mat.SetTexture("_UnlitColorMap",saved);HDShaderUtils.ResetMaterialKeywords(mat);
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial=mat;
            EditorUtility.SetDirty(mat);ps.Play();return ps;
        }

        [MenuItem("Sunset Glow/Coastal/Build current environment")]
        public static void Build()
        {
            AssetDatabase.SaveAssets();
            Directory.CreateDirectory("../artifacts/coastal");
            var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions {scenes=new[]{PrototypeBuild.ScenePath},
                locationPathName=Path.GetFullPath("../Builds/M1/IslandPrototype.exe"),target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
            File.WriteAllText("../artifacts/coastal/build.json",JsonUtility.ToJson(new Summary{result=report.summary.result.ToString(),errors=report.summary.totalErrors,
                warnings=report.summary.totalWarnings,seconds=report.summary.totalTime.TotalSeconds,bytes=report.summary.totalSize},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new Exception("Coastal build failed");
        }
        [Serializable]class Summary{public string result;public int errors,warnings;public double seconds;public ulong bytes;}
    }
}
