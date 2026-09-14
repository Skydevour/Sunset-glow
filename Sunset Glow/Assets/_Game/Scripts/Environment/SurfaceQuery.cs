using UnityEngine;

namespace SunsetGlow.EnvironmentArt
{
    public enum SurfaceKind { Grass, DrySand, WetSand, Snow, Wood, Stone, Water }
    public struct SurfaceContact
    {
        public SurfaceKind kind, substrate;
        public Vector3 point, normal;
        public float wetness, snowCoverage, waterDepth;
        public Collider collider;
    }

    // Shared by terrain authoring and contact classification. Never infer snow from particles.
    public static class SurfaceCoverage
    {
        public static float TerrainSnow(Vector3 point, float slope)
        {
            float broad = Mathf.PerlinNoise(point.x * .25f + 47, point.z * .25f + 31);
            float cover = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.32f, 1.2f, point.y + (broad - .5f) * .5f));
            return cover * (1 - Mathf.Clamp01(slope - .35f) * .6f);
        }
    }

    public sealed class SurfaceQuery : MonoBehaviour
    {
        public CoastalEnvironmentPresenter environment;
        public WaterContactSystem water;
        [Range(0,1)] public float snowThreshold = .35f;
        int groundMask;
        void Awake() { groundMask = ~LayerMask.GetMask("Player"); }

        public bool TryContact(Vector3 foot, out SurfaceContact contact)
        {
            contact = default;
            if (!Physics.Raycast(foot + Vector3.up * .4f, Vector3.down, out var hit, .9f, groundMask, QueryTriggerInteraction.Ignore)) return false;
            if (hit.normal.y < .64f) return false;
            var definition = hit.collider.GetComponent<SurfaceDefinition>();
            contact.point=hit.point; contact.normal=hit.normal; contact.collider=hit.collider;
            contact.substrate=definition!=null ? definition.substrate : SurfaceKind.Stone;
            bool sheltered=Physics.Raycast(hit.point+Vector3.up*.05f,Vector3.up,12,LayerMask.GetMask("Shelter"),QueryTriggerInteraction.Ignore);
            if(definition!=null && definition.coastalTerrain)
            {
                contact.substrate=hit.point.y<.8f ? SurfaceKind.DrySand : SurfaceKind.Grass;
                contact.wetness=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.02f,.52f,hit.point.y));
                float slope=new Vector2(hit.normal.x,hit.normal.z).magnitude/Mathf.Max(.01f,hit.normal.y);
                if(!sheltered && environment!=null)contact.snowCoverage=environment.SnowCoverage*SurfaceCoverage.TerrainSnow(hit.point,slope);
            }
            else if(definition!=null && definition.receivesSnow && !sheltered && environment!=null)contact.snowCoverage=environment.SnowCoverage;
            if(water!=null && water.TrySample(hit.point,out float level))contact.waterDepth=Mathf.Max(0,level-hit.point.y);
            contact.kind=Classify(contact.substrate,contact.waterDepth,contact.snowCoverage,contact.wetness,snowThreshold);
            return true;
        }

        public static SurfaceKind Classify(SurfaceKind substrate,float waterDepth,float snow,float wetness,float threshold=.35f)
        {
            if(waterDepth>.045f)return SurfaceKind.Water;
            if(snow>=threshold)return SurfaceKind.Snow;
            if(substrate==SurfaceKind.DrySand && wetness>.25f)return SurfaceKind.WetSand;
            return substrate;
        }
    }
}
