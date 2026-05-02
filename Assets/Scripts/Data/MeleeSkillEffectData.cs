using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Melee skill effect: deals AOE damage to all enemies within the configured
    /// attack range shapes when the skill animation reaches its hit frame event.
    /// Shapes are defined as an array of AttackRangeShape (Box / Circle) with individual offsets.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMeleeSkillEffect", menuName = "Game/SkillEffect/Melee")]
    public class MeleeSkillEffectData : SkillEffectData
    {
        [Header("Skill Range Shapes")]
        [Tooltip("Composable attack range shapes for this skill. " +
                 "Union of all shapes defines the final skill damage area. " +
                 "Each shape supports independent offset and size.")]
        public AttackRangeShape[] skillRangeShapes;

        [Header("Editor Preview")]
        [Tooltip("Character sprite displayed in the Inspector preview to help adjust skill range shapes.")]
        public Sprite previewSprite;

        [Tooltip("Whether the preview sprite faces right by default. " +
                 "Used to correctly mirror shape offsets in the preview and at runtime.")]
        public bool defaultFacesRight = true;

        /// <summary>
        /// Execute the melee skill effect.
        /// Finds all opposing-faction alive characters within the skill range shapes
        /// and deals damage from the SkillData asset.
        /// </summary>
        public override void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)
        {
            if (caster == null) return;

            // Early out if no shapes configured
            if (skillRangeShapes == null || skillRangeShapes.Length == 0)
            {
                Debug.Log("[MeleeSkillEffectData] No skill range shapes configured, skipping damage.");
                return;
            }

            CharacterEntity casterEntity = caster.GetComponent<CharacterEntity>();
            if (casterEntity == null) return;

            // Determine the enemy tag based on caster's character type
            string targetTag = (casterEntity.RuntimeStats.characterType == CharacterType.Player)
                ? "Enemy"
                : "Player";

            // Get caster facing direction and compute effective facing sign
            float rawFacingSign = caster.CachedFacingSign;
            float effectiveFacingSign = defaultFacesRight ? rawFacingSign : -rawFacingSign;

            Vector2 casterPos = caster.transform.position;

            // Find all candidates and check against skill range shapes
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            int hitCount = 0;

            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                Vector2 candidatePos = candidateEntity.transform.position;

                if (AttackRangeHelper.IsTargetInRange(casterPos, effectiveFacingSign, skillRangeShapes, candidatePos))
                {
                    candidateEntity.TakeDamage(skillData.damage, casterEntity);
                    hitCount++;
                }
            }

            if (hitCount == 0)
            {
                Debug.Log($"[MeleeSkillEffectData] {caster.gameObject.name}: No targets in skill range at hit frame.");
            }
            else
            {
                Debug.Log($"[MeleeSkillEffectData] {caster.gameObject.name}: Skill hit {hitCount} target(s).");
            }
        }
    }
}
