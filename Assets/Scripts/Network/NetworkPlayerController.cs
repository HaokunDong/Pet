using Unity.Netcode;
using UnityEngine;
using PetGame.AI;

namespace PetGame.Network
{
    /// <summary>
    /// Network synchronization component for player characters in multiplayer mode.
    /// Handles ownership-based input control, position/animation sync.
    /// Attach this to the player character prefab alongside CharacterEntity.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class NetworkPlayerController : NetworkBehaviour
    {
        [Header("Sync Settings")]
        [Tooltip("How often to sync position (times per second)")]
        public float positionSyncRate = 20f;

        [Tooltip("Interpolation speed for remote player position smoothing")]
        public float interpolationSpeed = 12f;

        // Network variables for state synchronization
        private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> networkFlipX = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<int> networkAnimState = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<float> networkMoveSpeed = new NetworkVariable<float>(
            0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        // Component references
        private CharacterEntity entity;
        private CharacterAnimator charAnimator;
        private ManualController manualController;
        private AIController aiController;
        private ControlModeManager controlModeManager;
        private SpriteRenderer spriteRenderer;

        private float syncTimer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            entity = GetComponent<CharacterEntity>();
            charAnimator = GetComponent<CharacterAnimator>();
            manualController = GetComponent<ManualController>();
            aiController = GetComponent<AIController>();
            controlModeManager = GetComponent<ControlModeManager>();
            spriteRenderer = GetComponent<SpriteRenderer>();

            if (IsOwner)
            {
                // Local player: enable input controls
                EnableLocalControls();
                // Initialize network position
                networkPosition.Value = transform.position;
                networkFlipX.Value = spriteRenderer != null && spriteRenderer.flipX;
            }
            else
            {
                // Remote player: disable input controls, enable interpolation
                DisableLocalControls();
            }

            // Subscribe to network variable changes for remote players
            networkPosition.OnValueChanged += OnNetworkPositionChanged;
            networkFlipX.OnValueChanged += OnNetworkFlipXChanged;
            networkAnimState.OnValueChanged += OnNetworkAnimStateChanged;
        }

        public override void OnNetworkDespawn()
        {
            base.OnNetworkDespawn();

            networkPosition.OnValueChanged -= OnNetworkPositionChanged;
            networkFlipX.OnValueChanged -= OnNetworkFlipXChanged;
            networkAnimState.OnValueChanged -= OnNetworkAnimStateChanged;
        }

        private void Update()
        {
            if (!IsSpawned) return;

            if (IsOwner)
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
                // Interpolate remote player position
                transform.position = Vector3.Lerp(
                    transform.position,
                    networkPosition.Value,
                    Time.deltaTime * interpolationSpeed);
            }
        }

        /// <summary>
        /// Enable local input controls for the owner player.
        /// </summary>
        private void EnableLocalControls()
        {
            if (manualController != null)
                manualController.enabled = true;
            if (aiController != null)
                aiController.enabled = true;
            if (controlModeManager != null)
                controlModeManager.enabled = true;

            Debug.Log("[NetworkPlayerController] Local controls enabled (owner).");
        }

        /// <summary>
        /// Disable local input controls for remote players.
        /// </summary>
        private void DisableLocalControls()
        {
            if (manualController != null)
            {
                manualController.SetActive(false);
                manualController.enabled = false;
            }
            if (aiController != null)
                aiController.enabled = false;
            if (controlModeManager != null)
                controlModeManager.enabled = false;

            Debug.Log("[NetworkPlayerController] Local controls disabled (remote player).");
        }

        // --- Network Variable Change Callbacks ---

        private void OnNetworkPositionChanged(Vector3 oldValue, Vector3 newValue)
        {
            // Position interpolation handled in Update
        }

        private void OnNetworkFlipXChanged(bool oldValue, bool newValue)
        {
            if (!IsOwner && spriteRenderer != null)
            {
                spriteRenderer.flipX = newValue;
            }
        }

        private void OnNetworkAnimStateChanged(int oldValue, int newValue)
        {
            if (!IsOwner && charAnimator != null)
            {
                // Apply animation state to remote player
                // 0=Idle, 1=Walk, 2=Attack, 3=Hit, 4=Death
                switch (newValue)
                {
                    case 0: charAnimator.PlayIdle(); break;
                    case 1: charAnimator.PlayWalk(); break;
                    case 2: charAnimator.PlayAttack(); break;
                    case 3: charAnimator.PlayHit(); break;
                    case 4: charAnimator.PlayDeath(); break;
                }
            }
        }

        // --- RPC Methods for Actions ---

        /// <summary>
        /// Called by the owner to broadcast an attack action to all clients.
        /// </summary>
        /// <param name="skillIndex">Index of the skill being used</param>
        [ServerRpc]
        public void PlayAttackServerRpc(int skillIndex)
        {
            PlayAttackClientRpc(skillIndex);
        }

        [ClientRpc]
        private void PlayAttackClientRpc(int skillIndex)
        {
            if (!IsOwner && charAnimator != null)
            {
                // Trigger attack/skill animation on remote clients
                if (skillIndex <= 0)
                    charAnimator.PlayAttack();
                else
                    charAnimator.PlaySkill(skillIndex);
            }
        }

        /// <summary>
        /// Called by the owner to broadcast a hit reaction to all clients.
        /// </summary>
        [ServerRpc]
        public void PlayHitServerRpc()
        {
            PlayHitClientRpc();
        }

        [ClientRpc]
        private void PlayHitClientRpc()
        {
            if (!IsOwner && charAnimator != null)
            {
                charAnimator.PlayHit();
            }
        }

        /// <summary>
        /// Called by the owner to sync animation state index.
        /// </summary>
        /// <param name="stateIndex">The animation state index</param>
        public void SyncAnimationState(int stateIndex)
        {
            if (IsOwner)
            {
                networkAnimState.Value = stateIndex;
            }
        }

        /// <summary>
        /// Sync movement speed for blend tree animations.
        /// </summary>
        /// <param name="speed">Current movement speed</param>
        public void SyncMoveSpeed(float speed)
        {
            if (IsOwner)
            {
                networkMoveSpeed.Value = speed;
            }
        }
    }
}
