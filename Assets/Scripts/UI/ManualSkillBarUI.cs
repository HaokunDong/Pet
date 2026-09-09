using UnityEngine;
using UnityEngine.UI;

namespace PetGame
{
    /// <summary>
    /// UI driver for the manual-mode skill bar.
    /// Displays up to 4 skill icons for the currently manually-controlled character,
    /// shows cooldown grayout/countdown, and is bound/unbound by ControlModeManager.
    ///
    /// Lifetime is global: a single instance persists once instantiated and is
    /// shown/hidden by Bind/Unbind based on whether any character is in Manual mode.
    /// </summary>
    public class ManualSkillBarUI : MonoBehaviour
    {
        /// <summary>
        /// Single visual slot in the skill bar.
        /// </summary>
        [System.Serializable]
        public class SkillSlot
        {
            [Tooltip("Root GameObject of the slot. Activated/deactivated based on skill availability.")]
            public GameObject root;

            [Tooltip("Image component that displays the skill's icon sprite.")]
            public Image iconImage;

            [Tooltip("Overlay image used for cooldown grayout. Use Filled type with Radial360 for radial sweep.")]
            public Image cooldownMask;

            [Tooltip("Text component that displays the remaining cooldown in seconds.")]
            public Text cooldownText;
        }

        [Header("Slots (Slot1..Slot4)")]
        [Tooltip("Exactly 4 slots, mapped to skill index 0..3.")]
        public SkillSlot[] slots = new SkillSlot[4];

        /// <summary>
        /// Singleton-ish instance; assigned in Awake. Only one skill bar is expected.
        /// </summary>
        public static ManualSkillBarUI Instance { get; private set; }

        /// <summary>
        /// Currently bound character. Null when nothing is bound.
        /// </summary>
        private CharacterEntity boundCharacter;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Hide on awake until something binds.
            gameObject.SetActive(false);
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>
        /// Bind the skill bar to a specific character and show it.
        /// Refreshes icons/visibility based on the character's CharacterData.
        /// </summary>
        public void BindCharacter(CharacterEntity character)
        {
            boundCharacter = character;

            if (character == null || character.characterData == null)
            {
                Hide();
                return;
            }

            gameObject.SetActive(true);
            RefreshIcons();
            // Force an immediate cooldown visual refresh so we don't show stale data for one frame.
            RefreshCooldownVisuals();
        }

        /// <summary>
        /// Unbind any character and hide the skill bar.
        /// </summary>
        public void Unbind()
        {
            boundCharacter = null;
            Hide();
        }

        public void UnbindCharacter(CharacterEntity character)
        {
            if (boundCharacter == character) Unbind();
        }

        private void Hide()
        {
            // Reset visuals before hiding so next bind starts clean.
            for (int i = 0; i < slots.Length; i++)
            {
                ResetSlotVisuals(slots[i]);
                if (slots[i] != null && slots[i].root != null)
                {
                    slots[i].root.SetActive(false);
                }
            }
            gameObject.SetActive(false);
        }

        /// <summary>
        /// Activate the first N slots and assign their icon sprites from the bound character.
        /// </summary>
        private void RefreshIcons()
        {
            if (boundCharacter == null || boundCharacter.characterData == null) return;

            CharacterData data = boundCharacter.characterData;
            int maxSkills = data.GetMaxSkillCount();
            int skillArrayLen = (data.skills != null) ? data.skills.Length : 0;
            int activeCount = Mathf.Min(maxSkills, skillArrayLen);
            activeCount = Mathf.Clamp(activeCount, 0, slots.Length);

            for (int i = 0; i < slots.Length; i++)
            {
                SkillSlot slot = slots[i];
                if (slot == null || slot.root == null) continue;

                bool active = i < activeCount;
                slot.root.SetActive(active);

                if (!active)
                {
                    ResetSlotVisuals(slot);
                    continue;
                }

                SkillData sd = data.skills[i];
                if (slot.iconImage != null)
                {
                    slot.iconImage.sprite = (sd != null) ? sd.icon : null;
                    // Keep the icon visually present even if no sprite is set;
                    // designers can assign a fallback in the prefab if desired.
                    slot.iconImage.enabled = true;
                }

                ResetSlotVisuals(slot);
            }
        }

        /// <summary>
        /// Hide the cooldown overlay and clear the countdown text.
        /// </summary>
        private void ResetSlotVisuals(SkillSlot slot)
        {
            if (slot == null) return;

            if (slot.cooldownMask != null)
            {
                slot.cooldownMask.gameObject.SetActive(false);
                slot.cooldownMask.fillAmount = 0f;
            }
            if (slot.cooldownText != null)
            {
                slot.cooldownText.text = string.Empty;
                slot.cooldownText.gameObject.SetActive(false);
            }
        }

        private void Update()
        {
            // If the bound character died or got destroyed, auto-unbind.
            if (boundCharacter == null
                || boundCharacter.RuntimeStats == null
                || !boundCharacter.RuntimeStats.IsAlive)
            {
                if (gameObject.activeSelf)
                {
                    Unbind();
                }
                return;
            }

            RefreshCooldownVisuals();
        }

        /// <summary>
        /// Update grayout overlay and countdown text for each active slot
        /// based on RuntimeStats.skillCooldowns.
        /// </summary>
        private void RefreshCooldownVisuals()
        {
            if (boundCharacter == null || boundCharacter.characterData == null) return;
            if (boundCharacter.RuntimeStats == null
                || boundCharacter.RuntimeStats.skillCooldowns == null) return;

            CharacterData data = boundCharacter.characterData;
            int maxSkills = data.GetMaxSkillCount();
            int skillArrayLen = (data.skills != null) ? data.skills.Length : 0;
            int activeCount = Mathf.Min(maxSkills, skillArrayLen);
            activeCount = Mathf.Clamp(activeCount, 0, slots.Length);

            for (int i = 0; i < activeCount; i++)
            {
                SkillSlot slot = slots[i];
                if (slot == null || slot.root == null || !slot.root.activeSelf) continue;

                SkillData sd = data.skills[i];
                if (sd == null)
                {
                    ResetSlotVisuals(slot);
                    continue;
                }

                float remaining = 0f;
                if (boundCharacter.RuntimeStats.skillCooldowns.TryGetValue(i, out float cd))
                {
                    remaining = Mathf.Max(0f, cd);
                }

                if (remaining > 0f)
                {
                    // Show grayout overlay with radial fill = remaining / cooldown.
                    if (slot.cooldownMask != null)
                    {
                        slot.cooldownMask.gameObject.SetActive(true);
                        float baseCd = Mathf.Max(0.01f, sd.cooldown);
                        slot.cooldownMask.fillAmount = Mathf.Clamp01(remaining / baseCd);
                    }
                    if (slot.cooldownText != null)
                    {
                        slot.cooldownText.gameObject.SetActive(true);
                        slot.cooldownText.text = Mathf.CeilToInt(remaining).ToString();
                    }
                }
                else
                {
                    if (slot.cooldownMask != null)
                    {
                        slot.cooldownMask.gameObject.SetActive(false);
                        slot.cooldownMask.fillAmount = 0f;
                    }
                    if (slot.cooldownText != null)
                    {
                        slot.cooldownText.gameObject.SetActive(false);
                        slot.cooldownText.text = string.Empty;
                    }
                }
            }
        }
    }
}
