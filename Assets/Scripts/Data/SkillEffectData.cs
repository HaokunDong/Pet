using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Abstract base class for all skill effect types.
    /// Subclass this ScriptableObject to define a new skill effect (e.g. projectile, AOE, buff).
    /// The CombatSystem delegates skill execution to the concrete implementation via Execute().
    /// </summary>
    public abstract class SkillEffectData : ScriptableObject
    {
        /// <summary>
        /// Execute the skill effect. Called by CombatSystem when the skill animation
        /// reaches its hit frame event.
        /// </summary>
        /// <param name="caster">The CombatSystem component of the character casting the skill.</param>
        /// <param name="target">The primary target of the skill.</param>
        /// <param name="skillData">The SkillData asset containing damage, cooldown, etc.</param>
        public abstract void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData);
    }
}
