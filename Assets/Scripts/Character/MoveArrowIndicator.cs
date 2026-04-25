using System.Collections;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// A downward-pointing arrow indicator that spawns at a target position,
    /// plays a scale-in + fade-out animation, then self-destructs.
    /// Used to provide visual feedback for move-to-point commands.
    /// </summary>
    public class MoveArrowIndicator : MonoBehaviour
    {
        [Header("Arrow Settings")]
        [Tooltip("Arrow color")]
        public Color arrowColor = new Color(1f, 1f, 0f, 0.9f); // Yellow

        [Tooltip("Arrow overall scale")]
        public float arrowScale = 0.5f;

        [Tooltip("Total animation duration in seconds")]
        [Range(0.1f, 2f)]
        public float animationDuration = 0.5f;

        private SpriteRenderer spriteRenderer;

        /// <summary>
        /// Spawn a move arrow indicator at the given world position.
        /// Returns the MoveArrowIndicator component for tracking.
        /// </summary>
        public static MoveArrowIndicator Spawn(Vector3 worldPos, Color? color = null, float scale = 0.5f, float duration = 0.5f)
        {
            GameObject arrowObj = new GameObject("MoveArrowIndicator");
            arrowObj.transform.position = worldPos;

            MoveArrowIndicator indicator = arrowObj.AddComponent<MoveArrowIndicator>();
            if (color.HasValue) indicator.arrowColor = color.Value;
            indicator.arrowScale = scale;
            indicator.animationDuration = duration;

            return indicator;
        }

        private void Start()
        {
            CreateArrowVisual();
            StartCoroutine(AnimateAndDestroy());
        }

        /// <summary>
        /// Create the arrow visual using a child SpriteRenderer with a dynamically generated arrow texture.
        /// </summary>
        private void CreateArrowVisual()
        {
            // Create a simple downward arrow using a mesh rendered as a sprite
            GameObject visualObj = new GameObject("ArrowVisual");
            visualObj.transform.SetParent(transform, false);

            spriteRenderer = visualObj.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateArrowSprite();
            spriteRenderer.color = arrowColor;
            spriteRenderer.sortingOrder = 100; // Render on top

            // Set initial scale
            transform.localScale = Vector3.zero;
        }

        /// <summary>
        /// Create a simple downward-pointing arrow sprite procedurally.
        /// </summary>
        private Sprite CreateArrowSprite()
        {
            int size = 32;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;

            // Clear to transparent
            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = Color.clear;

            // Draw a downward-pointing arrow
            // Arrow head (triangle pointing down)
            int centerX = size / 2;
            int arrowTipY = 2;
            int arrowBaseY = size / 2;
            int arrowWidth = size - 4;

            for (int y = arrowTipY; y <= arrowBaseY; y++)
            {
                float progress = (float)(y - arrowTipY) / (arrowBaseY - arrowTipY);
                int halfWidth = Mathf.RoundToInt(progress * arrowWidth / 2f);
                for (int x = centerX - halfWidth; x <= centerX + halfWidth; x++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        pixels[y * size + x] = Color.white;
                }
            }

            // Arrow shaft (rectangle above the head)
            int shaftHalfWidth = size / 8;
            int shaftTop = size - 3;
            for (int y = arrowBaseY; y <= shaftTop; y++)
            {
                for (int x = centerX - shaftHalfWidth; x <= centerX + shaftHalfWidth; x++)
                {
                    if (x >= 0 && x < size && y >= 0 && y < size)
                        pixels[y * size + x] = Color.white;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 64f);
        }

        /// <summary>
        /// Animate: scale in with a bounce, hold briefly, then fade out and self-destruct.
        /// </summary>
        private IEnumerator AnimateAndDestroy()
        {
            float scaleInDuration = animationDuration * 0.4f;
            float holdDuration = animationDuration * 0.2f;
            float fadeOutDuration = animationDuration * 0.4f;

            Vector3 targetScale = Vector3.one * arrowScale;

            // Phase 1: Scale in with overshoot
            float elapsed = 0f;
            while (elapsed < scaleInDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / scaleInDuration);
                // Elastic ease-out for bounce effect
                float scale = 1f + Mathf.Sin(t * Mathf.PI) * 0.2f;
                transform.localScale = targetScale * (t * scale);
                yield return null;
            }
            transform.localScale = targetScale;

            // Phase 2: Hold
            yield return new WaitForSeconds(holdDuration);

            // Phase 3: Fade out
            elapsed = 0f;
            while (elapsed < fadeOutDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fadeOutDuration);

                // Fade alpha
                if (spriteRenderer != null)
                {
                    Color c = spriteRenderer.color;
                    c.a = Mathf.Lerp(arrowColor.a, 0f, t);
                    spriteRenderer.color = c;
                }

                // Shrink slightly
                float shrink = Mathf.Lerp(1f, 0.5f, t);
                transform.localScale = targetScale * shrink;

                yield return null;
            }

            // Self-destruct
            Destroy(gameObject);
        }

        /// <summary>
        /// Immediately destroy this arrow indicator.
        /// </summary>
        public void DestroyNow()
        {
            StopAllCoroutines();
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            StopAllCoroutines();
        }
    }
}
