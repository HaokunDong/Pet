using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the TalentSystem panels in TrainView.
/// Each Property button toggles its corresponding TalentSystem panel,
/// ensuring only one is active at a time.
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

    private GameObject currentActiveTalentSystem;

    private void Start()
    {
        InitializeTalentSystems();
    }

    private void InitializeTalentSystems()
    {
        // Find the Property container node
        Transform propertyContainer = transform.Find("Property");
        if (propertyContainer == null)
        {
            Debug.LogWarning("TrainViewController: Could not find 'Property' container node.");
            return;
        }

        // Iterate through each property and set up its TalentSystem
        for (int i = 0; i < PropertyNames.Length; i++)
        {
            string propertyName = PropertyNames[i];
            string talentSystemName = TalentSystemNames[i];

            // Find the Property node
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

            // Hide all TalentSystem panels by default
            talentSystemObj.SetActive(false);

            // Bind button click event on the Property node
            Button propertyButton = propertyNode.GetComponent<Button>();
            if (propertyButton == null)
            {
                Debug.LogWarning($"TrainViewController: Property node '{propertyName}' does not have a Button component.");
                continue;
            }

            // Capture local variable for closure
            GameObject capturedTalentSystem = talentSystemObj;
            propertyButton.onClick.AddListener(() => OnPropertyClicked(capturedTalentSystem));
        }
    }

    private void OnPropertyClicked(GameObject talentSystem)
    {
        // If clicking the same property that is already active, close it
        if (currentActiveTalentSystem == talentSystem)
        {
            currentActiveTalentSystem.SetActive(false);
            currentActiveTalentSystem = null;
            return;
        }

        // Hide the currently active TalentSystem
        if (currentActiveTalentSystem != null)
        {
            currentActiveTalentSystem.SetActive(false);
        }

        // Show the new TalentSystem
        talentSystem.SetActive(true);
        currentActiveTalentSystem = talentSystem;
    }
}
