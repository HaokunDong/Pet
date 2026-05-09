using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Core manager for the cultivation system.
    /// Manages cultivation data for each character and applies attribute bonuses to RuntimeCharacterStats.
    /// </summary>
    public class CultivationManager : Singleton<CultivationManager>
    {
        /// <summary>
        /// Cultivation data keyed by characterId.
        /// Each character has its own independent cultivation progress.
        /// </summary>
        private Dictionary<string, CultivationData> cultivationDataMap = new Dictionary<string, CultivationData>();

        /// <summary>
        /// The cultivation config loaded from Resources.
        /// </summary>
        private CultivationConfig config;

        /// <summary>
        /// Get or load the CultivationConfig from Resources.
        /// Falls back to a runtime-created default if no asset exists.
        /// </summary>
        public CultivationConfig Config
        {
            get
            {
                if (config == null)
                {
                    config = Resources.Load<CultivationConfig>("Data/CultivationConfig");
                    if (config == null)
                    {
                        // Create a runtime default config if none exists in Resources
                        config = ScriptableObject.CreateInstance<CultivationConfig>();
                        Debug.LogWarning("[CultivationManager] No CultivationConfig found in Resources/Data/. Using default values.");
                    }
                }
                return config;
            }
        }

        /// <summary>
        /// Get or create cultivation data for a specific character.
        /// </summary>
        public CultivationData GetCultivationData(string characterId)
        {
            if (string.IsNullOrEmpty(characterId))
            {
                Debug.LogError("[CultivationManager] characterId is null or empty.");
                return null;
            }

            if (!cultivationDataMap.ContainsKey(characterId))
            {
                CultivationData newData = new CultivationData();
                newData.SetConfig(Config);
                cultivationDataMap[characterId] = newData;
            }

            return cultivationDataMap[characterId];
        }

        /// <summary>
        /// Set cultivation data for a character (used when loading from save).
        /// </summary>
        public void SetCultivationData(string characterId, CultivationData data)
        {
            if (string.IsNullOrEmpty(characterId) || data == null) return;
            data.SetConfig(Config);
            cultivationDataMap[characterId] = data;
        }

        /// <summary>
        /// Apply cultivation bonuses to a character's RuntimeCharacterStats.
        /// Call this after initialization, after loading save data, or after any attribute upgrade.
        /// </summary>
        public void ApplyCultivationBonuses(CharacterEntity entity)
        {
            if (entity == null || entity.RuntimeStats == null) return;

            string characterId = entity.RuntimeStats.characterId;
            CultivationData data = GetCultivationData(characterId);
            if (data == null) return;

            // Get the base stats from CharacterData (original template)
            CharacterData baseData = entity.characterData;
            if (baseData == null) return;

            CultivationConfig cfg = Config;

            // Apply percentage bonuses: finalStat = baseStat * (1 + level * bonusPerLevel / 100)
            entity.RuntimeStats.attackPower = baseData.attackPower * (1f + data.attackLevel * cfg.attackBonusPerLevel / 100f);
            entity.RuntimeStats.defense = baseData.defense * (1f + data.defenseLevel * cfg.defenseBonusPerLevel / 100f);
            entity.RuntimeStats.maxHealth = baseData.maxHealth * (1f + data.healthLevel * cfg.healthBonusPerLevel / 100f);
            entity.RuntimeStats.attackSpeed = baseData.attackSpeed * (1f + data.attackSpeedLevel * cfg.attackSpeedBonusPerLevel / 100f);
            entity.RuntimeStats.moveSpeed = baseData.moveSpeed * (1f + data.moveSpeedLevel * cfg.moveSpeedBonusPerLevel / 100f);

            // Skill CD reduction is stored as a multiplier (lower is faster)
            // We don't modify a direct stat here; it will be applied when checking cooldowns
            // For now, store the reduction percentage in a way the combat system can use

            Debug.Log($"[CultivationManager] Applied bonuses to '{characterId}': " +
                $"ATK={entity.RuntimeStats.attackPower:F1}, DEF={entity.RuntimeStats.defense:F1}, " +
                $"HP={entity.RuntimeStats.maxHealth:F1}, ASPD={entity.RuntimeStats.attackSpeed:F2}, " +
                $"MSPD={entity.RuntimeStats.moveSpeed:F2}");
        }

        /// <summary>
        /// Add experience to a character and apply any resulting stat changes.
        /// On level-up, restores the character's health to full.
        /// </summary>
        public void AddExperience(CharacterEntity entity, int amount)
        {
            if (entity == null || entity.RuntimeStats == null) return;

            string characterId = entity.RuntimeStats.characterId;
            CultivationData data = GetCultivationData(characterId);
            if (data == null) return;

            int oldLevel = data.level;
            data.AddExp(amount);

            // If leveled up, re-apply bonuses and restore health to full
            if (data.level > oldLevel)
            {
                ApplyCultivationBonuses(entity);

                // Restore health to full on level-up
                entity.RuntimeStats.currentHealth = entity.RuntimeStats.maxHealth;
                Debug.Log($"[CultivationManager] '{characterId}' leveled up to {data.level}! Health restored to full ({entity.RuntimeStats.maxHealth:F0}).");
            }
        }

        /// <summary>
        /// Upgrade a specific attribute for a character and immediately apply the bonus.
        /// Returns true if the upgrade was successful.
        /// </summary>
        public bool UpgradeAttribute(CharacterEntity entity, AttributeType type)
        {
            if (entity == null || entity.RuntimeStats == null) return false;

            string characterId = entity.RuntimeStats.characterId;
            CultivationData data = GetCultivationData(characterId);
            if (data == null) return false;

            bool success = data.UpgradeAttribute(type);
            if (success)
            {
                ApplyCultivationBonuses(entity);
            }
            return success;
        }

        /// <summary>
        /// Get the skill cooldown reduction multiplier for a character.
        /// Returns a value like 0.85 meaning 15% CDR (skills cooldown 85% of original).
        /// </summary>
        public float GetSkillCDMultiplier(string characterId)
        {
            CultivationData data = GetCultivationData(characterId);
            if (data == null) return 1f;

            float reductionPercent = data.skillCDLevel * Config.skillCDBonusPerLevel;
            return Mathf.Max(0.1f, 1f - reductionPercent / 100f); // Cap at 90% CDR
        }

        /// <summary>
        /// Get all cultivation data (for save system).
        /// </summary>
        public Dictionary<string, CultivationData> GetAllCultivationData()
        {
            return cultivationDataMap;
        }

        /// <summary>
        /// Clear all cultivation data (for testing or new game).
        /// </summary>
        public void ClearAll()
        {
            cultivationDataMap.Clear();
        }
    }
}
