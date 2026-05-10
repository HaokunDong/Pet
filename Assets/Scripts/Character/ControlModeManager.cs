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
    /// Handles click detection, flash effect, button panel display, and mode transitions.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    [RequireComponent(typeof(FlashEffect))]
    [RequireComponent(typeof(Collider2D))]
    public class ControlModeManager : MonoBehaviour
    {
        [Header("UI Settings")]
        [Tooltip("Offset from character position to place the button panel")]
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

        // Button Panel UI
        private GameObject panelInstance;
        private Canvas sceneCanvas;
        private bool isButtonShowing;

        // Three button references
        private Button changeCharacterBtn;
        private Button trainBtn;
        private Button controlBtn;

        // Cached reference to the card selection system
        private CardSplineDistributor cardDistributor;

        // Cached reference to the cultivation panel
        private CultivationPanelUI cultivationPanel;

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
            flashEffect = GetComponent<FlashEffect>();
            aiController = GetComponent<AIController>();
            manualController = GetComponent<ManualController>();

            // Hide the manual skill bar when this character dies (only if it was bound to us).
            if (entity != null)
            {
                entity.OnDeath += HandleCharacterDeath;
            }
        }

        /// <summary>
        /// Hide the manual skill bar when the owning character dies, but only if
        /// the bar is currently bound to this character.
        /// </summary>
        private void HandleCharacterDeath(CharacterEntity dead)
        {
            if (ManualSkillBarUI.Instance == null) return;
            if (dead != entity) return;
            ManualSkillBarUI.Instance.Unbind();
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
        /// Handle character being clicked: flash effect → pause → show button panel.
        /// </summary>
        private void HandleCharacterClicked()
        {
            // Ignore if flash is playing (anti-repeat)
            if (flashEffect != null && flashEffect.IsFlashing) return;

            // If button panel is already showing, dismiss it
            if (isButtonShowing)
            {
                DismissButton();
                return;
            }

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

            // Show the button panel
            ShowButton();
        }

        private void Update()
        {
            // Check for click on non-button area to dismiss button panel
            if (isButtonShowing && Input.GetMouseButtonDown(0))
            {
                // Check if the click is NOT on the panel or the character
                if (!IsClickOnButton() && !IsClickOnCharacter())
                {
                    DismissButton();
                }
            }

            // Keep panel position following the character
            if (isButtonShowing)
            {
                UpdatePanelPosition();
            }
        }

        /// <summary>
        /// Show the CharacterButtonPanel on the side of the character.
        /// </summary>
        private void ShowButton()
        {
            if (panelInstance == null)
            {
                CreateButtonUI();
            }

            if (panelInstance == null) return; // Failed to create

            panelInstance.SetActive(true);
            isButtonShowing = true;
            CurrentMode = ControlMode.Paused_ShowingButton;

            // While the action menu is up, hide the manual skill bar so the player
            // doesn't see/use stale skill input. It will be re-shown if Manual mode resumes.
            HideManualSkillBarIfBoundToMe();
        }

        /// <summary>
        /// Dismiss the button panel and restore previous mode.
        /// </summary>
        private void DismissButton()
        {
            HideButton();

            // Restore previous mode
            SetMode(previousMode);
        }

        /// <summary>
        /// Handle the ChangeCharacter button being clicked.
        /// Opens the card selection panel.
        /// </summary>
        private void OnChangeCharacterClicked()
        {
            DismissButton();

            // Find and toggle card selection visibility
            if (cardDistributor == null)
                cardDistributor = FindObjectOfType<CardSplineDistributor>();

            if (cardDistributor != null)
                cardDistributor.ToggleVisibility();
        }

        /// <summary>
        /// Handle the Train button being clicked.
        /// Opens the cultivation panel for this character.
        /// </summary>
        private void OnTrainClicked()
        {
            HideButton();

            // Restore previous mode immediately so the character keeps moving
            SetMode(previousMode);

            // Find or create the cultivation panel
            if (cultivationPanel == null)
            {
                cultivationPanel = FindObjectOfType<CultivationPanelUI>(true);

                // If not found in scene, try to instantiate from prefab
                if (cultivationPanel == null)
                {
                    GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/CultivationPanel");
                    if (prefab != null && sceneCanvas != null)
                    {
                        GameObject panelObj = Instantiate(prefab, sceneCanvas.transform);
                        cultivationPanel = panelObj.GetComponent<CultivationPanelUI>();
                    }
                }
            }

            if (cultivationPanel != null)
            {
                cultivationPanel.Open(entity, this);
            }
        }

        /// <summary>
        /// Called by CultivationPanelUI when the panel is closed.
        /// Currently no-op since mode is restored when panel opens.
        /// </summary>
        public void OnCultivationPanelClosed()
        {
            // Mode already restored in OnTrainClicked, nothing to do here
        }

        /// <summary>
        /// Handle the Control button being clicked.
        /// Toggles between manual control mode and AI auto mode.
        /// </summary>
        private void OnControlClicked()
        {
            HideButton();

            // If previously in manual mode, return to AI auto; otherwise enter manual mode
            if (previousMode == ControlMode.Manual)
                SetMode(ControlMode.AI_Auto);
            else
                SetMode(ControlMode.Manual);
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
                    HideManualSkillBarIfBoundToMe();
                    break;

                case ControlMode.Manual:
                    if (aiController != null) aiController.PauseAI();
                    if (manualController != null) manualController.SetActive(true);
                    ShowManualSkillBar();
                    break;

                case ControlMode.Paused_ShowingButton:
                    // Both paused, handled by PauseCurrentBehavior
                    HideManualSkillBarIfBoundToMe();
                    break;
            }
        }

        /// <summary>
        /// Ensure the manual skill bar exists and bind it to this character.
        /// </summary>
        private void ShowManualSkillBar()
        {
            ManualSkillBarUI bar = EnsureManualSkillBar();
            if (bar != null)
            {
                bar.BindCharacter(entity);
            }
        }

        /// <summary>
        /// If the manual skill bar is currently bound to this character, unbind it (hide).
        /// Leaves it alone if another character has taken it over (race during fast switching).
        /// </summary>
        private void HideManualSkillBarIfBoundToMe()
        {
            if (ManualSkillBarUI.Instance == null) return;
            // Calling Unbind unconditionally is safe because at most one character can be Manual at a time.
            ManualSkillBarUI.Instance.Unbind();
        }

        /// <summary>
        /// Find or instantiate the global ManualSkillBarUI singleton in the scene Canvas.
        /// </summary>
        private ManualSkillBarUI EnsureManualSkillBar()
        {
            if (ManualSkillBarUI.Instance != null)
                return ManualSkillBarUI.Instance;

            // Try to find a pre-existing instance in the scene (in case it was placed manually).
            ManualSkillBarUI existing = FindObjectOfType<ManualSkillBarUI>(true);
            if (existing != null)
                return existing;

            // Otherwise instantiate from Resources.
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/ManualSkillBar");
            if (prefab == null)
            {
                Debug.LogWarning("ControlModeManager: Failed to load ManualSkillBar prefab from Resources/Prefabs/UI/ManualSkillBar");
                return null;
            }

            if (sceneCanvas == null)
                sceneCanvas = FindObjectOfType<Canvas>();

            if (sceneCanvas == null)
            {
                Debug.LogWarning("ControlModeManager: No Canvas found in scene; cannot host ManualSkillBar");
                return null;
            }

            GameObject go = Instantiate(prefab, sceneCanvas.transform);
            return go.GetComponent<ManualSkillBarUI>();
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
        /// Hide the button panel UI.
        /// </summary>
        private void HideButton()
        {
            if (panelInstance != null)
            {
                panelInstance.SetActive(false);
            }
            isButtonShowing = false;
        }

        /// <summary>
        /// Create the button panel UI by loading CharacterButtonPanel prefab into the scene Canvas.
        /// </summary>
        private void CreateButtonUI()
        {
            // Load the CharacterButtonPanel prefab
            GameObject prefab = Resources.Load<GameObject>("Prefabs/UI/CharacterButtonPanel");
            if (prefab == null)
            {
                Debug.LogWarning("ControlModeManager: Failed to load CharacterButtonPanel prefab from Resources/Prefabs/UI/CharacterButtonPanel");
                return;
            }

            // Find the existing scene Canvas
            if (sceneCanvas == null)
                sceneCanvas = FindObjectOfType<Canvas>();

            if (sceneCanvas == null)
            {
                Debug.LogWarning("ControlModeManager: No Canvas found in scene");
                return;
            }

            // Instantiate the panel prefab directly under the scene Canvas
            panelInstance = Instantiate(prefab, sceneCanvas.transform);

            // Get button references by finding child objects
            Transform changeCharacterTrans = panelInstance.transform.Find("ChangeCharacter");
            Transform trainTrans = panelInstance.transform.Find("Train");
            Transform controlTrans = panelInstance.transform.Find("Control");

            if (changeCharacterTrans != null)
                changeCharacterBtn = changeCharacterTrans.GetComponent<Button>();
            if (trainTrans != null)
                trainBtn = trainTrans.GetComponent<Button>();
            if (controlTrans != null)
                controlBtn = controlTrans.GetComponent<Button>();

            // Bind click events
            if (changeCharacterBtn != null)
                changeCharacterBtn.onClick.AddListener(OnChangeCharacterClicked);
            if (trainBtn != null)
                trainBtn.onClick.AddListener(OnTrainClicked);
            if (controlBtn != null)
                controlBtn.onClick.AddListener(OnControlClicked);

            panelInstance.SetActive(false);
        }

        /// <summary>
        /// Update the panel position to follow the character in screen space.
        /// </summary>
        private void UpdatePanelPosition()
        {
            if (panelInstance == null || sceneCanvas == null) return;

            Camera cam = Camera.main;
            if (cam == null) return;

            // Convert character world position (with offset) to screen position
            Vector3 worldPos = transform.position + new Vector3(buttonOffset.x, buttonOffset.y, 0f);
            Vector2 screenPos = cam.WorldToScreenPoint(worldPos);

            // Convert screen position to canvas local position
            RectTransform canvasRect = sceneCanvas.GetComponent<RectTransform>();
            RectTransform panelRect = panelInstance.GetComponent<RectTransform>();

            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screenPos, sceneCanvas.worldCamera, out localPoint);

            panelRect.anchoredPosition = localPoint;
        }

        /// <summary>
        /// Check if the current mouse click is on the button panel.
        /// </summary>
        private bool IsClickOnButton()
        {
            if (!isButtonShowing) return false;

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
            if (changeCharacterBtn != null)
                changeCharacterBtn.onClick.RemoveAllListeners();
            if (trainBtn != null)
                trainBtn.onClick.RemoveAllListeners();
            if (controlBtn != null)
                controlBtn.onClick.RemoveAllListeners();

            if (entity != null)
            {
                entity.OnDeath -= HandleCharacterDeath;
            }
        }
    }
}
