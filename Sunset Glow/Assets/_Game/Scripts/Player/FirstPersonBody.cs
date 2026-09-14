using UnityEngine;

namespace SunsetGlow.Player
{
    // Body animation is cosmetic. It never moves the controller or parents the camera.
    [DisallowMultipleComponent]
    public sealed class FirstPersonBody : MonoBehaviour
    {
        public FirstPersonController player;
        public Transform leftHand;
        public Transform rightHand;
        public Transform leftLeg;
        public Transform rightLeg;
        private Quaternion leftHandRest;
        private Quaternion rightHandRest;
        private Quaternion leftLegRest;
        private Quaternion rightLegRest;
        private float phase;
        private float amplitude;

        private void Start()
        {
            if (player == null) player = GetComponentInParent<FirstPersonController>();
            leftHandRest = leftHand != null ? leftHand.localRotation : Quaternion.identity;
            rightHandRest = rightHand != null ? rightHand.localRotation : Quaternion.identity;
            leftLegRest = leftLeg != null ? leftLeg.localRotation : Quaternion.identity;
            rightLegRest = rightLeg != null ? rightLeg.localRotation : Quaternion.identity;
        }

        private void LateUpdate()
        {
            if (player == null || player.Paused) return;
            Vector3 velocity = player.Velocity;
            float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.z * velocity.z);
            float target = Mathf.Clamp01(speed / 4f);
            amplitude = Mathf.MoveTowards(amplitude, target, Time.deltaTime * 6f);
            phase = Mathf.Repeat(phase + speed * Time.deltaTime * 2.5f, Mathf.PI * 2f);
            float swing = Mathf.Sin(phase) * amplitude;
            Apply(leftHand, leftHandRest, swing * 7f);
            Apply(rightHand, rightHandRest, -swing * 7f);
            Apply(leftLeg, leftLegRest, -swing * 5f);
            Apply(rightLeg, rightLegRest, swing * 5f);
        }

        private static void Apply(Transform limb, Quaternion rest, float angle)
        {
            if (limb != null) limb.localRotation = rest * Quaternion.Euler(angle, 0f, 0f);
        }
    }
}
