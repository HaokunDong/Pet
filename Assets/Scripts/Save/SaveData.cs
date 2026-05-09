using System.Collections.Generic;

namespace PetGame
{
    /// <summary>
    /// Serializable save data structure for a single character.
    /// </summary>
    [System.Serializable]
    public class CharacterSaveData
    {
        public string characterId;
        public float currentHealth;
        public float positionX;
        public float positionY;
        public List<SkillCooldownData> skillCooldowns = new List<SkillCooldownData>();
        public CultivationSaveData cultivation;
    }

    /// <summary>
    /// Serializable skill cooldown state.
    /// </summary>
    [System.Serializable]
    public class SkillCooldownData
    {
        public int skillIndex;
        public float remainingCooldown;
    }

    /// <summary>
    /// Serializable cultivation data for save/load.
    /// </summary>
    [System.Serializable]
    public class CultivationSaveData
    {
        public int level = 1;
        public int currentExp = 0;
        public int talentPoints = 0;
        public int attackLevel = 0;
        public int defenseLevel = 0;
        public int healthLevel = 0;
        public int attackSpeedLevel = 0;
        public int moveSpeedLevel = 0;
        public int skillCDLevel = 0;
    }

    /// <summary>
    /// Root save data container for all player characters.
    /// </summary>
    [System.Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public List<CharacterSaveData> characters = new List<CharacterSaveData>();
    }
}
