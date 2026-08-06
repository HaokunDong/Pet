using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manages combat actions (normal attack and skills) for a character.
    /// Provides public interfaces for both AI and manual control to invoke.
    /// Damage is applied via animation frame events through AnimEventReceiver.
    /// Normal attacks deal AOE damage to ALL enemies within the attack range shapes.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class CombatSystem : MonoBehaviour
    {
        private CharacterEntity entity;

        // --- Cached attack state for animation frame event callbacks ---
        private CharacterEntity _cachedTarget;
        private float _cachedFacingSign;
        private int _cachedSkillIndex = -1;
        private bool _isAttacking;
        private Vector2 _cachedAttackPosition;

        // --- Combo attack state ---
        private int _comboStep;
        private bool _comboWindowOpen;
        private bool _comboInputBuffered;

        // --- Animation cancel state ---
        private bool _canBeCancelled;

        /// <summary>
        /// The current cached attack target (primary target for skills / facing reference).
        /// </summary>
        public CharacterEntity CachedTarget => _cachedTarget;

        /// <summary>
        /// The current cached skill index (-1 means normal attack). Read by AnimEventReceiver.
        /// </summary>
        public int CachedSkillIndex => _cachedSkillIndex;

        /// <summary>
        /// The cached facing sign at the time the attack/skill was initiated.
        /// 1 = facing right, -1 = facing left. Used by SkillEffectData implementations.
        /// </summary>
        public float CachedFacingSign => _cachedFacingSign;

        /// <summary>
        /// Whether the character is currently in an attack/skill animation waiting for hit frame.
        /// </summary>
        public bool IsAttacking => _isAttacking;

        /// <summary>
        /// Current combo step (0-based). 0 = first attack.
        /// </summary>
        public int ComboStep => _comboStep;

        /// <summary>
        /// Whether the combo input window is currently open (accepting next-step input).
        /// </summary>
        public bool IsComboWindowOpen => _comboWindowOpen;

        /// <summary>
        /// Whether the current animation is in a "cancellable" state.
        /// Set to true when OnCancellablePoint fires; reset on new attack/combo step/state clear.
        /// </summary>
        public bool CanBeCancelled => _canBeCancelled;

        /// <summary>
        /// Maximum combo count for this character (from CharacterData).
        /// </summary>
        public int MaxComboCount => entity != null && entity.RuntimeStats != null
            ? entity.RuntimeStats.comboCount
            : 1;

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
        }

        /// <summary>
        /// Attempt a normal attack on the target.
        /// Respects attack speed interval (no CD, but limited by attack speed).
        /// Damage is deferred to the animation hit frame event.
        /// </summary>
        /// <returns>True if attack was executed.</returns>
        public bool TryNormalAttack(CharacterEntity target)
        {
            if (entity == null || !entity.RuntimeStats.IsAlive) return false;
            if (target == null || !target.RuntimeStats.IsAlive) return false;

            // If the attack animation is still playing (facing is locked), don't start a new attack.
            // This prevents re-triggering during the window between hit-frame and OnStateEnd.
            if (entity.CharAnimator != null && entity.CharAnimator.IsFacingLocked)
            {
                return false;
            }

            // Check attack range using multi-shape system.
            // When overlapping (|dx| very small), check BOTH directions to ensure
            // the attack doesn't fail just because the target is slightly "behind" the facing.
            float dx = target.ColliderCenter.x - entity.ColliderCenter.x;
            float facingSign;
            const float FacingDeadzone = 0.15f;
            if (Mathf.Abs(dx) < FacingDeadzone && entity.CharAnimator != null)
            {
                facingSign = entity.CharAnimator.FacingDirection;
            }
            else
            {
                facingSign = dx >= 0f ? 1f : -1f;
            }

            bool inRange;
            if (Mathf.Abs(dx) < 0.3f)
            {
                // Overlapping: check both facing directions for range
                inRange = entity.RuntimeStats.IsTargetInAttackRange(
                              entity.ColliderCenter, 1f, target.ColliderCenter, target.ColliderHalfExtentX) ||
                          entity.RuntimeStats.IsTargetInAttackRange(
                              entity.ColliderCenter, -1f, target.ColliderCenter, target.ColliderHalfExtentX);
            }
            else
            {
                inRange = entity.RuntimeStats.IsTargetInAttackRange(
                    entity.ColliderCenter, facingSign, target.ColliderCenter, target.ColliderHalfExtentX);
            }

            if (!inRange)
                return false;

            // NOTE: Attack speed interval is managed by BTCombat (context.LastAttackTime).
            // No duplicate check here to avoid desync between two separate timers.

            // Cache target, facing, and position for frame event callback (AOE will find all targets at hit frame)
            _cachedTarget = target;
            _cachedFacingSign = facingSign;
            _cachedSkillIndex = -1;
            _isAttacking = true;
            _cachedAttackPosition = transform.position;

            // Lock facing and play attack animation.
            // NOTE: We do NOT call FaceTowards here. The caller (ManualController / BTCombat)
            // is responsible for setting the correct facing BEFORE calling TryNormalAttack.
            // This prevents jitter when overlapping with the target, because the caller
            // can decide to skip facing updates when the target is too close.
            if (entity.CharAnimator != null)
            {
                // Reset combo state for a fresh attack sequence
                _comboStep = 0;
                _comboWindowOpen = false;
                _comboInputBuffered = false;
                _canBeCancelled = false;

                // Lock facing direction for the duration of the attack to prevent jitter
                entity.CharAnimator.LockFacing();
                entity.CharAnimator.PlayAttack(_comboStep);
            }

            return true;
        }

        /// <summary>
        /// Attempt to use a skill on the target.
        /// Checks cooldown, range, and plays animation.
        /// Damage and effects are deferred to the animation hit frame event.
        /// </summary>
        /// <returns>True if skill was used.</returns>
        public bool TryUseSkill(int skillIndex, CharacterEntity target)
        {
            if (entity == null || !entity.RuntimeStats.IsAlive) return false;
            if (target == null || !target.RuntimeStats.IsAlive) return false;

            // If the attack/skill animation is still playing (facing is locked), don't start a new action.
            if (entity.CharAnimator != null && entity.CharAnimator.IsFacingLocked) return false;

            // Validate skill index
            if (entity.characterData.skills == null) return false;
            if (skillIndex < 0 || skillIndex >= entity.characterData.skills.Length) return false;
            if (skillIndex >= entity.characterData.GetMaxSkillCount()) return false;

            // Check cooldown
            if (!entity.RuntimeStats.IsSkillReady(skillIndex)) return false;

            SkillData skillData = entity.characterData.skills[skillIndex];
            if (skillData == null) return false;

            // Check skill range
            float dist = Vector2.Distance(entity.ColliderCenter, target.ColliderCenter);
            if (dist > skillData.skillRange) return false;

            // Compute facing sign toward target.
            // When overlapping, use current facing to avoid jitter.
            float dx = target.ColliderCenter.x - entity.ColliderCenter.x;
            float facingSign;
            if (Mathf.Abs(dx) < 0.15f && entity.CharAnimator != null)
            {
                facingSign = entity.CharAnimator.FacingDirection;
            }
            else
            {
                facingSign = dx >= 0f ? 1f : -1f;
            }

            // Cache target, facing, position, and skill index for frame event callback
            _cachedTarget = target;
            _cachedFacingSign = facingSign;
            _cachedSkillIndex = skillIndex;
            _isAttacking = true;
            _cachedAttackPosition = transform.position;
            _canBeCancelled = false;

            // Lock facing and play skill animation.
            // NOTE: We do NOT call FaceTowards here. The caller is responsible for
            // setting the correct facing BEFORE calling TryUseSkill.
            if (entity.CharAnimator != null)
            {
                // Lock facing direction for the duration of the skill to prevent jitter
                entity.CharAnimator.LockFacing();
                entity.CharAnimator.PlaySkill(skillIndex);
            }

            // Start cooldown immediately (animation is playing)
            entity.RuntimeStats.StartSkillCooldown(skillIndex, skillData.cooldown);

            return true;
        }

        // ==================== Frame Event Callbacks ====================

        /// <summary>
        /// Called by AnimEventReceiver when the normal attack animation reaches the hit frame.
        /// Applies AOE damage to ALL enemies within the attack range shapes.
        /// After dealing damage, applies displacement if configured in CharacterData.
        /// </summary>
        public void ApplyNormalAttackDamage()
        {
            // Determine the enemy tag based on owner's GameObject tag (not characterType),
            // because a Boss character used by the player still has tag "Player".
            string targetTag = entity.gameObject.CompareTag("Player")
                ? "Enemy"
                : "Player";

            // Get cached facing direction and position from when attack started.
            // Using cached position ensures knockback during the attack animation
            // does not cause the hit frame to miss targets that were originally in range.
            float facingSign = _cachedFacingSign;
            Vector2 attackOrigin = _cachedAttackPosition;

            // Find all enemies in attack range and apply damage
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            int hitCount = 0;

            // Calculate damage with combo multiplier
            float comboDamageMultiplier = entity.RuntimeStats.GetComboDamageMultiplier(_comboStep);
            float damage = entity.RuntimeStats.attackPower * comboDamageMultiplier;

            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity target = candidates[i].GetComponent<CharacterEntity>();
                if (target == null || !target.RuntimeStats.IsAlive) continue;

                if (entity.RuntimeStats.IsTargetInAttackRange(
                        attackOrigin, facingSign, target.ColliderCenter, target.ColliderHalfExtentX))
                {
                    target.TakeDamage(damage, entity);
                    hitCount++;
                }
            }

            // Apply normal attack displacement if configured (Fixed type only)
            ApplyNormalAttackDisplacement(targetTag, facingSign);

            // Clear attack state but preserve combo state (combo continues until OnStateEnd)
            ClearAttackStateKeepCombo();
        }

        /// <summary>
        /// Called by AnimEventReceiver when the normal attack animation reaches the lock-target frame
        /// (before the hit frame). Used for LockOn type normal attack displacement.
        /// Locks the target's current position and starts moving toward it with optional continuous damage.
        /// </summary>
        public void ApplyNormalAttackLockOnDisplacement()
        {
            CharacterData data = entity.characterData;
            if (data.attackDisplacementType != SkillDisplacementType.LockOn)
                return;

            // Lock the target's current position
            if (_cachedTarget == null || !_cachedTarget.RuntimeStats.IsAlive)
                return;

            Vector2 lockedPos = _cachedTarget.ColliderCenter;

            // Determine the enemy tag based on GameObject tag
            string targetTag = entity.gameObject.CompareTag("Player")
                ? "Enemy"
                : "Player";

            // Get or add the displacement controller
            SkillDisplacementController controller = GetComponent<SkillDisplacementController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<SkillDisplacementController>();
            }

            if (data.attackDamagesDuringDisplacement)
            {
                // Compute effective facing sign for attack range shapes
                float rawFacingSign = _cachedFacingSign;
                float effectiveFacingSign = data.defaultFacesRight ? rawFacingSign : -rawFacingSign;

                // Do initial damage at starting position
                Vector2 casterPos = transform.position;
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
                for (int i = 0; i < candidates.Length; i++)
                {
                    CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                    if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                    Vector2 candidatePos = candidateEntity.transform.position;
                    if (AttackRangeHelper.IsTargetInRange(casterPos, effectiveFacingSign, data.attackRangeShapes, candidatePos))
                    {
                        candidateEntity.TakeDamage(entity.RuntimeStats.attackPower, entity);
                    }
                }

                controller.StartLockOnDisplacementWithDamage(lockedPos, data.attackDisplacementDuration,
                    data.attackRangeShapes, effectiveFacingSign, targetTag, entity.RuntimeStats.attackPower, entity);
            }
            else
            {
                controller.StartLockOnDisplacement(lockedPos, data.attackDisplacementDuration);
            }

            // Clear attack state after lock-on displacement has been initiated
            ClearAttackState();
        }

        /// <summary>
        /// Apply normal attack displacement (Fixed type) after dealing hit-frame damage.
        /// If damagesDuringDisplacement is enabled, enemies along the path will also be hit.
        /// </summary>
        private void ApplyNormalAttackDisplacement(string targetTag, float facingSign)
        {
            CharacterData data = entity.characterData;

            // Only apply Fixed type displacement here; LockOn is handled by OnAttackLockTarget event
            if (data.attackDisplacementType != SkillDisplacementType.Fixed)
                return;
            if (data.attackDisplacementDirection == SkillDisplacementDirection.None || data.attackDisplacementDistance <= 0f)
                return;

            // Skip displacement when it would cause the character to pass through the target.
            // This prevents jitter from the character overshooting and then AI chasing back.
            if (_cachedTarget != null && _cachedTarget.RuntimeStats.IsAlive)
            {
                float dirMul = (data.attackDisplacementDirection == SkillDisplacementDirection.Forward) ? 1f : -1f;
                float moveDir = facingSign * dirMul;
                float currentX = transform.position.x;
                float targetX = _cachedTarget.ColliderCenter.x;
                float afterDisplacementX = currentX + moveDir * data.attackDisplacementDistance;

                // Check if displacement would overshoot the target:
                // Before displacement, target is in front (or we're overlapping).
                // After displacement, target would be behind us.
                bool targetInFront = (moveDir > 0f) ? (targetX >= currentX) : (targetX <= currentX);
                bool targetBehindAfter = (moveDir > 0f) ? (targetX < afterDisplacementX) : (targetX > afterDisplacementX);

                if (targetInFront && targetBehindAfter)
                    return;

                // Also skip if we're already overlapping or very close (within displacement distance)
                float distToTarget = Mathf.Abs(targetX - currentX);
                if (distToTarget < data.attackDisplacementDistance)
                    return;
            }

            // Get or add the displacement controller
            SkillDisplacementController controller = GetComponent<SkillDisplacementController>();
            if (controller == null)
            {
                controller = gameObject.AddComponent<SkillDisplacementController>();
            }

            // Calculate displacement direction based on facing
            float directionMultiplier = (data.attackDisplacementDirection == SkillDisplacementDirection.Forward) ? 1f : -1f;
            float finalDirection = facingSign * directionMultiplier;

            if (data.attackDamagesDuringDisplacement)
            {
                // Compute effective facing sign for attack range shapes
                float effectiveFacingSign = data.defaultFacesRight ? facingSign : -facingSign;

                controller.StartDisplacementWithDamage(finalDirection, data.attackDisplacementDistance,
                    data.attackDisplacementDuration, data.attackRangeShapes, effectiveFacingSign,
                    targetTag, entity.RuntimeStats.attackPower, entity);
            }
            else
            {
                controller.StartDisplacement(finalDirection, data.attackDisplacementDistance, data.attackDisplacementDuration);
            }
        }

        /// <summary>
        /// Called by AnimEventReceiver when the skill animation reaches the hit frame.
        /// Applies skill damage and spawns effects, then clears attack state.
        /// </summary>
        public void ApplySkillDamage()
        {
            if (_cachedSkillIndex < 0 || entity.characterData.skills == null ||
                _cachedSkillIndex >= entity.characterData.skills.Length)
            {
                ClearAttackState();
                return;
            }

            SkillData skillData = entity.characterData.skills[_cachedSkillIndex];
            if (skillData == null)
            {
                ClearAttackState();
                return;
            }

            // Delegate to SkillEffectData if configured.
            // NOTE: We pass _cachedTarget even if it's null or dead — the skill effect
            // (e.g. projectile) should still fly to the predicted position and explode.
            if (skillData.skillEffect != null)
            {
                skillData.skillEffect.Execute(this, _cachedTarget, skillData);

                // For lock-on displacement skills, do NOT clear attack state here.
                // The state will be cleared after the lock-on displacement completes
                // (OnSkillLockTarget fires after OnSkillHit for these skills).
                if (!skillData.skillEffect.UsesLockOnDisplacement)
                {
                    ClearAttackState();
                }
                return;
            }

            // Legacy fallback: direct damage (no SkillEffectData assigned)
            if (_cachedTarget == null || !_cachedTarget.RuntimeStats.IsAlive)
            {
                ClearAttackState();
                return;
            }

            ClearAttackState();
        }

        /// <summary>
        /// Clear all cached attack state. Called when attack is interrupted or completed.
        /// Only clears CombatSystem's internal state (_isAttacking, cached target, etc.).
        /// Does NOT trigger animation state transitions — that is handled by the
        /// OnStateEnd animation event at the last frame of the attack/skill clip.
        /// Does NOT unlock facing — facing remains locked until the animation fully ends
        /// (OnStateEnd → ClearCurrentState → UnlockFacing).
        /// </summary>
        public void ClearAttackState()
        {
            _cachedTarget = null;
            _cachedFacingSign = 0f;
            _cachedSkillIndex = -1;
            _isAttacking = false;
            _cachedAttackPosition = Vector2.zero;
            ResetComboState();
        }

        /// <summary>
        /// Clear cached attack state but preserve combo state.
        /// Used after the hit frame fires damage — the combo window hasn't opened yet,
        /// and we need to keep combo state intact for the upcoming OnComboWindowOpen and OnStateEnd events.
        /// </summary>
        private void ClearAttackStateKeepCombo()
        {
            _cachedTarget = null;
            _cachedFacingSign = 0f;
            _cachedSkillIndex = -1;
            _isAttacking = false;
            _cachedAttackPosition = Vector2.zero;
        }

        // ==================== Combo Attack System ====================

        /// <summary>
        /// Called by AnimEventReceiver when the OnComboWindowOpen animation event fires.
        /// Opens the combo input window, allowing the next attack step to be buffered.
        /// </summary>
        public void OpenComboWindow()
        {
            _comboWindowOpen = true;
        }

        /// <summary>
        /// Buffer the next combo input. Called by ManualController or AI when the combo window is open.
        /// If the window is open and the current step has not reached max combo count, marks input as buffered.
        /// </summary>
        public void BufferComboInput()
        {
            if (_comboWindowOpen && _comboStep + 1 < MaxComboCount)
            {
                _comboInputBuffered = true;
            }
        }

        /// <summary>
        /// Reset all combo state to initial values.
        /// Called when combo ends, is interrupted, or attack state is cleared.
        /// </summary>
        public void ResetComboState()
        {
            _comboStep = 0;
            _comboWindowOpen = false;
            _comboInputBuffered = false;
            _canBeCancelled = false;
        }

        /// <summary>
        /// Called by CharacterAnimator.ClearCurrentState() when OnStateEnd fires.
        /// Determines whether to continue the combo or return to Idle.
        /// </summary>
        /// <returns>True if combo continues (next step initiated), false if should return to Idle.</returns>
        public bool HandleComboOnStateEnd()
        {
            if (_comboInputBuffered && _comboStep + 1 < MaxComboCount)
            {
                TryComboNextStep();
                return true;
            }

            // No buffered input or reached max combo — reset and let caller return to Idle
            ResetComboState();
            return false;
        }

        /// <summary>
        /// Advance to the next combo step. Increments combo step, resets window/buffer,
        /// and plays the next attack animation without returning to Idle.
        /// </summary>
        private void TryComboNextStep()
        {
            _comboStep++;
            _comboWindowOpen = false;
            _comboInputBuffered = false;
            _canBeCancelled = false;

            // Re-cache attack position for the new step
            _cachedAttackPosition = transform.position;
            _isAttacking = true;

            if (entity.CharAnimator != null)
            {
                // Keep facing locked, play next combo attack
                entity.CharAnimator.LockFacing();
                entity.CharAnimator.PlayComboNextAttack(_comboStep);
            }
        }

        // ==================== Animation Cancel ====================

        /// <summary>
        /// Called by AnimEventReceiver when OnCancellablePoint animation event fires.
        /// Implements the full cancellable-point logic with priority:
        ///   1. If combo input is buffered and next step exists → immediate combo skip (accelerate)
        ///   2. Otherwise → just mark as cancellable, wait for non-attack input or OnStateEnd
        /// For skill states, combo check is skipped; only marks as cancellable.
        /// </summary>
        public void HandleCancellablePoint()
        {
            // If in skill state (not normal attack), skip combo logic — just mark cancellable
            if (entity.CharAnimator != null && entity.CharAnimator.IsInSkillState)
            {
                _canBeCancelled = true;
                return;
            }

            // Priority 1: Combo skip acceleration
            // If combo input has been buffered and there is a next step available,
            // immediately skip remaining frames and chain to the next attack.
            if (_comboInputBuffered && _comboStep + 1 < MaxComboCount)
            {
                // Do NOT mark as cancellable — we're skipping directly to next attack
                _canBeCancelled = false;
                TryComboNextStep();
                return;
            }

            // Priority 2: No combo buffered — just mark as cancellable
            // The animation will continue playing normally until either:
            //   - A non-attack input triggers TryCancelAnimation()
            //   - OnStateEnd fires naturally
            _canBeCancelled = true;
        }

        /// <summary>
        /// Attempt to cancel the current animation (skip remaining frames).
        /// Used when the player inputs a non-attack action (move, skill) while
        /// the animation is in a cancellable state.
        /// Returns true if cancellation was successful.
        /// </summary>
        public bool TryCancelAnimation()
        {
            if (!_canBeCancelled) return false;

            // Prevent double-trigger within the same animation
            _canBeCancelled = false;

            // Reset combo state (combo chain is broken by non-attack action)
            ResetComboState();

            // Clear attack state
            _cachedTarget = null;
            _cachedFacingSign = 0f;
            _cachedSkillIndex = -1;
            _isAttacking = false;
            _cachedAttackPosition = Vector2.zero;

            // Delegate animation cleanup to CharacterAnimator
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.CancelCurrentAnimation();
            }

            return true;
        }

        // ==================== Utility ====================

        /// <summary>
        /// Called by AnimEventReceiver when the skill animation reaches the lock-target frame
        /// (before the hit frame). Locks the target's current position and starts lock-on displacement.
        /// </summary>
        public void ApplySkillLockOnDisplacement()
        {
            Debug.Log($"[CombatSystem] {gameObject.name}: ApplySkillLockOnDisplacement() called. SkillIndex={_cachedSkillIndex}");

            if (_cachedSkillIndex < 0 || entity.characterData.skills == null ||
                _cachedSkillIndex >= entity.characterData.skills.Length)
            {
                Debug.LogWarning($"[CombatSystem] {gameObject.name}: ApplySkillLockOnDisplacement - invalid skill index {_cachedSkillIndex}");
                return;
            }

            SkillData skillData = entity.characterData.skills[_cachedSkillIndex];
            if (skillData == null || skillData.skillEffect == null)
            {
                Debug.LogWarning($"[CombatSystem] {gameObject.name}: ApplySkillLockOnDisplacement - skillData or skillEffect is null");
                return;
            }

            SkillEffectData skillEffect = skillData.skillEffect;
            if (!skillEffect.UsesLockOnDisplacement)
            {
                Debug.LogWarning($"[CombatSystem] {gameObject.name}: ApplySkillLockOnDisplacement - skill does NOT use LockOn displacement (type={skillEffect.displacementType})");
                return;
            }

            // Lock the target's current position
            if (_cachedTarget == null || !_cachedTarget.RuntimeStats.IsAlive)
            {
                Debug.LogWarning($"[CombatSystem] {gameObject.name}: ApplySkillLockOnDisplacement - target is null or dead");
                return;
            }
            Vector2 lockedPos = _cachedTarget.ColliderCenter;
            Debug.Log($"[CombatSystem] {gameObject.name}: LockOn target position locked at {lockedPos}");


            // Check if we need continuous damage during lock-on displacement
            if (skillEffect.damagesDuringDisplacement)
            {
                // For MeleeSkillEffectData, we need the shapes and facing info
                MeleeSkillEffectData meleeEffect = skillEffect as MeleeSkillEffectData;
                if (meleeEffect != null && meleeEffect.skillRangeShapes != null && meleeEffect.skillRangeShapes.Length > 0)
                {
                    CharacterEntity casterEntity = GetComponent<CharacterEntity>();
                    string targetTag = casterEntity.gameObject.CompareTag("Player")
                        ? "Enemy"
                        : "Player";

                    float rawFacingSign = _cachedFacingSign;
                    float effectiveFacingSign = meleeEffect.defaultFacesRight ? rawFacingSign : -rawFacingSign;

                    // Do initial damage at starting position
                    Vector2 casterPos = transform.position;
                    GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                        if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                        Vector2 candidatePos = candidateEntity.transform.position;
                        if (AttackRangeHelper.IsTargetInRange(casterPos, effectiveFacingSign, meleeEffect.skillRangeShapes, candidatePos))
                        {
                            candidateEntity.TakeDamage(skillData.damage, casterEntity);
                        }
                    }

                    skillEffect.ApplyLockOnDisplacementWithDamage(this, lockedPos, skillData,
                        meleeEffect.skillRangeShapes, effectiveFacingSign, targetTag);
                }
                else
                {
                    // No shapes, just do lock-on displacement without damage
                    skillEffect.ApplyLockOnDisplacement(this, lockedPos);
                }
            }
            else
            {
                skillEffect.ApplyLockOnDisplacement(this, lockedPos);
            }

            // Clear attack state after lock-on displacement has been initiated.
            // The displacement controller will handle the actual movement independently.
            ClearAttackState();
        }

        /// <summary>
        /// Get the index of the first skill that is ready to use.
        /// Returns -1 if no skill is available.
        /// </summary>
        public int GetFirstReadySkillIndex()
        {
            if (entity.characterData.skills == null)
                return -1;

            int maxSkills = entity.characterData.GetMaxSkillCount();
            for (int i = 0; i < entity.characterData.skills.Length && i < maxSkills; i++)
            {
                if (entity.RuntimeStats.IsSkillReady(i))
                    return i;
            }

            return -1;
        }
    }
}
