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
        public float attackDistance;
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

            // Store attack distance directly
            attackDistance = data.attackDistance > 0f ? data.attackDistance : 0.5f;

            // Compute engage distance directly from attack distance (90%).
            float engageDist = attackDistance * 0.9f;
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
        /// Uses simple horizontal distance check from owner's Transform forward direction.
        /// </summary>
        /// <param name="ownerPos">Owner character world position.</param>
        /// <param name="facingSign">1 for facing right, -1 for facing left.</param>
        /// <param name="targetPos">Target world position.</param>
        /// <returns>True if target is in attack range.</returns>
        public bool IsTargetInAttackRange(Vector2 ownerPos, float facingSign, Vector2 targetPos)
        {
            float dx = targetPos.x - ownerPos.x;
            // Target must be in the facing direction
            if (facingSign > 0 && dx < 0) return false;
            if (facingSign < 0 && dx > 0) return false;
            return Mathf.Abs(dx) <= attackDistance;
        }

        /// <summary>
        /// Get the engage distance derived from attack distance.
        /// AI uses this to decide when to switch from Engage to Strike state.
        /// The engage distance is the effective attack trigger distance (attackDistance - minAttackDistance buffer)
        /// multiplied by 0.9 to ensure the character walks slightly past the engage threshold.
        /// </summary>
        public float GetEngageDistance()
        {
            float effectiveRange = attackDistance - minAttackDistance;
            float engage = effectiveRange * 0.9f;
            return engage > 0f ? engage : 0.5f;
        }

        /// <summary>
        /// Get the maximum attack distance for AI chase calculations.
        /// Returns the configured attackDistance directly.
        /// </summary>
        public float GetMaxAttackDistance()
        {
            return attackDistance;
        }

        /// <summary>
        /// Calculate the horizontal distance from a position to the nearest X-axis edge of a target's collider.
        /// Used by AI system for distance-based attack range detection against collider edges.
        /// </summary>
        /// <param name="ownerPosX">Owner's X position (Transform.position.x).</param>
        /// <param name="targetCollider">Target's Collider2D component.</param>
        /// <returns>The horizontal distance to the nearest edge of the target's collider bounds.</returns>
        public static float GetDistanceToColliderEdge(float ownerPosX, Collider2D targetCollider)
        {
            if (targetCollider == null) return float.MaxValue;

            Bounds bounds = targetCollider.bounds;
            float nearestX;

            if (ownerPosX < bounds.min.x)
            {
                // Owner is to the left of the collider
                nearestX = bounds.min.x;
            }
            else if (ownerPosX > bounds.max.x)
            {
                // Owner is to the right of the collider
                nearestX = bounds.max.x;
            }
            else
            {
                // Owner is inside the collider's X range
                nearestX = ownerPosX;
            }

            return Mathf.Abs(ownerPosX - nearestX);
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
