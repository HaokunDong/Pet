using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: execute a normal attack on the current target.
    /// Delegates all attack logic (animation, damage timing) to CombatSystem.
    /// Returns Running while in combat (including cooldown), Failure if target lost or out of range.
    /// </summary>
    public class BTAttack : BTNode
    {
        private readonly BTContext context;
        private CombatSystem combatSystem;

        public BTAttack(BTContext context)
        {
            this.context = context;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            CharacterEntity target = context.CurrentTarget;

            if (owner == null || target == null || !target.RuntimeStats.IsAlive)
            {
                // Target dead or missing — exit combat state
                context.IsInCombat = false;
                return BTState.Failure;
            }

            // Check if in attack range
            float dist = Vector2.Distance(owner.transform.position, target.transform.position);
            if (dist > owner.RuntimeStats.attackRange)
            {
                // Target moved out of range — exit combat state
                context.IsInCombat = false;
                return BTState.Failure;
            }

            // Enter combat state — stop all movement
            context.IsInCombat = true;

            // Lazy-cache CombatSystem reference
            if (combatSystem == null)
                combatSystem = owner.GetComponent<CombatSystem>();

            // If the character is in Hit animation protection period, skip attack and idle
            // to avoid interrupting the hurt feedback animation
            if (owner.CharAnimator != null && owner.CharAnimator.IsInHitState)
            {
                return BTState.Running;
            }

            // Attempt attack via CombatSystem (handles cooldown, animation, and deferred damage)
            bool attacked = combatSystem.TryNormalAttack(target);

            if (!attacked && !combatSystem.IsAttacking)
            {
                // During cooldown and no attack animation playing — play idle and wait
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.PlayIdle();
                }
            }

            // Return Running to stay in combat — don't reset the behavior tree
            return BTState.Running;
        }
    }
}
