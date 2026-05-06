using UnityEngine;
using PetGame.States;

namespace PetGame
{
    /// <summary>
    /// Enemy-specific state machine (for both regular enemies and Bosses).
    /// Registers all enemy-related states and handles enemy-specific transition logic.
    /// Responds to behavior tree (BTCombat) state transition requests.
    /// Boss characters with multiple skills use SkillOne/SkillTwo/SkillThree/SkillFour Bool parameters.
    /// </summary>
    public class EnemyStateMachine : EntityStateMachine
    {
        /// <summary>
        /// Initialize the enemy state machine, registering all enemy states.
        /// </summary>
        /// <param name="anim">The Animator component.</param>
        /// <param name="charAnimator">The CharacterAnimator component.</param>
        public override void Initialize(Animator anim, CharacterAnimator charAnimator)
        {
            base.Initialize(anim, charAnimator);

            // Register all enemy states
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
        /// Boss characters typically have multiple skills.
        /// </summary>
        /// <param name="skillCount">Number of skills the enemy has.</param>
        /// <param name="hasSkill">Whether Animator has Skill parameter.</param>
        /// <param name="hasSkillOne">Whether Animator has SkillOne parameter.</param>
        /// <param name="hasSkillTwo">Whether Animator has SkillTwo parameter.</param>
        /// <param name="hasSkillThree">Whether Animator has SkillThree parameter.</param>
        /// <param name="hasSkillFour">Whether Animator has SkillFour parameter.</param>
        /// <param name="hasDeath">Whether Animator has Death parameter.</param>
        public void ConfigureSkills(int skillCount, bool hasSkill, bool hasSkillOne,
            bool hasSkillTwo, bool hasSkillThree, bool hasSkillFour, bool hasDeath)
        {
            bool useMultiSkillBools = skillCount > 1;

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
        }

        protected override void OnStateChanged(IState from, IState to)
        {
            // Enemy-specific transition logic can be added here if needed
            // (e.g. notifying the behavior tree of state changes)
        }
    }
}
