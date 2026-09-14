using UnityEngine;
namespace SunsetGlow.EnvironmentArt
{
    public sealed class SurfaceDefinition : MonoBehaviour
    {
        public SurfaceKind substrate=SurfaceKind.Grass;
        public bool coastalTerrain;
        public bool receivesSnow;
    }
}
