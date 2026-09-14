using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SunsetGlow.EnvironmentArt;
using SunsetGlow.Player;
using SunsetGlow.Debugging;

namespace SunsetGlow.Editor
{
    public static class InteractionBuild
    {
        [MenuItem("Sunset Glow/Interaction/Configure D1 and build")]
        public static void ConfigureAndBuild()
        {
            const string backup="Assets/_Game/Scenes/CoastalBeforeInteraction.unity";
            if(!File.Exists(backup))AssetDatabase.CopyAsset(PrototypeBuild.ScenePath,backup);
            var scene=EditorSceneManager.OpenScene(PrototypeBuild.ScenePath,OpenSceneMode.Single);
            Apply();
            EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            CoastalBuild.Build();
        }

        // Can be called again after rebuilding the coastal art without replacing a future character prefab.
        public static void Apply()
        {
            var player=Object.FindFirstObjectByType<FirstPersonController>();
            var presenter=Object.FindFirstObjectByType<CoastalEnvironmentPresenter>();
            var water=WaterInteractionSetup.Apply(player.transform);
            player.water=water;
            var wind=Ensure<WindField>(presenter.gameObject);wind.player=player;
            presenter.wind=wind;
            var consumer=Ensure<WindEnvironmentConsumer>(presenter.gameObject);consumer.field=wind;consumer.presenter=presenter;
            var surfaces=Ensure<SurfaceQuery>(presenter.gameObject);surfaces.environment=presenter;surfaces.water=water;
            var feet=Ensure<FootContactEmitter>(player.gameObject);feet.player=player;feet.surfaces=surfaces;
            var feedback=Ensure<FootContactWaterFeedback>(player.gameObject);feedback.player=player;feedback.water=water;feedback.feet=feet;
            var shelf=GameObject.Find("Coastal shelf — submerged sand");
            if(shelf!=null)Ensure<MeshCollider>(shelf).sharedMesh=shelf.GetComponent<MeshFilter>().sharedMesh;
            foreach(var collider in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if(collider.gameObject.layer==LayerMask.NameToLayer("Player"))continue;
                var definition=Ensure<SurfaceDefinition>(collider.gameObject);
                bool terrain=collider.name.StartsWith("Island terrain")||collider.gameObject==shelf;
                definition.coastalTerrain=terrain;definition.receivesSnow=terrain||collider.gameObject.layer==LayerMask.NameToLayer("Shelter");
                definition.substrate=terrain?SurfaceKind.DrySand:collider.transform.parent!=null&&collider.transform.parent.name.StartsWith("Cabin")&&!collider.name.StartsWith("Foundation")?SurfaceKind.Wood:SurfaceKind.Stone;
                EditorUtility.SetDirty(definition);
            }
            var validation=Ensure<InteractionValidationRunner>(presenter.gameObject);
            validation.player=player;validation.wind=wind;validation.water=water;validation.surfaces=surfaces;validation.feet=feet;validation.presenter=presenter;
            foreach(var component in new Component[]{player,presenter,wind,consumer,surfaces,feet,feedback,validation})EditorUtility.SetDirty(component);
        }
        static T Ensure<T>(GameObject go)where T:Component {var c=go.GetComponent<T>();return c!=null?c:go.AddComponent<T>();}
    }
}
