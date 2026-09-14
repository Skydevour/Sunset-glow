using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;

namespace SunsetGlow.Editor
{
    /// <summary>Deterministic, scene-local art pass. Original walkable and Shelter colliders remain authoritative.</summary>
    public static class CoastalLandArt
    {
        const string Root = "Assets/_Game/Art/Land";
        static readonly Color Sand = new Color(.63f, .57f, .40f);
        static readonly Color Grass = new Color(.23f, .29f, .115f);
        static readonly Color Rock = new Color(.34f, .36f, .33f);

        public static void Apply()
        {
            Directory.CreateDirectory(Root);
            AssetDatabase.Refresh();
            var sample = GameObject.Find("M1SampleRoot");
            if (!sample) throw new InvalidOperationException("M1 sample scene must be open.");
            var previous = sample.transform.Find("Coastal land detail");
            if (previous) UnityEngine.Object.DestroyImmediate(previous.gameObject);
            var detail = new GameObject("Coastal land detail").transform;
            detail.SetParent(sample.transform, false);
            Material terrain = TerrainMaterial(false);
            TerrainMaterial(true);
            Material bark = Surface("PineBark", new Color(.31f,.245f,.175f), false);
            Material wood = Surface("CabinTimber", new Color(.43f,.32f,.205f), true);
            Material roof = Surface("CabinRoof_Summer", new Color(.21f,.24f,.235f), true);
            Material eaves = Surface("CabinEaves", new Color(.21f,.24f,.235f), true);
            Surface("CabinRoof_Winter", new Color(.84f,.88f,.87f), true);
            var needles = Lit("PineNeedles_Summer", new Color(.16f,.25f,.085f), .24f);
            var snowy = Lit("PineNeedles_Winter", new Color(.65f,.73f,.66f), .18f);
            foreach(var mat in new [] {needles, snowy})
            {
                bool isWinter=mat==snowy;
                var pixels=new Color[128*128];
                for(int y=0;y<128;y++)for(int x=0;x<128;x++)
                {
                    float noise=Mathf.PerlinNoise(x*.12f,y*.12f);
                    Color green=new Color(.19f,.29f,.105f)*(.84f+noise*.3f);
                    // Only horizontal, exposed fronds use the upper UV band; vertical needles remain green.
                    pixels[y*128+x]=isWinter&&y>=64?Color.Lerp(green,new Color(.91f,.94f,.92f),.78f+noise*.22f):green;
                }
                mat.SetColor("_BaseColor",Color.white);
                mat.SetTexture("_BaseColorMap",Texture(mat.name+"_Albedo",128,pixels,false));
                mat.SetFloat("_DoubleSidedEnable", 1); mat.SetFloat("_DoubleSidedNormalMode", 1);
                HDShaderUtils.ResetMaterialKeywords(mat);
            }
            Material stone = Surface("CoastalRock", new Color(.38f,.39f,.35f), false);
            var ground = FindChild(sample.transform, "Island terrain");
            if (!ground) throw new InvalidOperationException("M1 terrain missing.");
            var filter = ground.GetComponent<MeshFilter>();
            Mesh src = filter.sharedMesh;
            Mesh mesh = UnityEngine.Object.Instantiate(src);
            mesh.name = "Continuous textured coastal terrain";
            var triangles = new List<int>();
            for (int i=0; i<src.subMeshCount; i++) triangles.AddRange(src.GetTriangles(i));
            var uv = new Vector2[mesh.vertexCount];
            var vertices = mesh.vertices;
            for(int i=0;i<uv.Length;i++) uv[i]=new Vector2((vertices[i].x+60)/120,(vertices[i].z+50)/100);
            mesh.uv=uv; mesh.subMeshCount=1; mesh.SetTriangles(triangles,0); mesh.RecalculateTangents();
            filter.sharedMesh=Save(mesh,"CoastalTerrain.asset");
            ground.GetComponent<MeshRenderer>().sharedMaterial=terrain;
            // Keep the original MeshCollider and its original sharedMesh: art never changes traversal.
            var forest = FindChild(sample.transform,"Forest");
            if(forest)
            {
                int index=0;
                foreach(Transform tree in forest)
                {
                    foreach(var r in tree.GetComponentsInChildren<Renderer>()) r.enabled=false;
                    var oldLod = tree.GetComponent<LODGroup>();
                    if(oldLod) UnityEngine.Object.DestroyImmediate(oldLod);
                    var visual=new GameObject("Coastal pine "+(++index)).transform;
                    visual.SetParent(detail,false); visual.position=tree.position;
                    visual.localRotation=Quaternion.Euler(0,index*137.5f,0);
                    float height=5.7f+((index-1)%4)*.67f;
                    visual.localScale=new Vector3(.88f+(index%3)*.09f,1,.93f+(index%2)*.12f);
                    var trunk=Draw("Root flare and forked pine branches",visual,Trunk(height,index),bark);
                    var high=Draw("Pine canopy near",visual,Pine(height,index,1),needles);
                    var low=Draw("Pine canopy distant",visual,Pine(height,index,2),needles);
                    foreach(var moving in new[]{trunk,high,low})
                    {
                        moving.gameObject.isStatic=false;
                        var motion=moving.gameObject.AddComponent<SunsetGlow.EnvironmentArt.VegetationMeshWind>();
                        motion.height=height;motion.animationDistance=72;
                    }
                    var lod=visual.gameObject.AddComponent<LODGroup>();
                    lod.fadeMode=LODFadeMode.CrossFade; lod.animateCrossFading=true;
                    lod.SetLODs(new [] {new LOD(.16f,new Renderer[]{trunk,high}),new LOD(.003f,new Renderer[]{trunk,low})});
                    lod.RecalculateBounds();
                }
            }
            var cabin=FindChild(sample.transform,"Cabin 6x5m");
            if(cabin)
            {
                foreach(Transform piece in cabin)
                {
                    var r=piece.GetComponent<Renderer>();
                    if(!r)continue;
                    if(piece.name.StartsWith("Roof"))r.sharedMaterial=eaves;
                    else if(!piece.name.StartsWith("Foundation"))r.sharedMaterial=wood;
                }
                var roofVisual=new GameObject("Cabin pitched roof surface").transform;
                roofVisual.SetParent(detail,false); roofVisual.position=cabin.position;
                Draw("Cabin snow receiving roof",roofVisual,Roof(),roof);
                for(int i=0;i<=10;i++)
                {
                    float x=-3.15f+i*.63f;
                    for(int side=-1;side<=1;side+=2)
                    {
                        Beam("Roof standing seam",roofVisual,new Vector3(x,3.615f,side*1.55f),new Vector3(.035f,.035f,3.32f),roof);
                        roofVisual.GetChild(roofVisual.childCount-1).localRotation=Quaternion.Euler(side*21.16f,0,0);
                    }
                }
                // Thin horizontal weatherboards follow each original wall segment and do not bridge openings.
                foreach(Transform wall in cabin)
                {
                    if(wall.name.StartsWith("Roof")||wall.name.StartsWith("Foundation"))continue;
                    var size=wall.localScale;
                    bool northSouth=size.x>size.z;
                    int rows=Mathf.FloorToInt(size.y/.21f);
                    for(int j=0;j<rows;j++)
                    {
                        Vector3 p=wall.position+Vector3.up*(-size.y*.5f+.12f+j*.21f);
                        float sign=northSouth?Mathf.Sign(wall.localPosition.z):Mathf.Sign(wall.localPosition.x);
                        p+=northSouth?new Vector3(0,0,sign*.108f):new Vector3(sign*.108f,0,0);
                        Beam("Weatherboard edge",detail,p,northSouth?new Vector3(size.x,.025f,.025f):new Vector3(.025f,.025f,size.z),wood);
                    }
                }
            }
            // All decorative rocks and grass are non-colliding and placed clear of the acceptance traversal lanes.
            Vector2[] rocks={new Vector2(46,16),new Vector2(47,21),new Vector2(41,28),new Vector2(-48,17),new Vector2(-43,-20),new Vector2(34,-29),new Vector2(-35,31)};
            for(int i=0;i<rocks.Length;i++)
            {
                var p=rocks[i];
                var r=Draw("Shore weathered rock "+i,detail,Boulder(i),stone);
                r.transform.localPosition=new Vector3(p.x,IslandSampleBuilder.Height(p.x,p.y)-.3f,p.y);
                r.transform.localScale=new Vector3(2.2f+(i%3)*.8f,1.1f+(i%2)*.5f,1.6f);
                r.transform.localRotation=Quaternion.Euler(0,i*57,0);
            }
            Mesh tuft=Tuft();
            var tuftMesh=Save(tuft,"CoastalGrass.asset");
            var meadow=Lit("MeadowGrass_Summer",new Color(.39f,.44f,.19f),.08f);
            var winterMeadow=Lit("MeadowGrass_Winter",new Color(.57f,.48f,.29f),.08f);
            var grassPixels=new Color[32*32];
            for(int i=0;i<grassPixels.Length;i++)grassPixels[i]=Color.white*(.88f+.12f*Mathf.Sin(i*.37f)*Mathf.Sin(i*.37f));
            var grassTexture=Texture("MeadowGrass_Albedo",32,grassPixels,false);
            foreach(var mat in new[]{meadow,winterMeadow})
            {
                mat.SetTexture("_BaseColorMap",grassTexture);
                mat.SetFloat("_DoubleSidedEnable",1);mat.SetFloat("_DoubleSidedNormalMode",1);
                HDShaderUtils.ResetMaterialKeywords(mat);
            }
            var grassPatches=new List<CombineInstance>[64];
            for(int p=0;p<grassPatches.Length;p++)grassPatches[p]=new List<CombineInstance>();
            var rng=new System.Random(914);
            for(int i=0;i<11000;i++)
            {
                float x=(float)rng.NextDouble()*96-48,z=(float)rng.NextDouble()*78-39;
                float h=IslandSampleBuilder.Height(x,z);
                if(h<.85f||Mathf.Abs(z+3)<1.1f||Mathf.Abs(x-22)<.6f||Mathf.Abs(x+15)<.6f||Mathf.Abs(x)<4&&Mathf.Abs(z-8)<3.3f)continue;
                float density=Mathf.PerlinNoise(x*.11f+67,z*.11f+37);
                if(density<.34f||(float)rng.NextDouble()>Mathf.Lerp(.18f,.96f,Mathf.InverseLerp(.34f,.72f,density)))continue;
                float scale=.58f+(float)rng.NextDouble()*.58f;
                var matrix=Matrix4x4.TRS(new Vector3(x,h-.035f,z),Quaternion.Euler(0,i*137.5f,0),new Vector3(scale,scale*.8f,scale));
                int patch=Mathf.Clamp((int)((x+48)/12),0,7)+Mathf.Clamp((int)((z+39)/9.75f),0,7)*8;
                grassPatches[patch].Add(new CombineInstance{mesh=tuftMesh,transform=matrix});
            }
            for(int p=0;p<grassPatches.Length;p++)
            {
                if(grassPatches[p].Count==0)continue;
                int maxTufts=Mathf.Max(1,90000/tuftMesh.vertexCount);
                for(int first=0;first<grassPatches[p].Count;first+=maxTufts)
                {
                    int count=Mathf.Min(maxTufts,grassPatches[p].Count-first);
                    var merged=new Mesh{name="CoastalMeadowPatch_"+p+"_"+first,indexFormat=IndexFormat.UInt32};
                    merged.CombineMeshes(grassPatches[p].GetRange(first,count).ToArray(),true,true);
                    var renderer=Draw("Coastal meadow patch "+p+" "+first,detail,merged,meadow);
                    renderer.shadowCastingMode=ShadowCastingMode.Off;
                    renderer.gameObject.isStatic=false;
                    var motion=renderer.gameObject.AddComponent<SunsetGlow.EnvironmentArt.VegetationMeshWind>();
                    motion.grass=true;motion.animationDistance=38;
                }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("COASTAL_LAND_ART_APPLIED: blended terrain, coastal pines, timber cabin, exterior snow material pairs; original collision preserved.");
        }

        static Transform FindChild(Transform t,string prefix)
        { foreach(Transform child in t)if(child.name.StartsWith(prefix,StringComparison.Ordinal))return child; return null; }
        static Material TerrainMaterial(bool winter)
        {
            const int n=1024;
            var colors=new Color[n*n];var normals=new Color[n*n];
            var treeCenters=new Vector2[18];
            for(int i=0;i<18;i++)treeCenters[i]=new Vector2(-28+(i%6)*4.5f+Mathf.Sin(i*2.3f),10+(i/6)*6+Mathf.Cos(i*1.7f));
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float wx=x/(n-1f)*120-60,wz=y/(n-1f)*100-50;
                float h=IslandSampleBuilder.Height(wx,wz);
                float nx=IslandSampleBuilder.Height(wx+.2f,wz)-IslandSampleBuilder.Height(wx-.2f,wz);
                float nz=IslandSampleBuilder.Height(wx,wz+.2f)-IslandSampleBuilder.Height(wx,wz-.2f);
                float slope=Mathf.Sqrt(nx*nx+nz*nz)/.4f;
                float broad=Mathf.PerlinNoise(wx*.25f+47,wz*.25f+31);
                float fine=Mathf.PerlinNoise(wx*8+93,wz*8+53);
                float moss=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.58f,1.65f,h+(broad-.5f)*.85f));
                Color c=Color.Lerp(Sand,Grass,moss);
                c=Color.Lerp(c,Rock,Mathf.SmoothStep(0,1,Mathf.InverseLerp(.23f,.65f,slope))*.8f);
                float patch=Mathf.PerlinNoise(wx*.77f+19,wz*.77f+83);
                float mottling=Mathf.PerlinNoise(wx*2.3f+47,wz*2.3f+99);
                c*=.62f+broad*.52f+mottling*.24f+fine*.10f;
                float nearestTree=10000;
                for(int tree=0;tree<treeCenters.Length;tree++)nearestTree=Mathf.Min(nearestTree,(treeCenters[tree]-new Vector2(wx,wz)).sqrMagnitude);
                float litter=Mathf.Exp(-nearestTree/7.5f)*moss;
                float soil=Mathf.SmoothStep(0,1,Mathf.InverseLerp(.49f,.77f,patch))*.58f*moss;
                Color earth=Color.Lerp(new Color(.18f,.125f,.075f),new Color(.32f,.25f,.14f),mottling);
                c=Color.Lerp(c,earth,Mathf.Clamp01(litter*.78f+soil));
                float wet=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.52f,h));
                c*=1-wet*.36f;
                if(winter)
                {
                    float cover=SunsetGlow.EnvironmentArt.SurfaceCoverage.TerrainSnow(new Vector3(wx,h,wz),slope);
                    c=Color.Lerp(c,new Color(.79f,.835f,.835f)*(.94f+fine*.08f),cover);
                }
                colors[y*n+x]=c;
                float ddx=Mathf.PerlinNoise((wx+.025f)*8+93,wz*8+53)-fine;
                float ddy=Mathf.PerlinNoise(wx*8+93,(wz+.025f)*8+53)-fine;
                Vector3 norm=new Vector3(-ddx*.75f,-ddy*.75f,1).normalized;
                normals[y*n+x]=new Color(norm.x*.5f+.5f,norm.y*.5f+.5f,norm.z*.5f+.5f);
            }
            string name="LandTerrain_"+(winter?"Winter":"Summer");
            var albedo=Texture(name+"_Albedo",n,colors,false);
            var normal=Texture(name+"_Normal",n,normals,true);
            var mat=Lit(name,Color.white,.13f);
            mat.SetTexture("_BaseColorMap",albedo);mat.SetTexture("_NormalMap",normal);mat.SetFloat("_NormalScale",.6f);
            // A 1.5m physical tile adds exposed grit and short fallen-needle strokes below the macro coverage.
            const int detailSize=256;var details=new Color[detailSize*detailSize];
            var relief=new float[detailSize*detailSize];
            for(int y=0;y<detailSize;y++)for(int x=0;x<detailSize;x++)
                relief[y*detailSize+x]=Mathf.PerlinNoise(x*.67f+13,y*.67f+53)*.68f+Mathf.PerlinNoise(x*.19f+37,y*.19f+79)*.32f;
            var litterRandom=new System.Random(190914);
            for(int needle=0;needle<580;needle++)
            {
                float x=(float)litterRandom.NextDouble()*detailSize,y=(float)litterRandom.NextDouble()*detailSize;
                float angle=(float)litterRandom.NextDouble()*Mathf.PI*2;
                int length=3+litterRandom.Next(12);float value=needle%3==0?.78f:.24f;
                for(int k=0;k<length;k++)
                {
                    int px=((int)(x+Mathf.Cos(angle)*k)+detailSize)%detailSize;
                    int py=((int)(y+Mathf.Sin(angle)*k)+detailSize)%detailSize;
                    relief[py*detailSize+px]=value;
                }
            }
            for(int y=0;y<detailSize;y++)for(int x=0;x<detailSize;x++)
            {
                float value=relief[y*detailSize+x];
                float dx=relief[y*detailSize+(x+1)%detailSize]-value;
                float dy=relief[((y+1)%detailSize)*detailSize+x]-value;
                details[y*detailSize+x]=new Color(.5f+(value-.5f)*.85f,.5f-dy*.33f,.5f,.5f-dx*.33f);
            }
            var detail=Texture("GroundMicroDetail",detailSize,details,false);
            var detailImporter=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(detail));
            detailImporter.sRGBTexture=false;detailImporter.SaveAndReimport();
            mat.SetTexture("_DetailMap",detail);mat.SetTextureScale("_DetailMap",new Vector2(80,66.667f));
            mat.SetFloat("_DetailAlbedoScale",1.3f);mat.SetFloat("_DetailNormalScale",.4875f);mat.SetFloat("_DetailSmoothnessScale",0);
            mat.SetFloat("_UVDetail",0);mat.SetColor("_UVDetailsMappingMask",new Color(1,0,0,0));
            HDShaderUtils.ResetMaterialKeywords(mat);return mat;
        }
        static Material Surface(string name,Color tint,bool planks)
        {
            const int n=256;var c=new Color[n*n];var normal=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                if(name=="PineBark")
                {
                    float value=BarkHeight(x,y);
                    float dx=BarkHeight(x+1,y)-value,dy=BarkHeight(x,y+1)-value;
                    c[y*n+x]=Color.Lerp(new Color(.105f,.074f,.045f),new Color(.37f,.27f,.16f),value);
                    Vector3 norm=new Vector3(-dx*2.5f,-dy*2.5f,1).normalized;
                    normal[y*n+x]=new Color(norm.x*.5f+.5f,norm.y*.5f+.5f,norm.z*.5f+.5f);
                    continue;
                }
                if(name=="CoastalRock")
                {
                    // Isotropic mineral grains and weathering pockets, never the directional timber generator.
                    float mineral=StoneNoise(x,y);
                    float pocket=Mathf.PerlinNoise(x*.049f+73,y*.049f+91);
                    Color baseStone=Color.Lerp(new Color(.29f,.30f,.285f),new Color(.49f,.48f,.44f),mineral);
                    c[y*n+x]=baseStone*(.91f+Mathf.SmoothStep(0,1,pocket)*.12f);
                    float dx=StoneNoise(x+1,y)-mineral,dy=StoneNoise(x,y+1)-mineral;
                    Vector3 nm=new Vector3(-dx*1.1f,-dy*1.1f,1).normalized;
                    normal[y*n+x]=new Color(nm.x*.5f+.5f,nm.y*.5f+.5f,nm.z*.5f+.5f);
                    continue;
                }
                float grain=Mathf.PerlinNoise(x*.12f,y*.014f)*.6f+Mathf.PerlinNoise(x*.7f,y*.04f)*.4f;
                float seam=planks&&x%64<2 ? .57f:1;
                Color surfaceTint=tint;
                if(name=="CabinRoof_Winter")surfaceTint=y>=n/2?new Color(.94f,.965f,.95f):new Color(.21f,.24f,.235f);
                c[y*n+x]=surfaceTint*(.7f+grain*.5f)*seam;
                normal[y*n+x]=new Color(.5f+(grain-.5f)*.2f,.5f,.99f);
            }
            var mat=Lit(name,Color.white,.24f);
            mat.SetTexture("_BaseColorMap",Texture(name+"_Albedo",n,c,false));
            mat.SetTexture("_NormalMap",Texture(name+"_Normal",n,normal,true));
            HDShaderUtils.ResetMaterialKeywords(mat);return mat;
        }
        static float BarkHeight(float x,float y)
        {
            float warp=Mathf.PerlinNoise(x*.027f+17,y*.038f+49)*8;
            float ridge=Mathf.PerlinNoise((x+warp)*.16f+53,y*.021f+37);
            float plates=Mathf.PerlinNoise(x*.09f+11,y*.13f+79);
            return Mathf.Clamp01(Mathf.SmoothStep(0,1,Mathf.InverseLerp(.27f,.72f,ridge))*.8f+plates*.2f);
        }
        static float StoneNoise(float x,float y)
            => Mathf.PerlinNoise(x*.028f+13,y*.028f+37)*.5f
             + Mathf.PerlinNoise(x*.12f+29,y*.12f+61)*.3f
             + Mathf.PerlinNoise(x*.43f+71,y*.43f+89)*.2f;
        static Texture2D Texture(string name,int size,Color[] pixels,bool normal)
        {
            var tex=new Texture2D(size,size,TextureFormat.RGBA32,false,normal);tex.SetPixels(pixels);tex.Apply();
            string path=Root+"/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;
            importer.sRGBTexture=!normal;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;
            importer.anisoLevel=8;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
        static Material Lit(string name,Color color,float smoothness)
        {
            string path=Root+"/"+name+".mat";
            var m=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!m){m=new Material(Shader.Find("HDRP/Lit"));AssetDatabase.CreateAsset(m,path);}
            m.name=name;m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smoothness);
            m.enableInstancing=true;HDShaderUtils.ResetMaterialKeywords(m);EditorUtility.SetDirty(m);return m;
        }
        static T Save<T>(T value,string file) where T:UnityEngine.Object
        {
            string path=Root+"/"+file;var existing=AssetDatabase.LoadAssetAtPath<T>(path);
            if(existing){EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;}
            AssetDatabase.CreateAsset(value,path);return value;
        }
        static MeshRenderer Draw(string name,Transform parent,Mesh mesh,Material material)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);go.isStatic=true;
            go.AddComponent<MeshFilter>().sharedMesh=Save(mesh,mesh.name+".asset");
            var r=go.AddComponent<MeshRenderer>();r.sharedMaterial=material;return r;
        }
        static void Beam(string name,Transform parent,Vector3 p,Vector3 scale,Material material)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;
            UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;
        }
        sealed class Geometry
        {
            readonly List<Vector3> v=new List<Vector3>();readonly List<Vector2> uv=new List<Vector2>();readonly List<int> t=new List<int>();
            readonly List<Color> colors=new List<Color>();
            public float bladeHeight;
            public Vector3 bladeRoot;
            public void Tri(Vector3 a,Vector3 b,Vector3 c,bool? snow=null)
            {
                float low=snow==true ? .53f:.03f,high=snow==true ? .97f:.47f;
                Emit(a,b,c,snow.HasValue?new Vector2(.03f,low):Vector2.zero,snow.HasValue?new Vector2(.97f,low):Vector2.right,snow.HasValue?new Vector2(.03f,high):Vector2.up);
            }
            public void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d,bool? snow=null)
            {
                // Map the entire quad once; restarting UVs on each triangle makes diagonal seams.
                float low=snow.HasValue?(snow.Value ? .53f:.03f):0;
                float high=snow.HasValue?(snow.Value ? .97f:.47f):1;
                float left=snow.HasValue ? .03f:0,right=snow.HasValue ? .97f:1;
                var ua=new Vector2(left,low);var ub=new Vector2(left,high);
                var uc=new Vector2(right,high);var ud=new Vector2(right,low);
                Emit(a,b,c,ua,ub,uc);Emit(a,c,d,ua,uc,ud);
            }
            void Emit(Vector3 a,Vector3 b,Vector3 c,Vector2 ua,Vector2 ub,Vector2 uc)
            {
                if(Vector3.Cross(b-a,c-a).sqrMagnitude<1e-12f)return;
                int k=v.Count;v.Add(a);v.Add(b);v.Add(c);uv.Add(ua);uv.Add(ub);uv.Add(uc);
                t.Add(k);t.Add(k+1);t.Add(k+2);
                colors.Add(Weight(a));colors.Add(Weight(b));colors.Add(Weight(c));
            }
            Color Weight(Vector3 p)=>new Color(bladeHeight>0?Mathf.Clamp01((p.y-bladeRoot.y)/bladeHeight):0,1,1,1);
            public Mesh Mesh(string name,bool smooth=false)
            {
                var m=new Mesh{name=name,indexFormat=IndexFormat.UInt32};m.SetVertices(v);m.SetColors(colors);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateNormals();
                if(smooth)
                {
                    var normals=m.normals;var average=new Dictionary<Vector3,Vector3>();
                    for(int i=0;i<v.Count;i++)average[v[i]]=average.TryGetValue(v[i],out var n)?n+normals[i]:normals[i];
                    for(int i=0;i<v.Count;i++)normals[i]=average[v[i]].normalized;
                    m.normals=normals;
                }
                m.RecalculateTangents();m.RecalculateBounds();return m;
            }
        }
        static void Limb(Geometry g,Vector3 a,Vector3 b,float ra,float rb,int sides=7)
        {
            Vector3 axis=(b-a).normalized;
            Vector3 u=Vector3.Cross(axis,Mathf.Abs(axis.y)>.95f?Vector3.forward:Vector3.up).normalized;
            Vector3 v=Vector3.Cross(u,axis);
            for(int i=0;i<sides;i++)
            {
                float q=i*Mathf.PI*2/sides,r=(i+1)*Mathf.PI*2/sides;
                Vector3 d=u*Mathf.Cos(q)+v*Mathf.Sin(q),e=u*Mathf.Cos(r)+v*Mathf.Sin(r);
                g.Quad(a+d*ra,b+d*rb,b+e*rb,a+e*ra);
            }
        }
        static void Branch(int layer,int branch,int seed,float h,out Vector3 origin,out Vector3 dir,out float length)
        {
            float level=.27f+layer*.078f+Mathf.Sin(branch*8.7f+seed)*.031f;
            float angle=branch*Mathf.PI*2/7+layer*1.71f+seed*.38f;
            dir=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle));
            origin=Stem(level,h);
            length=(2.05f-layer*.19f)*(1+.29f*Mathf.Sin(seed+branch*9+layer*2.7f));
        }
        static Vector3 BranchPoint(Vector3 origin,Vector3 dir,float length,float t)
            =>origin+dir*(length*t)+Vector3.up*(-.25f*Mathf.Sin(t*Mathf.PI)+.39f*t*t);
        static Mesh Trunk(float h,int seed)
        {
            var g=new Geometry();
            const int rings=13,sides=13;
            var trunkRings=new Vector3[rings,sides];
            Vector3 transportedRight=Vector3.right;
            for(int y=0;y<rings;y++)
            {
                float t=y/(rings-1f);
                Vector3 center=y==0?Vector3.down*.09f:Stem(t,h);
                Vector3 tangent=(Stem(Mathf.Min(1,t+.01f),h)-Stem(Mathf.Max(0,t-.01f),h)).normalized;
                // Parallel-transport one frame along the stem; adjacent segments reuse the exact ring positions.
                transportedRight=Vector3.ProjectOnPlane(transportedRight,tangent).normalized;
                Vector3 forward=Vector3.Cross(transportedRight,tangent);
                float radius=y==0?.34f:Mathf.Lerp(.27f,.019f,t);
                for(int side=0;side<sides;side++)
                {
                    float angle=side*Mathf.PI*2/sides;
                    trunkRings[y,side]=center+radius*(transportedRight*Mathf.Cos(angle)+forward*Mathf.Sin(angle));
                }
            }
            for(int y=0;y<rings-1;y++)for(int side=0;side<sides;side++)
            {
                int next=(side+1)%sides;
                g.Quad(trunkRings[y,side],trunkRings[y+1,side],trunkRings[y+1,next],trunkRings[y,next]);
            }
            // Buttressed roots, lower dead stubs and visibly tapering primary/secondary limbs.
            for(int i=0;i<6;i++)
            {
                float a=i*2.399f+seed;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Limb(g,Vector3.down*.025f+d*.17f,d*(.46f+.09f*Mathf.Sin(i*3)) - Vector3.up*.14f,.105f,.018f,9);
            }
            for(int layer=0;layer<9;layer++)for(int branch=0;branch<7;branch++)
            {
                Branch(layer,branch,seed,h,out var origin,out var dir,out float length);
                if((layer*7+branch+seed)%13==0)length*=.47f;
                for(int segment=0;segment<4;segment++)
                {
                    float a=segment/4f,b=(segment+1)/4f;
                    Limb(g,BranchPoint(origin,dir,length,a),BranchPoint(origin,dir,length,b),(.063f-layer*.004f)*(1-a)+.005f,(.063f-layer*.004f)*(1-b)+.003f,6);
                }
                Vector3 side=new Vector3(-dir.z,0,dir.x);
                for(int twig=2;twig<7;twig++)for(int sign=-1;sign<=1;sign+=2)
                {
                    float t=twig/8f;Vector3 start=BranchPoint(origin,dir,length,t);
                    Vector3 end=start+side*sign*(.42f*(1-t)+.14f)+dir*.24f+Vector3.up*.10f;
                    Limb(g,start,end,.012f,.002f,4);
                }
            }
            return g.Mesh("PineTrunk_"+seed,true);
        }
        static Vector3 Stem(float t,float h)=>new Vector3(Mathf.Sin(t*2)*t*.43f,t*h,Mathf.Sin(t*3)*t*.19f);
        static Mesh Pine(float h,int seed,int step)
        {
            var g=new Geometry();
            // Needles radiate in short three-dimensional fascicles from visible woody twigs.
            // Uneven branch lengths and occasional gaps break the former repeated fern tiers.
            for(int layer=0;layer<9;layer++)for(int branch=0;branch<7;branch++)
            {
                if((layer*7+branch+seed)%13==0)continue;
                Branch(layer,branch,seed,h,out var origin,out var dir,out float length);
                Vector3 side=new Vector3(-dir.z,0,dir.x);
                for(int twig=2;twig<=7;twig+=(step==1?2:3))for(int sign=-1;sign<=1;sign+=2)
                {
                    float f=twig/8f;Vector3 center=BranchPoint(origin,dir,length,f);
                    Vector3 tip=center+side*(sign*(.42f*(1-f)+.14f))+dir*.24f+Vector3.up*.10f;
                    Vector3 axis=(tip-center).normalized;
                    Vector3 across=Vector3.Cross(axis,Vector3.up).normalized;
                    for(int cluster=1;cluster<=(step==1?3:2);cluster++)
                    {
                        Vector3 basePoint=Vector3.Lerp(center,tip,cluster/3.4f);
                        int count=step==1?5:4;
                        for(int needle=0;needle<count;needle++)
                        {
                            float a=needle*Mathf.PI*2/count+twig*.91f+layer;
                            Vector3 radial=across*Mathf.Cos(a)+Vector3.up*Mathf.Sin(a);
                            float extent=.22f+.055f*Mathf.Sin(twig*9+needle*3+seed);
                            Vector3 end=basePoint+axis*.115f+radial*extent;
                            Vector3 width=Vector3.Cross(end-basePoint,axis).normalized*(step==1?.021f:.029f);
                            Vector3 left=(basePoint+end)*.38f + basePoint*.24f - width + axis*.012f;
                            Vector3 right=(basePoint+end)*.38f + basePoint*.24f + width + axis*.012f;
                            // A folded lance catches thin highlights; upper needles retain the authored snow UV band.
                            bool snow=radial.y>.25f;
                            g.Tri(basePoint,left,end,snow);
                            g.Tri(basePoint,end,right,snow);
                        }
                    }
                }
            }
            return g.Mesh("PineCanopy_"+seed+"_LOD"+step);
        }
        static Mesh Roof()
        {
            var g=new Geometry();
            g.Quad(new Vector3(-3.5f,3, -3.1f),new Vector3(-3.5f,4.2f,0),new Vector3(3.5f,4.2f,0),new Vector3(3.5f,3,-3.1f),true);
            g.Quad(new Vector3(3.5f,3,3.1f),new Vector3(3.5f,4.2f,0),new Vector3(-3.5f,4.2f,0),new Vector3(-3.5f,3,3.1f),true);
            g.Tri(new Vector3(-3.5f,3,3.1f),new Vector3(-3.5f,4.2f,0),new Vector3(-3.5f,3,-3.1f),false);
            g.Tri(new Vector3(3.5f,3,-3.1f),new Vector3(3.5f,4.2f,0),new Vector3(3.5f,3,3.1f),false);
            return g.Mesh("CabinPitchedRoof");
        }
        static Mesh Boulder(int seed)
        {
            var g=new Geometry();int segments=11;
            for(int j=0;j<4;j++)for(int i=0;i<segments;i++)
            {
                Vector3 a=StonePoint(j,i,seed),b=StonePoint(j+1,i,seed),c=StonePoint(j+1,(i+1)%segments,seed),d=StonePoint(j,(i+1)%segments,seed);
                g.Quad(a,b,c,d);
            }
            return g.Mesh("ShoreRock_"+seed);
        }
        static Vector3 StonePoint(int j,int i,int seed)
        {
            float a=i*Mathf.PI*2/11,r=Mathf.Sin(j*Mathf.PI/4)*(.83f+.17f*Mathf.Sin(i*9+seed));
            return new Vector3(Mathf.Cos(a)*r,-Mathf.Cos(j*Mathf.PI/4)*.85f,Mathf.Sin(a)*r);
        }
        static Mesh Tuft()
        {
            var g=new Geometry();
            // Narrow folded blades and broader sedge blades of different heights share one batch.
            for(int i=0;i<19;i++)
            {
                float a=i*2.399f;Vector3 d=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                Vector3 p=d*(.06f+(i%3)*.038f);
                float h=.13f+(i%6)*.026f;
                float width=i%5==0?.009f:.0045f;
                Vector3 side=Vector3.Cross(d,Vector3.up)*width;
                g.bladeRoot=p;g.bladeHeight=h;
                for(int segment=0;segment<3;segment++)
                {
                    float t=segment/3f,u=(segment+1)/3f;
                    Vector3 b=p+d*(t*t*.11f)+Vector3.up*(h*t);
                    Vector3 e=p+d*(u*u*.11f)+Vector3.up*(h*u);
                    float wb=1-t,we=1-u;
                    Vector3 rb=b-d*(width*.30f*wb),re=e-d*(width*.30f*we);
                    g.Quad(b-side*wb,e-side*we,re,rb,false);
                    g.Quad(rb,re,e+side*we,b+side*wb,false);
                }
            }
            return g.Mesh("CoastalGrass");
        }
    }
}

