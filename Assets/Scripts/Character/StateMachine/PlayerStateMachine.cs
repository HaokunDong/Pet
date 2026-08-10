using UnityEngine;
using PetGame.States;

namespace PetGame
{
    /// <summary>
    /// Player-specific state machine.
    /// Registers all player-related states and handles player-specific transition logic.
    /// Supports both single-skill and multi-skill trigger modes based on character configuration.
    /// </summary>
    public class PlayerStateMachine : EntityStateMachine
    {
    /// <summary>
    /// Whether this player character uses multiple skill Bools.
    /// Set based on the number of skills the character has.
    /// </summary>
    private bool useMultiSkillBools;

        /// <summary>
        /// Initialize the player state machine, registering all player states.
        /// </summary>
        /// <param name="anim">The Animator component.</param>
        /// <param name="charAnimator">The CharacterAnimator component.</param>
        public override void Initialize(Animator anim, CharacterAnimator charAnimator)
        {
            base.Initialize(anim, charAnimator);

            // Register all player states
            RegisterState<IdleState>(new IdleState(this));
            RegisterState<WalkState>(new WalkState(this));
            RegisterState<AttackState>(new AttackState(this));
            RegisterState<SkillState>(new SkillState(this));
            RegisterState<HitState>(new HitState(this));
            RegisterState<DeathState>(new DeathState(this));
        }

        /// <summary>
        /// Configure skill trigger mode based on the number of skills.
        /// Call this after Initialize() when character data is available.
        /// </summary>
        /// <param name="skillCount">Number of skills the character has.</param>
        /// <param name="hasSkill">Whether Animator has Skill parameter.</param>
        /// <param name="hasSkillOne">Whether Animator has SkillOne parameter.</param>
        /// <param name="hasSkillTwo">Whether Animator has SkillTwo parameter.</param>
        /// <param name="hasSkillThree">Whether Animator has SkillThree parameter.</param>
        /// <param name="hasSkillFour">Whether Animator has SkillFour parameter.</param>
        /// <param name="hasDeath">Whether Animator has Death parameter.</param>
        /// <param name="hasAttackIndex">Whether Animator has AttackIndex parameter.</param>
        public void ConfigureSkills(int skillCount, bool hasSkill, bool hasSkillOne,
            bool hasSkillTwo, bool hasSkillThree, bool hasSkillFour, bool hasDeath, bool hasAttackIndex)
        {
            useMultiSkillBools = skillCount > 1;

            var skillState = GetState<SkillState>();
            if (skillState != null)
            {
                skillState.SetUseMultiSkillTriggers(useMultiSkillBools);
                skillState.CacheParameterFlags(hasSkill, hasSkillOne, hasSkillTwo, hasSkillThree, hasSkillFour);
            }

            var deathState = GetState<DeathState>();
            if (deathState != null)
            {
                deathState.CacheParameterFlags(hasDeath);
            }

            var attackState = GetState<AttackState>();
            if (attackState != null)
            {
                attackState.CacheParameterFlags(hasAttackIndex);
            }
        }

        protected override void OnStateChanged(IState from, IState to)
        {
            // Player-specific transition logic can be added here if needed
        }
    }
}
