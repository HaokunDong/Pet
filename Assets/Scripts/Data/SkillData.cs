using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Skill data template (ScriptableObject).
    /// Contains all static configuration for a single skill.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSkillData", menuName = "Game/SkillData")]
    public class SkillData : ScriptableObject
    {
        [Header("Basic Info")]
        [Tooltip("Unique skill identifier")]
        public string skillId;

        [Tooltip("Display name of the skill")]
        public string skillName;

        [Header("Combat Stats")]
        [Tooltip("Cooldown time in seconds")]
        [Min(0f)]
        public float cooldown = 5f;

        [Tooltip("Damage dealt by this skill")]
        [Min(0f)]
        public float damage = 10f;

        [Tooltip("Effective range of the skill")]
        [Min(0f)]
        public float skillRange = 2f;

        [Header("Visuals")]
        [Tooltip("Skill icon for UI display")]
        public Sprite icon;

        [Tooltip("Skill VFX prefab to instantiate on use")]
        public GameObject effectPrefab;
    }
}
