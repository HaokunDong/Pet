using UnityEngine;
using UnityEngine.UI;
using PetGame.AI;

namespace PetGame
{
    /// <summary>
    /// Control mode state for a character.
    /// </summary>
    public enum ControlMode
    {
        AI_Auto,
        Manual,
        Paused_ShowingButton
    }

    /// <summary>
    /// Manages the control mode switching interaction for a player character.
    /// Handles click detection, flash effect, button display, and mode transitions.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    [RequireComponent(typeof(FlashEffect))]
    [RequireComponent(typeof(Collider2D))]
    public class ControlModeManager : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Button prefab for control mode switching (should contain a Button + Text)")]
        public GameObject buttonPrefab;

        [Tooltip("Offset from character position to place the button (left side)")]
        public Vector2 buttonOffset = new Vector2(-1.5f, 0.5f);

        /// <summary>
        /// Current control mode.
        /// </summary>
        public ControlMode CurrentMode { get; private set; } = ControlMode.AI_Auto;

        /// <summary>
        /// The mode that was active before entering Paused_ShowingButton state.
        /// </summary>
        private ControlMode previousMode = ControlMode.AI_Auto;

        private CharacterEntity entity;
        private FlashEffect flashEffect;
        private AIController aiController;
        private ManualController manualController;

