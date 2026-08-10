using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manages kill statistics for each character.
    /// Tracks cumulative kill counts per characterId for evolution requirement checks.
    /// </summary>
    public class KillStatsManager : Singleton<KillStatsManager>
    {
        /// <summary>
        /// Kill counts keyed by characterId.
        /// </summary>
        private Dictionary<string, int> killCountMap = new Dictionary<string, int>();

        /// <summary>
        /// Record a kill for the specified character.
        /// </summary>
        /// <param name="characterId">The characterId of the character who made the kill.</param>
        public void AddKill(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return;

            if (!killCountMap.ContainsKey(characterId))
            {
                killCountMap[characterId] = 0;
            }
            killCountMap[characterId]++;

            Debug.Log($"[KillStatsManager] '{characterId}' kill count: {killCountMap[characterId]}");
        }

        /// <summary>
        /// Get the cumulative kill count for a specific character.
        /// </summary>
        /// <param name="characterId">The characterId to query.</param>
        /// <returns>The total number of kills recorded for this character.</returns>
        public int GetKillCount(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return 0;

            if (killCountMap.ContainsKey(characterId))
            {
                return killCountMap[characterId];
            }
            return 0;
        }

        /// <summary>
        /// Set the kill count for a character (used when loading from save data).
        /// </summary>
        /// <param name="characterId">The characterId to set.</param>
        /// <param name="count">The kill count value.</param>
        public void SetKillCount(string characterId, int count)
        {
            if (string.IsNullOrEmpty(characterId)) return;
            killCountMap[characterId] = Mathf.Max(0, count);
        }

        /// <summary>
        /// Get all kill stats data (for save system).
        /// </summary>
        public Dictionary<string, int> GetAllKillStats()
        {
            return new Dictionary<string, int>(killCountMap);
        }

        /// <summary>
        /// Clear all kill statistics (for testing or new game).
        /// </summary>
        public void ClearAll()
        {
            killCountMap.Clear();
        }
    }
}
