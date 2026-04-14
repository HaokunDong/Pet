using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: execute a normal attack on the current target.
    /// Returns Success if attack was performed, Failure if conditions not met.
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
                return BTState.Failure;

            // Check if in attack range
            float dist = Vector2.Distance(owner.transform.position, target.transform.position);
            if (dist > owner.RuntimeStats.attackRange)
                return BTState.Failure;

            // Check attack speed cooldown
            float attackInterval = 1f / owner.RuntimeStats.attackSpeed;
            if (Time.time - lastAttackTime < attackInterval)
                return BTState.Running;

            // Face the target
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.FaceTowards(target.transform.position);
                owner.CharAnimator.PlayAttack();
            }

            // Deal damage
            target.TakeDamage(owner.RuntimeStats.attackPower);
            lastAttackTime = Time.time;

            return BTState.Success;
        }
    }
}
