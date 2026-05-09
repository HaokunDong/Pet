using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PetGame
{
    /// <summary>
    /// Data-binding script for a character card UI prefab.
    /// Attach this to the card prefab root and drag-assign the child UI components
    /// in the Inspector. Call SetData() to populate the card with character info.
    /// Also handles cooldown state visual feedback when a character dies.
    /// </summary>
    public class CharacterCard : MonoBehaviour
    {
        [Header("UI References (drag-assign in prefab)")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private TMP_Text nameText;
        [SerializeField] private TMP_Text attackText;
        [SerializeField] private TMP_Text healthText;
        [SerializeField] private TMP_Text defenseText;
        [SerializeField] private TMP_Text descriptionText;

        [Header("Cooldown UI")]
        [Tooltip("Text overlay to display cooldown remaining seconds. Will be created dynamically if not assigned.")]
        [SerializeField] private TMP_Text cooldownText;

        /// <summary>
        /// The CharacterData currently bound to this card. Null if no data is set.
        /// </summary>
        public CharacterData Data { get; private set; }

        /// <summary>
        /// Whether this card is currently in cooldown state (character is dead and waiting to respawn).
        /// </summary>
        public bool IsCooldown { get; private set; }

        /// <summary>
        /// Remaining cooldown time in seconds.
        /// </summary>
        public float CooldownRemaining { get; private set; }

        private float cooldownDuration;

        private const string DefaultDescription = "\u5b83\u8fd8\u5f88\u795e\u79d8\u54e6~";

        /// <summary>
        /// Populates the card UI with data from the given CharacterData.
        /// Pass null to reset the card to its default/blank state.
        /// </summary>
        public void SetData(CharacterData data)
        {
            Data = data;

            if (data == null)
            {
                ClearCard();
                return;
            }

            if (portraitImage != null)
            {
                portraitImage.sprite = data.portraitSprite;
                portraitImage.enabled = data.portraitSprite != null;
            }

            if (nameText != null)
                nameText.text = data.characterName;

            if (attackText != null)
                attackText.text = data.attackPower.ToString("F0");

            if (healthText != null)
                healthText.text = data.maxHealth.ToString("F0");

            if (defenseText != null)
                defenseText.text = data.defense.ToString("F0");

            if (descriptionText != null)
            {
                descriptionText.text = string.IsNullOrEmpty(data.characterDescription)
                    ? DefaultDescription
                    : data.characterDescription;
            }
        }

        /// <summary>
        /// Resets the card to a blank/default state.
        /// </summary>
        private void ClearCard()
        {
            if (portraitImage != null)
            {
                portraitImage.sprite = null;
                portraitImage.enabled = false;
            }

            if (nameText != null)
                nameText.text = "";

            if (attackText != null)
                attackText.text = "";

            if (healthText != null)
                healthText.text = "";

            if (defenseText != null)
                defenseText.text = "";

            if (descriptionText != null)
                descriptionText.text = "";
        }

        /// <summary>
        /// Start the cooldown state for this card.
        /// Sets the card to grey, shows countdown text, and begins timer.
        /// </summary>
        /// <param name="duration">Cooldown duration in seconds.</param>
        public void StartCooldown(float duration)
        {
            IsCooldown = true;
            cooldownDuration = duration;
            CooldownRemaining = duration;

            // Set card portrait to grey
            if (portraitImage != null)
            {
                portraitImage.color = Color.gray;
            }

            // Ensure cooldown text exists
            EnsureCooldownText();

            // Show cooldown text
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(true);
                cooldownText.text = Mathf.CeilToInt(CooldownRemaining).ToString();
            }
        }

        /// <summary>
        /// End the cooldown state, restoring normal card visuals.
        /// </summary>
        private void EndCooldown()
        {
            IsCooldown = false;
            CooldownRemaining = 0f;

            // Restore card portrait color
            if (portraitImage != null)
            {
                portraitImage.color = Color.white;
            }

            // Hide cooldown text
            if (cooldownText != null)
            {
                cooldownText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            if (!IsCooldown) return;

            CooldownRemaining -= Time.deltaTime;

            if (CooldownRemaining <= 0f)
            {
                EndCooldown();
                return;
            }

            // Update cooldown text display
            if (cooldownText != null)
            {
                cooldownText.text = Mathf.CeilToInt(CooldownRemaining).ToString();
            }
        }

        /// <summary>
        /// Ensures the cooldown text component exists.
        /// Creates one dynamically if not assigned in the Inspector.
        /// </summary>
        private void EnsureCooldownText()
        {
            if (cooldownText != null) return;

            // Create a new GameObject for cooldown text overlay
            GameObject textObj = new GameObject("CooldownText");
            textObj.transform.SetParent(transform, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            cooldownText = textObj.AddComponent<TextMeshProUGUI>();
            cooldownText.alignment = TextAlignmentOptions.Center;
            cooldownText.fontSize = 36;
            cooldownText.color = Color.white;
            cooldownText.fontStyle = FontStyles.Bold;
        }
    }
}
