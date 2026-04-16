using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Receives Unity Animation Events from attack/skill animation clips
    /// and delegates damage application to CombatSystem.
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
    /// </summary>
    [RequireComponent(typeof(CombatSystem))]
    public class AnimEventReceiver : MonoBehaviour
    {
        private CombatSystem combatSystem;

        private void Awake()
        {
            combatSystem = GetComponent<CombatSystem>();
        }

        /// <summary>
        /// Called by Unity Animation Event on the normal attack hit frame.
        /// </summary>
        public void OnAttackHit()
        {
            if (combatSystem == null)
            {
                Debug.LogWarning($"[AnimEventReceiver] {gameObject.name}: CombatSystem is null, skipping OnAttackHit.");
                return;
            }

            if (!combatSystem.IsAttacking)
            {
                Debug.Log($"[AnimEventReceiver] {gameObject.name}: OnAttackHit called but not in attacking state, skipping.");
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
            if (combatSystem == null)
            {
                Debug.LogWarning($"[AnimEventReceiver] {gameObject.name}: CombatSystem is null, skipping OnSkillHit.");
                return;
            }

            if (!combatSystem.IsAttacking)
            {
                Debug.Log($"[AnimEventReceiver] {gameObject.name}: OnSkillHit called but not in attacking state, skipping.");
                return;
            }

            combatSystem.ApplySkillDamage();
        }
    }
}