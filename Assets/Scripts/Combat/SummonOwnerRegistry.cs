using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PetGame
{
    /// <summary>
    /// Static registry that tracks the relationship between casters (owners) and their summoned entities.
    /// Provides methods to register, unregister, and batch-destroy summons for a given owner.
    /// Automatically clears all records on scene change.
    /// </summary>
    public static class SummonOwnerRegistry
    {
        private static readonly Dictionary<CharacterEntity, List<GameObject>> ownerToSummons
            = new Dictionary<CharacterEntity, List<GameObject>>();

        private static bool isSubscribed = false;

        /// <summary>
        /// Register a summoned entity under its owner.
        /// </summary>
        public static void Register(CharacterEntity owner, GameObject summon)
        {
            if (owner == null || summon == null) return;

            EnsureSceneCallbackSubscribed();

            if (!ownerToSummons.ContainsKey(owner))
            {
                ownerToSummons[owner] = new List<GameObject>();
            }

            ownerToSummons[owner].Add(summon);
        }

        /// <summary>
        /// Unregister a summoned entity from its owner's list.
        /// Called when a summon is destroyed (timeout, killed, or forced).
        /// </summary>
        public static void Unregister(CharacterEntity owner, GameObject summon)
        {
            if (owner == null) return;

            if (ownerToSummons.TryGetValue(owner, out List<GameObject> summons))
            {
                summons.Remove(summon);
                if (summons.Count == 0)
                {
                    ownerToSummons.Remove(owner);
                }
            }
        }

        /// <summary>
        /// Destroy all existing summons for a given owner.
        /// Used when allowMultipleWaves is false to refresh summons on re-cast.
        /// </summary>
        public static void DestroyAllForOwner(CharacterEntity owner)
        {
            if (owner == null) return;

            if (!ownerToSummons.TryGetValue(owner, out List<GameObject> summons)) return;

            // Copy the list to avoid modification during iteration
            List<GameObject> toDestroy = new List<GameObject>(summons);

            foreach (GameObject summonObj in toDestroy)
            {
                if (summonObj == null) continue;

                SummonedEntityTracker tracker = summonObj.GetComponent<SummonedEntityTracker>();
                if (tracker != null)
                {
                    tracker.ForceDestroy();
                }
                else
                {
                    // Fallback: directly destroy if tracker is missing
                    Object.Destroy(summonObj);
                }
            }

            // Clean up the entry
            ownerToSummons.Remove(owner);
        }

        /// <summary>
        /// Clear all summon records and destroy all active summons.
        /// Called on scene transitions.
        /// </summary>
        public static void ClearAll()
        {
            // Collect all summon objects before clearing
            List<GameObject> allSummons = new List<GameObject>();
            foreach (var kvp in ownerToSummons)
            {
                foreach (GameObject summon in kvp.Value)
                {
                    if (summon != null)
                    {
                        allSummons.Add(summon);
                    }
                }
            }

            ownerToSummons.Clear();

            // Destroy all collected summon objects
            foreach (GameObject summon in allSummons)
            {
                if (summon != null)
                {
                    Object.Destroy(summon);
                }
            }
        }

        /// <summary>
        /// Ensure we are subscribed to scene change events for automatic cleanup.
        /// </summary>
        private static void EnsureSceneCallbackSubscribed()
        {
            if (isSubscribed) return;

            SceneManager.sceneUnloaded += OnSceneUnloaded;
            isSubscribed = true;
        }

        /// <summary>
        /// Called when a scene is unloaded. Clears all summon records.
        /// </summary>
        private static void OnSceneUnloaded(Scene scene)
        {
            ClearAll();
        }
    }
}
