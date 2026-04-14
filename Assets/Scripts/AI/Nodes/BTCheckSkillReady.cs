namespace PetGame.AI
{
    /// <summary>
    /// Condition node: checks if any skill is off cooldown and ready to use.
    /// Returns Success if at least one skill is ready, Failure otherwise.
    /// </summary>
    public class BTCheckSkillReady : BTNode
    {
        private readonly BTContext context;

        /// <summary>
        /// After execution, holds the index of the first ready skill (-1 if none).
        /// </summary>
        public int ReadySkillIndex { get; private set; } = -1;

        public BTCheckSkillReady(BTContext context)
        {
            this.context = context;
        }

        public override BTState Execute()
        {
            ReadySkillIndex = -1;

            CharacterEntity owner = context.Owner;
            if (owner == null || owner.characterData == null || owner.characterData.skills == null)
                return BTState.Failure;

            int maxSkills = owner.characterData.GetMaxSkillCount();
            for (int i = 0; i < owner.characterData.skills.Length && i < maxSkills; i++)
            {
                if (owner.RuntimeStats.IsSkillReady(i))
                {
                    ReadySkillIndex = i;
                    return BTState.Success;
                }
            }

            return BTState.Failure;
        }
    }
}
