using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manages saving and loading player character data to/from local JSON files.
    /// Inherits from the project's Singleton pattern.
    /// </summary>
    public class SaveManager : Singleton<SaveManager>
    {
        private const string SAVE_FILE_NAME = "game_save.json";

        /// <summary>
        /// Full path to the save file.
        /// </summary>
        private string SaveFilePath => Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);

        /// <summary>
        /// Save all player characters' runtime data to a JSON file.
        /// </summary>
        public void SaveGame(List<CharacterEntity> playerCharacters)
        {
            GameSaveData saveData = new GameSaveData();

            foreach (CharacterEntity entity in playerCharacters)
            {
                if (entity == null || entity.RuntimeStats == null) continue;
                if (entity.RuntimeStats.characterType != CharacterType.Player) continue;

                CharacterSaveData charSave = new CharacterSaveData
                {
                    characterId = entity.RuntimeStats.characterId,
                    currentHealth = entity.RuntimeStats.currentHealth,
                    positionX = entity.transform.position.x,
                    positionY = entity.transform.position.y,
                    skillCooldowns = new List<SkillCooldownData>()
                };

                // Save skill cooldown states
                if (entity.RuntimeStats.skillCooldowns != null)
                {
                    foreach (var kvp in entity.RuntimeStats.skillCooldowns)
                    {
                        charSave.skillCooldowns.Add(new SkillCooldownData
                        {
                            skillIndex = kvp.Key,
                            remainingCooldown = kvp.Value
                        });
                    }
                }

                // Save cultivation data
                CultivationData cultData = CultivationManager.Instance.GetCultivationData(entity.RuntimeStats.characterId);
                if (cultData != null)
                {
                    charSave.cultivation = new CultivationSaveData
                    {
                        level = cultData.level,
                        currentExp = cultData.currentExp,
                        talentPoints = cultData.talentPoints,
                        attackLevel = cultData.attackLevel,
                        defenseLevel = cultData.defenseLevel,
                        healthLevel = cultData.healthLevel,
                        attackSpeedLevel = cultData.attackSpeedLevel,
                        moveSpeedLevel = cultData.moveSpeedLevel,
                        skillCDLevel = cultData.skillCDLevel
                    };
                }

                saveData.characters.Add(charSave);
            }

            string json = JsonUtility.ToJson(saveData, true);

            try
            {
                File.WriteAllText(SaveFilePath, json);
                Debug.Log($"[SaveManager] Game saved to: {SaveFilePath}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to save game: {e.Message}");
            }
        }

        /// <summary>
        /// Load saved game data from the JSON file.
        /// Returns null if no save file exists or if it's corrupted.
        /// </summary>
        public GameSaveData LoadGame()
        {
            if (!File.Exists(SaveFilePath))
            {
                Debug.Log("[SaveManager] No save file found, using defaults.");
                return null;
            }

            try
            {
                string json = File.ReadAllText(SaveFilePath);
                GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
                Debug.Log($"[SaveManager] Game loaded from: {SaveFilePath}");
                return saveData;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[SaveManager] Failed to load game: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Apply loaded save data to a character entity.
        /// If no matching save data is found, the character keeps its default values.
        /// </summary>
        public void ApplySaveData(CharacterEntity entity, GameSaveData saveData)
        {
            if (entity == null || saveData == null || entity.RuntimeStats == null) return;

            CharacterSaveData charSave = saveData.characters.Find(
                c => c.characterId == entity.RuntimeStats.characterId
            );

            if (charSave == null)
            {
                // No save data for this character, keep defaults (forward compatibility)
                Debug.Log($"[SaveManager] No save data for character '{entity.RuntimeStats.characterId}', using defaults.");
                return;
            }

            // Restore health
            entity.RuntimeStats.currentHealth = charSave.currentHealth;

            // Restore position
            entity.transform.position = new Vector3(charSave.positionX, charSave.positionY, 0f);

            // Restore skill cooldowns
            if (charSave.skillCooldowns != null)
            {
                foreach (SkillCooldownData cd in charSave.skillCooldowns)
                {
                    if (entity.RuntimeStats.skillCooldowns.ContainsKey(cd.skillIndex))
                    {
                        entity.RuntimeStats.skillCooldowns[cd.skillIndex] = cd.remainingCooldown;
                    }
                }
            }

            // Restore cultivation data
            if (charSave.cultivation != null)
            {
                CultivationData cultData = new CultivationData
                {
                    level = charSave.cultivation.level,
                    currentExp = charSave.cultivation.currentExp,
                    talentPoints = charSave.cultivation.talentPoints,
                    attackLevel = charSave.cultivation.attackLevel,
                    defenseLevel = charSave.cultivation.defenseLevel,
                    healthLevel = charSave.cultivation.healthLevel,
                    attackSpeedLevel = charSave.cultivation.attackSpeedLevel,
                    moveSpeedLevel = charSave.cultivation.moveSpeedLevel,
                    skillCDLevel = charSave.cultivation.skillCDLevel
                };
                CultivationManager.Instance.SetCultivationData(entity.RuntimeStats.characterId, cultData);
                CultivationManager.Instance.ApplyCultivationBonuses(entity);
            }
        }

        /// <summary>
        /// Delete the save file.
        /// </summary>
        public void DeleteSave()
        {
            if (File.Exists(SaveFilePath))
            {
                File.Delete(SaveFilePath);
                Debug.Log("[SaveManager] Save file deleted.");
            }
        }

        /// <summary>
        /// Check if a save file exists.
        /// </summary>
        public bool HasSaveFile()
        {
            return File.Exists(SaveFilePath);
        }
    }
}
