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

            // Check attack range using multi-shape system.
            // Always use target direction for facing, not the cached FacingDirection,
            // because FacingDirection may be stale when characters are very close
            // (StrikeFacingDeadzone prevents updates at < 0.05 distance).
            float dx = target.transform.position.x - transform.position.x;
            float facingSign = dx >= 0f ? 1f : -1f;

            if (!entity.RuntimeStats.IsTargetInAttackRange(
                    transform.position, facingSign, target.transform.position))
                return false;

            // NOTE: Attack speed interval is managed by BTCombat (context.LastAttackTime).
            // No duplicate check here to avoid desync between two separate timers.

            // Cache target and facing for frame event callback (AOE will find all targets at hit frame)
            _cachedTarget = target;
            _cachedFacingSign = facingSign;
            _cachedSkillIndex = -1;
            _isAttacking = true;

            // Face the target and play attack animation
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.FaceTowards(target.transform.position);
                entity.CharAnimator.PlayAttack();
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

            // Validate skill index
            if (entity.characterData.skills == null) return false;
            if (skillIndex < 0 || skillIndex >= entity.characterData.skills.Length) return false;
            if (skillIndex >= entity.characterData.GetMaxSkillCount()) return false;

            // Check cooldown
            if (!entity.RuntimeStats.IsSkillReady(skillIndex)) return false;

            SkillData skillData = entity.characterData.skills[skillIndex];
            if (skillData == null) return false;

            // Check skill range
            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist > skillData.skillRange) return false;

            // Compute facing sign toward target
            float dx = target.transform.position.x - transform.position.x;
            float facingSign = dx >= 0f ? 1f : -1f;

            // Cache target, facing, and skill index for frame event callback
            _cachedTarget = target;
            _cachedFacingSign = facingSign;
            _cachedSkillIndex = skillIndex;
            _isAttacking = true;

            Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill SUCCESS. skillIndex={skillIndex}, _isAttacking=true");

            // Face the target and play skill animation
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.FaceTowards(target.transform.position);
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
        /// </summary>
        public void ApplyNormalAttackDamage()
        {
            // Determine the enemy tag based on owner's character type
            string targetTag = (entity.RuntimeStats.characterType == CharacterType.Player)
                ? "Enemy"
                : "Player";

            // Get current facing direction (use cached value from when attack started)
            float facingSign = _cachedFacingSign;

            // Find all enemies in attack range and apply damage
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            int hitCount = 0;

            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity target = candidates[i].GetComponent<CharacterEntity>();
                if (target == null || !target.RuntimeStats.IsAlive) continue;

                if (entity.RuntimeStats.IsTargetInAttackRange(
                        transform.position, facingSign, target.transform.position))
                {
                    target.TakeDamage(entity.RuntimeStats.attackPower, entity);
                    hitCount++;
                }
            }

            ClearAttackState();
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
        /// </summary>
        public void ClearAttackState()
        {
            Debug.Log($"[CombatSystem] {gameObject.name}: ClearAttackState() called. Was: skillIndex={_cachedSkillIndex}, isAttacking={_isAttacking}\n{UnityEngine.StackTraceUtility.ExtractStackTrace()}");
            _cachedTarget = null;
            _cachedFacingSign = 0f;
            _cachedSkillIndex = -1;
            _isAttacking = false;
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
            Vector2 lockedPos = _cachedTarget.transform.position;
            Debug.Log($"[CombatSystem] {gameObject.name}: LockOn target position locked at {lockedPos}");


            // Check if we need continuous damage during lock-on displacement
            if (skillEffect.damagesDuringDisplacement)
            {
                // For MeleeSkillEffectData, we need the shapes and facing info
                MeleeSkillEffectData meleeEffect = skillEffect as MeleeSkillEffectData;
                if (meleeEffect != null && meleeEffect.skillRangeShapes != null && meleeEffect.skillRangeShapes.Length > 0)
                {
                    CharacterEntity casterEntity = GetComponent<CharacterEntity>();
                    string targetTag = (casterEntity.RuntimeStats.characterType == CharacterType.Player)
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
