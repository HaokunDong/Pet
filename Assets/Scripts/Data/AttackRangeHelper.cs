using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Static utility class for multi-shape attack range checks.
    /// Provides union-based hit detection and max distance calculation.
    /// </summary>
    public static class AttackRangeHelper
    {
        /// <summary>
        /// Check if a target position falls within any of the attack range shapes (union).
        /// Falls back to a simple circle check using fallbackRange if shapes array is null or empty.
        /// </summary>
        /// <param name="ownerPos">Owner character world position.</param>
        /// <param name="facingSign">1 for facing right, -1 for facing left.</param>
        /// <param name="shapes">Array of attack range shapes to check.</param>
        /// <param name="fallbackRange">Fallback circle radius if shapes is null/empty.</param>
        /// <param name="targetPos">Target world position to test.</param>
        /// <returns>True if target is inside any shape (or within fallback range).</returns>
        public static bool IsTargetInRange(Vector2 ownerPos, float facingSign, AttackRangeShape[] shapes, float fallbackRange, Vector2 targetPos)
        {
            // Fallback: use simple circle distance check
            if (shapes == null || shapes.Length == 0)
            {
                float distSqr = (targetPos - ownerPos).sqrMagnitude;
                return distSqr <= fallbackRange * fallbackRange;
            }

            // Union check: return true if target is inside ANY shape
            for (int i = 0; i < shapes.Length; i++)
            {
                if (shapes[i] != null && shapes[i].Contains(ownerPos, facingSign, targetPos))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Get the maximum attack distance across all shapes.
        /// Used by AI to determine chase stopping distance.
        /// Falls back to fallbackRange if shapes array is null or empty.
        /// </summary>
        /// <param name="shapes">Array of attack range shapes.</param>
        /// <param name="fallbackRange">Fallback range if shapes is null/empty.</param>
        /// <returns>Maximum distance from owner center to the farthest edge of any shape.</returns>
        public static float GetMaxAttackDistance(AttackRangeShape[] shapes, float fallbackRange)
        {
            if (shapes == null || shapes.Length == 0)
            {
                return fallbackRange;
            }

            float maxDist = 0f;
            for (int i = 0; i < shapes.Length; i++)
            {
                if (shapes[i] != null)
                {
                    float reach = shapes[i].GetMaxReach();
                    if (reach > maxDist)
                    {
                        maxDist = reach;
                    }
                }
            }

            // If all shapes are null somehow, fallback
            return maxDist > 0f ? maxDist : fallbackRange;
        }
    }
}
