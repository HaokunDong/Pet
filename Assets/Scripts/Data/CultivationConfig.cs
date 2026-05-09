using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Configuration ScriptableObject for the pet cultivation system.
    /// Defines experience formula parameters and per-level attribute bonus percentages.
    /// </summary>
    [CreateAssetMenu(fileName = "CultivationConfig", menuName = "Game/CultivationConfig")]
    public class CultivationConfig : ScriptableObject
    {
        [Header("Level & Experience")]
        [Tooltip("Base experience required to level up from level 1 to level 2")]
        [Min(1)]
        public int baseExp = 100;

        [Tooltip("Experience growth exponent. Required exp = baseExp * level^growthExponent")]
        [Min(1f)]
        public float growthExponent = 1.5f;

        [Tooltip("Maximum achievable level (0 = no limit)")]
        [Min(0)]
        public int maxLevel = 50;

        [Header("Attribute Bonuses (per level %)")]
        [Tooltip("Attack power bonus per talent level (e.g. 5 = 5% per level)")]
        [Min(0f)]
        public float attackBonusPerLevel = 5f;

        [Tooltip("Defense bonus per talent level (e.g. 5 = 5% per level)")]
        [Min(0f)]
        public float defenseBonusPerLevel = 5f;

        [Tooltip("Max health bonus per talent level (e.g. 5 = 5% per level)")]
        [Min(0f)]
        public float healthBonusPerLevel = 5f;

        [Tooltip("Attack speed bonus per talent level (e.g. 3 = 3% per level)")]
        [Min(0f)]
        public float attackSpeedBonusPerLevel = 3f;

        [Tooltip("Move speed bonus per talent level (e.g. 3 = 3% per level)")]
        [Min(0f)]
        public float moveSpeedBonusPerLevel = 3f;

        [Tooltip("Skill cooldown reduction per talent level (e.g. 3 = 3% per level)")]
        [Min(0f)]
        public float skillCDBonusPerLevel = 3f;

        /// <summary>
        /// Calculate the experience required to level up from the given level.
        /// Formula: baseExp * level^growthExponent
        /// </summary>
        public int GetRequiredExp(int level)
        {
            if (level <= 0) level = 1;
            return Mathf.RoundToInt(baseExp * Mathf.Pow(level, growthExponent));
        }

        /// <summary>
        /// Check if the given level has reached the maximum.
        /// </summary>
        public bool IsMaxLevel(int level)
        {
            return maxLevel > 0 && level >= maxLevel;
        }
    }
}
