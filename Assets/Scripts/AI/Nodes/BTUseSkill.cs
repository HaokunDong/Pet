using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Action node: use a skill on the current target.
    /// Requires a BTCheckSkillReady reference to know which skill to use.
    /// Returns Success if skill was used, Failure otherwise.
    /// </summary>
    public class BTUseSkill : BTNode
    {
        private readonly BTContext context;
        private readonly BTCheckSkillReady skillReadyCheck;

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

            SkillData skillData = owner.characterData.skills[skillIndex];
            if (skillData == null)
                return BTState.Failure;

            // Check if target is within skill range
            float dist = Vector2.Distance(owner.transform.position, target.transform.position);
            if (dist > skillData.skillRange)
                return BTState.Failure;

            // Face the target
            if (owner.CharAnimator != null)
            {
                owner.CharAnimator.FaceTowards(target.transform.position);
                owner.CharAnimator.PlaySkill(skillIndex);
            }

            // Deal skill damage
            target.TakeDamage(skillData.damage);

            // Start cooldown
            owner.RuntimeStats.StartSkillCooldown(skillIndex, skillData.cooldown);

            // Spawn skill effect if available
            if (skillData.effectPrefab != null)
            {
                GameObject effect = Object.Instantiate(
                    skillData.effectPrefab,
                    target.transform.position,
                    Quaternion.identity
                );
                Object.Destroy(effect, 2f);
            }

            return BTState.Success;
        }
    }
}
