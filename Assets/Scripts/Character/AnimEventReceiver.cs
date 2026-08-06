using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Receives Unity Animation Events from attack/skill/hit animation clips
    /// and delegates damage application to CombatSystem or state clearing to CharacterAnimator.
    ///
    /// [Unity Editor Setup]
    /// 1. Attach this component to the character GameObject (same object as Animator).
    /// 2. For normal attack animation clips:
    ///    - Open the Animation window, select the attack clip.
    ///    - Add an Animation Event at the hit frame.
    ///    - Set Function to "OnAttackHit" (no parameters).
    /// 3. For normal attack with LockOn displacement:
    ///    - Add an Animation Event at the frame BEFORE the hit frame (where dash should start).
    ///    - Set Function to "OnAttackLockTarget" (no parameters).
    ///    - This locks the target's position and starts moving toward it.
    /// 4. For combo attack window (multi-step normal attacks):
    ///    - Add an Animation Event AFTER the hit frame but BEFORE the last frame.
    ///    - Set Function to "OnComboWindowOpen" (no parameters).
    ///    - This opens the combo input window for chaining to the next attack step.
    ///    - Event order: OnAttackHit → OnComboWindowOpen → OnCancellablePoint → OnStateEnd.
    ///    - If not configured, the attack will not chain (backward compatible).
    /// 4b. For animation cancel/skip-frame support:
    ///    - Add an Animation Event AFTER OnComboWindowOpen but BEFORE OnStateEnd.
    ///    - Set Function to "OnCancellablePoint" (no parameters).
    ///    - This marks the animation as cancellable from this frame onward.
    ///    - If combo input was buffered, immediately skips to the next attack step.
    ///    - If not configured, the animation cannot be cancelled (backward compatible).
    /// 5. For skill animation clips:
    ///    - Open the Animation window, select the skill clip.
    ///    - Add an Animation Event at the hit frame.
    ///    - Set Function to "OnSkillHit" (no parameters).
    ///    - The skill index is read from CombatSystem's cached state.
    /// 6. For lock-on displacement skills:
    ///    - Add an Animation Event at the frame BEFORE the hit frame (where dash should start).
    ///    - Set Function to "OnSkillLockTarget" (no parameters).
    ///    - This locks the target's position and starts moving toward it.
    /// 7. For any state animation that should auto-exit after playing once (Attack, Skill, Hit):
    ///    - Add an Animation Event at the last frame of the clip.
    ///    - Set Function to "OnStateEnd" (no parameters).
    /// </summary>
    [RequireComponent(typeof(CombatSystem))]
    [RequireComponent(typeof(CharacterAnimator))]
    public class AnimEventReceiver : MonoBehaviour
    {
        private CombatSystem combatSystem;
        private CharacterAnimator characterAnimator;

        private void Awake()
        {
            combatSystem = GetComponent<CombatSystem>();
            characterAnimator = GetComponent<CharacterAnimator>();
        }

        /// <summary>
        /// Called by Unity Animation Event on the normal attack hit frame.
        /// </summary>
        public void OnAttackHit()
        {
            if (combatSystem == null) return;

            if (!combatSystem.IsAttacking)
            {
                return;
            }

            combatSystem.ApplyNormalAttackDamage();
        }

        /// <summary>
        /// Called by Unity Animation Event on a frame BEFORE the normal attack hit frame.
        /// Used for LockOn displacement: locks the target's position and starts
        /// moving toward it. Add this event to the attack animation clip at the
        /// desired frame where the character should begin dashing toward the target.
        /// Only triggers if CharacterData.attackDisplacementType is LockOn.
        /// </summary>
        public void OnAttackLockTarget()
        {
            if (combatSystem == null) return;

            if (!combatSystem.IsAttacking && (characterAnimator == null || !characterAnimator.IsAttacking))
            {
                return;
            }

            combatSystem.ApplyNormalAttackLockOnDisplacement();
        }

        /// <summary>
        /// Called by Unity Animation Event on the skill hit frame.
        /// The skill index is retrieved from CombatSystem's cached skill index.
        /// </summary>
        public void OnSkillHit()
        {
            if (combatSystem == null) return;

            if (!combatSystem.IsAttacking)
            {
                return;
            }

            combatSystem.ApplySkillDamage();
        }

        /// <summary>
        /// Called by Unity Animation Event on a frame BEFORE the skill hit frame.
        /// Used for lock-on displacement: locks the target's position and starts
        /// moving toward it. Add this event to the skill animation clip at the
        /// desired frame where the character should begin dashing toward the target.
        /// </summary>
        public void OnSkillLockTarget()
        {
            Debug.Log($"[AnimEventReceiver] {gameObject.name}: OnSkillLockTarget() called. IsAttacking={combatSystem?.IsAttacking}, IsInSkillState={characterAnimator?.IsInSkillState}, CachedSkillIndex={combatSystem?.CachedSkillIndex}");

            if (combatSystem == null)
            {
                Debug.LogWarning($"[AnimEventReceiver] {gameObject.name}: OnSkillLockTarget - combatSystem is null!");
                return;
            }

            // Use either CombatSystem.IsAttacking OR state machine's IsInSkillState
            // because IsAttacking may have been cleared by edge cases while the skill animation is still playing
            if (!combatSystem.IsAttacking && !characterAnimator.IsInSkillState)
            {
                Debug.LogWarning($"[AnimEventReceiver] {gameObject.name}: OnSkillLockTarget - neither IsAttacking nor IsInSkillState, skipping.");
                return;
            }

            combatSystem.ApplySkillLockOnDisplacement();
        }

        /// <summary>
        /// Called by Unity Animation Event when the attack animation reaches the combo window frame.
        /// This opens the combo input window, allowing the next attack step to be buffered.
        /// Add this event to attack animation clips at the frame where combo input should be accepted
        /// (typically shortly after the hit frame, before OnStateEnd).
        /// Event order in attack clips: OnAttackHit → OnComboWindowOpen → OnStateEnd.
        /// If this event is not configured in the animation clip, the combo window never opens
        /// and the attack will not chain (backward compatible with single-attack characters).
        /// </summary>
        public void OnComboWindowOpen()
        {
            if (combatSystem == null) return;

            combatSystem.OpenComboWindow();
        }

        /// <summary>
        /// Called by Unity Animation Event when the attack/skill animation reaches the cancellable point.
        /// This marks the animation as cancellable from this frame onward.
        /// If combo input was already buffered, immediately skips remaining frames and chains
        /// to the next attack step (combo acceleration).
        /// If no combo input is buffered, simply marks the animation as cancellable so that
        /// non-attack inputs (move, skill) can trigger an immediate cancel.
        ///
        /// [Unity Editor Setup]
        /// Add an Animation Event at the desired cancellable frame (typically after OnComboWindowOpen,
        /// before OnStateEnd). Set Function to "OnCancellablePoint" (no parameters).
        /// Recommended event order: OnAttackHit → OnComboWindowOpen → OnCancellablePoint → OnStateEnd.
        /// If this event is not configured, the animation cannot be cancelled early (backward compatible).
        /// </summary>
        public void OnCancellablePoint()
        {
            if (combatSystem == null) return;

            combatSystem.HandleCancellablePoint();
        }

        /// <summary>
        /// Called by Unity Animation Event at the last frame of any state animation
        /// (Attack, Skill, Hit) that should auto-exit after playing once.
        /// Clears the current state protection and transitions back to Idle.
        /// </summary>
        public void OnStateEnd()
        {
            if (characterAnimator == null) return;

            characterAnimator.ClearCurrentState();
        }
    }
}