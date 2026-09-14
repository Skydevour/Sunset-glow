using UnityEngine;
using UnityEngine.Rendering;

namespace SunsetGlow.Player
{
    /// <summary>Presentation only: the existing controller remains the sole movement authority.</summary>
    public sealed class ReferenceCharacter : MonoBehaviour
    {
        public FirstPersonController player;
        public Animator animator;
        public Renderer[] headRenderers;
        public bool Showcase { get; private set; }
        static readonly int Speed = Animator.StringToHash("Speed");
        Transform cameraTransform;
        Vector3 cameraRest;
        Quaternion cameraRotation;
        float orbit;

        void Start()
        {
            if (player == null) player = GetComponentInParent<FirstPersonController>();
            if (animator == null) animator = GetComponentInChildren<Animator>();
            cameraTransform = player.viewCamera.transform;
            cameraRest = cameraTransform.localPosition;
            cameraRotation = cameraTransform.localRotation;
            SetHeadVisible(false);
        }

        void Update()
        {
            if (animator != null)
            {
                animator.speed = player.Paused ? 0 : 1;
                float speed = new Vector2(player.Velocity.x, player.Velocity.z).magnitude;
                animator.SetFloat(Speed, speed, .12f, player.Paused ? 0 : Time.deltaTime);
            }
            if (!player.CaptureMode && Input.GetKeyDown(KeyCode.V)) SetShowcase(!Showcase);
            if (!Showcase || player.CaptureMode) return;
            if (Input.GetKeyDown(KeyCode.F10)) Application.Quit();
            if (Input.GetKeyDown(KeyCode.Escape)) { SetShowcase(false); player.SetPaused(true); return; }
            if (Input.GetKey(KeyCode.LeftArrow)) orbit -= Time.unscaledDeltaTime * 45;
            if (Input.GetKey(KeyCode.RightArrow)) orbit += Time.unscaledDeltaTime * 45;
            SetView(orbit, 2.8f, 1.05f);
        }

        public void SetShowcase(bool enabled)
        {
            if (enabled && !Showcase) { cameraRest = cameraTransform.localPosition; cameraRotation = cameraTransform.localRotation; }
            Showcase = enabled;
            SetHeadVisible(enabled);
            if (!player.CaptureMode) player.enabled = !enabled;
            if (enabled) { orbit = 0; SetView(0, 2.8f, 1.05f); }
            else { cameraTransform.localPosition = cameraRest; cameraTransform.localRotation = cameraRotation; if (!player.CaptureMode) player.SetPaused(player.Paused); }
        }

        public void SetView(float angle, float distance, float targetHeight)
        {
            Vector3 target = transform.position + Vector3.up * targetHeight;
            Vector3 offset = Quaternion.Euler(0, angle, 0) * transform.forward * distance;
            cameraTransform.position = target + offset + Vector3.up * .06f;
            cameraTransform.rotation = Quaternion.LookRotation(target - cameraTransform.position, Vector3.up);
        }

        void SetHeadVisible(bool visible)
        {
            if (headRenderers == null) return;
            foreach (var renderer in headRenderers)
                if (renderer != null) renderer.shadowCastingMode = visible ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
        }
    }
}
