using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Skill state: character is performing a skill attack.
    /// Sets isUsingSkill = true and canBeInterrupted = false on entry (protection active).
    /// Supports both single-skill (Skill Bool) and multi-skill (SkillOne/Two/Three/Four Bools).
    /// The protection is cleared by frame event callbacks via ClearProtection().
    /// Uses Bool parameters in Animator Controller.
    /// </summary>
    public class SkillState : StateBase
    {
        private static readonly int HashSkill = Animator.StringToHash("Skill");
        private static readonly int HashSkillOne = Animator.StringToHash("SkillOne");
        private static readonly int HashSkillTwo = Animator.StringToHash("SkillTwo");
        private static readonly int HashSkillThree = Animator.StringToHash("SkillThree");
        private static readonly int HashSkillFour = Animator.StringToHash("SkillFour");

        /// <summary>
        /// The skill index to use (0-based): 0=SkillOne, 1=SkillTwo, 2=SkillThree, 3=SkillFour.
        /// Set before calling ChangeState to determine which Bool to activate.
        /// </summary>
        private int skillIndex;

        /// <summary>
        /// Whether this character uses multiple skill Bools (SkillOne/Two/Three/Four)
        /// or a single Skill Bool.
        /// </summary>
        private bool useMultiSkillBools;

        // Cached parameter existence flags (set during initialization)
        private bool hasSkillParam;
        private bool hasSkillOneParam;
        private bool hasSkillTwoParam;
        private bool hasSkillThreeParam;
        private bool hasSkillFourParam;

        // Track which Bool hash was activated on enter, so we can deactivate on exit
        private int activeSkillHash;

        public SkillState(EntityStateMachine stateMachine) : base(stateMachine) { }

        /// <summary>
        /// Set the skill index before transitioning to this state.
        /// </summary>
        /// <param name="index">0-based skill index.</param>
        public void SetSkillIndex(int index)
        {
            skillIndex = index;
        }

        /// <summary>
        /// Configure whether to use multi-skill Bools.
        /// </summary>
        public void SetUseMultiSkillTriggers(bool useMulti)
        {
            useMultiSkillBools = useMulti;
        }

        /// <summary>
        /// Cache which skill parameters exist in the Animator Controller.
        /// Called during state machine initialization.
        /// </summary>
        public void CacheParameterFlags(bool hasSkill, bool hasOne, bool hasTwo, bool hasThree, bool hasFour)
        {
            hasSkillParam = hasSkill;
            hasSkillOneParam = hasOne;
            hasSkillTwoParam = hasTwo;
            hasSkillThreeParam = hasThree;
            hasSkillFourParam = hasFour;
        }

        public override void OnEnter()
        {
            machine.isUsingSkill = true;
            machine.canBeInterrupted = false;
            activeSkillHash = 0;

            if (machine.Animator != null)
            {
                if (useMultiSkillBools)
                {
                    int boolHash = GetSkillBoolHash(skillIndex);
                    if (boolHash != 0)
                    {
                        machine.Animator.SetBool(boolHash, true);
                        activeSkillHash = boolHash;
                    }
                }
                else
                {
                    // Single-skill mode: prefer "Skill" parameter, fall back to "SkillOne"
                    // if "Skill" doesn't exist (common when Animator uses SkillOne for 1-skill characters).
                    if (hasSkillParam)
                    {
                        machine.Animator.SetBool(HashSkill, true);
                        activeSkillHash = HashSkill;
                    }
                    else if (hasSkillOneParam)
                    {
                        // Fallback: use SkillOne when only one skill exists but Animator
                        // doesn't have a generic "Skill" parameter.
                        machine.Animator.SetBool(HashSkillOne, true);
                        activeSkillHash = HashSkillOne;
                    }
                }
            }
        }

        public override void OnExit()
        {
            machine.isUsingSkill = false;

            // Deactivate the Bool that was set on enter
            if (machine.Animator != null && activeSkillHash != 0)
            {
                machine.Animator.SetBool(activeSkillHash, false);
                activeSkillHash = 0;
            }
        }

        public override bool CanEnter()
        {
            return machine.canBeInterrupted;
        }

        public override bool CanExit()
        {
            return machine.canBeInterrupted;
        }

        /// <summary>
        /// Get the Bool hash for a given skill index (0-based).
        /// Returns 0 if the skill index is out of range or the parameter does not exist.
        /// </summary>
        private int GetSkillBoolHash(int index)
        {
            switch (index)
            {
                case 0: return hasSkillOneParam ? HashSkillOne : 0;
                case 1: return hasSkillTwoParam ? HashSkillTwo : 0;
                case 2: return hasSkillThreeParam ? HashSkillThree : 0;
                case 3: return hasSkillFourParam ? HashSkillFour : 0;
                default: return 0;
            }
        }
    }
}
