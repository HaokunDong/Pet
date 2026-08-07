using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Melee skill effect: deals AOE damage to all enemies within the configured
    /// skill attack distance when the skill animation reaches its hit frame event.
    /// Uses a simple horizontal distance check from the caster's Transform forward direction.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMeleeSkillEffect", menuName = "Game/SkillEffect/Melee")]
    public class MeleeSkillEffectData : SkillEffectData
    {
        [Header("Skill Attack Distance")]
        [Tooltip("The horizontal distance from the caster's Transform forward direction. " +
                 "Enemies within this distance in the facing direction are considered in skill range.")]
        [Min(0.1f)]
        public float skillAttackDistance = 1.5f;

        /// <summary>
        /// Execute the melee skill effect.
        /// Finds all opposing-faction alive characters within the skill attack distance
        /// and deals damage from the SkillData asset.
        /// </summary>
        public override void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)
        {
            if (caster == null) return;

            CharacterEntity casterEntity = caster.GetComponent<CharacterEntity>();
            if (casterEntity == null) return;

            // Determine the enemy tag based on caster's GameObject tag (not characterType),
            // because a Boss character used by the player still has tag "Player".
            string targetTag = casterEntity.gameObject.CompareTag("Player")
                ? "Enemy"
                : "Player";

            // Get caster facing direction sign
            float facingSign = caster.CachedFacingSign;

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

                        if (IsInSkillAttackDistance(casterPos, facingSign, candidatePos))
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

                    if (IsInSkillAttackDistance(casterPos, facingSign, candidatePos))
                    {
                        candidateEntity.TakeDamage(skillData.damage, casterEntity);
                    }
                }

                // Start displacement with continuous damage detection
                ApplyDisplacementWithDamage(caster, skillData, skillAttackDistance, facingSign, targetTag);
            }
            else
            {
                // Standard behavior: instant damage at current position, then displace
                GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);

                for (int i = 0; i < candidates.Length; i++)
                {
                    CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                    if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                    Vector2 candidatePos = candidateEntity.transform.position;

                    if (IsInSkillAttackDistance(casterPos, facingSign, candidatePos))
                    {
                        candidateEntity.TakeDamage(skillData.damage, casterEntity);
                    }
                }

                // Apply displacement if configured (no continuous damage)
                ApplyDisplacement(caster);
            }
        }

        /// <summary>
        /// Checks if a target position is within the skill attack distance in the facing direction.
        /// Only checks horizontal (X-axis) distance.
        /// </summary>
        private bool IsInSkillAttackDistance(Vector2 casterPos, float facingSign, Vector2 targetPos)
        {
            float dx = targetPos.x - casterPos.x;
            // Target must be in the facing direction
            if (facingSign > 0 && dx < 0) return false;
            if (facingSign < 0 && dx > 0) return false;
            return Mathf.Abs(dx) <= skillAttackDistance;
        }
    }
}
