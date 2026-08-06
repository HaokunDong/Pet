using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Shape type for attack range definition.
    /// </summary>
    public enum AttackShapeType
    {
        Circle,
        Box
    }

    /// <summary>
    /// Serializable attack range shape definition.
    /// Supports Circle and Box shapes with offset, used to compose complex attack areas.
    /// </summary>
    [System.Serializable]
    public class AttackRangeShape
    {
        [Tooltip("Shape type: Circle or Box")]
        public AttackShapeType shapeType = AttackShapeType.Circle;

        [Tooltip("Offset from character center (will be mirrored on X when facing left)")]
        public Vector2 offset = Vector2.zero;

        [Tooltip("Radius (Circle only)")]
        [Min(0.01f)]
        public float radius = 1f;

        [Tooltip("Size width and height (Box only)")]
        public Vector2 size = new Vector2(1f, 1f);

        /// <summary>
        /// Check if a target position is inside this shape.
        /// </summary>
        /// <param name="ownerPos">Owner character world position.</param>
        /// <param name="facingSign">1 for facing right, -1 for facing left. Used to mirror offset.x.</param>
        /// <param name="targetPos">Target world position to test.</param>
        /// <returns>True if target is inside this shape.</returns>
        public bool Contains(Vector2 ownerPos, float facingSign, Vector2 targetPos)
        {
            // Apply mirrored offset based on facing direction
            Vector2 center = ownerPos + new Vector2(offset.x * facingSign, offset.y);

            switch (shapeType)
            {
                case AttackShapeType.Circle:
                    float distSqr = (targetPos - center).sqrMagnitude;
                    return distSqr <= radius * radius;

                case AttackShapeType.Box:
                    float halfW = size.x * 0.5f;
                    float halfH = size.y * 0.5f;
                    Vector2 diff = targetPos - center;
                    return Mathf.Abs(diff.x) <= halfW && Mathf.Abs(diff.y) <= halfH;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Get the maximum reach distance on the X axis from the owner center to the farthest point of this shape.
        /// Used for AI chase/engage distance calculation.
        /// Since this is a side-scrolling game and Y-axis differences are ignored in attack range checks,
        /// only the X-axis extent matters.
        /// </summary>
        /// <returns>Maximum X-axis distance from owner center to the edge of this shape.</returns>
        public float GetMaxReach()
        {
            float offsetX = Mathf.Abs(offset.x);

            switch (shapeType)
            {
                case AttackShapeType.Circle:
                    return offsetX + radius;

                case AttackShapeType.Box:
                    float halfW = size.x * 0.5f;
                    return offsetX + halfW;

                default:
                    return offsetX;
            }
        }
    }
}
