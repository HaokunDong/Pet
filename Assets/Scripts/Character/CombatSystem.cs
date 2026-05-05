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

            // Check attack range using multi-shape system
            float facingSign = 1f;
            if (entity.CharAnimator != null)
                facingSign = entity.CharAnimator.FacingDirection;
            else
                facingSign = target.transform.position.x >= transform.position.x ? 1f : -1f;

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
            if (entity == null || !entity.RuntimeStats.IsAlive) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - entity null or dead"); return false; }
            if (target == null || !target.RuntimeStats.IsAlive) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - target null or dead"); return false; }

            // Validate skill index
            if (entity.characterData.skills == null) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - skills null"); return false; }
            if (skillIndex < 0 || skillIndex >= entity.characterData.skills.Length) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - skillIndex {skillIndex} out of range (length={entity.characterData.skills.Length})"); return false; }
            if (skillIndex >= entity.characterData.GetMaxSkillCount()) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - skillIndex {skillIndex} >= maxSkillCount {entity.characterData.GetMaxSkillCount()}"); return false; }

            // Check cooldown
            if (!entity.RuntimeStats.IsSkillReady(skillIndex)) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - skill {skillIndex} not ready (on cooldown)"); return false; }

            SkillData skillData = entity.characterData.skills[skillIndex];
            if (skillData == null) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - skillData null"); return false; }

            // Check skill range
            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist > skillData.skillRange) { Debug.Log($"[CombatSystem] {gameObject.name}: TryUseSkill - out of range (dist={dist:F2}, skillRange={skillData.skillRange})"); return false; }

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

            if (hitCount == 0)
            {
                Debug.Log($"[CombatSystem] {gameObject.name}: No targets in attack range at hit frame.");
            }
            else
            {
                Debug.Log($"[CombatSystem] {gameObject.name}: Normal attack hit {hitCount} target(s).");
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
                ClearAttackState();
                return;
            }

            // Legacy fallback: direct damage (no SkillEffectData assigned)
            if (_cachedTarget == null || !_cachedTarget.RuntimeStats.IsAlive)
            {
                Debug.Log($"[CombatSystem] {gameObject.name}: Cached target is null or dead, skipping skill damage.");
                ClearAttackState();
                return;
            }

            ClearAttackState();
        }

        /// <summary>
        /// Clear all cached attack state. Called when attack is interrupted or completed.
        /// Also clears skill animation protection state so the character can immediately
        /// transition to other actions (e.g. normal attack while skills are on cooldown).
        /// </summary>
        public void ClearAttackState()
        {
            _cachedTarget = null;
            _cachedFacingSign = 0f;
            _cachedSkillIndex = -1;
            _isAttacking = false;

            // Clear skill animation protection so the character can act again immediately
            if (entity != null && entity.CharAnimator != null)
            {
                entity.CharAnimator.ClearSkillState();
            }
        }

        // ==================== Utility ====================

        /// <summary>
        /// Get the index of the first skill that is ready to use.
        /// Returns -1 if no skill is available.
        /// </summary>
        public int GetFirstReadySkillIndex()
        {
            if (entity.characterData.skills == null)
            {
                Debug.Log($"[CombatSystem] {gameObject.name}: GetFirstReadySkillIndex - skills array is null");
                return -1;
            }

            int maxSkills = entity.characterData.GetMaxSkillCount();
            Debug.Log($"[CombatSystem] {gameObject.name}: GetFirstReadySkillIndex - skills.Length={entity.characterData.skills.Length}, maxSkills={maxSkills}");
            for (int i = 0; i < entity.characterData.skills.Length && i < maxSkills; i++)
            {
                bool ready = entity.RuntimeStats.IsSkillReady(i);
                Debug.Log($"[CombatSystem] {gameObject.name}: Skill[{i}] ready={ready}");
                if (ready)
                    return i;
            }

            return -1;
        }
    }
}
