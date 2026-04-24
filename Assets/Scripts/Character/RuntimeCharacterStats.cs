using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Runtime data copy for a character instance.
    /// Created from CharacterData at instantiation time so each instance has independent state.
    /// </summary>
    [System.Serializable]
    public class RuntimeCharacterStats
    {
        public string characterId;
        public float currentHealth;
        public float maxHealth;
        public float attackPower;
        public float defense;
        public float moveSpeed;
        public float attackSpeed;
        public float engageDistance;
        public float minAttackDistance;
        public AttackRangeShape[] attackRangeShapes;
        public bool defaultFacesRight;
        public QualityLevel qualityLevel;
        public CharacterType characterType;

        // Knockback parameters
        public float knockbackHorizontalSpeed;
        public float knockbackVerticalSpeed;
        public float knockbackGravity;

        /// <summary>
        /// Remaining cooldown time for each skill (indexed by skill array position).
        /// </summary>
        public Dictionary<int, float> skillCooldowns;

        /// <summary>
        /// Whether the character is alive.
        /// </summary>
        public bool IsAlive => currentHealth > 0f;

        /// <summary>
        /// Initialize runtime stats from a CharacterData template.
        /// </summary>
        public void InitFromData(CharacterData data)
        {
            characterId = data.characterId;
            maxHealth = data.maxHealth;
            currentHealth = data.maxHealth;
            attackPower = data.attackPower;
            defense = data.defense;
            moveSpeed = data.moveSpeed;
            attackSpeed = data.attackSpeed;

            // Clamp engageDistance to be strictly smaller than the max attack distance.
            // If the designer set a value >= max attack distance (or left it at a larger default),
            // fall back to 90% of max attack distance so the AI stops inside its attack range.
            // Note: BTCombat.Execute() also uses IsTargetInAttackRange() for Strike entry,
            // so even if engageDistance is slightly off, the character will still attack correctly.
            float maxAttackDist = AttackRangeHelper.GetMaxAttackDistance(data.attackRangeShapes);
            engageDistance = Mathf.Min(data.engageDistance, maxAttackDist * 0.9f);
            if (engageDistance <= 0f)
            {
                // Safety: never let engageDistance be non-positive, otherwise AI can never enter Strike.
                engageDistance = maxAttackDist * 0.9f;
            }

            // Clamp minAttackDistance to be no larger than engageDistance.
            minAttackDistance = Mathf.Min(data.minAttackDistance, engageDistance);
            if (minAttackDistance <= 0f)
            {
                minAttackDistance = 0.05f;
            }

            attackRangeShapes = data.attackRangeShapes;
            defaultFacesRight = data.defaultFacesRight;
            qualityLevel = data.qualityLevel;
            characterType = data.characterType;

            // Knockback parameters
            knockbackHorizontalSpeed = data.knockbackHorizontalSpeed;
            knockbackVerticalSpeed = data.knockbackVerticalSpeed;
            knockbackGravity = data.knockbackGravity;

            skillCooldowns = new Dictionary<int, float>();
            if (data.skills != null)
            {
                int maxSkills = data.GetMaxSkillCount();
                for (int i = 0; i < data.skills.Length && i < maxSkills; i++)
                {
                    skillCooldowns[i] = 0f; // All skills start ready
                }
            }
        }

        /// <summary>
        /// Check if a target position is within this character's attack range.
        /// Uses multi-shape union check, falls back to simple circle if no shapes defined.
        /// </summary>
        /// <param name="ownerPos">Owner character world position.</param>
        /// <param name="facingSign">1 for facing right, -1 for facing left.</param>
        /// <param name="targetPos">Target world position.</param>
        /// <returns>True if target is in attack range.</returns>
        public bool IsTargetInAttackRange(Vector2 ownerPos, float facingSign, Vector2 targetPos)
        {
            // Convert runtime facing direction to effective facing sign relative to sprite's native orientation.
            // Shapes are configured relative to the sprite's default facing direction.
            // If sprite faces right by default: facingSign 1 (right) means no mirror, -1 (left) means mirror.
            // If sprite faces left by default:  facingSign -1 (left) means no mirror, 1 (right) means mirror.
            float effectiveFacingSign = defaultFacesRight ? facingSign : -facingSign;
            return AttackRangeHelper.IsTargetInRange(ownerPos, effectiveFacingSign, attackRangeShapes, targetPos);
        }

        /// <summary>
        /// Get the maximum attack distance for AI chase calculations.
        /// Returns the farthest reach across all attack range shapes.
        /// </summary>
        public float GetMaxAttackDistance()
        {
            return AttackRangeHelper.GetMaxAttackDistance(attackRangeShapes);
        }

        /// <summary>
        /// Check if a skill is off cooldown and ready to use.
        /// </summary>
        public bool IsSkillReady(int skillIndex)
        {
            if (skillCooldowns == null || !skillCooldowns.ContainsKey(skillIndex))
                return false;
            return skillCooldowns[skillIndex] <= 0f;
        }

        /// <summary>
        /// Start cooldown for a skill.
        /// </summary>
        public void StartSkillCooldown(int skillIndex, float cooldownTime)
        {
            if (skillCooldowns != null && skillCooldowns.ContainsKey(skillIndex))
            {
                skillCooldowns[skillIndex] = cooldownTime;
            }
        }

        /// <summary>
        /// Tick all skill cooldowns by deltaTime.
        /// </summary>
        public void UpdateCooldowns(float deltaTime)
        {
            if (skillCooldowns == null) return;

            var keys = new List<int>(skillCooldowns.Keys);
            foreach (int key in keys)
            {
                if (skillCooldowns[key] > 0f)
                {
                    skillCooldowns[key] -= deltaTime;
                    if (skillCooldowns[key] < 0f)
                        skillCooldowns[key] = 0f;
                }
            }
        }
    }
}
