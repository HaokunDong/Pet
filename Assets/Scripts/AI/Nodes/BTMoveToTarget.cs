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

        /// <summary>
        /// Minimum stopping distance to prevent entities from overlapping.
        /// </summary>
        private const float MIN_STOPPING_DISTANCE = 0.3f;

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

            // Compute facing sign for multi-shape range check
            float facingSign = target.transform.position.x >= owner.transform.position.x ? 1f : -1f;
            if (owner.CharAnimator != null)
                facingSign = owner.CharAnimator.FacingDirection;

            // If already in combat (attacking), don't interfere with attack animation
            if (context.IsInCombat)
            {
                return BTState.Success;
            }

            // Already in attack range — stop and switch to idle
            if (owner.RuntimeStats.IsTargetInAttackRange(
                    owner.transform.position, facingSign, target.transform.position))
            {
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.FaceTowards(target.transform.position);
                    owner.CharAnimator.PlayIdle();
                }
                return BTState.Success;
            }

            // Too close to target — prevent overlap, stop moving
            if (dist <= MIN_STOPPING_DISTANCE)
            {
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.FaceTowards(target.transform.position);
                    owner.CharAnimator.PlayIdle();
                }
                return BTState.Success;
            }

            // Determine movement direction
            float direction = target.transform.position.x > owner.transform.position.x ? 1f : -1f;

            // Wall detection: raycast ahead in movement direction
            // Use y + 0.5f (character center height) to avoid hitting ground collider
            Vector2 rayOrigin = new Vector2(owner.transform.position.x, owner.transform.position.y + 0.5f);
            Vector2 rayDir = new Vector2(direction, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDir, context.WallDetectDistance, context.TerrainLayerMask);

            if (hit.collider != null)
            {
                // Wall detected ahead — cannot reach target, fallback to patrol
                return BTState.Failure;
            }

            // Move towards target
            float speed = owner.RuntimeStats.moveSpeed;
            Vector3 pos = owner.transform.position;
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
