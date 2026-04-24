using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Lightweight SpriteRenderer-based health bar that floats above a character.
    /// Dynamically creates background and foreground bar sprites at runtime.
    /// Attach as a child GameObject of the character, or let CharacterEntity create it automatically.
    /// </summary>
    public class HealthBar : MonoBehaviour
    {
        /// <summary>
        /// Vertical offset above the character's pivot point.
        /// </summary>
        [Header("Layout")]
        [Tooltip("Y offset above the character pivot")]
        public float yOffset = 0.9f;

        /// <summary>
        /// Total width of the health bar in world units.
        /// </summary>
        [Tooltip("Width of the health bar in world units")]
        public float barWidth = 0.6f;

        /// <summary>
        /// Total height of the health bar in world units.
        /// </summary>
        [Tooltip("Height of the health bar in world units")]
        public float barHeight = 0.08f;

        private SpriteRenderer backgroundRenderer;
        private SpriteRenderer foregroundRenderer;
        private Transform foregroundTransform;

        private SpriteRenderer parentSpriteRenderer;

        private float currentHealthRatio = 1f;
        private bool isInitialized;

        /// <summary>
        /// Initialize the health bar. Creates child sprite objects for background and foreground.
        /// </summary>
        public void Initialize(SpriteRenderer parentSprite)
        {
            parentSpriteRenderer = parentSprite;

            // Create a 1x1 white pixel sprite to use for both bars
            Sprite whiteSprite = CreateWhiteSprite();

            // --- Background bar (dark gray) ---
            GameObject bgObj = new GameObject("HealthBar_BG");
            bgObj.transform.SetParent(transform, false);
            bgObj.transform.localPosition = Vector3.zero;
            bgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);

            backgroundRenderer = bgObj.AddComponent<SpriteRenderer>();
            backgroundRenderer.sprite = whiteSprite;
            backgroundRenderer.color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
            backgroundRenderer.sortingOrder = 100;

            // --- Foreground bar (health fill) ---
            GameObject fgObj = new GameObject("HealthBar_FG");
            fgObj.transform.SetParent(transform, false);
            // Anchor foreground to the left edge so it scales from left to right
            fgObj.transform.localPosition = Vector3.zero;
            fgObj.transform.localScale = new Vector3(barWidth, barHeight, 1f);

            foregroundRenderer = fgObj.AddComponent<SpriteRenderer>();
            foregroundRenderer.sprite = whiteSprite;
            foregroundRenderer.color = Color.green;
            foregroundRenderer.sortingOrder = 101;
            foregroundTransform = fgObj.transform;

            // Position the health bar above the character
            transform.localPosition = new Vector3(0f, yOffset, 0f);

            // Inherit layer from parent GameObject so the health bar
            // shares the same physics/rendering layer as the character
            if (transform.parent != null)
            {
                int parentLayer = transform.parent.gameObject.layer;
                gameObject.layer = parentLayer;
                bgObj.layer = parentLayer;
                fgObj.layer = parentLayer;
            }

            isInitialized = true;
            UpdateVisual();
        }

        /// <summary>
        /// Update the health bar to reflect current health values.
        /// </summary>
        /// <param name="currentHealth">Current health points.</param>
        /// <param name="maxHealth">Maximum health points.</param>
        public void UpdateHealth(float currentHealth, float maxHealth)
        {
            if (maxHealth <= 0f)
            {
                currentHealthRatio = 0f;
            }
            else
            {
                currentHealthRatio = Mathf.Clamp01(currentHealth / maxHealth);
            }

            UpdateVisual();
        }

        /// <summary>
        /// Show the health bar.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the health bar.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!isInitialized) return;

            // Compensate for parent SpriteRenderer flipX so the bar always faces forward
            if (parentSpriteRenderer != null)
            {
                float flipCompensation = parentSpriteRenderer.flipX ? -1f : 1f;
                transform.localScale = new Vector3(flipCompensation, 1f, 1f);
            }
        }

        /// <summary>
        /// Update the foreground bar scale and color based on current health ratio.
        /// </summary>
        private void UpdateVisual()
        {
            if (!isInitialized) return;

            // Scale the foreground bar width based on health ratio
            foregroundTransform.localScale = new Vector3(barWidth * currentHealthRatio, barHeight, 1f);

            // Offset foreground so it shrinks from right to left (anchored at left edge)
            float xOffset = -(barWidth - barWidth * currentHealthRatio) * 0.5f;
            foregroundTransform.localPosition = new Vector3(xOffset, 0f, 0f);

            // Update color based on health percentage
            if (currentHealthRatio > 0.5f)
            {
                foregroundRenderer.color = Color.green;
            }
            else if (currentHealthRatio > 0.25f)
            {
                foregroundRenderer.color = Color.yellow;
            }
            else
            {
                foregroundRenderer.color = Color.red;
            }
        }

        /// <summary>
        /// Create a simple 1x1 white pixel sprite at runtime.
        /// </summary>
        private Sprite CreateWhiteSprite()
        {
            Texture2D tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        }
    }
}
