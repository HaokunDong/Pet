using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: execute a normal attack on the current target.
    /// Returns Running while in combat (including cooldown), Failure if target lost or out of range.
    /// </summary>
    public class BTAttack : BTNode
    {
        private readonly BTContext context;
        private float lastAttackTime = -999f;

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

            // Always face the target while in combat
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.FaceTowards(target.transform.position);
            }

            // Check attack speed cooldown
            float attackInterval = 1f / owner.RuntimeStats.attackSpeed;
            if (Time.time - lastAttackTime < attackInterval)
            {
                // During cooldown — play idle animation and wait
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.PlayIdle();
                }
                return BTState.Running;
            }

            // Perform attack
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayAttack();
            }

            // Deal damage
            target.TakeDamage(owner.RuntimeStats.attackPower);
            lastAttackTime = Time.time;

            // Return Running to stay in combat — don't reset the behavior tree
            return BTState.Running;
        }
    }
}
