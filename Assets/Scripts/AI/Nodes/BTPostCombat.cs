using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: drive the PostCombat cooldown phase after a combat encounter ends
    /// (target died or disengaged). The character stays idle for a short window so the
    /// transition back to Wander feels natural rather than instantaneous.
    /// 
    /// Behavior:
    /// - On entry (first tick after switching from Combat), record PostCombatEndTime =
    ///   Time.time + PostCombatDuration and play Idle.
    /// - Every tick: if a living enemy re-enters detectionRange, immediately allow the
    ///   root Selector to fall back to Combat by reporting Failure (so BTFindNearestEnemy
    ///   can pick up the new target on the same frame).
    /// - On timeout, switch CurrentState to Wander and report Failure so Wander takes over.
    /// - While cooling down, report Success to "consume" the tick (keeps PostCombat active).
    /// 
    /// Root tree structure expected:
    ///   Selector(
    ///     Sequence(FindNearestEnemy, Combat),   // highest priority — any valid target
    ///     PostCombat,                           // cooldown right after a fight
    ///     Wander                                // default idle behavior
    ///   )
    /// 
    /// Returning Failure here lets the Selector fall through to Wander (or lets a
    /// newly-found target re-enter Combat on the same tick, since FindNearestEnemy
    /// is evaluated before us).
    /// </summary>
    public class BTPostCombat : BTNode
    {
        private readonly BTContext context;

        public BTPostCombat(BTContext context)
        {
            this.context = context;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            if (owner == null || !owner.RuntimeStats.IsAlive)
                return BTState.Failure;

            // Only activate immediately after leaving a Combat sub-state. If we're not already
            // in PostCombat and we didn't just come from Engage/Strike, don't engage this node.
            if (context.CurrentState != AIState.PostCombat)
            {
                if (context.CurrentState == AIState.Engage || context.CurrentState == AIState.Strike)
                {
                    // Target was lost during combat — enter PostCombat cooldown.
                    context.CurrentState = AIState.PostCombat;
                    context.PostCombatEndTime = Time.time + context.PostCombatDuration;
                    // Reset first-strike flag so the next encounter starts clean.
                    context.HasFiredFirstStrike = false;
                    context.CurrentTarget = null;
                }
                else
                {
                    // Not coming from combat — let Wander handle it.
                    return BTState.Failure;
                }
            }

            // Already in PostCombat: check timeout.
            if (Time.time >= context.PostCombatEndTime)
            {
                context.CurrentState = AIState.Wander;
                return BTState.Failure;
            }

            // Stay idle during cooldown.
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.PlayIdle();
            }

            return BTState.Success;
        }
    }
}
