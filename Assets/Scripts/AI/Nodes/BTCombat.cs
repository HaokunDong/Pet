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

        /// <summary>Maximum consecutive frames TryNormalAttack can fail in Strike before forcing back to Engage.</summary>
        private const int MaxStrikeFailFrames = 5;

        private readonly BTContext context;
        private CombatSystem combatSystem;
        private KnockbackController knockbackController;
        private SkillDisplacementController displacementController;

        /// <summary>Consecutive frames where TryNormalAttack failed while in Strike state.</summary>
        private int strikeFailCount;

        /// <summary>Time when Engage state was last entered. Used to enforce minimum stay before re-entering Strike.</summary>
        private float engageEnteredTime;

        /// <summary>
        /// When true, inSkillRange will NOT trigger Strike entry. Set by the safety valve
        /// when attack fails in Strike (meaning the character entered Strike via skill range
        /// but couldn't actually attack). Cleared when the character reaches engageDist or inAttackRange.
        /// This forces the character to walk all the way to melee range instead of repeatedly
        /// entering Strike at skill range and failing.
        /// </summary>
        private bool skipSkillRangeEntry;

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

            // Target object may have been deactivated/recycled (e.g. player switched
            // character and the old one was returned to the pool). In that case the
            // combat target is no longer valid — abandon combat and re-scan.
            if (!target.gameObject.activeInHierarchy)
            {
                context.CurrentTarget = null;
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
            // However, if skipSkillRangeEntry is set (safety valve fired because attack failed
            // at skill range), ignore skill range until we reach melee range.
            bool inSkillRange = !skipSkillRangeEntry && IsTargetInSkillRange(owner, target);

            // Clear skipSkillRangeEntry once we're actually in attack range or engage distance.
            // This means the character has walked close enough to attack normally.
            if (skipSkillRangeEntry && (inAttackRange || targetColliderDist <= engageDist))
            {
                skipSkillRangeEntry = false;
                // Re-evaluate inSkillRange now that the flag is cleared
                inSkillRange = IsTargetInSkillRange(owner, target);
            }

            // Decide Engage vs Strike:
            // - Enter Strike when within engage distance OR actually in attack range OR in skill range.
            // - Exit Strike when both out of attack range AND not in skill range AND beyond
            //   effective exit distance (atkDist - buffer + hysteresis). Minimum stay prevents jitter.
            if (context.CurrentState == AIState.Strike)
            {
                // Minimum stay in Strike: at least one attack interval to prevent oscillation
                float minStrikeDuration = 0.3f;
                bool minStayElapsed = (Time.time - context.StrikeEnteredTime) >= minStrikeDuration;
                // Exit Strike when not in attack range AND no skill ready in range AND
                // beyond the effective attack threshold (atkDist - buffer) plus hysteresis.
                // Using (atkDist - buffer) as the base ensures no dead zone between
                // inAttackRange check and the exit threshold.
                float effectiveExitDist = (atkDist - buffer) + context.EngageExitHysteresis;
                if (minStayElapsed && !inAttackRange && !inSkillRange && targetColliderDist > effectiveExitDist)
                {
                    context.CurrentState = AIState.Engage;
                    engageEnteredTime = Time.time;
                }
            }
            else
            {
                // Coming from Wander / PostCombat / Engage.
                // Enter Strike if in attack range OR within engage distance OR in skill range.
                // But require minimum stay in Engage (prevents Strike↔Engage oscillation when
                // the safety valve forces back to Engage but the character is already close).
                // NOTE: Only inAttackRange bypasses the minimum stay. inSkillRange does NOT bypass
                // because the character might be in skill range but out of normal attack range,
                // and if the skill fails (cooldown just started), it would cause an infinite loop.
                bool engageMinStayElapsed = (Time.time - engageEnteredTime) >= 0.15f;
                if (inAttackRange || (engageMinStayElapsed && (inSkillRange || targetColliderDist <= engageDist)))
                {
                    context.CurrentState = AIState.Strike;
                    context.StrikeEnteredTime = Time.time;
                    strikeFailCount = 0;
                }
                else
                {
                    context.CurrentState = AIState.Engage;
                    if (engageEnteredTime == 0f) engageEnteredTime = Time.time;
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
            // Respect skipSkillRangeEntry: if the safety valve fired, don't use skill range.
            bool inSkillRangeEngage = !skipSkillRangeEntry && IsTargetInSkillRange(owner, target);

            // Clear skipSkillRangeEntry if we've reached melee range
            if (skipSkillRangeEntry && (inRange || targetColliderDist <= engageDist))
            {
                skipSkillRangeEntry = false;
                inSkillRangeEngage = IsTargetInSkillRange(owner, target);
            }

            // Only inRange (actual attack range) bypasses the minimum Engage stay.
            // inSkillRange and engageDist require the minimum stay to prevent
            // Strike↔Engage oscillation when the safety valve forces back to Engage.
            bool engageMinStayOk = (Time.time - engageEnteredTime) >= 0.15f;
            bool shouldEnterStrike = inRange || (engageMinStayOk && (inSkillRangeEngage || targetColliderDist <= engageDist));

            if (shouldEnterStrike)
            {
                context.CurrentState = AIState.Strike;
                context.StrikeEnteredTime = Time.time;
                strikeFailCount = 0;
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
            bool nowInSkillRange = !skipSkillRangeEntry && IsTargetInSkillRange(owner, target);
            // Clear skipSkillRangeEntry if we've reached melee range after moving
            if (skipSkillRangeEntry && (nowInRange || newTargetColliderDist <= engageDist))
            {
                skipSkillRangeEntry = false;
                nowInSkillRange = IsTargetInSkillRange(owner, target);
            }
            // Same rule: only nowInRange and clampedToStop bypass minimum stay.
            bool engageMinStayOkPost = (Time.time - engageEnteredTime) >= 0.15f;
            bool enteredStrike = nowInRange || clampedToStop || (engageMinStayOkPost && (nowInSkillRange || newTargetColliderDist <= engageDist));
            if (enteredStrike)
            {
                context.CurrentState = AIState.Strike;
                context.StrikeEnteredTime = Time.time;
                strikeFailCount = 0;
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
            // on one side (deadzone matches TryNormalAttack's FacingDeadzone to prevent mismatch).
            float facingDx = target.ColliderCenter.x - owner.ColliderCenter.x;
            bool isInAttackAnim = (combatSystem != null && combatSystem.IsAttacking) ||
                (owner.CharAnimator != null && owner.CharAnimator.StateMachine != null 
                 && owner.CharAnimator.StateMachine.isAttacking);

            // Use the same deadzone (0.15) as TryNormalAttack to ensure facing is always
            // consistent with the attack range check. Previously 0.3 caused a gap where
            // TickStrike wouldn't update facing but TryNormalAttack would check it, leading
            // to permanent attack failures when the character faced the wrong direction.
            if (!isInAttackAnim && owner.CharAnimator != null 
                && !owner.CharAnimator.IsFacingLocked 
                && Mathf.Abs(facingDx) > StrikeFacingDeadzone)
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
                strikeFailCount = 0;
            }
            else if (owner.CharAnimator != null)
            {
                // Attack range check may have failed this frame; stay in Strike and idle.
                // Execute() will re-evaluate next frame and switch to Engage if needed.
                owner.CharAnimator.PlayIdle();

                // Safety: if attack keeps failing in Strike (e.g. facing mismatch, range edge case),
                // first try to force-face the target (bypassing deadzone), then if still failing
                // force back to Engage to walk closer.
                strikeFailCount++;
                if (strikeFailCount == 2 && owner.CharAnimator != null && !owner.CharAnimator.IsFacingLocked)
                {
                    // After 2 failures, force face the target regardless of deadzone.
                    // This fixes the case where the character spawned facing the wrong direction
                    // and the target is within the facing deadzone.
                    float forceDx = target.transform.position.x - owner.transform.position.x;
                    if (Mathf.Abs(forceDx) > 0.01f)
                    {
                        owner.CharAnimator.SetFacingDirection(forceDx);
                    }
                }
                else if (strikeFailCount >= MaxStrikeFailFrames)
                {
                    Debug.LogWarning($"[BTCombat] '{owner.gameObject.name}': TryNormalAttack failed {strikeFailCount} times in Strike. Forcing back to Engage.");
                    context.CurrentState = AIState.Engage;
                    engageEnteredTime = Time.time;
                    // Set StrikeEnteredTime far in the future so the minStrikeDuration check
                    // won't immediately allow re-entry to Strike on the next frame.
                    context.StrikeEnteredTime = Time.time;
                    strikeFailCount = 0;
                    // Disable skill-range-based Strike entry. The character was in Strike
                    // (likely entered via skill range) but couldn't attack. Force it to
                    // walk all the way to melee range before trying again.
                    skipSkillRangeEntry = true;
                }
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
