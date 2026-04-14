using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: move towards the current target.
    /// Returns Running while moving, Success when within attack range, Failure if no target.
    /// </summary>
    public class BTMoveToTarget : BTNode
    {
        private readonly BTContext context;

        public BTMoveToTarget(BTContext context)
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

            // Already in attack range
            if (dist <= owner.RuntimeStats.attackRange)
                return BTState.Success;

            // Move towards target
            float speed = owner.RuntimeStats.moveSpeed;
            Vector3 pos = owner.transform.position;
            float direction = target.transform.position.x > pos.x ? 1f : -1f;

            pos.x += direction * speed * Time.deltaTime;
            owner.transform.position = pos;

            // Update animation
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayWalk();
                owner.CharAnimator.SetFacingDirection(direction);
            }

            return BTState.Running;
        }
    }
}
