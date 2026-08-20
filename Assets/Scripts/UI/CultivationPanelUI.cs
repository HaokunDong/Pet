using UnityEngine;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// UI controller for the Cultivation Panel.
    /// Manages display of cultivation data and handles upgrade button interactions.
    /// </summary>
    public class CultivationPanelUI : MonoBehaviour
    {
        [Header("Info Display")]
        [SerializeField] private Text levelText;
        [SerializeField] private Text talentPointsText;

        [Header("Experience Bar")]
        [SerializeField] private Image expFillImage;
        [SerializeField] private Text expText;

        [Header("Attribute Rows")]
        [SerializeField] private Text attackLevelText;
        [SerializeField] private Text attackBonusText;
        [SerializeField] private Button attackUpgradeBtn;

        [SerializeField] private Text defenseLevelText;
        [SerializeField] private Text defenseBonusText;
        [SerializeField] private Button defenseUpgradeBtn;

        [SerializeField] private Text healthLevelText;
        [SerializeField] private Text healthBonusText;
        [SerializeField] private Button healthUpgradeBtn;

        [SerializeField] private Text attackSpeedLevelText;
        [SerializeField] private Text attackSpeedBonusText;
        [SerializeField] private Button attackSpeedUpgradeBtn;

        [SerializeField] private Text moveSpeedLevelText;
        [SerializeField] private Text moveSpeedBonusText;
        [SerializeField] private Button moveSpeedUpgradeBtn;

        [SerializeField] private Text skillCDLevelText;
        [SerializeField] private Text skillCDBonusText;
        [SerializeField] private Button skillCDUpgradeBtn;

        [Header("Controls")]
        [SerializeField] private Button closeBtn;

        // Runtime references
        private CharacterEntity currentEntity;
        private CultivationData currentData;

        /// <summary>
        /// Whether the panel is currently open.
        /// </summary>
        public bool IsOpen { get; private set; }

        private void Awake()
        {
            // Bind button click events
            if (attackUpgradeBtn != null)
                attackUpgradeBtn.onClick.AddListener(() => OnUpgradeClicked(AttributeType.Attack));
            if (defenseUpgradeBtn != null)
                defenseUpgradeBtn.onClick.AddListener(() => OnUpgradeClicked(AttributeType.Defense));
            if (healthUpgradeBtn != null)
                healthUpgradeBtn.onClick.AddListener(() => OnUpgradeClicked(AttributeType.Health));
            if (attackSpeedUpgradeBtn != null)
                attackSpeedUpgradeBtn.onClick.AddListener(() => OnUpgradeClicked(AttributeType.AttackSpeed));
            if (moveSpeedUpgradeBtn != null)
                moveSpeedUpgradeBtn.onClick.AddListener(() => OnUpgradeClicked(AttributeType.MoveSpeed));
            if (skillCDUpgradeBtn != null)
                skillCDUpgradeBtn.onClick.AddListener(() => OnUpgradeClicked(AttributeType.SkillCD));

            if (closeBtn != null)
                closeBtn.onClick.AddListener(Close);

            // Start hidden
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Open the cultivation panel for a specific character.
        /// </summary>
        public void Open(CharacterEntity entity)
        {
            if (entity == null || entity.RuntimeStats == null) return;

            currentEntity = entity;

            // Get cultivation data for this character
            string characterId = entity.RuntimeStats.characterId;
            currentData = CultivationManager.Instance.GetCultivationData(characterId);

            if (currentData == null) return;

            // Subscribe to events for real-time updates
            SubscribeEvents();

            gameObject.SetActive(true);
            IsOpen = true;

            RefreshUI();
        }

        /// <summary>
        /// Close the cultivation panel and restore character control mode.
        /// </summary>
        public void Close()
        {
            UnsubscribeEvents();

            gameObject.SetActive(false);
            IsOpen = false;



            currentEntity = null;
            currentData = null;
        }

        /// <summary>
        /// Refresh all UI elements to reflect current cultivation data.
        /// </summary>
        public void RefreshUI()
        {
            if (currentData == null) return;

            // Level
            if (levelText != null)
                levelText.text = $"Lv. {currentData.level}";

            // Talent points
            if (talentPointsText != null)
                talentPointsText.text = $"\u5929\u8d4b\u70b9: {currentData.talentPoints}";

            // Experience bar
            int requiredExp = currentData.GetRequiredExp();
            if (expFillImage != null)
                expFillImage.fillAmount = (float)currentData.currentExp / requiredExp;
            if (expText != null)
                expText.text = $"{currentData.currentExp}/{requiredExp}";

            // Attribute rows
            CultivationConfig cfg = CultivationManager.Instance.Config;

            RefreshAttributeRow(attackLevelText, attackBonusText, currentData.attackLevel, cfg.attackBonusPerLevel, "\u653b\u51fb");
            RefreshAttributeRow(defenseLevelText, defenseBonusText, currentData.defenseLevel, cfg.defenseBonusPerLevel, "\u9632\u5fa1");
            RefreshAttributeRow(healthLevelText, healthBonusText, currentData.healthLevel, cfg.healthBonusPerLevel, "\u751f\u547d");
            RefreshAttributeRow(attackSpeedLevelText, attackSpeedBonusText, currentData.attackSpeedLevel, cfg.attackSpeedBonusPerLevel, "\u653b\u901f");
            RefreshAttributeRow(moveSpeedLevelText, moveSpeedBonusText, currentData.moveSpeedLevel, cfg.moveSpeedBonusPerLevel, "\u79fb\u901f");
            RefreshAttributeRow(skillCDLevelText, skillCDBonusText, currentData.skillCDLevel, cfg.skillCDBonusPerLevel, "\u6280\u80fdCD");

            // Update button interactability
            UpdateButtonStates();
        }

        /// <summary>
        /// Refresh a single attribute row display.
        /// </summary>
        private void RefreshAttributeRow(Text levelTxt, Text bonusTxt, int attrLevel, float bonusPerLevel, string attrName)
        {
            if (levelTxt != null)
                levelTxt.text = $"{attrName} Lv.{attrLevel}";
            if (bonusTxt != null)
                bonusTxt.text = $"+{attrLevel * bonusPerLevel:F1}%";
        }

        /// <summary>
        /// Update all upgrade buttons' interactable state based on available talent points.
        /// </summary>
        private void UpdateButtonStates()
        {
            bool canUpgrade = currentData != null && currentData.HasTalentPoints;

            if (attackUpgradeBtn != null) attackUpgradeBtn.interactable = canUpgrade;
            if (defenseUpgradeBtn != null) defenseUpgradeBtn.interactable = canUpgrade;
            if (healthUpgradeBtn != null) healthUpgradeBtn.interactable = canUpgrade;
            if (attackSpeedUpgradeBtn != null) attackSpeedUpgradeBtn.interactable = canUpgrade;
            if (moveSpeedUpgradeBtn != null) moveSpeedUpgradeBtn.interactable = canUpgrade;
            if (skillCDUpgradeBtn != null) skillCDUpgradeBtn.interactable = canUpgrade;
        }

        /// <summary>
        /// Handle upgrade button click for a specific attribute.
        /// </summary>
        private void OnUpgradeClicked(AttributeType type)
        {
            if (currentEntity == null || currentData == null) return;

            bool success = CultivationManager.Instance.UpgradeAttribute(currentEntity, type);
            if (success)
            {
                RefreshUI();
            }
        }

        // =====================================================================
        // Event Subscription for Real-time Updates
        // =====================================================================

        private void SubscribeEvents()
        {
            if (currentData == null) return;
            currentData.OnExpChanged += HandleExpChanged;
            currentData.OnLevelUp += HandleLevelUp;
            currentData.OnTalentPointChanged += HandleTalentPointChanged;
            currentData.OnAttributeUpgraded += HandleAttributeUpgraded;
        }

        private void UnsubscribeEvents()
        {
            if (currentData == null) return;
            currentData.OnExpChanged -= HandleExpChanged;
            currentData.OnLevelUp -= HandleLevelUp;
            currentData.OnTalentPointChanged -= HandleTalentPointChanged;
            currentData.OnAttributeUpgraded -= HandleAttributeUpgraded;
        }

        private void HandleExpChanged(int currentExp, int requiredExp)
        {
            if (expFillImage != null)
                expFillImage.fillAmount = (float)currentExp / requiredExp;
            if (expText != null)
                expText.text = $"{currentExp}/{requiredExp}";
        }

        private void HandleLevelUp(int newLevel)
        {
            if (levelText != null)
                levelText.text = $"Lv. {newLevel}";
        }

        private void HandleTalentPointChanged(int talentPoints)
        {
            if (talentPointsText != null)
                talentPointsText.text = $"\u5929\u8d4b\u70b9: {talentPoints}";
            UpdateButtonStates();
        }

        private void HandleAttributeUpgraded(AttributeType type, int newLevel)
        {
            // Full refresh is simpler and ensures consistency
            RefreshUI();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }
    }
}
