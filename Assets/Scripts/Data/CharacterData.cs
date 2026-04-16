using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Character data template (ScriptableObject).
    /// Reusable configuration for players, enemies, and bosses.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCharacterData", menuName = "Game/CharacterData")]
    public class CharacterData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique character identifier")]
        public string characterId;

        [Tooltip("Display name of the character")]
        public string characterName;

        [Header("Combat Stats")]
        [Tooltip("Base attack power")]
        [Min(0f)]
        public float attackPower = 10f;

        [Tooltip("Maximum health points")]
        [Min(1f)]
        public float maxHealth = 100f;

        [Tooltip("Defense value, reduces incoming damage")]
        [Min(0f)]
        public float defense = 5f;

        [Tooltip("Movement speed (units per second)")]
        [Min(0f)]
        public float moveSpeed = 3f;

        [Tooltip("Attack speed (attacks per second)")]
        [Min(0.1f)]
        public float attackSpeed = 1f;

        [Tooltip("Normal attack range (fallback if no attack range shapes defined)")]
        [Min(0f)]
        public float attackRange = 1.5f;

        [Header("Attack Range Shapes")]
        [Tooltip("Composable attack range shapes. Union of all shapes defines the final attack area. Leave empty to use attackRange as a simple circle.")]
        public AttackRangeShape[] attackRangeShapes;

        [Header("Sprite Orientation")]
        [Tooltip("Whether the sprite asset faces right by default. " +
                 "Uncheck this if the sprite faces left in its original art. " +
                 "Attack range shapes are configured relative to this default facing direction.")]
        public bool defaultFacesRight = true;

        [Header("Classification")]
        [Tooltip("Quality level determines max skill count: A=1, S=2, SS=3, SSS=4")]
        public QualityLevel qualityLevel = QualityLevel.A;

        [Tooltip("Character type: Player, MinorEnemy, or Boss")]
        public CharacterType characterType = CharacterType.Player;

        [Header("Skills")]
        [Tooltip("Skill list. Count must not exceed quality level limit.")]
        public SkillData[] skills;

        [Header("Visuals")]
        [Tooltip("Character sprite")]
        public Sprite sprite;

        [Tooltip("Animator controller for this character")]
        public RuntimeAnimatorController animatorController;

        /// <summary>
        /// Returns the maximum number of skills allowed for the current quality level.
        /// </summary>
        public int GetMaxSkillCount()
        {
            switch (qualityLevel)
            {
                case QualityLevel.A:   return 1;
                case QualityLevel.S:   return 2;
                case QualityLevel.SS:  return 3;
                case QualityLevel.SSS: return 4;
                default:               return 1;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only validation: warns if skill count exceeds quality level limit.
        /// </summary>
        private void OnValidate()
        {
            if (skills != null && skills.Length > GetMaxSkillCount())
            {
                Debug.LogWarning(
                    $"[CharacterData] \"{characterName}\" has {skills.Length} skills, " +
                    $"but quality level {qualityLevel} only allows {GetMaxSkillCount()}. " +
                    $"Extra skills will be ignored at runtime.",
                    this
                );
            }
        }
#endif
    }
}
