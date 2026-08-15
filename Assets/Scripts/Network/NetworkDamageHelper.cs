using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Utility class for routing damage through the network.
    /// In client-only mode, damage to mirror enemies is sent to the server.
    /// In host/offline mode, damage is applied directly.
    /// </summary>
    public static class NetworkDamageHelper
    {
        /// <summary>
        /// Apply damage to a target, routing through network if necessary.
        /// Call this instead of target.TakeDamage() directly when the target might be a mirror enemy.
        /// </summary>
        public static void ApplyDamage(CharacterEntity target, float damage, CharacterEntity attacker)
        {
            if (target == null) return;

            // Check if this is a mirror enemy on a client
            MirrorEnemyTag mirrorTag = target.GetComponent<MirrorEnemyTag>();
            if (mirrorTag != null && mirrorTag.enemyNetId != 0)
            {
                // Route damage through network
                if (MirrorNetworkManager.singleton != null &&
                    MirrorNetworkManager.singleton.LocalPlayer != null)
                {
                    MirrorNetworkManager.singleton.LocalPlayer.RequestDamageEnemy(mirrorTag.enemyNetId, damage);
                }
            }
            else
            {
                // Direct damage (host or offline mode)
                target.TakeDamage(damage, attacker);
            }
        }
    }
}
