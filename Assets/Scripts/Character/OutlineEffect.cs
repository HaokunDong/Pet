using System.Collections;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Controls the outline highlight effect on a character's sprite.
    /// Uses MaterialPropertyBlock to avoid creating material instances.
    /// Provides hover outline and bold-pulse feedback for manual control mode.
    /// All configurable parameters are read from the global OutlineSettings asset.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class OutlineEffect : MonoBehaviour
    {

        private SpriteRenderer spriteRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Coroutine boldCoroutine;

        private static readonly int OutlineEnabledId = Shader.PropertyToID("_OutlineEnabled");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineThicknessId = Shader.PropertyToID("_OutlineThickness");

        /// <summary>
        /// Whether the outline is currently visible.
        /// </summary>
        public bool IsOutlineActive { get; private set; }

        /// <summary>
        /// Whether a bold pulse is currently playing.
        /// </summary>
        public bool IsBoldPulsing { get; private set; }

        /// <summary>
        /// Whether the outline should remain visible after a bold pulse ends.
        /// Set by the caller (ManualController) to indicate if the mouse is still hovering.
        /// </summary>
        public bool ShouldRemainAfterPulse { get; set; }

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            propertyBlock = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Set the outline on or off with a specific thickness.
        /// </summary>
        /// <param name="enabled">Whether to show the outline.</param>
        /// <param name="thickness">Outline thickness in pixels. If 0, uses hoverThickness.</param>
        public void SetOutline(bool enabled, float thickness = 0f)
        {
            if (spriteRenderer == null || propertyBlock == null) return;

            IsOutlineActive = enabled;

            if (thickness <= 0f) thickness = OutlineSettings.Instance.hoverThickness;

            spriteRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetFloat(OutlineEnabledId, enabled ? 1f : 0f);
            propertyBlock.SetColor(OutlineColorId, OutlineSettings.Instance.outlineColor);
            propertyBlock.SetFloat(OutlineThicknessId, enabled ? thickness : 0f);
            spriteRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Trigger a bold pulse: immediately set outline to bold thickness,
        /// then after boldDuration, restore to hover thickness (if ShouldRemainAfterPulse)
        /// or turn off outline.
        /// </summary>
        public void TriggerBoldPulse()
        {
            // Interrupt any existing bold pulse
            if (boldCoroutine != null)
            {
                StopCoroutine(boldCoroutine);
                boldCoroutine = null;
            }

            boldCoroutine = StartCoroutine(BoldPulseRoutine());
        }

        /// <summary>
        /// Immediately reset all outline state. Called on death, recycle, or mode deactivation.
        /// </summary>
        public void ResetOutline()
        {
            if (boldCoroutine != null)
            {
                StopCoroutine(boldCoroutine);
                boldCoroutine = null;
            }

            IsBoldPulsing = false;
            IsOutlineActive = false;
            ShouldRemainAfterPulse = false;

            if (spriteRenderer != null && propertyBlock != null)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(OutlineEnabledId, 0f);
                propertyBlock.SetFloat(OutlineThicknessId, 0f);
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }

        private IEnumerator BoldPulseRoutine()
        {
            IsBoldPulsing = true;

            OutlineSettings settings = OutlineSettings.Instance;

            // Set bold thickness immediately
            SetOutline(true, settings.boldThickness);

            // Wait for bold duration
            float elapsed = 0f;
            while (elapsed < settings.boldDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            IsBoldPulsing = false;
            boldCoroutine = null;

            // After pulse ends, check if outline should remain (mouse still hovering)
            if (ShouldRemainAfterPulse)
            {
                SetOutline(true, settings.hoverThickness);
            }
            else
            {
                SetOutline(false);
            }
        }

        private void OnDisable()
        {
            if (boldCoroutine != null)
            {
                StopCoroutine(boldCoroutine);
                boldCoroutine = null;
            }

            IsBoldPulsing = false;
            IsOutlineActive = false;

            if (spriteRenderer != null && propertyBlock != null)
            {
                spriteRenderer.GetPropertyBlock(propertyBlock);
                propertyBlock.SetFloat(OutlineEnabledId, 0f);
                propertyBlock.SetFloat(OutlineThicknessId, 0f);
                spriteRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
