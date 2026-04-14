using System.Collections;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Controls the flash highlight effect on a character's sprite.
    /// Uses MaterialPropertyBlock to avoid creating material instances.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class FlashEffect : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Duration of the flash effect in seconds")]
        [Range(0.05f, 0.5f)]
        public float flashDuration = 0.15f;

        [Tooltip("Flash color (default white)")]
        public Color flashColor = Color.white;

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Coroutine flashCoroutine;
        private static readonly int FlashAmountId = Shader.PropertyToID("_FlashAmount");
        private static readonly int FlashColorId = Shader.PropertyToID("_FlashColor");

        /// <summary>
        /// Whether a flash effect is currently playing.
        /// </summary>
        public bool IsFlashing { get; private set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Trigger the flash effect. Ignored if already flashing.
        /// </summary>
        /// <returns>True if flash was triggered, false if already flashing.</returns>
        public bool TriggerFlash()
        {
            if (IsFlashing) return false;

            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);

            flashCoroutine = StartCoroutine(FlashRoutine());
            return true;
        }

        private IEnumerator FlashRoutine()
        {
            IsFlashing = true;

            // Set flash color
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(FlashColorId, flashColor);

            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / flashDuration);

                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(FlashAmountId, t);
                spriteRenderer.SetPropertyBlock(propertyBlock);

                yield return null;
            }

            // Ensure flash is fully off
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(FlashAmountId, 0f);
            spriteRenderer.SetPropertyBlock(propertyBlock);

            IsFlashing = false;
            flashCoroutine = null;
        }

        private void OnDisable()
        {
            // Reset flash state when disabled
            if (flashCoroutine != null)
            {
                StopCoroutine(flashCoroutine);
                flashCoroutine = null;
            }

            IsFlashing = false;

            if (spriteRenderer != null && propertyBlock != null)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(FlashAmountId, 0f);
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
