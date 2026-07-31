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

        [Header("Train Button")]
        [Tooltip("Reference to the Train button. Will be found dynamically if not assigned.")]
        [SerializeField] private Button trainButton;

        // Path to the TrainView prefab in Resources
        private const string TrainViewPrefabPath = "Prefabs/UI/View/TrainView";

        // X offset for TrainView position relative to this card
        private const float TrainViewXOffset = 546f;

        // Current TrainView instance managed by this card
        private GameObject trainViewInstance;

        /// <summary>
        /// Whether this card is currently the focus (center) card.
        /// Only the focus card allows button clicks and card-face interactions.
        /// Updated by CardSplineDistributor when focus changes.
        /// </summary>
        public bool IsFocused { get; private set; }

        /// <summary>
        /// The CharacterData currently bound to this card. Null if no data is set.
        /// </summary>
        public CharacterData Data { get; private set; }

        /// <summary>
        /// Whether this card is currently in cooldown state (character is dead and waiting to respawn).
        /// Driven by CharacterDeathManager (which owns the actual timer).
        /// </summary>
        public bool IsCooldown { get; private set; }

        /// <summary>
        /// Remaining cooldown time in seconds (last value pushed by CharacterDeathManager).
        /// </summary>
        public float CooldownRemaining { get; private set; }

        private const string DefaultDescription = "\u5b83\u8fd8\u5f88\u795e\u79d8\u54e6~";

        private void Awake()
        {
            // Find Train button if not assigned in Inspector
            if (trainButton == null)
            {
                Transform trainTrans = transform.Find("Train");
                if (trainTrans != null)
                    trainButton = trainTrans.GetComponent<Button>();
            }

            // Bind click event
            if (trainButton != null)
                trainButton.onClick.AddListener(OnTrainButtonClicked);
        }

        /// <summary>
        /// Sets the focus state of this card. Called by CardSplineDistributor
        /// when the focus card index changes.
        /// </summary>
        /// <param name="focused">True if this card is the center/focus card.</param>
        public void SetFocused(bool focused)
        {
            IsFocused = focused;

            // Update Train button interactable state
            if (trainButton != null)
                trainButton.interactable = focused;

            // Auto-dismiss TrainView when this card loses focus (e.g. drag switch)
            if (!focused && trainViewInstance != null)
            {
                Destroy(trainViewInstance);
                trainViewInstance = null;
            }
        }

        /// <summary>
        /// Handles Train button click. Toggles the TrainView on/off.
        /// </summary>
        private void OnTrainButtonClicked()
        {
            // Only the focus (center) card can respond to button clicks
            if (!IsFocused) return;

            // If TrainView already exists, destroy it (toggle off)
            if (trainViewInstance != null)
            {
                Destroy(trainViewInstance);
                trainViewInstance = null;
                return;
            }

            // Load TrainView prefab
            GameObject prefab = Resources.Load<GameObject>(TrainViewPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("CharacterCard: Failed to load TrainView prefab from " + TrainViewPrefabPath);
                return;
            }

            // Instantiate under the same parent (Canvas level)
            Transform parentTransform = transform.parent != null ? transform.parent : transform;
            trainViewInstance = Instantiate(prefab, parentTransform);

            // Position the TrainView at card position + X offset
            RectTransform cardRect = (RectTransform)transform;
            RectTransform viewRect = trainViewInstance.GetComponent<RectTransform>();
            if (viewRect != null)
            {
                viewRect.anchoredPosition = cardRect.anchoredPosition + new Vector2(TrainViewXOffset, 0f);
            }
        }

        private void OnDestroy()
        {
            // Remove button listener
            if (trainButton != null)
                trainButton.onClick.RemoveListener(OnTrainButtonClicked);

            // Destroy TrainView instance if still exists
            if (trainViewInstance != null)
            {
                Destroy(trainViewInstance);
                trainViewInstance = null;
            }
        }

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
        /// Apply the cooldown visual state for this card.
        /// Called every frame by CharacterDeathManager while the character is in cooldown,
        /// regardless of whether the card panel is currently visible.
        /// Setting text/colors on inactive UI is fine — the values will be shown next time the panel opens.
        /// </summary>
        /// <param name="remainingSeconds">Remaining cooldown time in seconds.</param>
        public void ApplyCooldownVisual(float remainingSeconds)
        {
            IsCooldown = true;
            CooldownRemaining = remainingSeconds;

            // Set card portrait to grey
            if (portraitImage != null)
            {
                portraitImage.color = Color.gray;
            }

            // Ensure cooldown text exists
            EnsureCooldownText();

            // Show cooldown text with current remaining seconds
            if (cooldownText != null)
            {
                if (!cooldownText.gameObject.activeSelf)
                    cooldownText.gameObject.SetActive(true);
                cooldownText.text = Mathf.CeilToInt(remainingSeconds).ToString();
            }
        }

        /// <summary>
        /// Clear the cooldown visual state, restoring normal card visuals.
        /// Called by CharacterDeathManager when the cooldown finishes.
        /// </summary>
        public void ClearCooldownVisual()
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

        /// <summary>
        /// Legacy entry point retained for backwards compatibility.
        /// New cooldown logic is driven by CharacterDeathManager — this just forwards the visual call.
        /// </summary>
        /// <param name="duration">Cooldown duration in seconds.</param>
        public void StartCooldown(float duration)
        {
            ApplyCooldownVisual(duration);
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
