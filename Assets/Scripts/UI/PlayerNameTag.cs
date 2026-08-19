using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Displays the player's Steam name above the character's health bar.
    /// Dynamically creates a TextMesh at runtime and handles flip compensation.
    /// Attach as a child GameObject of the character, or let CharacterEntity create it automatically.
    /// </summary>
    public class PlayerNameTag : MonoBehaviour
    {
        /// <summary>
        /// Vertical offset above the character's pivot point.
        /// Positioned above the HealthBar (which uses yOffset=0.9f).
        /// </summary>
        [Header("Layout")]
        [Tooltip("Y offset above the character pivot")]
        public float yOffset = 1.1f;

        /// <summary>
        /// Maximum number of characters to display before truncating.
        /// </summary>
        [Header("Text Settings")]
        [Tooltip("Max characters before truncation")]
        public int maxNameLength = 12;

        /// <summary>
        /// Font size for the TextMesh.
        /// </summary>
        [Tooltip("Font size for the name text")]
        public int fontSize = 32;

        /// <summary>
        /// Character size (world-space scale of each character).
        /// </summary>
        [Tooltip("Character size in world units")]
        public float characterSize = 0.05f;

        /// <summary>
        /// Color of the name text.
        /// </summary>
        [Tooltip("Color of the name text")]
        public Color textColor = Color.white;

        /// <summary>
        /// Color of the text outline/shadow for readability.
        /// </summary>
        [Tooltip("Color of the outline")]
        public Color outlineColor = Color.black;

        private TextMesh textMesh;
        private TextMesh[] outlineMeshes;
        private bool isInitialized;
        private string currentName = "";

        private const string DEFAULT_NAME = "Player";
        private const int OUTLINE_COUNT = 4;
        private const float OUTLINE_OFFSET = 0.01f;

        /// <summary>
        /// Initialize the name tag. Creates TextMesh objects for text and outline.
        /// </summary>
        /// <param name="parentSprite">The parent character's SpriteRenderer (reserved for future use).</param>
        public void Initialize(SpriteRenderer parentSprite)
        {

            // Create outline TextMeshes for readability (4 directions)
            outlineMeshes = new TextMesh[OUTLINE_COUNT];
            Vector2[] offsets = new Vector2[]
            {
                new Vector2(-OUTLINE_OFFSET, 0f),
                new Vector2(OUTLINE_OFFSET, 0f),
                new Vector2(0f, -OUTLINE_OFFSET),
                new Vector2(0f, OUTLINE_OFFSET)
            };

            for (int i = 0; i < OUTLINE_COUNT; i++)
            {
                GameObject outlineObj = new GameObject($"NameTag_Outline_{i}");
                outlineObj.transform.SetParent(transform, false);
                outlineObj.transform.localPosition = new Vector3(offsets[i].x, offsets[i].y, 0.01f);

                TextMesh outline = outlineObj.AddComponent<TextMesh>();
                outline.alignment = TextAlignment.Center;
                outline.anchor = TextAnchor.MiddleCenter;
                outline.fontSize = fontSize;
                outline.characterSize = characterSize;
                outline.color = outlineColor;

                MeshRenderer outlineRenderer = outlineObj.GetComponent<MeshRenderer>();
                outlineRenderer.sortingOrder = 102;

                outlineMeshes[i] = outline;

                // Inherit layer from parent
                if (transform.parent != null)
                {
                    outlineObj.layer = transform.parent.gameObject.layer;
                }
            }

            // Create main TextMesh
            GameObject textObj = new GameObject("NameTag_Text");
            textObj.transform.SetParent(transform, false);
            textObj.transform.localPosition = Vector3.zero;

            textMesh = textObj.AddComponent<TextMesh>();
            textMesh.alignment = TextAlignment.Center;
            textMesh.anchor = TextAnchor.MiddleCenter;
            textMesh.fontSize = fontSize;
            textMesh.characterSize = characterSize;
            textMesh.color = textColor;

            MeshRenderer textRenderer = textObj.GetComponent<MeshRenderer>();
            textRenderer.sortingOrder = 103;

            // Inherit layer from parent
            if (transform.parent != null)
            {
                int parentLayer = transform.parent.gameObject.layer;
                gameObject.layer = parentLayer;
                textObj.layer = parentLayer;
            }

            // Position the name tag above the character (above health bar)
            transform.localPosition = new Vector3(0f, yOffset, 0f);

            isInitialized = true;

            // Apply current name if already set
            if (!string.IsNullOrEmpty(currentName))
            {
                ApplyNameText(currentName);
            }
            else
            {
                ApplyNameText(DEFAULT_NAME);
            }
        }

        /// <summary>
        /// Set the displayed player name. Handles truncation for long names.
        /// </summary>
        /// <param name="name">The player's Steam name to display.</param>
        public void SetName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                currentName = DEFAULT_NAME;
            }
            else
            {
                currentName = name;
            }

            if (isInitialized)
            {
                ApplyNameText(currentName);
            }
        }

        /// <summary>
        /// Show the name tag.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the name tag.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Reset the name tag state (for object pool reuse).
        /// </summary>
        public void ResetState()
        {
            currentName = "";
            if (isInitialized)
            {
                ApplyNameText(DEFAULT_NAME);
            }
        }

        private void LateUpdate()
        {
            if (!isInitialized) return;

            // Keep the name tag's world rotation fixed at identity so it never
            // rotates or flips regardless of the parent's transform changes.
            // SpriteRenderer.flipX does not affect child transforms, but we guard
            // against any parent scale/rotation changes here.
            transform.rotation = Quaternion.identity;

            // Ensure scale stays positive (guards against parent scale flip)
            Vector3 parentLossyScale = transform.parent != null ? transform.parent.lossyScale : Vector3.one;
            float signX = parentLossyScale.x < 0 ? -1f : 1f;
            float signY = parentLossyScale.y < 0 ? -1f : 1f;
            transform.localScale = new Vector3(signX, signY, 1f);
        }

        /// <summary>
        /// Apply the name text to all TextMesh objects, with truncation if needed.
        /// </summary>
        private void ApplyNameText(string name)
        {
            string displayName = TruncateName(name);

            if (textMesh != null)
            {
                textMesh.text = displayName;
            }

            if (outlineMeshes != null)
            {
                for (int i = 0; i < outlineMeshes.Length; i++)
                {
                    if (outlineMeshes[i] != null)
                    {
                        outlineMeshes[i].text = displayName;
                    }
                }
            }
        }

        /// <summary>
        /// Truncate the name if it exceeds the maximum length.
        /// </summary>
        private string TruncateName(string name)
        {
            if (string.IsNullOrEmpty(name)) return DEFAULT_NAME;

            if (name.Length > maxNameLength)
            {
                return name.Substring(0, maxNameLength - 1) + "…";
            }

            return name;
        }
    }
}
