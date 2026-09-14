using System.Collections.Generic;
using UnityEngine;

namespace SunsetGlow.EnvironmentArt
{
    /// <summary>Bounded, allocation-free deformation using the shared presentation wind.
    /// Original meshes/colliders are never modified. Root weights pin grass and trunks to the ground.</summary>
    [DisallowMultipleComponent]
    public sealed class VegetationMeshWind : MonoBehaviour
    {
        public bool grass;
        public float height = 6;
        [Min(5)] public float updateRate = 24;
        [Min(10)] public float animationDistance = 48;
        const int VertexBudget = 180000;
        static int budgetFrame = -1;
        static readonly List<VegetationMeshWind> instances = new List<VegetationMeshWind>(128);
        static readonly List<VegetationMeshWind> candidates = new List<VegetationMeshWind>(128);
        static readonly System.Comparison<VegetationMeshWind> comparePriority = (a,b) => a.priority.CompareTo(b.priority);
        float priority, distanceSquared;
        WindField wind;
        Camera observer;
        Mesh runtimeMesh;
        MeshRenderer meshRenderer;
        Vector3[] rest, deformed;
        float[] weights, phases, tipWeights;
        double nextUpdate;
        bool hasBend;
        Vector3 samplePosition;
        public int UpdateCount { get; private set; }
        public float LastWindStrength { get; private set; }
        public int VertexCount => rest == null ? 0 : rest.Length;

        void Start()
        {
            wind = FindFirstObjectByType<WindField>();
            observer = Camera.main;
            meshRenderer = GetComponent<MeshRenderer>();
            var filter = GetComponent<MeshFilter>();
            if (!filter || !filter.sharedMesh) { enabled = false; return; }
            runtimeMesh = Instantiate(filter.sharedMesh);
            runtimeMesh.name = filter.sharedMesh.name + " (wind instance)";
            runtimeMesh.MarkDynamic();
            filter.sharedMesh = runtimeMesh;
            rest = runtimeMesh.vertices;
            deformed = new Vector3[rest.Length];
            weights = new float[rest.Length];
            phases = new float[rest.Length];
            tipWeights = new float[rest.Length];
            var colors = runtimeMesh.colors;
            for (int i = 0; i < rest.Length; i++)
            {
                float t = grass && colors.Length == rest.Length ? colors[i].r : Mathf.Clamp01(rest[i].y / Mathf.Max(1, height));
                weights[i] = t * t;
                tipWeights[i] = grass ? weights[i] : Mathf.Clamp01(new Vector2(rest[i].x, rest[i].z).magnitude / 2.3f) * t;
                phases[i] = Mathf.Sin(rest[i].x * .63f + rest[i].z * .47f + rest[i].y * 1.3f);
            }
            var bounds = runtimeMesh.bounds;
            bounds.Expand(.8f);
            runtimeMesh.bounds = bounds;
            samplePosition = grass ? transform.TransformPoint(runtimeMesh.bounds.center) : transform.position;
            instances.Add(this);
            nextUpdate = Mathf.Repeat(samplePosition.x * .37f + samplePosition.z * .19f, 1f) / updateRate;
        }

        void LateUpdate()
        {
            if (budgetFrame == Time.frameCount) return;
            budgetFrame = Time.frameCount;
            candidates.Clear();
            for (int i = 0; i < instances.Count; i++)
            {
                var item = instances[i];
                if (!item || !item.isActiveAndEnabled || !item.wind || item.wind.IsPaused || !item.runtimeMesh || !item.meshRenderer.isVisible) continue;
                double time = item.wind.PresentationTime;
                if (time < item.nextUpdate) continue;
                item.distanceSquared = item.observer ? item.meshRenderer.bounds.SqrDistance(item.observer.transform.position) : 0;
                if (item.distanceSquared > item.animationDistance * item.animationDistance) continue;
                // Nearest meshes lead; overdue work ages into the queue so component order cannot starve a tree.
                item.priority = item.distanceSquared - (float)(time - item.nextUpdate) * 1200;
                candidates.Add(item);
            }
            candidates.Sort(comparePriority);
            int usedVertices = 0;
            for (int i = 0; i < candidates.Count; i++)
            {
                var item = candidates[i];
                if (usedVertices + item.rest.Length > VertexBudget) continue;
                usedVertices += item.rest.Length;
                item.Deform();
            }
        }

        void Deform()
        {
            double time = wind.PresentationTime;
            float rate = distanceSquared < 144 ? updateRate : distanceSquared < 784 ? Mathf.Min(12,updateRate) : Mathf.Min(6,updateRate);
            nextUpdate = time + 1.0 / rate;
            Vector3 velocity = wind.SampleVelocity(samplePosition);
            float strength = Mathf.Clamp01(velocity.magnitude / 9f);
            if (strength < .0001f && !hasBend) return;
            Vector3 direction = transform.InverseTransformDirection(velocity.normalized);
            Vector3 cross = new Vector3(-direction.z, 0, direction.x);
            float phase = (float)(time % 100000) * (grass ? 2.9f : 1.1f) + samplePosition.x * .13f + samplePosition.z * .21f;
            float sway = Mathf.Sin(phase), flutter = Mathf.Sin(phase * 2.13f + .8f);
            float amplitude = (grass ? .16f : .22f) * strength;
            for (int i = 0; i < rest.Length; i++)
            {
                float w = weights[i];
                float bend = amplitude * w * (.72f + .23f * sway + .15f * phases[i] * flutter);
                deformed[i] = rest[i] + direction * bend + cross * (amplitude * tipWeights[i] * .22f * flutter * phases[i]);
                // A small downward arc conserves the visual length of bending stems.
                deformed[i].y -= bend * bend * (grass ? .7f : .08f);
            }
            runtimeMesh.SetVertices(deformed, 0, deformed.Length, UnityEngine.Rendering.MeshUpdateFlags.DontRecalculateBounds);
            LastWindStrength = strength;
            UpdateCount++;
            hasBend = strength > .0001f;
        }

        void OnDestroy()
        {
            instances.Remove(this);
            if (runtimeMesh) Destroy(runtimeMesh);
        }
    }
}
