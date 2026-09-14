using System;
using System.Collections.Generic;
using System.IO;
using SunsetGlow.Debugging;
using SunsetGlow.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace SunsetGlow.Editor
{
    /// <summary>One-shot, metre-scale M1 weather test island. Does not alter the original template.</summary>
    public static class IslandSampleBuilder
    {
        const string AssetsRoot = "Assets/_Game/M1";

        [MenuItem("Sunset Glow/M1/Create sample island")]
        public static void CreateSample()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeBuild.ScenePath, OpenSceneMode.Single);
            if (GameObject.Find("M1SampleRoot") != null)
                throw new InvalidOperationException("M1 sample already exists; refusing to overwrite authored work.");
            const string backup = "Assets/_Game/Scenes/M0Baseline.unity";
            if (!File.Exists(backup) && !AssetDatabase.CopyAsset(PrototypeBuild.ScenePath, backup))
                throw new IOException("Could not preserve M0 baseline scene.");
            Directory.CreateDirectory(AssetsRoot);
            AssetDatabase.Refresh();
            foreach (var root in scene.GetRootGameObjects())
                if (root.name == "Baseline ground 40m" || root.name == "Human scale 1.8m" ||
                    root.name == "North +Z marker" || root.name == "East +X marker" ||
                    root.name.StartsWith("ObservationPoints", StringComparison.Ordinal))
                    UnityEngine.Object.DestroyImmediate(root);
            foreach (var runner in UnityEngine.Object.FindObjectsByType<BaselineRunner>(FindObjectsSortMode.None))
                UnityEngine.Object.DestroyImmediate(runner);

            int shelterLayer = EnsureLayer("Shelter");
            int playerLayer = EnsureLayer("Player");
            var sample = new GameObject("M1SampleRoot").transform;
            Material sand = Material("Sand", new Color(.58f, .43f, .25f));
            Material grass = Material("Grass", new Color(.16f, .24f, .075f));
            Material stone = Material("Stone", new Color(.29f, .32f, .32f));
            Material wood = Material("Wood", new Color(.26f, .12f, .05f));
            Material soil = Material("Soil", new Color(.15f, .09f, .055f));
            Material cloth = Material("Cloth", new Color(.49f, .22f, .12f));
            Material foliage = Material("Foliage", new Color(.055f, .16f, .075f));
            Material sea = Material("Sea", new Color(.055f, .23f, .25f), .25f);
            Material roof = Material("Roof", new Color(.15f, .19f, .21f));
            CreateTerrain(sample, sand, grass, stone);
            var ocean = Block("Sea level 0 — opaque M1 water", sample, new Vector3(0, -.05f, 0), new Vector3(1000, .1f, 1000), sea);
            UnityEngine.Object.DestroyImmediate(ocean.GetComponent<Collider>());
            CreateTrees(sample, wood, foliage);
            CreateCabin(sample, wood, stone, roof, shelterLayer);
            CreateMaterialTests(sample, new[] { wood, stone, soil, cloth });

            var points = new GameObject("ObservationPoints (+Z North, +X East, metres)").transform;
            points.SetParent(sample, false);
            Transform[] observations = {
                Point(points, "EastCoast", 48, -5, 95),
                Point(points, "WestCoast", -48, -5, 265),
                Point(points, "Forest", -15, 15, 320),
                Point(points, "Hilltop", 22, 25, 220),
                Point(points, "CabinWindow", -1.9f, 8, 270, CabinFloor + .03f)
            };
            Transform spawn = Point(points, "Spawn", 0, -2, 0);
            Camera camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("M0 scene has no main camera.");
            var player = new GameObject("Player (1.8m)");
            player.transform.SetParent(sample, false);
            player.layer = playerLayer;
            player.transform.SetPositionAndRotation(spawn.position, spawn.rotation);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.radius = .3f; cc.center = new Vector3(0, .9f, 0);
            cc.stepOffset = .3f; cc.slopeLimit = 45; cc.skinWidth = .03f;
            camera.transform.SetParent(player.transform, false);
            camera.transform.localPosition = new Vector3(0, 1.65f, 0);
            camera.transform.localRotation = Quaternion.identity;
            camera.nearClipPlane = .03f; camera.farClipPlane = 1500; camera.fieldOfView = 75;
            var controller = player.AddComponent<FirstPersonController>();
            controller.viewCamera = camera;
            controller.observationPoints = observations;
            controller.spawnPoint = spawn;
            CreateBody(player.transform, cloth, wood, playerLayer, controller);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log("M1_SCENE_CREATED: continuous island, coast/forest/cabin, Shelter layer=" + shelterLayer);
        }

        // Height is deterministic and shared by terrain placement and editor validation.
        public static float Height(float x, float z)
        {
            float r = Mathf.Sqrt(x * x / (60 * 60) + z * z / (50 * 50));
            float shore = Mathf.SmoothStep(-2, .4f, Mathf.InverseLerp(1, .88f, r));
            float inland = Mathf.SmoothStep(0, 1.5f, Mathf.InverseLerp(.88f, .56f, r));
            float hill = 4.1f * Mathf.Exp(-((x - 22) * (x - 22) + (z - 25) * (z - 25)) / 230f);
            float h = shore + inland + hill * Mathf.Clamp01((1 - r) * 6);
            float cabinDistance = Mathf.Max(Mathf.Abs(x) / 7, Mathf.Abs(z - 8) / 6);
            return Mathf.Lerp(1.35f, h, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(1, 1.7f, cabinDistance)));
        }

        static void CreateTerrain(Transform parent, params Material[] materials)
        {
            const int width = 120, depth = 100;
            var vertices = new Vector3[(width + 1) * (depth + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new[] { new List<int>(), new List<int>(), new List<int>() };
            for (int z = 0; z <= depth; z++)
                for (int x = 0; x <= width; x++)
                {
                    int i = z * (width + 1) + x;
                    float px = x - 60, pz = z - 50;
                    vertices[i] = new Vector3(px, Height(px, pz), pz);
                    uv[i] = new Vector2(px / 8, pz / 8);
                }
            for (int z = 0; z < depth; z++)
                for (int x = 0; x < width; x++)
                {
                    int a = z * (width + 1) + x, b = a + width + 1;
                    float h = Height(x - 59.5f, z - 49.5f);
                    var indices = triangles[h < .8f ? 0 : h > 4.3f ? 2 : 1];
                    indices.Add(a); indices.Add(b); indices.Add(a + 1);
                    indices.Add(a + 1); indices.Add(b); indices.Add(b + 1);
                }
            var mesh = new Mesh { name = "Continuous island 120x100m", vertices = vertices, uv = uv, subMeshCount = 3 };
            for (int i = 0; i < 3; i++) mesh.SetTriangles(triangles[i], i);
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, AssetDatabase.GenerateUniqueAssetPath(AssetsRoot + "/IslandTerrain.asset"));
            var terrain = new GameObject("Island terrain — continuous collision mesh");
            terrain.transform.SetParent(parent, false);
            terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
            terrain.AddComponent<MeshRenderer>().sharedMaterials = materials;
            terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
            terrain.isStatic = true;
        }

        static void CreateTrees(Transform parent, Material bark, Material leaves)
        {
            var forest = new GameObject("Forest — 18 trees with trunk collision and crown LOD").transform;
            forest.SetParent(parent, false);
            for (int i = 0; i < 18; i++)
            {
                float x = -28 + (i % 6) * 4.5f + Mathf.Sin(i * 2.3f), z = 10 + (i / 6) * 6 + Mathf.Cos(i * 1.7f);
                float height = 5.5f + (i % 4) * .6f;
                var tree = new GameObject("Tree " + (i + 1)).transform;
                tree.SetParent(forest, false); tree.position = new Vector3(x, Height(x, z), z);
                Block("Trunk", tree, new Vector3(0, height * .35f, 0), new Vector3(.45f, height * .7f, .45f), bark);
                var close = Primitive("Crown near", PrimitiveType.Sphere, tree, new Vector3(0, height * .77f, 0), new Vector3(3.8f, height * .55f, 3.8f), leaves, false);
                var far = Primitive("Crown far", PrimitiveType.Cube, tree, new Vector3(0, height * .77f, 0), new Vector3(3.2f, height * .45f, 3.2f), leaves, false);
                var lod = tree.gameObject.AddComponent<LODGroup>();
                lod.SetLODs(new[] { new LOD(.12f, new[] { close.GetComponent<Renderer>() }), new LOD(.012f, new[] { far.GetComponent<Renderer>() }) });
                lod.RecalculateBounds();
            }
        }

        public const float CabinFloor = 1.45f;
        static void CreateCabin(Transform parent, Material wood, Material stone, Material roof, int shelterLayer)
        {
            var cabin = new GameObject("Cabin 6x5m — south door, west window").transform;
            cabin.SetParent(parent, false); cabin.position = new Vector3(0, CabinFloor, 8);
            Block("Foundation and walkable floor", cabin, new Vector3(0, -.15f, 0), new Vector3(6, .3f, 5), stone);
            Block("North wall", cabin, new Vector3(0, 1.35f, 2.4f), new Vector3(6, 2.7f, .2f), wood);
            Block("East wall", cabin, new Vector3(2.9f, 1.35f, 0), new Vector3(.2f, 2.7f, 4.8f), wood);
            Block("Door left", cabin, new Vector3(-1.85f, 1.35f, -2.4f), new Vector3(2.3f, 2.7f, .2f), wood);
            Block("Door right", cabin, new Vector3(1.85f, 1.35f, -2.4f), new Vector3(2.3f, 2.7f, .2f), wood);
            Block("Door lintel (2.3m clearance)", cabin, new Vector3(0, 2.5f, -2.4f), new Vector3(1.4f, .4f, .2f), wood);
            Block("West wall front", cabin, new Vector3(-2.9f, 1.35f, -1.65f), new Vector3(.2f, 2.7f, 1.5f), wood);
            Block("West wall rear", cabin, new Vector3(-2.9f, 1.35f, 1.65f), new Vector3(.2f, 2.7f, 1.5f), wood);
            Block("Window sill", cabin, new Vector3(-2.9f, .5f, 0), new Vector3(.2f, 1, 1.8f), wood);
            Block("Window lintel", cabin, new Vector3(-2.9f, 2.5f, 0), new Vector3(.2f, .4f, 1.8f), wood);
            var shelter = Block("Roof — shared Shelter collider, 0.5m eaves", cabin, new Vector3(0, 2.85f, 0), new Vector3(7, .3f, 6), roof);
            shelter.layer = shelterLayer;
        }

        static void CreateMaterialTests(Transform parent, Material[] materials)
        {
            var tests = new GameObject("Material tests — wood stone soil cloth").transform;
            tests.SetParent(parent, false);
            for (int i = 0; i < materials.Length; i++)
            {
                float x = 9 + i * 2, z = 3, y = Height(x, z);
                Block(materials[i].name + " horizontal", tests, new Vector3(x, y + .08f, z), new Vector3(1.5f, .12f, 1.5f), materials[i]);
                Block(materials[i].name + " vertical", tests, new Vector3(x, y + .8f, z + .85f), new Vector3(1.5f, 1.6f, .12f), materials[i]);
            }
        }

        [MenuItem("Sunset Glow/M1/Refresh first person body")]
        public static void RefreshBody()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeBuild.ScenePath, OpenSceneMode.Single);
            var sample = GameObject.Find("M1SampleRoot");
            if (sample == null) throw new InvalidOperationException("M1 sample root is missing.");
            var controller = sample.GetComponentInChildren<FirstPersonController>();
            if (controller == null) throw new InvalidOperationException("M1 first person controller is missing.");
            var clothes = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/Cloth.mat");
            var skin = AssetDatabase.LoadAssetAtPath<Material>(AssetsRoot + "/Wood.mat");
            if (clothes == null || skin == null) throw new InvalidOperationException("M1 body materials are missing.");
            var oldBody = controller.transform.Find("First person visible body");
            if (oldBody == null || oldBody.GetComponent<FirstPersonBody>() == null)
                throw new InvalidOperationException("Generated M1 body is missing; refusing to replace another object.");
            UnityEngine.Object.DestroyImmediate(oldBody.gameObject);
            CreateBody(controller.transform, clothes, skin, controller.gameObject.layer, controller);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("M1_BODY_REFRESHED");
        }

        static void CreateBody(Transform player, Material clothes, Material skin, int layer, FirstPersonController controller)
        {
            var referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReferenceCharacterSetup.PrefabPath);
            if (referencePrefab != null)
            {
                var characterObject = (GameObject)PrefabUtility.InstantiatePrefab(referencePrefab, player);
                characterObject.transform.localPosition = Vector3.zero;
                characterObject.transform.localRotation = Quaternion.identity;
                characterObject.GetComponent<ReferenceCharacter>().player = controller;
                controller.eyeHeight = 1.425f; controller.eyeForward = .16f; controller.capsuleHeight = 1.65f;
                return;
            }
            var body = new GameObject("First person visible body").transform;
            body.SetParent(player, false);
            Primitive("Torso", PrimitiveType.Cube, body, new Vector3(0, 1.08f, -.14f), new Vector3(.36f, .60f, .16f), clothes, false);
            var leftLeg = Primitive("Left leg", PrimitiveType.Cube, body, new Vector3(-.10f, .46f, -.03f), new Vector3(.13f, .70f, .12f), clothes, false);
            var rightLeg = Primitive("Right leg", PrimitiveType.Cube, body, new Vector3(.10f, .46f, -.03f), new Vector3(.13f, .70f, .12f), clothes, false);
            var leftHand = Primitive("Left hand", PrimitiveType.Cube, body, new Vector3(-.16f, 1.02f, .14f), new Vector3(.10f, .24f, .10f), skin, false);
            var rightHand = Primitive("Right hand", PrimitiveType.Cube, body, new Vector3(.16f, 1.02f, .14f), new Vector3(.10f, .24f, .10f), skin, false);
            // Rounded feet preserve the requested dimensions inside the 0.3m controller radius.
            Primitive("Left foot", PrimitiveType.Sphere, body, new Vector3(-.10f, .09f, .08f), new Vector3(.14f, .12f, .17f), skin, false);
            Primitive("Right foot", PrimitiveType.Sphere, body, new Vector3(.10f, .09f, .08f), new Vector3(.14f, .12f, .17f), skin, false);
            var head = Primitive("Head shadow only", PrimitiveType.Sphere, body, new Vector3(0, 1.61f, 0), new Vector3(.28f, .34f, .28f), skin, false);
            head.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            foreach (Transform part in body) part.gameObject.layer = layer;
            body.gameObject.layer = layer;
            var animation = body.gameObject.AddComponent<FirstPersonBody>();
            animation.player = controller;
            animation.leftHand = leftHand.transform; animation.rightHand = rightHand.transform;
            animation.leftLeg = leftLeg.transform; animation.rightLeg = rightLeg.transform;
        }

        static Transform Point(Transform parent, string name, float x, float z, float yaw, float? floor = null)
        {
            var point = new GameObject(name).transform; point.SetParent(parent, false);
            point.SetPositionAndRotation(new Vector3(x, floor ?? Height(x, z) + .04f, z), Quaternion.Euler(0, yaw, 0));
            return point;
        }

        static Material Material(string name, Color color, float smoothness = .08f)
        {
            Shader shader = Shader.Find("HDRP/Lit");
            if (shader == null) throw new InvalidOperationException("HDRP/Lit shader is missing.");
            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            AssetDatabase.CreateAsset(material, AssetDatabase.GenerateUniqueAssetPath(AssetsRoot + "/" + name + ".mat"));
            return material;
        }

        static GameObject Block(string name, Transform parent, Vector3 localPosition, Vector3 scale, Material material)
            => Primitive(name, PrimitiveType.Cube, parent, localPosition, scale, material, true);

        static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 scale, Material material, bool collision)
        {
            var go = GameObject.CreatePrimitive(type); go.name = name;
            go.transform.SetParent(parent, false); go.transform.localPosition = localPosition; go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collision) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static int EnsureLayer(string name)
        {
            int existing = LayerMask.NameToLayer(name);
            if (existing >= 0) return existing;
            var tags = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tags.FindProperty("layers");
            for (int i = 8; i < 32; i++)
            {
                var layer = layers.GetArrayElementAtIndex(i);
                if (!string.IsNullOrEmpty(layer.stringValue)) continue;
                layer.stringValue = name; tags.ApplyModifiedPropertiesWithoutUndo(); return i;
            }
            throw new InvalidOperationException("No available user layer for " + name);
        }
    }
}
