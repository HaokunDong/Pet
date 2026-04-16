using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manages combat actions (normal attack and skills) for a character.
    /// Provides public interfaces for both AI and manual control to invoke.
    /// Damage is applied via animation frame events through AnimEventReceiver.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class CombatSystem : MonoBehaviour
    {
        private CharacterEntity entity;
        private float lastAttackTime = -999f;

        // --- Cached attack state for animation frame event callbacks ---
        private CharacterEntity _cachedTarget;
        private int _cachedSkillIndex = -1;
        private bool _isAttacking;

        /// <summary>
        /// The current cached attack target. Read by AnimEventReceiver.
        /// </summary>
        public CharacterEntity CachedTarget => _cachedTarget;

        /// <summary>
        /// The current cached skill index (-1 means normal attack). Read by AnimEventReceiver.
        /// </summary>
        public int CachedSkillIndex => _cachedSkillIndex;

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

            // Check attack range using multi-shape system
            float facingSign = 1f;
            if (entity.CharAnimator != null)
                facingSign = entity.CharAnimator.FacingDirection;
            else
                facingSign = target.transform.position.x >= transform.position.x ? 1f : -1f;

            if (!entity.RuntimeStats.IsTargetInAttackRange(
                    transform.position, facingSign, target.transform.position))
                return false;

            // Check attack speed interval
            float attackInterval = 1f / entity.RuntimeStats.attackSpeed;
            if (Time.time - lastAttackTime < attackInterval) return false;

            // Cache target for frame event callback
            _cachedTarget = target;
            _cachedSkillIndex = -1;
            _isAttacking = true;

            // Face the target and play attack animation
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.FaceTowards(target.transform.position);
                entity.CharAnimator.PlayAttack();
            }

            lastAttackTime = Time.time;

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

            // Cache target and skill index for frame event callback
            _cachedTarget = target;
            _cachedSkillIndex = skillIndex;
            _isAttacking = true;

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
        /// Applies damage to the cached target and clears attack state.
        /// </summary>
        public void ApplyNormalAttackDamage()
        {
            if (_cachedTarget == null || !_cachedTarget.RuntimeStats.IsAlive)
            {
                Debug.Log($"[CombatSystem] {gameObject.name}: Cached target is null or dead, skipping normal attack damage.");
                ClearAttackState();
                return;
            }

            _cachedTarget.TakeDamage(entity.RuntimeStats.attackPower);
            ClearAttackState();
        }

        /// <summary>
        /// Called by AnimEventReceiver when the skill animation reaches the hit frame.
        /// Applies skill damage and spawns effects, then clears attack state.
        /// </summary>
        public void ApplySkillDamage()
        {
            if (_cachedTarget == null || !_cachedTarget.RuntimeStats.IsAlive)
            {
                Debug.Log($"[CombatSystem] {gameObject.name}: Cached target is null or dead, skipping skill damage.");
                ClearAttackState();
                return;
            }

            if (_cachedSkillIndex < 0 || entity.characterData.skills == null ||
                _cachedSkillIndex >= entity.characterData.skills.Length)
            {
                Debug.LogWarning($"[CombatSystem] {gameObject.name}: Invalid cached skill index {_cachedSkillIndex}, skipping skill damage.");
                ClearAttackState();
                return;
            }

            SkillData skillData = entity.characterData.skills[_cachedSkillIndex];
            if (skillData == null)
            {
                Debug.LogWarning($"[CombatSystem] {gameObject.name}: SkillData at index {_cachedSkillIndex} is null, skipping skill damage.");
                ClearAttackState();
                return;
            }

            // Apply skill damage
            _cachedTarget.TakeDamage(skillData.damage);

            // Spawn skill effect at target position
            if (skillData.effectPrefab != null)
            {
                GameObject effect = Instantiate(
                    skillData.effectPrefab,
                    _cachedTarget.transform.position,
                    Quaternion.identity
                );
                Destroy(effect, 2f);
            }

            ClearAttackState();
        }

        /// <summary>
        /// Clear all cached attack state. Called when attack is interrupted or completed.
        /// </summary>
        public void ClearAttackState()
        {
            _cachedTarget = null;
            _cachedSkillIndex = -1;
            _isAttacking = false;
        }

        // ==================== Utility ====================

        /// <summary>
        /// Get the index of the first skill that is ready to use.
        /// Returns -1 if no skill is available.
        /// </summary>
        public int GetFirstReadySkillIndex()
        {
            if (entity.characterData.skills == null) return -1;

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
