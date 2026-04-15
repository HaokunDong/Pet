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

            // Clear combat state when entering patrol — ensure no stale combat flags
            context.IsInCombat = false;
            context.CurrentTarget = null;

            float speed = owner.RuntimeStats.moveSpeed;
            float direction = context.IsMovingRight ? 1f : -1f;

            // Wall detection: raycast ahead in movement direction
            // Use y + 0.5f (character center height) to avoid hitting ground collider
            Vector2 rayOrigin = new Vector2(owner.transform.position.x, owner.transform.position.y + 0.5f);
            Vector2 rayDir = new Vector2(direction, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDir, context.WallDetectDistance, context.TerrainLayerMask);

            if (hit.collider != null)
            {
                // Wall detected — reverse direction
                context.IsMovingRight = !context.IsMovingRight;
                direction = -direction;
            }

            // Move in current direction
            Vector3 pos = owner.transform.position;
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