        // Button UI
        private GameObject buttonInstance;
        private Button actionButton;
        private Text buttonText;
        private Canvas worldCanvas;
        private bool isButtonShowing;

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
            flashEffect = GetComponent<FlashEffect>();
            aiController = GetComponent<AIController>();
            manualController = GetComponent<ManualController>();
        }

        private void Start()
        {
            // Start in AI mode
            SetMode(ControlMode.AI_Auto);
        }

        /// <summary>
        /// Called when the character is clicked (via OnMouseDown on Collider2D).
        /// </summary>
        private void OnMouseDown()
        {
            // Only respond to left mouse button
            if (Input.GetMouseButtonDown(0))
            {
                HandleCharacterClicked();
            }
        }

        /// <summary>
        /// Handle character being clicked: flash effect → pause → show button.
        /// </summary>
        private void HandleCharacterClicked()
        {
            // Ignore if flash is playing (anti-repeat)
            if (flashEffect != null && flashEffect.IsFlashing) return;

            // Ignore if button is already showing (click on character while button visible
            // is treated as "non-button area" click, handled in Update)
            if (isButtonShowing) return;

            // Trigger flash effect
            if (flashEffect != null)
            {
                flashEffect.TriggerFlash();
            }

            // Remember current mode before pausing
            previousMode = CurrentMode;

            // Pause current behavior
            PauseCurrentBehavior();

            // Play idle animation
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.PlayIdle();
            }

            // Show the appropriate button
            ShowButton();
        }

        private void Update()
        {
            // Check for click on non-button area to dismiss button
            if (isButtonShowing && Input.GetMouseButtonDown(0))
            {
                // Check if the click is NOT on the button or the character
                if (!IsClickOnButton() && !IsClickOnCharacter())
                {
                    DismissButton();
                }
            }
        }

        /// <summary>
        /// Show the control/exit button on the left side of the character.
        /// </summary>
        private void ShowButton()
        {
            if (buttonInstance == null)
            {
                CreateButtonUI();
            }

            // Set button text based on previous mode
            if (previousMode == ControlMode.AI_Auto)
            {
                buttonText.text = "操控";
            }
            else if (previousMode == ControlMode.Manual)
            {
                buttonText.text = "退出操控";
            }

            buttonInstance.SetActive(true);
            isButtonShowing = true;
            CurrentMode = ControlMode.Paused_ShowingButton;
        }

        /// <summary>
        /// Dismiss the button and restore previous mode.
        /// </summary>
        private void DismissButton()
        {
            HideButton();

            // Restore previous mode
            SetMode(previousMode);
        }

        /// <summary>
        /// Handle the action button being clicked.
        /// </summary>
        private void OnActionButtonClicked()
        {
            HideButton();

            if (previousMode == ControlMode.AI_Auto)
            {
                // Switch to manual control
                SetMode(ControlMode.Manual);
            }
            else if (previousMode == ControlMode.Manual)
            {
                // Switch back to AI auto
                SetMode(ControlMode.AI_Auto);
            }
        }

        /// <summary>
        /// Set the control mode and activate/deactivate corresponding controllers.
        /// </summary>
        private void SetMode(ControlMode mode)
        {
            CurrentMode = mode;

            switch (mode)
            {
                case ControlMode.AI_Auto:
                    if (aiController != null) aiController.ResumeAI();
                    if (manualController != null) manualController.SetActive(false);
                    break;

                case ControlMode.Manual:
                    if (aiController != null) aiController.PauseAI();
                    if (manualController != null) manualController.SetActive(true);
                    break;

                case ControlMode.Paused_ShowingButton:
                    // Both paused, handled by PauseCurrentBehavior
                    break;
            }
        }

        /// <summary>
        /// Pause whatever behavior is currently active.
        /// </summary>
        private void PauseCurrentBehavior()
        {
            if (aiController != null) aiController.PauseAI();
            if (manualController != null) manualController.SetActive(false);
        }

        /// <summary>
        /// Hide the button UI.
        /// </summary>
        private void HideButton()
        {
            if (buttonInstance != null)
            {
                buttonInstance.SetActive(false);
            }
            isButtonShowing = false;
        }

        /// <summary>
        /// Create the button UI using a world-space canvas.
        /// </summary>
        private void CreateButtonUI()
        {
            // Create world canvas
            GameObject canvasObj = new GameObject("ControlButtonCanvas");
            canvasObj.transform.SetParent(transform);
            canvasObj.transform.localPosition = new Vector3(buttonOffset.x, buttonOffset.y, 0f);

            worldCanvas = canvasObj.AddComponent<Canvas>();
            worldCanvas.renderMode = RenderMode.WorldSpace;
            worldCanvas.sortingOrder = 100;

            CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
            scaler.dynamicPixelsPerUnit = 100;

            canvasObj.AddComponent<GraphicRaycaster>();

            RectTransform canvasRect = canvasObj.GetComponent<RectTransform>();
            canvasRect.sizeDelta = new Vector2(2f, 1f);
            canvasRect.localScale = Vector3.one * 0.01f;

            // Create button
            if (buttonPrefab != null)
            {
                buttonInstance = Instantiate(buttonPrefab, canvasObj.transform);
            }
            else
            {
                // Create a simple button programmatically
                buttonInstance = new GameObject("ActionButton");
                buttonInstance.transform.SetParent(canvasObj.transform, false);

                RectTransform btnRect = buttonInstance.AddComponent<RectTransform>();
                btnRect.sizeDelta = new Vector2(160f, 50f);
                btnRect.anchoredPosition = Vector2.zero;

                Image btnImage = buttonInstance.AddComponent<Image>();
                btnImage.color = new Color(0.2f, 0.2f, 0.2f, 0.85f);

                actionButton = buttonInstance.AddComponent<Button>();
                actionButton.targetGraphic = btnImage;

                // Create text
                GameObject textObj = new GameObject("Text");
                textObj.transform.SetParent(buttonInstance.transform, false);

                RectTransform textRect = textObj.AddComponent<RectTransform>();
                textRect.anchorMin = Vector2.zero;
                textRect.anchorMax = Vector2.one;
                textRect.sizeDelta = Vector2.zero;

                buttonText = textObj.AddComponent<Text>();
                buttonText.text = "操控";
                buttonText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                buttonText.fontSize = 28;
                buttonText.color = Color.white;
                buttonText.alignment = TextAnchor.MiddleCenter;
            }

            // Get references if using prefab
            if (actionButton == null)
                actionButton = buttonInstance.GetComponentInChildren<Button>();
            if (buttonText == null)
                buttonText = buttonInstance.GetComponentInChildren<Text>();

            // Bind click event
            actionButton.onClick.AddListener(OnActionButtonClicked);

            buttonInstance.SetActive(false);
        }

        /// <summary>
        /// Check if the current mouse click is on the button.
        /// </summary>
        private bool IsClickOnButton()
        {
            if (!isButtonShowing || actionButton == null) return false;

            // Use EventSystem to check if pointer is over UI
            return UnityEngine.EventSystems.EventSystem.current != null &&
                   UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject();
        }

        /// <summary>
        /// Check if the current mouse click is on this character.
        /// </summary>
        private bool IsClickOnCharacter()
        {
            Vector2 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Collider2D col = GetComponent<Collider2D>();
            if (col != null)
            {
                return col.OverlapPoint(mousePos);
            }
            return false;
        }

        private void OnDestroy()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
            }
        }
    }
}
