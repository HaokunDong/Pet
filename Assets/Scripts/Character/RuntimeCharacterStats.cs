using System.Collections.Generic;

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
        public float attackRange;
        public QualityLevel qualityLevel;
        public CharacterType characterType;

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
            attackRange = data.attackRange;
            qualityLevel = data.qualityLevel;
            characterType = data.characterType;

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
