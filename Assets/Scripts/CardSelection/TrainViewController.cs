using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using PetGame;

/// <summary>
/// Controls the TrainView panel: manages TalentSystem panel toggling,
/// property text display, talent point allocation, and data binding.
/// </summary>
public class TrainViewController : MonoBehaviour
{
    // Property names that match the child nodes under the "Property" container
    private static readonly string[] PropertyNames = new string[]
    {
        "Attack",
        "Defense",
        "Health",
        "Agility",
        "AttackSpeed",
        "CD"
    };

    // Display names for each property (Chinese)
    private static readonly string[] PropertyDisplayNames = new string[]
    {
        "攻击力",
        "防御力",
        "生命值",
        "敏捷",
        "攻速",
        "冷却缩减"
    };

    // Mapping from property index to AttributeType
    private static readonly AttributeType[] PropertyAttributeTypes = new AttributeType[]
    {
        AttributeType.Attack,
        AttributeType.Defense,
        AttributeType.Health,
        AttributeType.MoveSpeed,
        AttributeType.AttackSpeed,
        AttributeType.SkillCD
    };

    // Mapping from property name to TalentSystem child name
    // Note: "DefenseTalentSystem " has a trailing space in the prefab
    private static readonly string[] TalentSystemNames = new string[]
    {
        "AttackTalentSystem",
        "DefenseTalentSystem ",
        "HealthTalentSystem",
        "AgilityTalentSystem",
        "AttackSpeedTalentSystem",
        "CDTalentSystem"
    };

    // Data references
    private CharacterData characterData;
    private CultivationData cultivationData;

    // UI references
    private Text[] propertyTexts;
    private TalentSystemController[] talentControllers;
    private GameObject[] talentSystemObjects;

    // LevelSystem UI references
    private Text levelText;
    private Text talentPointText;
    private Text experienceText;

    // Evolution system UI references
    private Text requirementText;
    private Button evolutionButton;
    private bool hasEvolved = false;

    // Available talent points (tracked locally for temp adjustments during preview)
    private int availableTalentPoints;

    private GameObject currentActiveTalentSystem;
    private int currentActiveTalentIndex = -1;

    /// <summary>
    /// Set the character data for this TrainView. Must be called before Start().
    /// </summary>
    public void SetCharacterData(CharacterData data)
    {
        characterData = data;
    }

    private void Start()
    {
        // Get cultivation data for the character
        if (characterData != null)
        {
            cultivationData = CultivationManager.Instance.GetCultivationData(characterData.characterId);
        }
        else
        {
            Debug.LogWarning("TrainViewController: CharacterData is null. Displaying default values.");
        }

        // Calculate available talent points
        if (cultivationData != null)
        {
            availableTalentPoints = cultivationData.talentPoints;
        }

        InitializePropertyTexts();
        InitializeTalentSystems();
        InitializeLevelSystem();
        InitializeEvolutionSystem();

        // Subscribe to cultivation data events for real-time updates
        if (cultivationData != null)
        {
            cultivationData.OnLevelUp += HandleLevelUp;
            cultivationData.OnExpChanged += HandleExpChanged;
        }
    }

    /// <summary>
    /// Initialize LevelSystem panel: find and bind Level, TalentPoint, Experience text nodes,
    /// then fill them with initial data.
    /// </summary>
    private void InitializeLevelSystem()
    {
        Transform levelSystemNode = transform.Find("LevelSystem");
        if (levelSystemNode == null)
        {
            Debug.LogWarning("TrainViewController: Could not find 'LevelSystem' node.");
            return;
        }

        // Find Level text
        Transform levelNode = levelSystemNode.Find("Level");
        if (levelNode != null)
        {
            levelText = levelNode.GetComponent<Text>();
        }
        if (levelText == null)
        {
            Debug.LogWarning("TrainViewController: Could not find Text component on 'LevelSystem/Level'.");
        }

        // Find TalentPoint text
        Transform talentPointNode = levelSystemNode.Find("TalentPoint");
        if (talentPointNode != null)
        {
            talentPointText = talentPointNode.GetComponent<Text>();
        }
        if (talentPointText == null)
        {
            Debug.LogWarning("TrainViewController: Could not find Text component on 'LevelSystem/TalentPoint'.");
        }

        // Find Experience text
        Transform experienceNode = levelSystemNode.Find("Experience");
        if (experienceNode != null)
        {
            experienceText = experienceNode.GetComponent<Text>();
        }
        if (experienceText == null)
        {
            Debug.LogWarning("TrainViewController: Could not find Text component on 'LevelSystem/Experience'.");
        }

        // Fill initial text values
        UpdateLevelText();
        UpdateTalentPointText();
        UpdateExperienceText();
    }

