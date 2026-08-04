using System;
using UnityEngine;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// Controls a single TalentSystem panel's interaction logic.
    /// Handles PlusButton, MinusButton, and SquareFrame (confirm) button interactions.
    /// Manages temporary talent level changes and confirmation.
    /// </summary>
    public class TalentSystemController : MonoBehaviour
    {
        private AttributeType attributeType;
        private int confirmedTalentLevel;
        private int tempTalentLevel;

        private Button plusButton;
        private Button minusButton;
        private Button confirmButton;
        private Text squareFrameText;

        // Reference to get/set available talent points
        private Func<int> getAvailableTalentPoints;
        private Action<int> setAvailableTalentPoints;

        // Callback to notify parent controller to update property text
        private Action<AttributeType, int> onTalentLevelChanged;
        // Callback to notify parent controller when talent is confirmed
        private Action<AttributeType, int> onTalentConfirmed;

        /// <summary>
        /// Initialize this TalentSystem controller with required references and callbacks.
        /// </summary>
        /// <param name="type">The attribute type this panel controls.</param>
        /// <param name="currentTalentLevel">The current confirmed talent level from CultivationData.</param>
        /// <param name="getAvailablePoints">Function to get current available talent points.</param>
        /// <param name="setAvailablePoints">Action to set available talent points (for temp adjustments).</param>
        /// <param name="onLevelChanged">Callback when temp talent level changes (for preview updates).</param>
        /// <param name="onConfirmed">Callback when talent is confirmed (for final updates).</param>
        public void Initialize(
            AttributeType type,
            int currentTalentLevel,
            Func<int> getAvailablePoints,
            Action<int> setAvailablePoints,
            Action<AttributeType, int> onLevelChanged,
            Action<AttributeType, int> onConfirmed)
        {
            attributeType = type;
            confirmedTalentLevel = currentTalentLevel;
            tempTalentLevel = currentTalentLevel;
            getAvailableTalentPoints = getAvailablePoints;
            setAvailableTalentPoints = setAvailablePoints;
            onTalentLevelChanged = onLevelChanged;
            onTalentConfirmed = onConfirmed;

            BindUIElements();
            UpdateSquareFrameText();
        }

        /// <summary>
        /// Find and bind the PlusButton, MinusButton, and SquareFrame UI elements.
        /// </summary>
        private void BindUIElements()
        {
            // Find PlusButton
            Transform plusTrans = transform.Find("PlusButton");
            if (plusTrans != null)
            {
                plusButton = plusTrans.GetComponent<Button>();
                if (plusButton != null)
                    plusButton.onClick.AddListener(OnPlusButtonClicked);
            }
            else
            {
                Debug.LogWarning($"[TalentSystemController] Could not find 'PlusButton' in {gameObject.name}");
            }

            // Find MinusButton
            Transform minusTrans = transform.Find("MinusButton");
            if (minusTrans != null)
            {
                minusButton = minusTrans.GetComponent<Button>();
                if (minusButton != null)
                    minusButton.onClick.AddListener(OnMinusButtonClicked);
            }
            else
            {
                Debug.LogWarning($"[TalentSystemController] Could not find 'MinusButton' in {gameObject.name}");
            }

            // Find SquareFrame (confirm button with text)
            Transform squareTrans = transform.Find("SquareFrame");
            if (squareTrans != null)
            {
                confirmButton = squareTrans.GetComponent<Button>();
                if (confirmButton != null)
                    confirmButton.onClick.AddListener(OnConfirmButtonClicked);

                // Find text component in SquareFrame (use true to include inactive children)
                squareFrameText = squareTrans.GetComponentInChildren<Text>(true);
            }
            else
            {
                Debug.LogWarning($"[TalentSystemController] Could not find 'SquareFrame' in {gameObject.name}");
            }
        }

        /// <summary>
        /// Handle PlusButton click: increase temp talent level by 1.
        /// </summary>
        private void OnPlusButtonClicked()
        {
            int availablePoints = getAvailableTalentPoints?.Invoke() ?? 0;
            if (availablePoints <= 0) return;

            tempTalentLevel++;
            setAvailableTalentPoints?.Invoke(availablePoints - 1);

            UpdateSquareFrameText();
            onTalentLevelChanged?.Invoke(attributeType, tempTalentLevel);
        }

        /// <summary>
        /// Handle MinusButton click: decrease temp talent level by 1 and return talent point.
        /// </summary>
        private void OnMinusButtonClicked()
        {
            // Cannot go below confirmed level (or below 0)
            if (tempTalentLevel <= confirmedTalentLevel || tempTalentLevel <= 0) return;

            tempTalentLevel--;
            int availablePoints = getAvailableTalentPoints?.Invoke() ?? 0;
            setAvailableTalentPoints?.Invoke(availablePoints + 1);

            UpdateSquareFrameText();
            onTalentLevelChanged?.Invoke(attributeType, tempTalentLevel);
        }

        /// <summary>
        /// Handle SquareFrame (confirm) button click: commit talent changes.
        /// </summary>
        private void OnConfirmButtonClicked()
        {
            // No changes to confirm
            if (tempTalentLevel == confirmedTalentLevel) return;

            int oldConfirmed = confirmedTalentLevel;
            confirmedTalentLevel = tempTalentLevel;

            onTalentConfirmed?.Invoke(attributeType, confirmedTalentLevel);
        }

        /// <summary>
        /// Update the SquareFrame text to show current temp talent level.
        /// </summary>
        private void UpdateSquareFrameText()
        {
            if (squareFrameText != null)
            {
                squareFrameText.text = tempTalentLevel.ToString();
            }
        }

        /// <summary>
        /// Reset temporary changes back to confirmed state.
        /// Called when the panel is closed without confirming.
        /// </summary>
        public void ResetTempChanges()
        {
            if (tempTalentLevel != confirmedTalentLevel)
            {
                // Return the difference in talent points
                int diff = tempTalentLevel - confirmedTalentLevel;
                if (diff > 0)
                {
                    int availablePoints = getAvailableTalentPoints?.Invoke() ?? 0;
                    setAvailableTalentPoints?.Invoke(availablePoints + diff);
                }

                tempTalentLevel = confirmedTalentLevel;
                UpdateSquareFrameText();
                onTalentLevelChanged?.Invoke(attributeType, tempTalentLevel);
            }
        }

        /// <summary>
        /// Get the current temporary talent level (for preview calculations).
        /// </summary>
        public int TempTalentLevel => tempTalentLevel;

        /// <summary>
        /// Get the confirmed talent level.
        /// </summary>
        public int ConfirmedTalentLevel => confirmedTalentLevel;

        private void OnDestroy()
        {
            if (plusButton != null)
                plusButton.onClick.RemoveListener(OnPlusButtonClicked);
            if (minusButton != null)
                minusButton.onClick.RemoveListener(OnMinusButtonClicked);
            if (confirmButton != null)
                confirmButton.onClick.RemoveListener(OnConfirmButtonClicked);
        }
    }
}
