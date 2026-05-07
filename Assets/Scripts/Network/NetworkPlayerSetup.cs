using Unity.Netcode;
using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Handles initialization of a network-spawned player character.
    /// This component is added to the NetworkPlayerPrefab and configures
    /// the character based on the local player's CharacterData after spawning.
    /// </summary>
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkPlayerSetup : NetworkBehaviour
    {
        // Network variable to sync which character data to use (by index or name)
        private NetworkVariable<int> characterDataIndex = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<Vector3> networkPosition = new NetworkVariable<Vector3>(
            default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<bool> networkFlipX = new NetworkVariable<bool>(
            false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        private NetworkVariable<int> networkAnimState = new NetworkVariable<int>(
            0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        [Header("Sync Settings")]
        [Tooltip("How often to sync position (times per second)")]
        public float positionSyncRate = 20f;

        [Tooltip("Interpolation speed for remote player position smoothing")]
        public float interpolationSpeed = 12f;

        // Component references (set after initialization)
        private CharacterEntity entity;
        private CharacterAnimator charAnimator;
        private SpriteRenderer spriteRenderer;
        private Animator animator;
        private bool isInitialized = false;
        private float syncTimer;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            Debug.Log($"[NetworkPlayerSetup] OnNetworkSpawn called. IsOwner={IsOwner}, OwnerClientId={OwnerClientId}");

            if (IsOwner)
            {
                // This is the local player's network object
                InitializeAsLocalPlayer();
            }
            else
            {
                // This is a remote player's network object
                InitializeAsRemotePlayer();
            }

            // Subscribe to network variable changes
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

        /// <summary>
        /// Initialize this network object as the local player.
        /// Copies appearance and data from the existing local character.
        /// </summary>
        private void InitializeAsLocalPlayer()
        {
            // Find the existing local player character
            GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm == null || gcm.PlayerCharacters.Count == 0)
            {
                Debug.LogWarning("[NetworkPlayerSetup] No local player character found to sync from.");
                return;
            }

            CharacterEntity localEntity = gcm.PlayerCharacters[0];
            if (localEntity == null) return;

            // Copy character data to this network object
            SetupCharacterVisuals(localEntity.characterData);

            // Position this network object at the local player's position
            transform.position = localEntity.transform.position;
            networkPosition.Value = transform.position;

            // Hide the original local character (we'll use this network object instead)
            // Or better: make this network object invisible and just sync data
            // For simplicity, we'll hide this network object for the owner
            // and keep the original character visible
            HideForOwner();

            isInitialized = true;
            Debug.Log("[NetworkPlayerSetup] Initialized as local player (hidden, syncing position).");
        }

        /// <summary>
        /// Initialize this network object as a remote player.
        /// Sets up visuals based on synced data.
        /// </summary>
        private void InitializeAsRemotePlayer()
        {
            // Find character data to use for remote player appearance
            GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm != null && gcm.playerCharacterDataList != null && gcm.playerCharacterDataList.Length > 0)
            {
                // Use the first available character data for remote player appearance
                SetupCharacterVisuals(gcm.playerCharacterDataList[0]);
            }

            isInitialized = true;
            Debug.Log("[NetworkPlayerSetup] Initialized as remote player (visible).");
        }

        /// <summary>
        /// Setup character visuals (sprite, animator) from CharacterData.
        /// </summary>
        private void SetupCharacterVisuals(CharacterData data)
        {
            if (data == null) return;

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null)
                spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            if (data.sprite != null)
                spriteRenderer.sprite = data.sprite;

            animator = GetComponent<Animator>();
            if (animator == null)
                animator = gameObject.AddComponent<Animator>();

            if (data.animatorController != null)
                animator.runtimeAnimatorController = data.animatorController;

            // Add CharacterAnimator
            charAnimator = GetComponent<CharacterAnimator>();
            if (charAnimator == null)
                charAnimator = gameObject.AddComponent<CharacterAnimator>();
            if (data.animatorController != null)
                charAnimator.SetAnimatorController(data.animatorController);

            // Add CharacterEntity
            entity = GetComponent<CharacterEntity>();
            if (entity == null)
                entity = gameObject.AddComponent<CharacterEntity>();
            entity.Initialize(data);
        }

        /// <summary>
        /// Hide this object for the owner (since owner already has their local character visible).
        /// </summary>
        private void HideForOwner()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                spriteRenderer.enabled = false;

            // Disable collider for owner's network object
            var collider = GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
        }

        private void Update()
        {
            if (!IsSpawned || !isInitialized) return;

            if (IsOwner)
            {
                // Sync local player's position to network
                SyncLocalPlayerPosition();
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
        /// Sync the local player character's position to this network object.
        /// </summary>
        private void SyncLocalPlayerPosition()
        {
            syncTimer += Time.deltaTime;
            if (syncTimer < 1f / positionSyncRate) return;
            syncTimer = 0f;

            // Find local player character and sync its position
            GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm == null || gcm.PlayerCharacters.Count == 0) return;

            CharacterEntity localEntity = gcm.PlayerCharacters[0];
            if (localEntity == null) return;

            networkPosition.Value = localEntity.transform.position;

            // Sync flip state
            SpriteRenderer localSr = localEntity.GetComponent<SpriteRenderer>();
            if (localSr != null)
                networkFlipX.Value = localSr.flipX;

            // Sync animation state
            Animator localAnim = localEntity.GetComponent<Animator>();
            if (localAnim != null)
            {
                AnimatorStateInfo stateInfo = localAnim.GetCurrentAnimatorStateInfo(0);
                int animState = 0; // Idle
                if (stateInfo.IsName("Walk") || stateInfo.IsName("Run"))
                    animState = 1;
                else if (stateInfo.IsName("Attack") || stateInfo.IsTag("Attack"))
                    animState = 2;
                else if (stateInfo.IsName("Hit"))
                    animState = 3;
                else if (stateInfo.IsName("Death"))
                    animState = 4;

                networkAnimState.Value = animState;
            }
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

        // --- RPC Methods ---

        /// <summary>
        /// Called by the owner to broadcast an attack action to all clients.
        /// </summary>
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
                if (skillIndex <= 0)
                    charAnimator.PlayAttack();
                else
                    charAnimator.PlaySkill(skillIndex);
            }
        }
    }
}
