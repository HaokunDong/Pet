using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Direction of skill displacement relative to the caster's facing direction.
    /// </summary>
    public enum SkillDisplacementDirection
    {
        None = 0,
        Forward = 1,
        Backward = 2
    }

    /// <summary>
    /// Type of skill displacement movement.
    /// </summary>
    public enum SkillDisplacementType
    {
        /// <summary>Fixed direction displacement with a set distance.</summary>
        Fixed = 0,
        /// <summary>Lock-on displacement: locks target position once (via animation event before hit frame),
        /// then moves toward that locked position over duration. Distance is auto-calculated.</summary>
        LockOn = 1
    }

    /// <summary>
    /// Abstract base class for all skill effect types.
    /// Subclass this ScriptableObject to define a new skill effect (e.g. projectile, AOE, buff).
    /// The CombatSystem delegates skill execution to the concrete implementation via Execute().
    /// </summary>
    public abstract class SkillEffectData : ScriptableObject
    {
        [Header("Displacement")]
        [Tooltip("Type of displacement: Fixed (set direction + distance) or LockOn (lock target position, move toward it).")]
        public SkillDisplacementType displacementType = SkillDisplacementType.Fixed;

        [Tooltip("Direction of displacement relative to the caster's facing direction. Used by Fixed type.")]
        public SkillDisplacementDirection displacementDirection = SkillDisplacementDirection.None;

        [Tooltip("Distance to displace the caster (in world units). Used by Fixed type only.")]
        public float displacementDistance = 0f;

        [Tooltip("Duration of the displacement movement (in seconds). Used by both Fixed and LockOn types.")]
        public float displacementDuration = 0.2f;

        [Tooltip("If true, damage detection continues during displacement (dash-slash style). " +
                 "Enemies along the path will be hit. Each enemy is only hit once.")]
        public bool damagesDuringDisplacement = false;

        /// <summary>
        /// Whether this skill effect uses lock-on displacement.
        /// Used by CombatSystem to determine if OnSkillLockTarget event should trigger displacement.
        /// </summary>
        public bool UsesLockOnDisplacement => displacementType == SkillDisplacementType.LockOn;

        /// <summary>
        /// Execute the skill effect. Called by CombatSystem when the skill animation
        /// reaches its hit frame event.
        /// </summary>
        /// <param name="caster">The CombatSystem component of the character casting the skill.</param>
        /// <param name="target">The primary target of the skill.</param>
        /// <param name="skillData">The SkillData asset containing damage, cooldown, etc.</param>
        public abstract void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData);

        /// <summary>
        /// Apply displacement to the caster if configured.
        /// Call this from subclass Execute() implementations when displacement should occur.
        /// </summary>
        protected void ApplyDisplacement(CombatSystem caster)
        {
            if (displacementDirection == SkillDisplacementDirection.None || displacementDistance <= 0f)
                return;

            if (caster == null) return;

            // Get or add the displacement controller
            SkillDisplacementController controller = caster.GetComponent<SkillDisplacementController>();
            if (controller == null)
            {
                controller = caster.gameObject.AddComponent<SkillDisplacementController>();
            }

            // Calculate displacement direction based on caster's facing
            float facingSign = caster.CachedFacingSign;
            float directionMultiplier = (displacementDirection == SkillDisplacementDirection.Forward) ? 1f : -1f;
            float finalDirection = facingSign * directionMultiplier;

            controller.StartDisplacement(finalDirection, displacementDistance, displacementDuration);
        }

        /// <summary>
        /// Apply displacement with continuous damage detection during movement.
        /// Enemies within the skill range shapes along the path will be hit (each only once).
        /// </summary>
        protected void ApplyDisplacementWithDamage(CombatSystem caster, SkillData skillData,
            AttackRangeShape[] shapes, float effectiveFacingSign, string targetTag)
        {
            if (displacementDirection == SkillDisplacementDirection.None || displacementDistance <= 0f)
                return;

            if (caster == null) return;

            // Get or add the displacement controller
            SkillDisplacementController controller = caster.GetComponent<SkillDisplacementController>();
            if (controller == null)
            {
                controller = caster.gameObject.AddComponent<SkillDisplacementController>();
            }

            // Calculate displacement direction based on caster's facing
            float facingSign = caster.CachedFacingSign;
            float directionMultiplier = (displacementDirection == SkillDisplacementDirection.Forward) ? 1f : -1f;
            float finalDirection = facingSign * directionMultiplier;

            CharacterEntity casterEntity = caster.GetComponent<CharacterEntity>();
            controller.StartDisplacementWithDamage(finalDirection, displacementDistance, displacementDuration,
                shapes, effectiveFacingSign, targetTag, skillData.damage, casterEntity);
        }

        /// <summary>
        /// Apply lock-on displacement: move toward a locked target position over duration.
        /// Called by CombatSystem when the OnSkillLockTarget animation event fires.
        /// </summary>
        public void ApplyLockOnDisplacement(CombatSystem caster, Vector2 lockedTargetPos)
        {
            if (displacementType != SkillDisplacementType.LockOn) return;
            if (caster == null) return;

            // Get or add the displacement controller
            SkillDisplacementController controller = caster.GetComponent<SkillDisplacementController>();
            if (controller == null)
            {
                controller = caster.gameObject.AddComponent<SkillDisplacementController>();
            }

            controller.StartLockOnDisplacement(lockedTargetPos, displacementDuration);
        }

        /// <summary>
        /// Apply lock-on displacement with continuous damage detection.
        /// Called by CombatSystem when the OnSkillLockTarget animation event fires
        /// and damagesDuringDisplacement is enabled.
        /// </summary>
        public void ApplyLockOnDisplacementWithDamage(CombatSystem caster, Vector2 lockedTargetPos,
            SkillData skillData, AttackRangeShape[] shapes, float effectiveFacingSign, string targetTag)
        {
            if (displacementType != SkillDisplacementType.LockOn) return;
            if (caster == null) return;

            // Get or add the displacement controller
            SkillDisplacementController controller = caster.GetComponent<SkillDisplacementController>();
            if (controller == null)
            {
                controller = caster.gameObject.AddComponent<SkillDisplacementController>();
            }

            CharacterEntity casterEntity = caster.GetComponent<CharacterEntity>();
            controller.StartLockOnDisplacementWithDamage(lockedTargetPos, displacementDuration,
                shapes, effectiveFacingSign, targetTag, skillData.damage, casterEntity);
        }
    }
}
