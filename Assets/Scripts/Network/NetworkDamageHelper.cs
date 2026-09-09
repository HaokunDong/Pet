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

            var summon = target.GetComponent<NetworkSummonReplica>();
            if (summon != null)
            {
                var player = MirrorNetworkManager.singleton != null ? MirrorNetworkManager.singleton.LocalPlayer : null;
                if (player != null && summon.Owner != null)
                    player.RequestDamageSummon(summon.Owner.netId, summon.SummonId, damage);
                return;
            }

            // Check if this is a mirror enemy on a client
            MirrorEnemyTag mirrorTag = target.GetComponent<MirrorEnemyTag>();
            if (mirrorTag != null && mirrorTag.enemyNetId != 0)
            {
                // Route damage through network to the host
                if (MirrorNetworkManager.singleton != null &&
                    MirrorNetworkManager.singleton.LocalPlayer != null)
                {
                    MirrorNetworkManager.singleton.LocalPlayer.RequestDamageEnemy(mirrorTag.enemyNetId, damage);
                }
            }
            // Check if this is a mirror character (remote player proxy) on the host.
            // Damage must be routed to the owning client instead of applied directly.
            else if (target.GetComponent<MirrorCharacterTag>() != null)
            {
                MirrorCharacterTag mirrorCharTag = target.GetComponent<MirrorCharacterTag>();
                RouteDamageToMirrorCharacterOwner(mirrorCharTag.ownerConnectionId, damage);
            }
            else
            {
                // Direct damage (host or offline mode)
                target.TakeDamage(damage, attacker);
            }
        }

        /// <summary>
        /// Route damage from the host to a remote client's LocalCharacter via TargetRpc.
        /// Called when Boss attacks a MirrorCharacter on the host — the damage must be
        /// applied on the owning client's LocalCharacter, not on the proxy.
        /// </summary>
        private static void RouteDamageToMirrorCharacterOwner(int ownerConnectionId, float damage)
        {
            if (MirrorNetworkManager.singleton == null) return;

            foreach (NetworkPlayer np in MirrorNetworkManager.singleton.ConnectedPlayers)
            {
                if (np != null &&
                    np.connectionToClient != null &&
                    (int)np.connectionToClient.connectionId == ownerConnectionId)
                {
                    np.TargetTakeDamageFromBoss(damage);
                    return;
                }
            }

            Debug.LogWarning($"[NetworkDamageHelper] Could not find NetworkPlayer for connectionId={ownerConnectionId}");
        }
    }
}
