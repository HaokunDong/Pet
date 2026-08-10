using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Types of evolution requirements that can be configured for a character.
    /// </summary>
    public enum EvolutionRequirementType
    {
        /// <summary>Character level must reach the target value.</summary>
        Level,

        /// <summary>Player must possess enough of a specific material.</summary>
        Material,

        /// <summary>Character must have accumulated enough kills.</summary>
        KillCount
    }

    /// <summary>
    /// A single evolution requirement entry.
    /// Serializable so it can be configured in the Unity Inspector on CharacterData.
    /// </summary>
    [System.Serializable]
    public class EvolutionRequirement
    {
        [Tooltip("The type of requirement to check.")]
        public EvolutionRequirementType type = EvolutionRequirementType.Level;

        [Tooltip("The target value that must be reached (e.g. required level, required kill count, required material amount).")]
        [Min(1)]
        public int targetValue = 1;

        [Tooltip("Material ID (only used when type is Material).")]
        public string materialId = "";

        [Tooltip("Display description shown in the RequirmentFrame UI (e.g. '达到10级', '收集5个进化石').")]
        public string description = "";
    }
}
