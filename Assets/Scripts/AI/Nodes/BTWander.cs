using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: wander around the PatrolOrigin with natural, random stop-and-go behavior.
    /// 
    /// Behavior:
    /// - Sample a random target X in [PatrolOrigin.x ± PatrolRange]. Walk toward it with PlayWalk.
    /// - After arriving (horizontal distance &lt; 0.1) or timing out (walk segment duration),
    ///   enter a WanderPause idle for a random duration, then re-sample a new target.
    /// - When a wall is detected ahead within WallDetectDistance, drop the current target,
    ///   enter WanderPause, and bias the next sample toward the opposite direction.
    /// 
    /// Always returns Running (continuous behavior); higher-priority Combat node will
    /// preempt this via the root Selector when a target is acquired.
    /// </summary>
    public class BTWander : BTNode
    {
        private const float ArriveEpsilon = 0.1f;

        private readonly BTContext context;

        public BTWander(BTContext context)
        {
            this.context = context;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            if (owner == null || !owner.RuntimeStats.IsAlive)
                return BTState.Failure;

            // If the owner is being knocked back, skip all movement this frame.
            if (owner.Knockback != null && owner.Knockback.IsInKnockback)
            {
                return BTState.Running;
            }

            // First entry / state just switched to Wander: initialize a walk segment.
            if (context.CurrentState != AIState.Wander && context.CurrentState != AIState.WanderPause)
            {
                StartWalkSegment(owner);
            }

            if (context.CurrentState == AIState.WanderPause)
            {
                TickPause(owner);
            }
            else
            {
                TickWalk(owner);
            }

            return BTState.Running;
        }

        // ---------------- Walk segment ----------------

        private void TickWalk(CharacterEntity owner)
        {
            // Wall detection in current movement direction.
            float dx = context.WanderTargetX - owner.transform.position.x;
            float dir = dx >= 0f ? 1f : -1f;

            Vector2 rayOrigin = new Vector2(owner.transform.position.x, owner.transform.position.y + 0.5f);
            Vector2 rayDir = new Vector2(dir, 0f);
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, rayDir, context.WallDetectDistance, context.TerrainLayerMask);
            if (hit.collider != null)
            {
                // Wall ahead: invalidate current target, enter pause, bias reverse next time.
                context.LastWanderDirection = (int)dir;
                context.WanderReverseBias = true;
                EnterPause(owner);
                return;
            }

            // Arrived at target OR walk segment timed out → enter pause.
            if (Mathf.Abs(dx) < ArriveEpsilon || Time.time >= context.WanderPhaseEndTime)
            {
                context.LastWanderDirection = (int)dir;
                context.WanderReverseBias = false;
                EnterPause(owner);
                return;
            }

            // Move horizontally toward target.
            float speed = owner.RuntimeStats.moveSpeed;
            Vector3 pos = owner.transform.position;
            pos.x += dir * speed * Time.deltaTime;
            owner.transform.position = pos;

            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayWalk();
                owner.CharAnimator.SetFacingDirection(dir);
            }

            context.CurrentState = AIState.Wander;
        }

        // ---------------- Pause segment ----------------

        private void TickPause(CharacterEntity owner)
        {
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayIdle();
            }

            if (Time.time >= context.WanderPhaseEndTime)
            {
                StartWalkSegment(owner);
            }
        }

        private void EnterPause(CharacterEntity owner)
        {
            context.CurrentState = AIState.WanderPause;
            float pauseDuration = Random.Range(context.WanderPauseDuration.x, context.WanderPauseDuration.y);
            context.WanderPhaseEndTime = Time.time + pauseDuration;

            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayIdle();
            }
        }

        // ---------------- Sampling ----------------

        /// <summary>
        /// Pick a new random target X and walk duration. Applies reverse bias if last segment hit a wall.
        /// </summary>
        private void StartWalkSegment(CharacterEntity owner)
        {
            float originX = context.PatrolOrigin.x;
            float range = context.PatrolRange;
            float currentX = owner.transform.position.x;

            float minX = originX - range;
            float maxX = originX + range;

            float targetX;
            if (context.WanderReverseBias && context.LastWanderDirection != 0)
            {
                // Bias sample toward the opposite side of LastWanderDirection to avoid hugging the wall.
                if (context.LastWanderDirection > 0)
                {
                    // Was going right and hit a wall: sample from [minX, currentX - epsilon].
                    float hi = Mathf.Max(minX, currentX - ArriveEpsilon);
                    targetX = Random.Range(minX, hi);
                }
                else
                {
                    float lo = Mathf.Min(maxX, currentX + ArriveEpsilon);
                    targetX = Random.Range(lo, maxX);
                }
                context.WanderReverseBias = false;
            }
            else
            {
                targetX = Random.Range(minX, maxX);
                // Ensure we don't pick a target that is basically where we already are.
                if (Mathf.Abs(targetX - currentX) < ArriveEpsilon)
                {
                    targetX = currentX >= originX ? minX : maxX;
                }
            }

            context.WanderTargetX = targetX;
            float walkDuration = Random.Range(context.WanderWalkDuration.x, context.WanderWalkDuration.y);
            context.WanderPhaseEndTime = Time.time + walkDuration;
            context.CurrentState = AIState.Wander;
        }
    }
}
