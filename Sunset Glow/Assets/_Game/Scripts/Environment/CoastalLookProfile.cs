using UnityEngine;

namespace SunsetGlow.EnvironmentArt
{
    // Art-direction preview data. This is not a second world clock or a weather simulation.
    [CreateAssetMenu(menuName = "Sunset Glow/Coastal look")]
    public sealed class CoastalLookProfile : ScriptableObject
    {
        public string displayName;
        public float sunElevation = 40;
        public float sunAzimuth = 270;
        public float sunLux = 85000;
        public float sunTemperature = 5700;
        public float exposureEV = 13.2f;
        public float cloudDensity = .32f;
        public float cloudShape = .85f;
        public float cloudErosion = .75f;
        public float aerosolDensity = .008f;
        public float fogDistance = 2400;
        public Color horizonTint = Color.white;
        public Color zenithTint = Color.white;
        [Range(0, 1)] public float snowCoverage;
        public float snowfallRate;
        public float windSpeed = 18;
    }
}