    /// <summary>
    /// Initialize property text displays with calculated values.
    /// </summary>
    private void InitializePropertyTexts()
    {
        propertyTexts = new Text[PropertyNames.Length];

        Transform propertyContainer = transform.Find("Property");
        if (propertyContainer == null)
        {
            Debug.LogWarning("TrainViewController: Could not find 'Property' container node.");
            return;
        }

        for (int i = 0; i < PropertyNames.Length; i++)
        {
            Transform propertyNode = propertyContainer.Find(PropertyNames[i]);
            if (propertyNode == null)
            {
                Debug.LogWarning($"TrainViewController: Could not find Property node '{PropertyNames[i]}'.");
                continue;
            }

            // Find the direct "Text (Legacy)" child under the Property node
            // Avoid using GetComponentInChildren which may find Text inside TalentSystem
            Transform textTransform = propertyNode.Find("Text (Legacy)");
            if (textTransform != null)
            {
                propertyTexts[i] = textTransform.GetComponent<Text>();
            }
            else
            {
                // Fallback: try to find Text component on direct children only
                for (int c = 0; c < propertyNode.childCount; c++)
                {
                    Transform child = propertyNode.GetChild(c);
                    Text text = child.GetComponent<Text>();
                    if (text != null)
                    {
                        propertyTexts[i] = text;
                        break;
                    }
                }
            }

            if (propertyTexts[i] == null)
            {
                Debug.LogWarning($"TrainViewController: Could not find Text component under Property '{PropertyNames[i]}'.");
            }

            // Set initial text
            UpdatePropertyTextAtIndex(i);
        }
    }

    /// <summary>
    /// Initialize TalentSystem panels and their controllers.
    /// </summary>
    private void InitializeTalentSystems()
    {
        talentControllers = new TalentSystemController[PropertyNames.Length];
        talentSystemObjects = new GameObject[PropertyNames.Length];

        Transform propertyContainer = transform.Find("Property");
        if (propertyContainer == null) return;

        for (int i = 0; i < PropertyNames.Length; i++)
        {
            string propertyName = PropertyNames[i];
            string talentSystemName = TalentSystemNames[i];

            Transform propertyNode = propertyContainer.Find(propertyName);
            if (propertyNode == null)
            {
                Debug.LogWarning($"TrainViewController: Could not find Property node '{propertyName}'.");
                continue;
            }

            // Find the TalentSystem child under this Property
            Transform talentSystemTransform = propertyNode.Find(talentSystemName);
            if (talentSystemTransform == null)
            {
                Debug.LogWarning($"TrainViewController: Could not find TalentSystem '{talentSystemName}' under Property '{propertyName}'.");
                continue;
            }

            GameObject talentSystemObj = talentSystemTransform.gameObject;
            talentSystemObjects[i] = talentSystemObj;

            // Hide all TalentSystem panels by default
            talentSystemObj.SetActive(false);

            // Add and initialize TalentSystemController
            TalentSystemController controller = talentSystemObj.GetComponent<TalentSystemController>();
            if (controller == null)
            {
                controller = talentSystemObj.AddComponent<TalentSystemController>();
            }

            int currentTalentLevel = 0;
            if (cultivationData != null)
            {
                currentTalentLevel = cultivationData.GetAttributeLevel(PropertyAttributeTypes[i]);
            }

            controller.Initialize(
                PropertyAttributeTypes[i],
                currentTalentLevel,
                GetAvailableTalentPoints,
                SetAvailableTalentPoints,
                OnTalentLevelChanged,
                OnTalentConfirmed
            );

            talentControllers[i] = controller;

            // Bind button click event on the Property node
            Button propertyButton = propertyNode.GetComponent<Button>();
            if (propertyButton == null)
            {
                Debug.LogWarning($"TrainViewController: Property node '{propertyName}' does not have a Button component.");
                continue;
            }

            // Capture local variable for closure
            int capturedIndex = i;
            propertyButton.onClick.AddListener(() => OnPropertyClicked(capturedIndex));
        }
    }

