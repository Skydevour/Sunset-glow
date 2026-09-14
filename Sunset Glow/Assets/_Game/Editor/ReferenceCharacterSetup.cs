using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;
using SunsetGlow.Player;

namespace SunsetGlow.Editor
{
    public static class ReferenceCharacterSetup
    {
        const string Root = "Assets/_Game/Art/Characters/RiceShower";
        const string Model = Root + "/RiceShower.fbx";
        public const string PrefabPath = Root + "/RiceShower.prefab";

        public static void Apply()
        {
            AssetDatabase.Refresh();
            var importer = AssetImporter.GetAtPath(Model) as ModelImporter;
            if (importer == null) throw new InvalidOperationException("Missing reference character FBX: " + Model);
            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.importCameras = false; importer.importLights = false;
            importer.isReadable = false;
            var animationClips = importer.defaultClipAnimations;
            foreach (var clip in animationClips) { clip.loopTime = true; clip.loopPose = true; }
            importer.clipAnimations = animationClips;
            importer.SaveAndReimport();
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            var fabricNormal = CreateFabricNormal();
            var player = UnityEngine.Object.FindFirstObjectByType<FirstPersonController>();
            player.eyeHeight = 1.425f; player.eyeForward = .16f; player.capsuleHeight = 1.65f;
            player.viewCamera.transform.localPosition = new Vector3(0,player.eyeHeight,player.eyeForward);
            EditorUtility.SetDirty(player);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            instance.name = "Rice Shower — reference character";
            instance.transform.SetParent(player.transform, false);
            var materials = new System.Collections.Generic.Dictionary<string,Material>();
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>())
            {
                var assigned = renderer.sharedMaterials;
                for (int i = 0; i < assigned.Length; i++)
                {
                    string name = assigned[i] != null ? assigned[i].name : "WhiteCloth";
                    if (!materials.TryGetValue(name, out var material))
                    {
                        string path = Root + "/" + name + "_HDRP.mat";
                        material = AssetDatabase.LoadAssetAtPath<Material>(path);
                        if (material == null) { material = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(material, path); }
                        Color color = assigned[i] != null && assigned[i].HasProperty("_BaseColor") ? assigned[i].GetColor("_BaseColor") : assigned[i] != null && assigned[i].HasProperty("_Color") ? assigned[i].color : Color.white;
                        // Blender exports its linear Principled base color numerically; HDRP's
                        // color property expects display-space values and linearizes for shading.
                        color = color.gamma;
                        material.SetColor("_BaseColor", color);
                        material.SetFloat("_Smoothness", name.ToLowerInvariant().Contains("eye") ? .6f : name.ToLowerInvariant().Contains("hair") ? .24f : .22f);
                        material.SetFloat("_DoubleSidedEnable", 1);
                        if(name.Contains("Cloth")) { material.SetTexture("_NormalMap",fabricNormal);material.SetFloat("_NormalScale",.22f); }
                        HDShaderUtils.ResetMaterialKeywords(material);
                        EditorUtility.SetDirty(material); materials[name] = material;
                    }
                    assigned[i] = material;
                }
                renderer.sharedMaterials = assigned;
                renderer.gameObject.layer = player.gameObject.layer;
                if (renderer is SkinnedMeshRenderer skin) { skin.updateWhenOffscreen = true; skin.localBounds = new Bounds(Vector3.up * .9f, new Vector3(3,3,3)); }
            }
            var animator = instance.GetComponent<Animator>();
            if (animator == null) animator = instance.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            string controllerPath = Root + "/RiceShower.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
            if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
            foreach (var layer in controller.layers) foreach (var state in layer.stateMachine.states) layer.stateMachine.RemoveState(state.state);
            if (!controller.parameters.Any(p => p.name == "Speed")) controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            var clips = AssetDatabase.LoadAllAssetsAtPath(Model).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
            var idle = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("idle"));
            var walk = clips.FirstOrDefault(c => c.name.ToLowerInvariant().Contains("walk"));
            if (idle == null || walk == null) throw new InvalidOperationException("Reference character requires exported Idle and Walk clips.");
            var tree = new BlendTree { name = "Locomotion", blendParameter = "Speed", useAutomaticThresholds = false };
            AssetDatabase.AddObjectToAsset(tree, controller);
            tree.AddChild(idle, 0); tree.AddChild(walk, 4);
            var locomotion = controller.layers[0].stateMachine.AddState("Locomotion"); locomotion.motion = tree;
            controller.layers[0].stateMachine.defaultState = locomotion;
            animator.runtimeAnimatorController = controller;
            var character = instance.AddComponent<ReferenceCharacter>();
            character.animator = animator;
            character.headRenderers = instance.GetComponentsInChildren<Renderer>().Where(r => {
                string n = r.name.ToLowerInvariant();
                return n.Contains("head") || n.Contains("hair") || n.Contains("eye") || n.Contains("face") || n.Contains("ear") || n.Contains("flower") || n.Contains("lash") || n.Contains("mouth") || n.Contains("brow");
            }).ToArray();
            // Keep scene controller references outside the reusable prefab.
            PrefabUtility.SaveAsPrefabAsset(instance, PrefabPath);
            character.player = player;
            var old = player.transform.Find("First person visible body");
            if (old != null) UnityEngine.Object.DestroyImmediate(old.gameObject);
            foreach (var other in player.GetComponentsInChildren<ReferenceCharacter>())
                if (other != character) UnityEngine.Object.DestroyImmediate(other.gameObject);
            EditorUtility.SetDirty(controller);
            AssetDatabase.SaveAssets();
            Debug.Log("REFERENCE_CHARACTER_READY meshes=" + instance.GetComponentsInChildren<SkinnedMeshRenderer>().Length + " clips=" + clips.Length + " headParts=" + character.headRenderers.Length);
        }

        static Texture2D CreateFabricNormal()
        {
            const int n=512;var pixels=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float warp=Mathf.Sin(x*Mathf.PI*.5f),weft=Mathf.Sin(y*Mathf.PI*.5f);
                var normal=new Vector3(warp*.18f,weft*.18f,1).normalized;
                pixels[y*n+x]=new Color(normal.x*.5f+.5f,normal.y*.5f+.5f,normal.z*.5f+.5f,1);
            }
            var texture=new Texture2D(n,n,TextureFormat.RGBA32,false,true);texture.SetPixels(pixels);texture.Apply();
            string path=Root+"/FabricMicroNormal.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.NormalMap;importer.wrapMode=TextureWrapMode.Repeat;importer.anisoLevel=8;importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
