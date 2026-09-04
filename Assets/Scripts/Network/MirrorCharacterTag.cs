using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Simple tag component attached to mirror character GameObjects on the host.
    /// Marks this object as a visual proxy for a remote player so that combat
    /// systems can skip or route damage through the network instead of applying
    /// it directly.
    /// </summary>
    public class MirrorCharacterTag : MonoBehaviour
    {
        /// <summary>The connection ID of the remote client that owns this character.</summary>
        public int ownerConnectionId;
    }
}
