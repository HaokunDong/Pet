using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Minimal material/inventory manager for the evolution system.
    /// Tracks material quantities by materialId and supports querying and consuming materials.
    /// </summary>
    public class MaterialManager : Singleton<MaterialManager>
    {
        /// <summary>
        /// Material quantities keyed by materialId.
        /// </summary>
        private Dictionary<string, int> materialMap = new Dictionary<string, int>();

        /// <summary>
        /// Get the current quantity of a specific material.
        /// </summary>
        /// <param name="materialId">The unique identifier of the material.</param>
        /// <returns>The quantity held, or 0 if not found.</returns>
        public int GetMaterialCount(string materialId)
        {
            if (string.IsNullOrEmpty(materialId)) return 0;

            if (materialMap.ContainsKey(materialId))
            {
                return materialMap[materialId];
            }
            return 0;
        }

        /// <summary>
        /// Add a quantity of material to the player's inventory.
        /// </summary>
        /// <param name="materialId">The unique identifier of the material.</param>
        /// <param name="amount">The amount to add (must be positive).</param>
        public void AddMaterial(string materialId, int amount)
        {
            if (string.IsNullOrEmpty(materialId) || amount <= 0) return;

            if (!materialMap.ContainsKey(materialId))
            {
                materialMap[materialId] = 0;
            }
            materialMap[materialId] += amount;

            Debug.Log($"[MaterialManager] Added {amount}x '{materialId}'. Total: {materialMap[materialId]}");
        }

        /// <summary>
        /// Consume (deduct) a quantity of material from the player's inventory.
        /// </summary>
        /// <param name="materialId">The unique identifier of the material.</param>
        /// <param name="amount">The amount to consume (must be positive).</param>
        /// <returns>True if consumption was successful, false if insufficient quantity.</returns>
        public bool ConsumeMaterial(string materialId, int amount)
        {
            if (string.IsNullOrEmpty(materialId) || amount <= 0) return false;

            int current = GetMaterialCount(materialId);
            if (current < amount)
            {
                Debug.LogWarning($"[MaterialManager] Cannot consume {amount}x '{materialId}'. Only have {current}.");
                return false;
            }

            materialMap[materialId] -= amount;
            Debug.Log($"[MaterialManager] Consumed {amount}x '{materialId}'. Remaining: {materialMap[materialId]}");
            return true;
        }

        /// <summary>
        /// Check if the player has at least the specified amount of a material.
        /// </summary>
        /// <param name="materialId">The unique identifier of the material.</param>
        /// <param name="amount">The required amount.</param>
        /// <returns>True if the player has enough.</returns>
        public bool HasEnoughMaterial(string materialId, int amount)
        {
            return GetMaterialCount(materialId) >= amount;
        }

        /// <summary>
        /// Set material count directly (used when loading from save data).
        /// </summary>
        /// <param name="materialId">The unique identifier of the material.</param>
        /// <param name="count">The count to set.</param>
        public void SetMaterialCount(string materialId, int count)
        {
            if (string.IsNullOrEmpty(materialId)) return;
            materialMap[materialId] = Mathf.Max(0, count);
        }

        /// <summary>
        /// Get all material data (for save system).
        /// </summary>
        public Dictionary<string, int> GetAllMaterials()
        {
            return new Dictionary<string, int>(materialMap);
        }

        /// <summary>
        /// Clear all material data (for testing or new game).
        /// </summary>
        public void ClearAll()
        {
            materialMap.Clear();
        }
    }
}
