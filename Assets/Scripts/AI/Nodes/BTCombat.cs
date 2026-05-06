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
    /// - Engage sub-state: if distance &gt; engageDistance, walk horizontally toward the target.
    ///   Every frame call SetFacingDirection(targetDir); once the facing aligns with the target
    ///   direction, clear TargetWasBehindOnEngage (natural turn during chase).
    /// - Strike sub-state: horizontal velocity = 0, FaceTowards(target); on each tick, if the
    ///   attack cooldown (1/attackSpeed) has elapsed, try skill first (Player/Boss) then normal
    ///   attack. IsAttacking or IsInHitState skips the attack trigger this frame.
    /// - First-strike-behind rule: if TargetWasBehindOnEngage is true and this is the first
    ///   strike of the encounter, spend this frame only turning (no attack) and clear the flag.
    /// - Hysteresis: stay in Strike until distance &gt; engageDistance + EngageExitHysteresis.
    /// 
    /// Returns Success when combat should continue (target alive and within reach),
    /// Failure when the target is dead / gone so the root Selector can fall through to PostCombat.
    /// </summary>
    public class BTCombat : BTNode
    {
        /// <summary>Minimum allowed engageDistance to prevent complete overlap.</summary>
        private const float MinEngageDistance = 0.1f;

        /// <summary>Horizontal dead-zone for FaceTowards in Strike state to prevent per-frame flip-flopping.</summary>
        private const float StrikeFacingDeadzone = 0.05f;

        private readonly BTContext context;
        private CombatSystem combatSystem;
        private KnockbackController knockbackController;

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

            float dist = Vector2.Distance(owner.transform.position, target.transform.position);
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

            // If the owner is being knocked back, skip all movement and attack logic this frame.
            if (knockbackController != null && knockbackController.IsInKnockback)
            {
                return BTState.Success;
            }

            // If the owner is in skill animation, freeze movement (speed = 0) and skip all logic.
            if (owner.CharAnimator != null && owner.CharAnimator.IsInSkillState)
            {
                return BTState.Success;
            }

            // If the owner is in attack animation (waiting for OnStateEnd), skip all logic.
            if (owner.CharAnimator != null && owner.CharAnimator.IsAttacking)
            {
                return BTState.Success;
            }

            float dist = Mathf.Abs(owner.transform.position.x - target.transform.position.x);
            float engageDist = Mathf.Max(owner.RuntimeStats.engageDistance, MinEngageDistance);
            float engageExitDist = engageDist + context.EngageExitHysteresis;

            // Use the actual attack range shape check to decide Strike eligibility.
            // This avoids the mismatch between X-axis engageDistance and 2D shape checks.
            // Always use target direction for range check consistency with TickEngage.
            float dx = target.transform.position.x - owner.transform.position.x;
            float facingSign = dx >= 0f ? 1f : -1f;
            bool inAttackRange = owner.RuntimeStats.IsTargetInAttackRange(
                owner.transform.position, facingSign, target.transform.position);

            // Also check if target is within any ready skill's range.
            // This allows the character to enter Strike early to use a ranged skill
            // instead of walking all the way to melee attack range.
            bool inSkillRange = IsTargetInSkillRange(owner, target);

            // Decide Engage vs Strike:
            // - Enter Strike when within engageDistance OR actually in attack range OR in skill range.
            // - Exit Strike only when both out of attack range AND beyond engageExitDist AND not in skill range
            //   AND minimum stay duration has elapsed (prevents jitter at boundary).
            if (context.CurrentState == AIState.Strike)
            {
                // Minimum stay in Strike: at least one attack interval to prevent oscillation
                float minStrikeDuration = 0.3f;
                bool minStayElapsed = (Time.time - context.StrikeEnteredTime) >= minStrikeDuration;
                if (minStayElapsed && !inAttackRange && !inSkillRange && dist > engageExitDist)
                {
                    context.CurrentState = AIState.Engage;
                }
            }
            else
            {
                // Coming from Wander / PostCombat / Engage.
                // Enter Strike if in attack range OR within engageDistance OR in skill range.
                if (inAttackRange || inSkillRange || dist <= engageDist)
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
            float dir = dx >= 0f ? 1f : -1f;
            float engageDist = Mathf.Max(owner.RuntimeStats.engageDistance, MinEngageDistance);

            // Use the larger minAttackDistance of both combatants to prevent overlap.
            float ownerMinDist = owner.RuntimeStats.minAttackDistance;
            float targetMinDist = target.RuntimeStats != null ? target.RuntimeStats.minAttackDistance : 0f;
            float minDist = Mathf.Max(ownerMinDist, targetMinDist);

            // If already in attack range, snap to Strike immediately — no movement.
            // Always use target direction (dir) for consistency with Execute()'s range check.
            bool inRange = owner.RuntimeStats.IsTargetInAttackRange(
                owner.transform.position, dir, target.transform.position);

            // Also check skill range — if a skill is ready and target is in skill range,
            // enter Strike immediately so the character can use the skill.
            bool inSkillRangeEngage = IsTargetInSkillRange(owner, target);

            if (inRange || inSkillRangeEngage || absDx <= engageDist)
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
            float newAbsDx = Mathf.Abs(targetX - newPos.x);
            bool nowInRange = owner.RuntimeStats.IsTargetInAttackRange(
                newPos, dir, target.transform.position);
            bool nowInSkillRange = IsTargetInSkillRange(owner, target);
            bool enteredStrike = nowInRange || nowInSkillRange || newAbsDx <= engageDist || clampedToStop;
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
            // Face the target while in Strike. Use SetFacingDirection with the target direction
            // to ensure facing is always correct, even when characters are very close.
            // Only skip if dx is exactly 0 (complete overlap) to avoid division issues.
            float facingDx = target.transform.position.x - owner.transform.position.x;
            if (owner.CharAnimator != null && !Mathf.Approximately(facingDx, 0f))
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
                return;
            }
            if (owner.CharAnimator != null && owner.CharAnimator.StateMachine != null 
                && owner.CharAnimator.StateMachine.isAttacking)
            {
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
                Debug.Log($"[BTCombat] {owner.gameObject.name}: Attack fired successfully");
            }
            else if (owner.CharAnimator != null)
            {
                // Attack range check may have failed this frame; stay in Strike and idle.
                // Execute() will re-evaluate next frame and switch to Engage if needed.
                Debug.Log($"[BTCombat] {owner.gameObject.name}: Attack NOT fired, calling PlayIdle(). isIdle={owner.CharAnimator.StateMachine?.isIdle}");
                owner.CharAnimator.PlayIdle();
            }
        }
    }
}
