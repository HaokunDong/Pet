using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Simple tag component attached to mirror enemy GameObjects on clients.
    /// Stores the network ID so CombatSystem can route damage to the server.
    /// </summary>
    public class MirrorEnemyTag : MonoBehaviour
    {
        /// <summary>The network ID assigned by the server's NetworkEnemySpawner.</summary>
        public uint enemyNetId;
    }
}