    /// <summary>
    /// Handle property button click: toggle TalentSystem panel.
    /// </summary>
    private void OnPropertyClicked(int index)
    {
        GameObject talentSystem = talentSystemObjects[index];
        if (talentSystem == null) return;

        // If clicking the same property that is already active, close it
        if (currentActiveTalentSystem == talentSystem)
        {
            // Reset temp changes when closing
            if (talentControllers[index] != null)
            {
                talentControllers[index].ResetTempChanges();
            }
            currentActiveTalentSystem.SetActive(false);
            currentActiveTalentSystem = null;
            currentActiveTalentIndex = -1;
            UpdateTalentPointText();
            return;
        }

        // Hide the currently active TalentSystem and reset its temp changes
        if (currentActiveTalentSystem != null && currentActiveTalentIndex >= 0)
        {
            if (talentControllers[currentActiveTalentIndex] != null)
            {
                talentControllers[currentActiveTalentIndex].ResetTempChanges();
            }
            currentActiveTalentSystem.SetActive(false);
            UpdateTalentPointText();
        }

        // Show the new TalentSystem
        talentSystem.SetActive(true);
        currentActiveTalentSystem = talentSystem;
        currentActiveTalentIndex = index;
    }

    /// <summary>
    /// Callback when a talent level changes temporarily (preview).
    /// Updates the corresponding property text and talent point display.
    /// </summary>
    private void OnTalentLevelChanged(AttributeType type, int newTempLevel)
    {
        int index = GetIndexForAttributeType(type);
        if (index >= 0)
        {
            UpdatePropertyTextAtIndex(index, newTempLevel);
        }
        UpdateTalentPointText();
    }

    /// <summary>
    /// Callback when talent is confirmed.
    /// Writes changes to CultivationData and updates property text.
    /// </summary>
    private void OnTalentConfirmed(AttributeType type, int confirmedLevel)
    {
        if (cultivationData == null) return;

        // Write the confirmed level to CultivationData
        cultivationData.SetAttributeLevel(type, confirmedLevel);

        // Update the property text with final values
        int index = GetIndexForAttributeType(type);
        if (index >= 0)
        {
            UpdatePropertyTextAtIndex(index, confirmedLevel);
        }
        UpdateTalentPointText();
    }

    /// <summary>
    /// Update the property text at the given index.
    /// Uses the talent level from CultivationData if no override is provided.
    /// </summary>
    private void UpdatePropertyTextAtIndex(int index, int overrideTalentLevel = -1)
    {
        if (propertyTexts == null || index < 0 || index >= propertyTexts.Length) return;
        if (propertyTexts[index] == null) return;

        int level = cultivationData != null ? cultivationData.level : 1;
        int talentLevel = overrideTalentLevel >= 0
            ? overrideTalentLevel
            : (cultivationData != null ? cultivationData.GetAttributeLevel(PropertyAttributeTypes[index]) : 0);

        // CD property has special formatting
        if (index == 5) // CD index
        {
            var cdResult = TrainPropertyCalculator.CalculateCDProperty(level, talentLevel);
            propertyTexts[index].text = TrainPropertyCalculator.FormatCDPropertyText(cdResult);
        }
        else
        {
            float baseValue = GetBaseValueForIndex(index);
            var result = TrainPropertyCalculator.CalculateProperty(baseValue, level, talentLevel);
            propertyTexts[index].text = TrainPropertyCalculator.FormatPropertyText(PropertyDisplayNames[index], result);
        }
    }

