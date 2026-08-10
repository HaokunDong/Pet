using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Result of checking a single evolution requirement.
    /// </summary>
    public struct EvolutionRequirementStatus
    {
        /// <summary>The requirement being checked.</summary>
        public EvolutionRequirement requirement;

        /// <summary>The current progress value.</summary>
        public int currentValue;

        /// <summary>Whether this requirement is met.</summary>
        public bool isMet;
    }

    /// <summary>
    /// Static service that checks evolution requirements for a character.
    /// Queries CultivationManager, KillStatsManager, and MaterialManager to determine
    /// whether each requirement is satisfied.
    /// </summary>
    public static class EvolutionChecker
    {
        /// <summary>
        /// Check all evolution requirements for a character and return their statuses.
        /// </summary>
        /// <param name="data">The CharacterData to check requirements for.</param>
        /// <returns>A list of requirement statuses. Empty list if no requirements configured.</returns>
        public static List<EvolutionRequirementStatus> CheckAllRequirements(CharacterData data)
        {
            var results = new List<EvolutionRequirementStatus>();

            if (data == null || data.evolutionRequirements == null || data.evolutionRequirements.Length == 0)
            {
                return results;
            }

            string characterId = data.characterId;

            foreach (var req in data.evolutionRequirements)
            {
                if (req == null) continue;

                int currentValue = GetCurrentValue(req, characterId);
                bool isMet = currentValue >= req.targetValue;

                results.Add(new EvolutionRequirementStatus
                {
                    requirement = req,
                    currentValue = currentValue,
                    isMet = isMet
                });
            }

            return results;
        }

        /// <summary>
        /// Check if all evolution requirements are met for a character.
        /// Returns true if there are no requirements (unconditional evolution)
        /// or if all requirements are satisfied.
        /// </summary>
        /// <param name="data">The CharacterData to check.</param>
        /// <returns>True if evolution is allowed.</returns>
        public static bool AreAllRequirementsMet(CharacterData data)
        {
            if (data == null) return false;
            if (data.evolutionTarget == null) return false;

            // No requirements means unconditional evolution
            if (data.evolutionRequirements == null || data.evolutionRequirements.Length == 0)
            {
                return true;
            }

            string characterId = data.characterId;

            foreach (var req in data.evolutionRequirements)
            {
                if (req == null) continue;

                int currentValue = GetCurrentValue(req, characterId);
                if (currentValue < req.targetValue)
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Get the current progress value for a specific requirement.
        /// </summary>
        /// <param name="req">The requirement to check.</param>
        /// <param name="characterId">The character's ID for context-dependent checks.</param>
        /// <returns>The current value (e.g. current level, current material count, current kill count).</returns>
        public static int GetCurrentValue(EvolutionRequirement req, string characterId)
        {
            if (req == null) return 0;

            switch (req.type)
            {
                case EvolutionRequirementType.Level:
                    return GetCharacterLevel(characterId);

                case EvolutionRequirementType.Material:
                    return MaterialManager.Instance.GetMaterialCount(req.materialId);

                case EvolutionRequirementType.KillCount:
                    return KillStatsManager.Instance.GetKillCount(characterId);

                default:
                    Debug.LogWarning($"[EvolutionChecker] Unknown requirement type: {req.type}");
                    return 0;
            }
        }

        /// <summary>
        /// Get the current level of a character from CultivationManager.
        /// </summary>
        private static int GetCharacterLevel(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return 1;

            CultivationData data = CultivationManager.Instance.GetCultivationData(characterId);
            if (data != null)
            {
                return data.level;
            }
            return 1;
        }
    }
}
