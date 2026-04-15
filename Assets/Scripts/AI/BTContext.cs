using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Shared context/blackboard for behavior tree nodes.
    /// Holds references to the owning entity and its current target.
    /// </summary>
    public class BTContext
    {
        public CharacterEntity Owner { get; set; }
        public CharacterEntity CurrentTarget { get; set; }
        public Vector3 PatrolOrigin { get; set; }
        public float PatrolRange { get; set; } = 5f;
        public bool IsMovingRight { get; set; } = true;

        /// <summary>
        /// Whether the owner is currently in combat (attacking a target).
        /// When true, movement nodes should skip movement.
        /// </summary>
        public bool IsInCombat { get; set; }

        /// <summary>
        /// Whether a wall has been detected ahead in the current movement direction.
        /// </summary>
        public bool WallDetectedAhead { get; set; }

        /// <summary>
        /// Distance for wall detection raycast. Configurable via AIController.
        /// </summary>
        public float WallDetectDistance { get; set; } = 0.5f;

        /// <summary>
        /// Layer mask for terrain/wall detection (Default layer).
        /// </summary>
        public int TerrainLayerMask { get; set; }

        public BTContext(CharacterEntity owner)
        {
            Owner = owner;
            PatrolOrigin = owner.transform.position;

            // Use "Wall" layer for wall/terrain detection to avoid ground collider interference
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer != -1)
            {
                TerrainLayerMask = LayerMask.GetMask("Wall");
            }
            else
            {
                // Fallback to Default layer if "Wall" layer doesn't exist
                TerrainLayerMask = LayerMask.GetMask("Default");
                Debug.LogWarning("[BTContext] Physics layer 'Wall' not found. Falling back to 'Default' layer for wall detection. " +
                    "Please create a 'Wall' layer in Edit → Project Settings → Tags and Layers and assign it to wall/terrain objects for accurate detection.");
            }
        }
    }
}
