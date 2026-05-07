using Unity.Netcode;
using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Network synchronization component for enemy characters in multiplayer mode.
    /// The Host controls enemy AI and state; Clients receive synced position, animation, and health.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class NetworkEnemyController : NetworkBehaviour
    {
        [Header("Sync Settings")]
        [Tooltip("How often to sync position (times per second)")]
        public float positionSyncRate = 15f;

        [Tooltip("Interpolation speed for client-side position smoothing")]
        public float interpolationSpeed = 10f;

        // Network variables - only server/host writes
        private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<bool> networkFlipX = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<float> networkHealth = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private NetworkVariable<bool> networkIsAlive = new NetworkVariable<bool>(
            true, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        // Component references
        private CharacterEntity entity;
        private CharacterAnimator charAnimator;
        private SpriteRenderer spriteRenderer;

        private float syncTimer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            entity = GetComponent<CharacterEntity>();
            charAnimator = GetComponent<CharacterAnimator>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (IsServer)
            {
                // Host: initialize network variables
                networkPosition.Value = transform.position;
                networkFlipX.Value = spriteRenderer != null && spriteRenderer.flipX;
                if (entity != null && entity.RuntimeStats != null)
                {
                    networkHealth.Value = entity.RuntimeStats.currentHealth;
                    networkIsAlive.Value = entity.RuntimeStats.IsAlive;
                }

                // Subscribe to damage/death events
                if (entity != null)
                {
                    entity.OnDamageTaken += OnEntityDamageTaken;
                    entity.OnDeath += OnEntityDeath;
                }
            }
            else
            {
                // Client: disable AI controller (host handles AI)
                var aiController = GetComponent<PetGame.AI.AIController>();
                if (aiController != null)
                    aiController.enabled = false;

                // Subscribe to network variable changes
                networkPosition.OnValueChanged += OnNetworkPositionChanged;
                networkFlipX.OnValueChanged += OnNetworkFlipXChanged;
                networkHealth.OnValueChanged += OnNetworkHealthChanged;
                networkIsAlive.OnValueChanged += OnNetworkIsAliveChanged;
            }
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            if (IsServer && entity != null)
            {
                entity.OnDamageTaken -= OnEntityDamageTaken;
                entity.OnDeath -= OnEntityDeath;
            }

            if (!IsServer)
            {
                networkPosition.OnValueChanged -= OnNetworkPositionChanged;
                networkFlipX.OnValueChanged -= OnNetworkFlipXChanged;
                networkHealth.OnValueChanged -= OnNetworkHealthChanged;
                networkIsAlive.OnValueChanged -= OnNetworkIsAliveChanged;
            }
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsServer)
            {
                // Sync position at fixed rate
                syncTimer += Time.deltaTime;
                if (syncTimer >= 1f / positionSyncRate)
                {
                    syncTimer = 0f;
                    networkPosition.Value = transform.position;
                    if (spriteRenderer != null)
                        networkFlipX.Value = spriteRenderer.flipX;
                }
            }
            else
            {
                // Client: interpolate position
                transform.position = Vector3.Lerp(
                    transform.position,
                    networkPosition.Value,
                    Time.deltaTime * interpolationSpeed);
            }
        }

        // --- Server-side event handlers ---

        private void OnEntityDamageTaken(CharacterEntity damagedEntity, float damage)
        {
            if (!IsServer) return;

            networkHealth.Value = entity.RuntimeStats.currentHealth;

            // Broadcast hit animation to clients
            PlayHitClientRpc();
        }

        private void OnEntityDeath(CharacterEntity deadEntity)
        {
            if (!IsServer) return;

            networkIsAlive.Value = false;

            // Broadcast death animation to clients
            PlayDeathClientRpc();

            // Despawn after death animation delay
            Invoke(nameof(DespawnEnemy), 1.5f);
        }

        private void DespawnEnemy()
        {
            if (IsServer && NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn(true);
            }
        }

        // --- Client RPCs for animation sync ---

        [ClientRpc]
        private void PlayHitClientRpc()
        {
            if (!IsServer && charAnimator != null)
            {
                charAnimator.PlayHit();
            }
        }

        [ClientRpc]
        private void PlayDeathClientRpc()
        {
            if (!IsServer && charAnimator != null)
            {
                charAnimator.PlayDeath();
            }
        }

        [ClientRpc]
        private void PlayAttackClientRpc(int skillIndex)
        {
            if (!IsServer && charAnimator != null)
            {
                if (skillIndex < 0)
                    charAnimator.PlayAttack();
                else
                    charAnimator.PlaySkill(skillIndex);
            }
        }

        /// <summary>
        /// Called by CombatSystem on the host to broadcast attack animation.
        /// </summary>
        public void BroadcastAttack(int skillIndex)
        {
            if (IsServer)
            {
                PlayAttackClientRpc(skillIndex);
            }
        }

        // --- Client-side network variable change callbacks ---

        private void OnNetworkPositionChanged(Vector3 oldValue, Vector3 newValue)
        {
            // Interpolation handled in Update
        }

        private void OnNetworkFlipXChanged(bool oldValue, bool newValue)
        {
            if (!IsServer && spriteRenderer != null)
            {
                spriteRenderer.flipX = newValue;
            }
        }

        private void OnNetworkHealthChanged(float oldValue, float newValue)
        {
            if (!IsServer && entity != null && entity.RuntimeStats != null)
            {
                // Sync health on client
                entity.RuntimeStats.currentHealth = newValue;
            }
        }

        private void OnNetworkIsAliveChanged(bool oldValue, bool newValue)
        {
            if (!IsServer && !newValue && entity != null)
            {
                // Enemy died on server, trigger death locally
                if (charAnimator != null)
                    charAnimator.PlayDeath();
            }
        }
    }
}
