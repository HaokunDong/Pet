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
        /// Returns false if no shapes are defined.
        /// </summary>
        /// <param name="ownerPos">Owner character world position.</param>
        /// <param name="facingSign">1 for facing right, -1 for facing left.</param>
        /// <param name="shapes">Array of attack range shapes to check.</param>
        /// <param name="targetPos">Target world position to test.</param>
        /// <returns>True if target is inside any shape.</returns>
        public static bool IsTargetInRange(Vector2 ownerPos, float facingSign, AttackRangeShape[] shapes, Vector2 targetPos)
        {
            if (shapes == null || shapes.Length == 0)
            {
                return false;
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
        /// Returns 0 if no shapes are defined.
        /// </summary>
        /// <param name="shapes">Array of attack range shapes.</param>
        /// <returns>Maximum distance from owner center to the farthest edge of any shape.</returns>
        public static float GetMaxAttackDistance(AttackRangeShape[] shapes)
        {
            if (shapes == null || shapes.Length == 0)
            {
                return 0f;
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

            return maxDist;
        }
    }
}
