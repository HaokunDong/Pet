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

        [Tooltip("Whether the character's sprite faces right by default. " +
                 "Used to correctly mirror shape offsets at runtime.")]
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

            // If damagesDuringDisplacement is enabled and displacement is configured,
            // delegate damage detection to the displacement controller (continuous hit along path).
            // For LockOn type, displacement is already started by OnSkillLockTarget event,
            // so we only do instant damage here (continuous damage is handled by the controller).
            if (displacementType == SkillDisplacementType.LockOn)
            {
                // LockOn displacement: displacement was already started by OnSkillLockTarget.
                // If damagesDuringDisplacement is enabled, damage is already being handled by the controller.
                // If not, do instant damage at current position (hit frame).
                if (!damagesDuringDisplacement)
                {
                    GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
                    for (int i = 0; i < candidates.Length; i++)
                    {
                        CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                        if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                        Vector2 candidatePos = candidateEntity.transform.position;

                        if (AttackRangeHelper.IsTargetInRange(casterPos, effectiveFacingSign, skillRangeShapes, candidatePos))
                        {
                            candidateEntity.TakeDamage(skillData.damage, casterEntity);
                        }
                    }
                }
            }
            else if (damagesDuringDisplacement && displacementDirection != SkillDisplacementDirection.None && displacementDistance > 0f)
            {
                // Do an initial hit check at the starting position
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
                for (int i = 0; i < candidates.Length; i++)
                {
                    CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                    if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                    Vector2 candidatePos = candidateEntity.transform.position;

                    if (AttackRangeHelper.IsTargetInRange(casterPos, effectiveFacingSign, skillRangeShapes, candidatePos))
                    {
                        candidateEntity.TakeDamage(skillData.damage, casterEntity);
                    }
                }

                // Start displacement with continuous damage detection
                ApplyDisplacementWithDamage(caster, skillData, skillRangeShapes, effectiveFacingSign, targetTag);
            }
            else
            {
                // Standard behavior: instant damage at current position, then displace
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

                // Apply displacement if configured (no continuous damage)
                ApplyDisplacement(caster);
            }
        }
    }
}
