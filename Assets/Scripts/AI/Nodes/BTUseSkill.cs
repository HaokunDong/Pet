using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: use a skill on the current target.
    /// Delegates all skill logic (animation, damage timing, effects) to CombatSystem.
    /// Requires a BTCheckSkillReady reference to know which skill to use.
    /// Returns Success if skill was used, Failure otherwise.
    /// </summary>
    public class BTUseSkill : BTNode
    {
        private readonly BTContext context;
        private readonly BTCheckSkillReady skillReadyCheck;
        private CombatSystem combatSystem;

        public BTUseSkill(BTContext context, BTCheckSkillReady skillReadyCheck)
        {
            this.context = context;
            this.skillReadyCheck = skillReadyCheck;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            CharacterEntity target = context.CurrentTarget;

            if (owner == null || target == null || !target.RuntimeStats.IsAlive)
                return BTState.Failure;

            int skillIndex = skillReadyCheck.ReadySkillIndex;
            if (skillIndex < 0)
                return BTState.Failure;

            // Lazy-cache CombatSystem reference
            if (combatSystem == null)
                combatSystem = owner.GetComponent<CombatSystem>();

            // Delegate all skill logic to CombatSystem (handles range, cooldown, animation, deferred damage & effects)
            bool used = combatSystem.TryUseSkill(skillIndex, target);

            return used ? BTState.Success : BTState.Failure;
        }
    }
}
