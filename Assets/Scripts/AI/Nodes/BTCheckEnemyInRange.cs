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

            float dist = Vector2.Distance(owner.transform.position, target.transform.position);
            return dist <= owner.RuntimeStats.attackRange ? BTState.Success : BTState.Failure;
        }
    }
}
