using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manages combat actions (normal attack and skills) for a character.
    /// Provides public interfaces for both AI and manual control to invoke.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class CombatSystem : MonoBehaviour
    {
        private CharacterEntity entity;
        private float lastAttackTime = -999f;

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
        }

        /// <summary>
        /// Attempt a normal attack on the target.
        /// Respects attack speed interval (no CD, but limited by attack speed).
        /// </summary>
        /// <returns>True if attack was executed.</returns>
        public bool TryNormalAttack(CharacterEntity target)
        {
            if (entity == null || !entity.RuntimeStats.IsAlive) return false;
            if (target == null || !target.RuntimeStats.IsAlive) return false;

            // Check attack range
            float dist = Vector2.Distance(transform.position, target.transform.position);
            if (dist > entity.RuntimeStats.attackRange) return false;

            // Check attack speed interval
            float attackInterval = 1f / entity.RuntimeStats.attackSpeed;
            if (Time.time - lastAttackTime < attackInterval) return false;

            // Face the target
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.FaceTowards(target.transform.position);
                entity.CharAnimator.PlayAttack();
            }

            // Deal damage
            target.TakeDamage(entity.RuntimeStats.attackPower);
            lastAttackTime = Time.time;

            return true;
        }

        /// <summary>
        /// Attempt to use a skill on the target.
        /// Checks cooldown, range, and plays animation/effects.
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

            // Face the target
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.FaceTowards(target.transform.position);
                entity.CharAnimator.PlaySkill(skillIndex);
            }

            // Deal skill damage
            target.TakeDamage(skillData.damage);

            // Start cooldown
            entity.RuntimeStats.StartSkillCooldown(skillIndex, skillData.cooldown);

            // Spawn skill effect
            if (skillData.effectPrefab != null)
            {
                GameObject effect = Instantiate(
                    skillData.effectPrefab,
                    target.transform.position,
                    Quaternion.identity
                );
                Destroy(effect, 2f);
            }

            return true;
        }

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
