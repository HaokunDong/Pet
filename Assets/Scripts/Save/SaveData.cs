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
    /// Root save data container for all player characters.
    /// </summary>
    [System.Serializable]
    public class GameSaveData
    {
        public int version = 1;
        public List<CharacterSaveData> characters = new List<CharacterSaveData>();
    }
}
