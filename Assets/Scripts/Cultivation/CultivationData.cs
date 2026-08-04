using System;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Enum representing the six upgradeable attribute types in the cultivation system.
    /// </summary>
    public enum AttributeType
    {
        Attack,
        Defense,
        Health,
        AttackSpeed,
        MoveSpeed,
        SkillCD
    }

    /// <summary>
    /// Runtime cultivation data for a single character.
    /// Tracks level, experience, talent points, and attribute bonus levels.
    /// </summary>
    [System.Serializable]
    public class CultivationData
    {
        public int level = 1;
        public int currentExp = 0;
        public int talentPoints = 0;

        // Attribute bonus levels
        public int attackLevel = 0;
        public int defenseLevel = 0;
        public int healthLevel = 0;
        public int attackSpeedLevel = 0;
        public int moveSpeedLevel = 0;
        public int skillCDLevel = 0;

        // Events for UI updates
        public event Action<int, int> OnExpChanged;       // (currentExp, requiredExp)
        public event Action<int> OnLevelUp;               // (newLevel)
        public event Action<int> OnTalentPointChanged;    // (currentTalentPoints)
        public event Action<AttributeType, int> OnAttributeUpgraded; // (type, newLevel)

        /// <summary>
        /// Reference to the cultivation config for formula calculations.
        /// Must be set before calling AddExp or UpgradeAttribute.
        /// </summary>
        [NonSerialized]
        private CultivationConfig config;

        public void SetConfig(CultivationConfig cultivationConfig)
        {
            config = cultivationConfig;
        }

        /// <summary>
        /// Get the experience required to level up from the current level.
        /// </summary>
        public int GetRequiredExp()
        {
            if (config == null)
            {
                Debug.LogWarning("[CultivationData] Config not set, using default formula.");
                return Mathf.RoundToInt(100 * Mathf.Pow(level, 1.5f));
            }
            return config.GetRequiredExp(level);
        }

        /// <summary>
        /// Add experience points. Handles level-up overflow (multiple level-ups in one call).
        /// </summary>
        public void AddExp(int amount)
        {
            if (amount <= 0) return;

            // Check if already at max level
            if (config != null && config.IsMaxLevel(level))
            {
                return;
            }

            currentExp += amount;

            // Process level-ups (handle overflow / multiple level-ups)
            int requiredExp = GetRequiredExp();
            while (currentExp >= requiredExp)
            {
                // Check max level before leveling up
                if (config != null && config.IsMaxLevel(level))
                {
                    currentExp = requiredExp; // Cap at max
                    break;
                }

                currentExp -= requiredExp;
                level++;
                talentPoints++;

                OnLevelUp?.Invoke(level);
                OnTalentPointChanged?.Invoke(talentPoints);

                // Recalculate required exp for new level
                requiredExp = GetRequiredExp();
            }

            OnExpChanged?.Invoke(currentExp, GetRequiredExp());
        }

        /// <summary>
        /// Attempt to upgrade the specified attribute by consuming one talent point.
        /// Returns true if successful, false if no talent points available.
        /// </summary>
        public bool UpgradeAttribute(AttributeType type)
        {
            if (talentPoints <= 0) return false;

            talentPoints--;

            switch (type)
            {
                case AttributeType.Attack:
                    attackLevel++;
                    break;
                case AttributeType.Defense:
                    defenseLevel++;
                    break;
                case AttributeType.Health:
                    healthLevel++;
                    break;
                case AttributeType.AttackSpeed:
                    attackSpeedLevel++;
                    break;
                case AttributeType.MoveSpeed:
                    moveSpeedLevel++;
                    break;
                case AttributeType.SkillCD:
                    skillCDLevel++;
                    break;
            }

            OnTalentPointChanged?.Invoke(talentPoints);
            OnAttributeUpgraded?.Invoke(type, GetAttributeLevel(type));

            return true;
        }

        /// <summary>
        /// Get the current bonus level for a specific attribute.
        /// </summary>
        public int GetAttributeLevel(AttributeType type)
        {
            switch (type)
            {
                case AttributeType.Attack: return attackLevel;
                case AttributeType.Defense: return defenseLevel;
                case AttributeType.Health: return healthLevel;
                case AttributeType.AttackSpeed: return attackSpeedLevel;
                case AttributeType.MoveSpeed: return moveSpeedLevel;
                case AttributeType.SkillCD: return skillCDLevel;
                default: return 0;
            }
        }

        /// <summary>
        /// Get the total bonus percentage for a specific attribute.
        /// </summary>
        public float GetAttributeBonusPercent(AttributeType type)
        {
            if (config == null) return 0f;

            int attrLevel = GetAttributeLevel(type);
            switch (type)
            {
                case AttributeType.Attack: return attrLevel * config.attackBonusPerLevel;
                case AttributeType.Defense: return attrLevel * config.defenseBonusPerLevel;
                case AttributeType.Health: return attrLevel * config.healthBonusPerLevel;
                case AttributeType.AttackSpeed: return attrLevel * config.attackSpeedBonusPerLevel;
                case AttributeType.MoveSpeed: return attrLevel * config.moveSpeedBonusPerLevel;
                case AttributeType.SkillCD: return attrLevel * config.skillCDBonusPerLevel;
                default: return 0f;
            }
        }

        /// <summary>
        /// Check if any talent points are available for spending.
        /// </summary>
        public bool HasTalentPoints => talentPoints > 0;

        /// <summary>
        /// Calculate available talent points based on level and total spent points.
        /// Formula: (level - 1) - sum of all attribute levels.
        /// This is a validation method; normally talentPoints field is authoritative.
        /// </summary>
        public int GetAvailableTalentPoints()
        {
            int totalSpent = attackLevel + defenseLevel + healthLevel + attackSpeedLevel + moveSpeedLevel + skillCDLevel;
            return Mathf.Max(0, (level - 1) - totalSpent);
        }

        /// <summary>
        /// Directly set the attribute level for a specific type.
        /// Used by the talent confirmation system to batch-apply changes.
        /// Also adjusts talentPoints accordingly.
        /// </summary>
        public void SetAttributeLevel(AttributeType type, int newLevel)
        {
            int oldLevel = GetAttributeLevel(type);
            int diff = newLevel - oldLevel;
            if (diff == 0) return;

            // Adjust talent points
            talentPoints -= diff;

            switch (type)
            {
                case AttributeType.Attack: attackLevel = newLevel; break;
                case AttributeType.Defense: defenseLevel = newLevel; break;
                case AttributeType.Health: healthLevel = newLevel; break;
                case AttributeType.AttackSpeed: attackSpeedLevel = newLevel; break;
                case AttributeType.MoveSpeed: moveSpeedLevel = newLevel; break;
                case AttributeType.SkillCD: skillCDLevel = newLevel; break;
            }

            OnTalentPointChanged?.Invoke(talentPoints);
            OnAttributeUpgraded?.Invoke(type, newLevel);
        }
    }
}
