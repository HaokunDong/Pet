using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: patrol back and forth within a range around the origin.
    /// Always returns Running (continuous behavior).
    /// </summary>
    public class BTPatrol : BTNode
    {
        private readonly BTContext context;

        public BTPatrol(BTContext context)
        {
            this.context = context;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            if (owner == null || !owner.RuntimeStats.IsAlive)
                return BTState.Failure;

            float speed = owner.RuntimeStats.moveSpeed;
            Vector3 pos = owner.transform.position;
            float direction = context.IsMovingRight ? 1f : -1f;

            // Move in current direction
            pos.x += direction * speed * Time.deltaTime;
            owner.transform.position = pos;

            // Update animation
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayWalk();
                owner.CharAnimator.SetFacingDirection(direction);
            }

            // Check patrol bounds
            float distFromOrigin = pos.x - context.PatrolOrigin.x;
            if (distFromOrigin > context.PatrolRange)
            {
                context.IsMovingRight = false;
            }
            else if (distFromOrigin < -context.PatrolRange)
            {
                context.IsMovingRight = true;
            }

            return BTState.Running;
        }
    }
}
