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
        [Tooltip("Total duration of the flash effect in seconds (hold + fade)")]
        [Range(0.05f, 1f)]
        public float flashDuration = 0.35f;

        [Tooltip("Duration the flash stays at full intensity before fading")]
        [Range(0f, 0.5f)]
        public float flashHoldDuration = 0.1f;

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
        /// Trigger the flash effect. If already flashing, interrupts and restarts.
        /// </summary>
        /// <returns>True if flash was triggered.</returns>
        public bool TriggerFlash()
        {
            if (flashCoroutine != null)
                StopCoroutine(flashCoroutine);

            flashCoroutine = StartCoroutine(FlashRoutine());
            return true;
        }

        /// <summary>
        /// Immediately stop the flash effect and reset to normal color.
        /// Called when the character dies or is recycled.
        /// </summary>
        public void ResetFlash()
        {
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

        private IEnumerator FlashRoutine()
        {
            IsFlashing = true;

            // Set flash color
            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(FlashColorId, flashColor);

            // Phase 1: Hold at full intensity
            float holdTime = Mathf.Min(flashHoldDuration, flashDuration);
            if (holdTime > 0f)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(FlashAmountId, 1f);
                spriteRenderer.SetPropertyBlock(propertyBlock);

                float holdElapsed = 0f;
                while (holdElapsed < holdTime)
                {
                    holdElapsed += Time.deltaTime;
                    yield return null;
                }
            }

            // Phase 2: Fade out from full intensity to zero
            float fadeDuration = flashDuration - holdTime;
            float elapsed = 0f;
            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;
                float t = 1f - Mathf.Clamp01(elapsed / fadeDuration);

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
