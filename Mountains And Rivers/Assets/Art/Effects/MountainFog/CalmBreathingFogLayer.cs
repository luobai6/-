using UnityEngine;

namespace ShanheWeijin.Visuals
{
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Renderer))]
    public sealed class CalmBreathingFogLayer : MonoBehaviour
    {
        private static readonly int LayerPhaseId = Shader.PropertyToID("_LayerPhase");
        private static readonly int SpeedMultiplierId = Shader.PropertyToID("_SpeedMultiplier");
        private static readonly int OpacityMultiplierId = Shader.PropertyToID("_OpacityMultiplier");

        [SerializeField]
        [Tooltip("Renderer that uses the CalmBreathingFog shader. Defaults to this GameObject's Renderer.")]
        private Renderer targetRenderer;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Offsets the noise and breathing cycle. Give each fog layer a different value.")]
        private float phase = 0.17f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Per-layer animation speed. Recommended range: 0.75 to 1.15.")]
        private float speedMultiplier = 1f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Per-layer opacity multiplier. Keep the combined fog subtle.")]
        private float opacityMultiplier = 1f;

        private MaterialPropertyBlock propertyBlock;

        private void Reset()
        {
            targetRenderer = GetComponent<Renderer>();
            ApplyProperties();
        }

        private void OnEnable()
        {
            CacheRenderer();
            ApplyProperties();
        }

        private void OnValidate()
        {
            speedMultiplier = Mathf.Max(0.01f, speedMultiplier);
            CacheRenderer();
            ApplyProperties();
        }

        private void Update()
        {
            if (!Application.isPlaying)
            {
                ApplyProperties();
            }
        }

        public void ApplyProperties()
        {
            if (!CacheRenderer())
            {
                return;
            }

            propertyBlock ??= new MaterialPropertyBlock();
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(LayerPhaseId, phase);
            propertyBlock.SetFloat(SpeedMultiplierId, speedMultiplier);
            propertyBlock.SetFloat(OpacityMultiplierId, opacityMultiplier);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }

        private bool CacheRenderer()
        {
            if (targetRenderer == null)
            {
                targetRenderer = GetComponent<Renderer>();
            }

            return targetRenderer != null;
        }
    }
}
