using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace PetGame
{
    /// <summary>
    /// Data-binding script for a character card UI prefab.
    /// Attach this to the card prefab root and drag-assign the child UI components
    /// in the Inspector. Call SetData() to populate the card with character info.
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

        /// <summary>
        /// The CharacterData currently bound to this card. Null if no data is set.
        /// </summary>
        public CharacterData Data { get; private set; }

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
                portraitImage.sprite = data.sprite;
                portraitImage.enabled = data.sprite != null;
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
    }
}
