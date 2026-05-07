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
    /// 3. For skill animation clips:
    ///    - Open the Animation window, select the skill clip.
    ///    - Add an Animation Event at the hit frame.
    ///    - Set Function to "OnSkillHit" (no parameters).
    ///    - The skill index is read from CombatSystem's cached state.
    /// 4. For lock-on displacement skills:
    ///    - Add an Animation Event at the frame BEFORE the hit frame (where dash should start).
    ///    - Set Function to "OnSkillLockTarget" (no parameters).
    ///    - This locks the target's position and starts moving toward it.
    /// 5. For any state animation that should auto-exit after playing once (Attack, Skill, Hit):
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