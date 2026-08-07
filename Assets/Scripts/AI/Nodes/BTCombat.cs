using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: drive the Combat phase (Engage → Strike) of the AI state machine.
    /// 
    /// Preconditions:
    /// - The root Selector routes the tick to this node only after a valid target has been
    ///   acquired by BTFindNearestEnemy (i.e. <see cref="BTContext.CurrentTarget"/> is alive).
    /// - The Wander/PostCombat → Combat transition (and the TargetWasBehindOnEngage / HasFiredFirstStrike
    ///   initialization) is handled by the target-acquisition node.
    /// 
    /// Behavior:
    /// - Engage sub-state: if distance &gt; engage distance, walk horizontally toward the target.
    ///   Every frame call SetFacingDirection(targetDir); once the facing aligns with the target
    ///   direction, clear TargetWasBehindOnEngage (natural turn during chase).
    /// - Strike sub-state: horizontal velocity = 0, FaceTowards(target); on each tick, if the
    ///   attack cooldown (1/attackSpeed) has elapsed, try skill first (Player/Boss) then normal
    ///   attack. IsAttacking or IsInHitState skips the attack trigger this frame.
    /// - First-strike-behind rule: if TargetWasBehindOnEngage is true and this is the first
    ///   strike of the encounter, spend this frame only turning (no attack) and clear the flag.
    /// - Hysteresis: stay in Strike until distance &gt; engage distance + EngageExitHysteresis.
    /// 
    /// Returns Success when combat should continue (target alive and within reach),
    /// Failure when the target is dead / gone so the root Selector can fall through to PostCombat.
    /// </summary>
    public class BTCombat : BTNode
    {
        /// <summary>Minimum allowed engage distance to prevent complete overlap.</summary>
        private const float MinEngageDistance = 0.1f;

        /// <summary>Horizontal dead-zone for facing direction changes to prevent per-frame flip-flopping
        /// when characters overlap. Works together with CharacterAnimator's FacingChangeCooldown.</summary>
        private const float StrikeFacingDeadzone = 0.15f;

        private readonly BTContext context;
        private CombatSystem combatSystem;
        private KnockbackController knockbackController;
        private SkillDisplacementController displacementController;

        public BTCombat(BTContext context)
        {
            this.context = context;
        }

        /// <summary>
        /// Check if any skill is ready and the target is within that skill's range.
        /// Returns true if the character should enter Strike to use a skill.
        /// </summary>
        private bool IsTargetInSkillRange(CharacterEntity owner, CharacterEntity target)
        {
            if (combatSystem == null) return false;
            CharacterType type = owner.RuntimeStats.characterType;
            if (type != CharacterType.Player && type != CharacterType.Boss) return false;
            if (owner.characterData.skills == null) return false;

            float dist = Vector2.Distance(owner.ColliderCenter, target.ColliderCenter);
            int maxSkills = owner.characterData.GetMaxSkillCount();
            for (int i = 0; i < owner.characterData.skills.Length && i < maxSkills; i++)
            {
                if (!owner.RuntimeStats.IsSkillReady(i)) continue;
                SkillData skill = owner.characterData.skills[i];
                if (skill != null && dist <= skill.skillRange)
                    return true;
            }
            return false;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            CharacterEntity target = context.CurrentTarget;

            if (owner == null || !owner.RuntimeStats.IsAlive)
                return BTState.Failure;

            if (target == null || !target.RuntimeStats.IsAlive)
            {
                // Target lost — let root fall through to PostCombat.
                return BTState.Failure;
            }

            if (combatSystem == null)
                combatSystem = owner.GetComponent<CombatSystem>();

            if (knockbackController == null)
                knockbackController = owner.GetComponent<KnockbackController>();

            if (displacementController == null)
                displacementController = owner.GetComponent<SkillDisplacementController>();

            // If the owner is being knocked back, skip all movement and attack logic this frame.
            if (knockbackController != null && knockbackController.IsInKnockback)
            {
                return BTState.Success;
            }

            // If the owner is being displaced by a skill, skip all movement and attack logic.
            if (displacementController != null && displacementController.IsDisplacing)
            {
                return BTState.Success;
            }

            // If the owner is in skill animation, freeze movement (speed = 0) and skip all logic.
            if (owner.CharAnimator != null && owner.CharAnimator.IsInSkillState)
            {
                return BTState.Success;
            }

            // If the owner is in attack animation (waiting for OnStateEnd), still need to
            // call TickStrike() so that HandleAIComboBuffer() can buffer combo input when
            // the combo window opens. Without this, combo chains never trigger in AI mode.
            if (owner.CharAnimator != null && owner.CharAnimator.IsAttacking)
            {
                // Only handle combo buffer and skill cancel — skip movement/state transitions.
                if (context.CurrentState == AIState.Strike)
                {
                    TickStrike(owner, target);
                }
                return BTState.Success;
            }

            // If the owner is in Hit state, skip all movement and attack logic this frame.
            if (owner.CharAnimator != null && owner.CharAnimator.IsInHitState)
            {
                return BTState.Success;
            }

            float ownerX = owner.transform.position.x;
            float targetColliderDist = RuntimeCharacterStats.GetDistanceToColliderEdge(ownerX, target.CharCollider);
            float engageDist = Mathf.Max(owner.RuntimeStats.GetEngageDistance(), MinEngageDistance);
            float engageExitDist = engageDist + context.EngageExitHysteresis;

            // Use collider-edge-based distance to decide Strike eligibility.
            // Check facing direction to ensure target is in front.
            float dx = target.transform.position.x - ownerX;
            float facingSign;
            if (Mathf.Abs(dx) < StrikeFacingDeadzone && owner.CharAnimator != null)
            {
                facingSign = owner.CharAnimator.FacingDirection;
            }
            else
            {
                facingSign = dx >= 0f ? 1f : -1f;
            }

            // Target is in attack range if:
            // 1. Distance to collider edge + minAttackDistance (buffer) <= attackDistance
            //    This ensures the attack distance extends BEYOND the collider edge by at least minAttackDistance
            //    before the AI triggers an attack, preventing edge-touch-then-stop behavior.
            // 2. Target is in the facing direction (or overlapping)
            float atkDist = owner.RuntimeStats.attackDistance;
            float buffer = owner.RuntimeStats.minAttackDistance;
            bool inAttackRange;
            if (Mathf.Abs(dx) < 0.3f)
            {
                // Overlapping: always consider in range
                inAttackRange = targetColliderDist + buffer <= atkDist;
            }
            else
            {
                bool targetInFront = (facingSign > 0 && dx > 0) || (facingSign < 0 && dx < 0);
                inAttackRange = targetInFront && (targetColliderDist + buffer <= atkDist);
            }

            // Also check if target is within any ready skill's range.
            // This allows the character to enter Strike early to use a ranged skill
            // instead of walking all the way to melee attack range.
            bool inSkillRange = IsTargetInSkillRange(owner, target);

            // Decide Engage vs Strike:
            // - Enter Strike when within engage distance OR actually in attack range OR in skill range.
            // - Exit Strike only when both out of attack range AND beyond engageExitDist AND not in skill range
            //   AND minimum stay duration has elapsed (prevents jitter at boundary).
            if (context.CurrentState == AIState.Strike)
            {
                // Minimum stay in Strike: at least one attack interval to prevent oscillation
                float minStrikeDuration = 0.3f;
                bool minStayElapsed = (Time.time - context.StrikeEnteredTime) >= minStrikeDuration;
                if (minStayElapsed && !inAttackRange && !inSkillRange && targetColliderDist > engageExitDist)
                {
                    context.CurrentState = AIState.Engage;
                }
            }
            else
            {
                // Coming from Wander / PostCombat / Engage.
                // Enter Strike if in attack range OR within engage distance OR in skill range.
                if (inAttackRange || inSkillRange || targetColliderDist <= engageDist)
                {
                    context.CurrentState = AIState.Strike;
                    context.StrikeEnteredTime = Time.time;
                }
                else
                {
                    context.CurrentState = AIState.Engage;
                }
            }

            if (context.CurrentState == AIState.Strike)
            {
                TickStrike(owner, target);
            }
            else
            {
                TickEngage(owner, target);
            }

            return BTState.Success;
        }

        // ---------------- Engage sub-state ----------------

        private void TickEngage(CharacterEntity owner, CharacterEntity target)
        {
            float ownerX = owner.transform.position.x;
            float targetX = target.transform.position.x;
            float dx = targetX - ownerX;
            float absDx = Mathf.Abs(dx);
            float targetColliderDist = RuntimeCharacterStats.GetDistanceToColliderEdge(ownerX, target.CharCollider);
            float engageDist = Mathf.Max(owner.RuntimeStats.GetEngageDistance(), MinEngageDistance);

            // When overlapping (|dx| < deadzone), use current facing to avoid jitter.
            float dir;
            if (absDx < StrikeFacingDeadzone && owner.CharAnimator != null)
            {
                dir = owner.CharAnimator.FacingDirection;
            }
            else
            {
                dir = dx >= 0f ? 1f : -1f;
            }

            // Use the larger minAttackDistance of both combatants to prevent overlap.
            float ownerMinDist = owner.RuntimeStats.minAttackDistance;
            float targetMinDist = target.RuntimeStats != null ? target.RuntimeStats.minAttackDistance : 0f;
            float minDist = Mathf.Max(ownerMinDist, targetMinDist);

            // If already in attack range (with buffer), snap to Strike immediately — no movement.
            // Use collider-edge distance for range check. Buffer ensures AI walks past the edge.
            float atkDist = owner.RuntimeStats.attackDistance;
            float buffer = owner.RuntimeStats.minAttackDistance;
            bool inRange;
            if (absDx < 0.3f)
            {
                inRange = targetColliderDist + buffer <= atkDist;
            }
            else
            {
                bool targetInFront = (dir > 0 && dx > 0) || (dir < 0 && dx < 0);
                inRange = targetInFront && (targetColliderDist + buffer <= atkDist);
            }

            // Also check skill range — if a skill is ready and target is in skill range,
            // enter Strike immediately so the character can use the skill.
            bool inSkillRangeEngage = IsTargetInSkillRange(owner, target);

            if (inRange || inSkillRangeEngage || targetColliderDist <= engageDist)
            {
                context.CurrentState = AIState.Strike;
                context.StrikeEnteredTime = Time.time;
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.PlayIdle();
                    owner.CharAnimator.SetFacingDirection(dir);
                }
                MaybeClearBehindFlagOnFacingAligned(owner, dir);
                return;
            }

            // Wall detection: if a wall blocks our path, stop and play idle, stay in Combat.
            Vector2 rayOrigin = new Vector2(ownerX, owner.transform.position.y + 0.5f);
            RaycastHit2D hit = Physics2D.Raycast(
                rayOrigin, new Vector2(dir, 0f),
                context.WallDetectDistance, context.TerrainLayerMask);

            if (hit.collider != null)
            {
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.PlayIdle();
                    owner.CharAnimator.SetFacingDirection(dir);
                }
                MaybeClearBehindFlagOnFacingAligned(owner, dir);
                return;
            }

            // Walk toward target with overshoot protection.
            // Stop at minAttackDistance to prevent overlap. The character will keep
            // approaching until Execute()'s attack range check triggers Strike.
            float speed = owner.RuntimeStats.moveSpeed;
            float step = speed * Time.deltaTime;
            float stopDist = minDist;
            float maxAllowedStep = absDx - stopDist;

            if (step >= maxAllowedStep)
            {
                // Clamp: place owner exactly at stopDist from target.
                step = Mathf.Max(maxAllowedStep, 0f);
            }

            // If step was clamped to zero, we've reached the stop distance — enter Strike directly.
            bool clampedToStop = (step <= 0.001f && maxAllowedStep <= 0f);

            Vector3 newPos = owner.transform.position;
            newPos.x += dir * step;
            owner.transform.position = newPos;

            // After clamped move, check if we've entered attack range or engage distance → switch to Strike.
            float newOwnerX = newPos.x;
            float newTargetColliderDist = RuntimeCharacterStats.GetDistanceToColliderEdge(newOwnerX, target.CharCollider);
            bool nowInRange;
            float newDx = targetX - newOwnerX;
            if (Mathf.Abs(newDx) < 0.3f)
            {
                nowInRange = newTargetColliderDist + buffer <= atkDist;
            }
            else
            {
                bool targetInFront = (dir > 0 && newDx > 0) || (dir < 0 && newDx < 0);
                nowInRange = targetInFront && (newTargetColliderDist + buffer <= atkDist);
            }
            bool nowInSkillRange = IsTargetInSkillRange(owner, target);
            bool enteredStrike = nowInRange || nowInSkillRange || newTargetColliderDist <= engageDist || clampedToStop;
            if (enteredStrike)
            {
                context.CurrentState = AIState.Strike;
                context.StrikeEnteredTime = Time.time;
            }

            if (owner.CharAnimator != null)
            {
                // If we just entered Strike range, play idle (Strike will handle attack next frame).
                // Otherwise, if we actually moved, play walk; if clamped to zero step, play idle.
                if (enteredStrike)
                    owner.CharAnimator.PlayIdle();
                else if (step > 0.001f)
                    owner.CharAnimator.PlayWalk();
                else
                    owner.CharAnimator.PlayIdle();
                owner.CharAnimator.SetFacingDirection(dir);
            }

            MaybeClearBehindFlagOnFacingAligned(owner, dir);
        }

        /// <summary>
        /// While chasing, once our facing aligns with the target direction, clear the
        /// "target was behind on engage" flag so we don't waste a frame turning at Strike entry.
        /// </summary>
        private void MaybeClearBehindFlagOnFacingAligned(CharacterEntity owner, float targetDir)
        {
            if (!context.TargetWasBehindOnEngage) return;
            if (owner.CharAnimator == null) return;

            int targetDirSign = targetDir >= 0f ? 1 : -1;
            if (owner.CharAnimator.FacingDirection == targetDirSign)
            {
                context.TargetWasBehindOnEngage = false;
            }
        }

        // ---------------- Strike sub-state ----------------

        private void TickStrike(CharacterEntity owner, CharacterEntity target)
        {
            // Face the target while in Strike.
            // During attack animation, facing is locked by CombatSystem (SetFacingDirection is a no-op).
            // During attack cooldown (idle waiting), only update facing if the target is clearly
            // on one side (large deadzone to prevent jitter when overlapping).
            float facingDx = target.ColliderCenter.x - owner.ColliderCenter.x;
            bool isInAttackAnim = (combatSystem != null && combatSystem.IsAttacking) ||
                (owner.CharAnimator != null && owner.CharAnimator.StateMachine != null 
                 && owner.CharAnimator.StateMachine.isAttacking);

            // Use a larger deadzone (0.3) to prevent jitter when overlapping with the target.
            // Only update facing when NOT in attack animation AND target is clearly to one side.
            if (!isInAttackAnim && owner.CharAnimator != null 
                && !owner.CharAnimator.IsFacingLocked 
                && Mathf.Abs(facingDx) > 0.3f)
            {
                owner.CharAnimator.SetFacingDirection(facingDx);
            }

            // Do not interrupt Hit animation.
            if (owner.CharAnimator != null && owner.CharAnimator.IsInHitState)
            {
                return;
            }

            // Do not interrupt Skill animation.
            if (owner.CharAnimator != null && owner.CharAnimator.IsInSkillState)
            {
                return;
            }

            // If already mid-attack (animation playing, waiting for frame event), don't stack new attacks.
            // Check both CombatSystem._isAttacking (pre-hit-frame) and StateMachine.isAttacking
            // (post-hit-frame but animation still playing until OnStateEnd).
            if (combatSystem != null && combatSystem.IsAttacking)
            {
                // During combo: if combo window is open, check if target is still in attack range
                // and buffer next combo input automatically.
                HandleAIComboBuffer(owner, target);

                // If animation is cancellable and a skill is ready, cancel attack to use skill.
                if (combatSystem.CanBeCancelled)
                {
                    if (TryAICancelForSkill(owner, target))
                        return;
                }
                return;
            }
            if (owner.CharAnimator != null && owner.CharAnimator.StateMachine != null 
                && owner.CharAnimator.StateMachine.isAttacking)
            {
                // During combo: if combo window is open, check if target is still in attack range
                // and buffer next combo input automatically.
                HandleAIComboBuffer(owner, target);

                // If animation is cancellable and a skill is ready, cancel attack to use skill.
                if (combatSystem != null && combatSystem.CanBeCancelled)
                {
                    if (TryAICancelForSkill(owner, target))
                        return;
                }
                return;
            }

            // First-strike-behind rule: spend this frame only turning, clear flag, wait for next tick.
            if (context.TargetWasBehindOnEngage && !context.HasFiredFirstStrike)
            {
                context.TargetWasBehindOnEngage = false;
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.PlayIdle();
                }
                return;
            }

            // Attack-speed gate.
            float attackInterval = 1f / Mathf.Max(0.0001f, owner.RuntimeStats.attackSpeed);
            if (Time.time - context.LastAttackTime < attackInterval)
            {
                if (owner.CharAnimator != null)
                {
                    owner.CharAnimator.PlayIdle();
                }
                return;
            }

            bool fired = false;

            // Skill priority for Player / Boss.
            CharacterType type = owner.RuntimeStats.characterType;
            if ((type == CharacterType.Player || type == CharacterType.Boss) && combatSystem != null)
            {
                int readySkill = combatSystem.GetFirstReadySkillIndex();
                if (readySkill >= 0)
                {
                    fired = combatSystem.TryUseSkill(readySkill, target);
                }
            }

            if (!fired && combatSystem != null)
            {
                fired = combatSystem.TryNormalAttack(target);
            }

            if (fired)
            {
                context.LastAttackTime = Time.time;
                context.HasFiredFirstStrike = true;
            }
            else if (owner.CharAnimator != null)
            {
                // Attack range check may have failed this frame; stay in Strike and idle.
                // Execute() will re-evaluate next frame and switch to Engage if needed.
                owner.CharAnimator.PlayIdle();
            }
        }

        // ---------------- AI Animation Cancel ----------------

        /// <summary>
        /// Attempt to cancel the current attack animation to use a ready skill.
        /// Only triggers if the animation is in a cancellable state AND a skill is ready
        /// AND the target is within skill range. This allows AI to interrupt combo chains
        /// for high-priority skill usage.
        /// Returns true if cancellation was performed and skill was fired.
        /// </summary>
        private bool TryAICancelForSkill(CharacterEntity owner, CharacterEntity target)
        {
            if (combatSystem == null) return false;

            CharacterType type = owner.RuntimeStats.characterType;
            if (type != CharacterType.Player && type != CharacterType.Boss) return false;

            int readySkill = combatSystem.GetFirstReadySkillIndex();
            if (readySkill < 0) return false;

            // Check if target is within skill range
            if (target == null || !target.RuntimeStats.IsAlive) return false;
            SkillData skillData = owner.characterData.skills[readySkill];
            if (skillData == null) return false;
            float dist = Vector2.Distance(owner.ColliderCenter, target.ColliderCenter);
            if (dist > skillData.skillRange) return false;

            // Cancel the attack animation
            combatSystem.TryCancelAnimation();

            // Face the target before casting
            float dx = target.ColliderCenter.x - owner.ColliderCenter.x;
            if (owner.CharAnimator != null && Mathf.Abs(dx) > 0.01f)
            {
                owner.CharAnimator.SetFacingDirection(dx);
            }

            // Use the skill
            bool fired = combatSystem.TryUseSkill(readySkill, target);
            if (fired)
            {
                context.LastAttackTime = Time.time;
            }
            return fired;
        }

        // ---------------- AI Combo Buffer ----------------

        /// <summary>
        /// During an ongoing attack animation, check if the combo window is open.
        /// If so, determine whether the target is still in attack range:
        /// - If yes: buffer the next combo input so the attack chains automatically.
        /// - If no: do nothing (combo will end naturally at OnStateEnd).
        /// This implements the AI behavior: "打完全套连击 if enemy stays in range,
        /// 当前段结束后回Idle if enemy leaves range."
        /// </summary>
        private void HandleAIComboBuffer(CharacterEntity owner, CharacterEntity target)
        {
            if (combatSystem == null) return;
            if (!combatSystem.IsComboWindowOpen) return;

            // Check if target is still alive and in attack range using collider edge distance
            if (target == null || !target.RuntimeStats.IsAlive)
            {
                // No valid target — don't buffer, combo will end at OnStateEnd
                return;
            }

            float ownerX = owner.transform.position.x;
            float dx = target.transform.position.x - ownerX;
            float facingSign;
            if (Mathf.Abs(dx) < StrikeFacingDeadzone && owner.CharAnimator != null)
            {
                facingSign = owner.CharAnimator.FacingDirection;
            }
            else
            {
                facingSign = dx >= 0f ? 1f : -1f;
            }

            float targetColliderDist = RuntimeCharacterStats.GetDistanceToColliderEdge(ownerX, target.CharCollider);
            bool inRange;
            if (Mathf.Abs(dx) < 0.3f)
            {
                inRange = targetColliderDist <= owner.RuntimeStats.attackDistance;
            }
            else
            {
                bool targetInFront = (facingSign > 0 && dx > 0) || (facingSign < 0 && dx < 0);
                inRange = targetInFront && targetColliderDist <= owner.RuntimeStats.attackDistance;
            }

            if (inRange)
            {
                // Target is in range — buffer next combo step
                combatSystem.BufferComboInput();
            }
            // If not in range, don't buffer — combo will end naturally at OnStateEnd
        }
    }
}
