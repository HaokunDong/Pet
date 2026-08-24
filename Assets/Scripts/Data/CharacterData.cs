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

        [Tooltip("Minimum attack distance: characters stop moving when closer than this distance to prevent overlap. " +
                 "Should be smaller than the attack range. Visualized as a green circle in Scene view and Editor preview.")]
        [Min(0.05f)]
        public float minAttackDistance = 0.3f;

        [Tooltip("Attack distance: the horizontal range from the character's Transform forward direction. " +
                 "Enemies within this distance in the facing direction are considered in attack range.")]
        [Min(0.1f)]
        public float attackDistance = 1.0f;

        [Header("Combo Attack")]
        [Tooltip("Number of combo attack steps. 1 = single attack (default, backward compatible). >1 = multi-step combo.")]
        [Min(1)]
        public int comboCount = 1;

        [Tooltip("Damage multiplier for each combo step. Index 0 = first attack, 1 = second, etc. " +
                 "If not set or length is insufficient, defaults to 1.0 for that step.")]
        public float[] comboDamageMultipliers;

        [Header("Normal Attack Displacement")]
        [Tooltip("Type of displacement for normal attack: Fixed (set direction + distance) or LockOn (lock target position, move toward it).")]
        public SkillDisplacementType attackDisplacementType = SkillDisplacementType.Fixed;

        [Tooltip("Direction of displacement for normal attack relative to facing direction. None = no displacement.")]
        public SkillDisplacementDirection attackDisplacementDirection = SkillDisplacementDirection.None;

        [Tooltip("Distance to displace during normal attack (world units). Used by Fixed type only.")]
        [Min(0f)]
        public float attackDisplacementDistance = 0f;

        [Tooltip("Duration of the normal attack displacement movement (seconds).")]
        [Min(0.01f)]
        public float attackDisplacementDuration = 0.2f;

        [Tooltip("If true, damage detection continues during normal attack displacement. " +
                 "Enemies along the path will be hit. Each enemy is only hit once.")]
        public bool attackDamagesDuringDisplacement = false;

        [Header("Sprite Orientation")]
        [Tooltip("Whether the sprite asset faces right by default. " +
                 "Uncheck this if the sprite faces left in its original art.")]
        public bool defaultFacesRight = true;

        [Header("Knockback Settings")]
        [Tooltip("Horizontal knockback speed when hit (units/second). Higher = pushed back further.")]
        [Min(0f)]
        public float knockbackHorizontalSpeed = 2.0f;

        [Tooltip("Vertical knockback speed when hit (units/second). Higher = launched higher.")]
        [Min(0f)]
        public float knockbackVerticalSpeed = 1.5f;

        [Tooltip("Gravity applied during knockback (units/second²). Controls how fast the character falls back down.")]
        [Min(0.1f)]
        public float knockbackGravity = 8.0f;

        [Header("Classification")]
        [Tooltip("Quality level determines max skill count: A=1, S=2, SS=3, SSS=4")]
        public QualityLevel qualityLevel = QualityLevel.A;

        [Tooltip("Character type: Player, MinorEnemy, or Boss")]
        public CharacterType characterType = CharacterType.Player;

        [Header("Cultivation / Rewards")]
        [Tooltip("Experience points awarded to the player when this enemy is defeated. Only relevant for enemies.")]
        [Min(0)]
        public int expReward = 10;

        [Header("Respawn Settings")]
        [Tooltip("Per-character respawn cooldown in seconds. 0 means use global default from CharacterDeathManager.")]
        [Min(0f)]
        public float respawnCooldown = 0f;

        [Header("Skills")]
        [Tooltip("Skill list. Count must not exceed quality level limit.")]
        public SkillData[] skills;

        [Header("Description")]
        [Tooltip("Character description / backstory displayed on the card UI")]
        [TextArea(2, 5)]
        public string characterDescription = "";

        [Header("Evolution")]
        [Tooltip("The CharacterData that this character can evolve into. Null means no evolution available.")]
        public CharacterData evolutionTarget;

        [Tooltip("List of requirements that must be met before evolution is allowed. Empty list means evolution is unconditional.")]
        public EvolutionRequirement[] evolutionRequirements;

        [Header("Prefab")]
        [Tooltip("Character-specific Prefab Variant. If null, the default PlayerPrefab will be used.")]
        public GameObject prefab;

        [Header("Visuals")]
        [Tooltip("Character sprite")]
        public Sprite sprite;

        [Tooltip("Portrait image displayed on the card UI")]
        public Sprite portraitSprite;

        [Tooltip("Decoration image displayed on the card UI")]
        public Sprite decorationSprite;

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

        /// <summary>
        /// Returns the prefab name to use for object pool lookup.
        /// If a character-specific prefab is assigned, returns its name; otherwise returns null (use default).
        /// </summary>
        public string GetPrefabName()
        {
            return prefab != null ? prefab.name : null;
        }

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

            if (minAttackDistance > attackDistance * 0.9f)
            {
                Debug.LogWarning(
                    $"[CharacterData] \"{characterName}\" minAttackDistance ({minAttackDistance}) is greater than " +
                    $"90% of attackDistance ({attackDistance * 0.9f:F2}). Characters may never enter Strike state. " +
                    $"Consider reducing minAttackDistance.",
                    this
                );
            }
        }
#endif
    }
}
