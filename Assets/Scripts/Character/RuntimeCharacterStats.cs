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
        public float minAttackDistance;
        public AttackRangeShape[] attackRangeShapes;
        public bool defaultFacesRight;
        public QualityLevel qualityLevel;
        public CharacterType characterType;

        // Combo attack parameters
        public int comboCount;
        public float[] comboDamageMultipliers;

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
        /// Returns the damage multiplier for the given combo step.
        /// If comboDamageMultipliers is not configured or the index is out of range, returns 1.0.
        /// </summary>
        /// <param name="step">Zero-based combo step index.</param>
        public float GetComboDamageMultiplier(int step)
        {
            if (comboDamageMultipliers == null || step < 0 || step >= comboDamageMultipliers.Length)
            {
                return 1.0f;
            }
            return comboDamageMultipliers[step];
        }

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

            // Compute engage distance directly from attack range (90% of max attack distance).
            float maxAttackDist = AttackRangeHelper.GetMaxAttackDistance(data.attackRangeShapes);
            float engageDist = maxAttackDist * 0.9f;
            if (engageDist <= 0f)
            {
                engageDist = 0.5f; // Safety fallback
            }

            // Clamp minAttackDistance to be no larger than the computed engage distance.
            minAttackDistance = Mathf.Min(data.minAttackDistance, engageDist);
            if (minAttackDistance <= 0f)
            {
                minAttackDistance = 0.05f;
            }

            attackRangeShapes = data.attackRangeShapes;
            defaultFacesRight = data.defaultFacesRight;
            qualityLevel = data.qualityLevel;
            characterType = data.characterType;

            // Combo attack parameters
            comboCount = Mathf.Max(1, data.comboCount);
            comboDamageMultipliers = data.comboDamageMultipliers;

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
        /// Check if a target is within this character's attack range, ignoring Y-axis difference
        /// and considering the target's collider X extent.
        /// For side-scrolling games: as long as the attack range overlaps with the target's
        /// collider X interval, the target is considered in range.
        /// </summary>
        /// <param name="ownerPos">Owner character collider center position.</param>
        /// <param name="facingSign">1 for facing right, -1 for facing left.</param>
        /// <param name="targetPos">Target collider center position.</param>
        /// <param name="targetHalfExtentX">Half width of the target's collider bounds on X axis.</param>
        /// <returns>True if target is in attack range.</returns>
        public bool IsTargetInAttackRange(Vector2 ownerPos, float facingSign, Vector2 targetPos, float targetHalfExtentX)
        {
            float effectiveFacingSign = defaultFacesRight ? facingSign : -facingSign;

            // Ignore Y-axis difference: project target onto owner's Y level
            Vector2 flatTargetPos = new Vector2(targetPos.x, ownerPos.y);

            // First check: target collider center (X-aligned) is in attack range
            if (AttackRangeHelper.IsTargetInRange(ownerPos, effectiveFacingSign, attackRangeShapes, flatTargetPos))
            {
                return true;
            }

            // Second check: if the attack range overlaps with the target's collider X interval,
            // test the left and right edges of the target's collider.
            // This ensures that even if the center is slightly out of range,
            // the attack still connects as long as it touches the collider's X bounds.
            if (targetHalfExtentX > 0f)
            {
                Vector2 leftEdge = new Vector2(targetPos.x - targetHalfExtentX, ownerPos.y);
                if (AttackRangeHelper.IsTargetInRange(ownerPos, effectiveFacingSign, attackRangeShapes, leftEdge))
                {
                    return true;
                }

                Vector2 rightEdge = new Vector2(targetPos.x + targetHalfExtentX, ownerPos.y);
                if (AttackRangeHelper.IsTargetInRange(ownerPos, effectiveFacingSign, attackRangeShapes, rightEdge))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the engage distance derived from attack range (90% of max attack distance).
        /// AI uses this to decide when to switch from Engage to Strike state.
        /// </summary>
        public float GetEngageDistance()
        {
            float maxDist = GetMaxAttackDistance();
            float engage = maxDist * 0.9f;
            return engage > 0f ? engage : 0.5f;
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
        /// Applies cultivation skill CD reduction if available.
        /// </summary>
        public void StartSkillCooldown(int skillIndex, float cooldownTime)
        {
            if (skillCooldowns != null && skillCooldowns.ContainsKey(skillIndex))
            {
                // Apply skill CD reduction from cultivation system
                float cdMultiplier = CultivationManager.Instance.GetSkillCDMultiplier(characterId);
                skillCooldowns[skillIndex] = cooldownTime * cdMultiplier;
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
