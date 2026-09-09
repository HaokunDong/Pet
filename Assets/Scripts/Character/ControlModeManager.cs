using UnityEngine;
using PetGame.AI;

namespace PetGame
{
    /// <summary>
    /// Control mode state for a character.
    /// </summary>
    public enum ControlMode
    {
        AI_Auto,
        Manual
    }

    /// <summary>
    /// Manages the control mode switching for a player character.
    /// Handles transitions between AI auto mode and manual control mode.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    [RequireComponent(typeof(FlashEffect))]
    [RequireComponent(typeof(Collider2D))]
    public class ControlModeManager : MonoBehaviour
    {
        /// <summary>
        /// Current control mode.
        /// </summary>
        public ControlMode CurrentMode { get; private set; } = ControlMode.AI_Auto;

        private CharacterEntity entity;
        private FlashEffect flashEffect;
        private AIController aiController;
        private ManualController manualController;

        // Cached reference to the scene Canvas (for ManualSkillBar instantiation)
        private Canvas sceneCanvas;

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
            ManualSkillBarUI.Instance.UnbindCharacter(entity);
        }

        private void Start()
        {
            // Start in AI mode
            SetMode(ControlMode.AI_Auto);
        }

        /// <summary>
        /// Explicitly reset to AI_Auto mode. Called by GameCharacterManager for pooled objects
        /// where Start() won't re-execute after SetActive(true).
        /// </summary>
        public void ResetToAIMode()
        {
            SetMode(ControlMode.AI_Auto);
        }

        /// <summary>
        /// Toggle between Manual and AI_Auto control modes.
        /// Called externally (e.g. from MainView's ControlButton on the RingRadialMenu).
        /// </summary>
        public void ToggleControlMode()
        {
            // Trigger flash effect
            if (flashEffect != null)
            {
                flashEffect.TriggerFlash();
            }

            if (CurrentMode == ControlMode.Manual)
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
            ManualSkillBarUI.Instance.UnbindCharacter(entity);
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

        private void OnDisable()
        {
            HideManualSkillBarIfBoundToMe();
        }

        private void OnDestroy()
        {
            if (entity != null)
            {
                entity.OnDeath -= HandleCharacterDeath;
            }
        }
    }
}
