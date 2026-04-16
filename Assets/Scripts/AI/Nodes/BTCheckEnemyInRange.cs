using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Condition node: checks if the current target is within attack range.
    /// Returns Success if in range, Failure otherwise.
    /// </summary>
    public class BTCheckEnemyInRange : BTNode
    {
        private readonly BTContext context;

        public BTCheckEnemyInRange(BTContext context)
        {
            this.context = context;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            CharacterEntity target = context.CurrentTarget;

            if (owner == null || target == null || !target.RuntimeStats.IsAlive)
                return BTState.Failure;

            float facingSign = target.transform.position.x >= owner.transform.position.x ? 1f : -1f;
            if (owner.CharAnimator != null)
                facingSign = owner.CharAnimator.FacingDirection;

            bool inRange = owner.RuntimeStats.IsTargetInAttackRange(
                owner.transform.position, facingSign, target.transform.position);
            return inRange ? BTState.Success : BTState.Failure;
        }
    }
}
