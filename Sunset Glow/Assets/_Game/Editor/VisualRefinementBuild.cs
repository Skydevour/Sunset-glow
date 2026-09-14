using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using SunsetGlow.EnvironmentArt;

namespace SunsetGlow.Editor
{
    public static class VisualRefinementBuild
    {
        public static void BuildEnvironmentPreview()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeBuild.ScenePath, OpenSceneMode.Single);
            CoastalLandArt.Apply();
            InteractionBuild.Apply();
            ApplyCloudArt();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CoastalBuild.Build();
        }
        [MenuItem("Sunset Glow/Refinement/Build detailed environment and character")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(PrototypeBuild.ScenePath, OpenSceneMode.Single);
            CoastalLandArt.Apply();
            InteractionBuild.Apply();
            ApplyCloudArt();
            ReferenceCharacterSetup.Apply();
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            CoastalBuild.Build();
        }

        static void ApplyCloudArt()
        {
            var consumer = Object.FindFirstObjectByType<WindEnvironmentConsumer>();
            consumer.cloudBaseAltitude=1400; consumer.cloudLayerDepth=1100; consumer.cloudShapeScale=9;
            EditorUtility.SetDirty(consumer);
            var presenter=Object.FindFirstObjectByType<CoastalEnvironmentPresenter>();
            float[] density={.48f,.46f,.55f},shape={.875f,.89f,.81f},erosion={.82f,.8f,.72f};
            for(int i=0;i<3;i++)
            {
                presenter.looks[i].cloudDensity=density[i];presenter.looks[i].cloudShape=shape[i];presenter.looks[i].cloudErosion=erosion[i];
                EditorUtility.SetDirty(presenter.looks[i]);
            }
        }
    }
}
