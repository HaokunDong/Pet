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

        public BTContext(CharacterEntity owner)
        {
            Owner = owner;
            PatrolOrigin = owner.transform.position;
        }
    }
}