    /// <summary>
    /// Update property text by AttributeType.
    /// </summary>
    public void UpdatePropertyText(AttributeType type)
    {
        int index = GetIndexForAttributeType(type);
        if (index >= 0)
        {
            UpdatePropertyTextAtIndex(index);
        }
    }

    /// <summary>
    /// Get the base value from CharacterData for the given property index.
    /// </summary>
    private float GetBaseValueForIndex(int index)
    {
        if (characterData == null) return 0f;

        switch (index)
        {
            case 0: return characterData.attackPower;    // Attack
            case 1: return characterData.defense;        // Defense
            case 2: return characterData.maxHealth;      // Health
            case 3: return characterData.moveSpeed;      // Agility
            case 4: return characterData.attackSpeed;    // AttackSpeed
            default: return 0f;
        }
    }

    /// <summary>
    /// Get the property index for a given AttributeType.
    /// </summary>
    private int GetIndexForAttributeType(AttributeType type)
    {
        for (int i = 0; i < PropertyAttributeTypes.Length; i++)
        {
            if (PropertyAttributeTypes[i] == type)
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Get available talent points (used by TalentSystemController).
    /// </summary>
    private int GetAvailableTalentPoints()
    {
        return availableTalentPoints;
    }

    /// <summary>
    /// Set available talent points (used by TalentSystemController for temp adjustments).
    /// </summary>
    private void SetAvailableTalentPoints(int points)
    {
        availableTalentPoints = Mathf.Max(0, points);
    }

    /// <summary>
    /// Update the Level text to display current character level.
    /// Format: "等级：X"
    /// </summary>
    private void UpdateLevelText()
    {
        if (levelText == null) return;

        int level = cultivationData != null ? cultivationData.level : 1;
        levelText.text = $"等级： {level}";
    }

    /// <summary>
    /// Update the TalentPoint text to display current available talent points.
    /// Format: "天赋点：X"
    /// </summary>
    private void UpdateTalentPointText()
    {
        if (talentPointText == null) return;

        int points = Mathf.Max(0, availableTalentPoints);
        talentPointText.text = $"天赋点： {points}";
    }

    /// <summary>
    /// Update the Experience text to display current exp and required exp.
    /// Format: "A / B"
    /// </summary>
    private void UpdateExperienceText()
    {
        if (experienceText == null) return;

        if (cultivationData != null)
        {
            int currentExp = cultivationData.currentExp;
            int requiredExp = cultivationData.GetRequiredExp();
            experienceText.text = $"{currentExp} / {requiredExp}";
        }
        else
        {
            experienceText.text = "0 / 100";
        }
    }

    /// <summary>
    /// Handle level up event: refresh all texts since level affects growth values.
    /// </summary>
    private void HandleLevelUp(int newLevel)
    {
        // Update available talent points (level up grants 1 new talent point)
        availableTalentPoints = cultivationData.talentPoints;

        // Update all LevelSystem texts
        UpdateLevelText();
        UpdateTalentPointText();
        UpdateExperienceText();

        // Update all property texts (growth value B depends on level)
        RefreshAllPropertyTexts();

        // Refresh evolution requirement display (level change may affect requirements)
        UpdateEvolutionRequirementDisplay();
        UpdateEvolutionButtonState();
    }

    /// <summary>
    /// Handle experience changed event: refresh experience text.
    /// </summary>
    private void HandleExpChanged(int currentExp, int requiredExp)
    {
        if (experienceText != null)
        {
            experienceText.text = $"{currentExp} / {requiredExp}";
        }
    }

    /// <summary>
    /// Refresh all property texts with current data values.
    /// Called when level changes since growth (B) depends on level.
    /// </summary>
    private void RefreshAllPropertyTexts()
    {
        if (propertyTexts == null) return;

        for (int i = 0; i < propertyTexts.Length; i++)
        {
            UpdatePropertyTextAtIndex(i);
        }
    }

    /// <summary>
    /// Reset all temporary changes when the panel is disabled/destroyed.
    /// </summary>
    private void OnDisable()
    {
        // Unsubscribe from cultivation data events
        if (cultivationData != null)
        {
            cultivationData.OnLevelUp -= HandleLevelUp;
            cultivationData.OnExpChanged -= HandleExpChanged;
        }

        if (talentControllers == null) return;

        for (int i = 0; i < talentControllers.Length; i++)
        {
            if (talentControllers[i] != null)
            {
                talentControllers[i].ResetTempChanges();
            }
        }
        UpdateTalentPointText();
    }

    /// <summary>
    /// Re-subscribe to events when the panel is re-enabled.
    /// </summary>
    private void OnEnable()
    {
        // Re-subscribe if cultivationData is already initialized
        if (cultivationData != null)
        {
            cultivationData.OnLevelUp -= HandleLevelUp; // Prevent double subscription
            cultivationData.OnExpChanged -= HandleExpChanged;
            cultivationData.OnLevelUp += HandleLevelUp;
            cultivationData.OnExpChanged += HandleExpChanged;

            // Refresh all texts in case data changed while panel was hidden
            availableTalentPoints = cultivationData.talentPoints;
            UpdateLevelText();
            UpdateTalentPointText();
            UpdateExperienceText();
            RefreshAllPropertyTexts();
        }

        // Refresh evolution display on re-enable
        UpdateEvolutionRequirementDisplay();
        UpdateEvolutionButtonState();
    }

    #region Evolution System

    /// <summary>
    /// Initialize the evolution system: find RequirmentFrame text and EvolutionButton,
    /// bind events, and display initial state.
    /// </summary>
    private void InitializeEvolutionSystem()
    {
        // Find RequirmentFrame and its Text child
        Transform requirementFrame = transform.Find("RequirmentFrame");
        if (requirementFrame != null)
        {
            Transform textNode = requirementFrame.Find("Text (Legacy)");
            if (textNode != null)
            {
                requirementText = textNode.GetComponent<Text>();
            }
            if (requirementText == null)
            {
                // Fallback: try GetComponentInChildren
                requirementText = requirementFrame.GetComponentInChildren<Text>();
            }
        }
        else
        {
            Debug.LogWarning("TrainViewController: Could not find 'RequirmentFrame' node.");
        }

        // Find EvolutionButton
        Transform evolutionButtonNode = transform.Find("EvolutionButton");
        if (evolutionButtonNode != null)
        {
            evolutionButton = evolutionButtonNode.GetComponent<Button>();
            if (evolutionButton != null)
            {
                evolutionButton.onClick.AddListener(OnEvolutionButtonClicked);
            }
        }
        else
        {
            Debug.LogWarning("TrainViewController: Could not find 'EvolutionButton' node.");
        }

        // Initial display
        UpdateEvolutionRequirementDisplay();
        UpdateEvolutionButtonState();
    }

    /// <summary>
    /// Update the RequirmentFrame text to show evolution requirement statuses.
    /// Each requirement is displayed as: "[description]: currentValue/targetValue ✓/✗"
    /// Uses rich text for color coding (green = met, red = not met).
    /// </summary>
    private void UpdateEvolutionRequirementDisplay()
    {
        if (requirementText == null) return;

        // No character data
        if (characterData == null)
        {
            requirementText.text = "";
            return;
        }

        // No evolution target configured
        if (characterData.evolutionTarget == null)
        {
            requirementText.text = "无可用进化";
            return;
        }

        // Already evolved
        if (hasEvolved)
        {
            requirementText.text = "<color=#00FF00>已进化</color>";
            return;
        }

        // No requirements = unconditional
        if (characterData.evolutionRequirements == null || characterData.evolutionRequirements.Length == 0)
        {
            requirementText.text = "<color=#00FF00>可以进化！</color>";
            return;
        }

        // Build requirement display text
        List<EvolutionRequirementStatus> statuses = EvolutionChecker.CheckAllRequirements(characterData);
        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        for (int i = 0; i < statuses.Count; i++)
        {
            var status = statuses[i];
            string desc = !string.IsNullOrEmpty(status.requirement.description)
                ? status.requirement.description
                : GetDefaultDescription(status.requirement);

            string color = status.isMet ? "#00FF00" : "#FF4444";
            string mark = status.isMet ? "✓" : "✗";

            sb.Append($"<color={color}>{mark} {desc}: {status.currentValue}/{status.requirement.targetValue}</color>");

            if (i < statuses.Count - 1)
            {
                sb.Append("\n");
            }
        }

        requirementText.text = sb.ToString();
    }

    /// <summary>
    /// Get a default description for a requirement if none is configured.
    /// </summary>
    private string GetDefaultDescription(EvolutionRequirement req)
    {
        switch (req.type)
        {
            case EvolutionRequirementType.Level:
                return $"等级达到{req.targetValue}";
            case EvolutionRequirementType.Material:
                return $"材料[{req.materialId}]x{req.targetValue}";
            case EvolutionRequirementType.KillCount:
                return $"击杀数达到{req.targetValue}";
            default:
                return "未知条件";
        }
    }

    /// <summary>
    /// Update the EvolutionButton interactable state based on requirement checks.
    /// </summary>
    private void UpdateEvolutionButtonState()
    {
        if (evolutionButton == null) return;

        // No evolution target or already evolved
        if (characterData == null || characterData.evolutionTarget == null || hasEvolved)
        {
            evolutionButton.interactable = false;
            return;
        }

        // Check if all requirements are met
        evolutionButton.interactable = EvolutionChecker.AreAllRequirementsMet(characterData);
    }

    /// <summary>
    /// Handle EvolutionButton click: perform evolution if all conditions are met.
    /// Adds the evolution target as a new character card and consumes required materials.
    /// </summary>
    private void OnEvolutionButtonClicked()
    {
        if (characterData == null || characterData.evolutionTarget == null) return;

        // Double-check requirements
        if (!EvolutionChecker.AreAllRequirementsMet(characterData))
        {
            Debug.LogWarning("[TrainViewController] Evolution requirements not met.");
            return;
        }

        // Find CardSplineDistributor to add the new character
        CardSplineDistributor cardDistributor = FindObjectOfType<CardSplineDistributor>();
        if (cardDistributor == null)
        {
            Debug.LogError("[TrainViewController] CardSplineDistributor not found! Cannot perform evolution.");
            return;
        }

        // Attempt to add the evolution target to the card list
        CharacterData evolutionTarget = characterData.evolutionTarget;
        bool added = cardDistributor.AddCharacterData(evolutionTarget);

        if (!added)
        {
            Debug.LogWarning($"[TrainViewController] Evolution target '{evolutionTarget.characterName}' already exists in card list.");
            // Update display to reflect that evolution target already exists
            hasEvolved = true;
            UpdateEvolutionRequirementDisplay();
            UpdateEvolutionButtonState();
            return;
        }

        // Consume materials for Material-type requirements
        if (characterData.evolutionRequirements != null)
        {
            foreach (var req in characterData.evolutionRequirements)
            {
                if (req != null && req.type == EvolutionRequirementType.Material)
                {
                    MaterialManager.Instance.ConsumeMaterial(req.materialId, req.targetValue);
                }
            }
        }

        // Mark as evolved and update UI
        hasEvolved = true;
        UpdateEvolutionRequirementDisplay();
        UpdateEvolutionButtonState();

        Debug.Log($"[TrainViewController] Evolution successful! '{characterData.characterName}' evolved into '{evolutionTarget.characterName}'.");
    }

    #endregion
}
