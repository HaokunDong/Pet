using UnityEngine;
using Mirror;

namespace PetGame.Network
{
    /// <summary>
    /// Syncs a networked character's position, animation state, and health across the network.
    /// Attached to character GameObjects that are spawned via NetworkServer.Spawn().
    /// The owning client has authority and sends position updates to the server.
    /// </summary>
    public class NetworkCharacterSync : NetworkBehaviour
    {
        #region Settings

        [Header("Sync Settings")]
        [Tooltip("How often to send position updates per second")]
        [SerializeField] private float syncRate = 20f;

        [Tooltip("Interpolation speed for smoothing remote player movement")]
        [SerializeField] private float interpolationSpeed = 15f;

        [Tooltip("Threshold distance to trigger position sync")]
        [SerializeField] private float positionThreshold = 0.01f;

        #endregion

        #region SyncVars

        /// <summary>Synced position from the authoritative client.</summary>
        [SyncVar]
        private Vector3 syncedPosition;

        /// <summary>Synced facing direction (true = right, false = left).</summary>
        [SyncVar]
        private bool syncedFacingRight = true;

        /// <summary>Synced current health for health bar display.</summary>
        [SyncVar(hook = nameof(OnHealthChanged))]
        private float syncedHealth;

        /// <summary>Synced max health for health bar display.</summary>
        [SyncVar]
        private float syncedMaxHealth;

        /// <summary>Synced animation state hash.</summary>
        [SyncVar]
        private int syncedAnimStateHash;

        /// <summary>Synced whether the character is alive.</summary>
        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool syncedIsAlive = true;

        #endregion

        #region Private Fields

        private CharacterEntity characterEntity;
        private CharacterAnimator characterAnimator;
        private SpriteRenderer spriteRenderer;
        private float syncTimer;
        private Vector3 lastSentPosition;

        #endregion

        #region Lifecycle

        private void Awake()
        {
            characterEntity = GetComponent<CharacterEntity>();
            characterAnimator = GetComponent<CharacterAnimator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();

            if (characterEntity != null && characterEntity.RuntimeStats != null)
            {
                syncedHealth = characterEntity.RuntimeStats.currentHealth;
                syncedMaxHealth = characterEntity.RuntimeStats.maxHealth;
                syncedIsAlive = characterEntity.RuntimeStats.IsAlive;
            }

            syncedPosition = transform.position;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Set initial position for remote characters
            if (!isOwned)
            {
                transform.position = syncedPosition;
            }
        }

        private void Update()
        {
            // Guard: ensure required references are available
            if (characterEntity == null)
            {
                characterEntity = GetComponent<CharacterEntity>();
                if (characterEntity == null) return;
            }
            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponent<SpriteRenderer>();
            }
            if (characterAnimator == null)
            {
                characterAnimator = GetComponent<CharacterAnimator>();
            }

            if (isOwned)
            {
                // Authoritative client: send position updates to server
                UpdateAuthoritativeClient();
            }
            else
            {
                // Remote client: interpolate to synced position
                UpdateRemoteClient();
            }
        }

        #endregion

        #region Authoritative Client (Owner)

        /// <summary>
        /// The owning client sends position and state updates to the server.
        /// </summary>
        private void UpdateAuthoritativeClient()
        {
            syncTimer += Time.deltaTime;

            if (syncTimer >= 1f / syncRate)
            {
                syncTimer = 0f;

                // Send position if changed
                if (Vector3.Distance(transform.position, lastSentPosition) > positionThreshold)
                {
                    CmdUpdatePosition(transform.position);
                    lastSentPosition = transform.position;
                }

                // Send facing direction
                if (spriteRenderer != null)
                {
                    bool facingRight = spriteRenderer.flipX == false;
                    if (facingRight != syncedFacingRight)
                    {
                        CmdUpdateFacing(facingRight);
                    }
                }

                // Send health updates
                if (characterEntity != null && characterEntity.RuntimeStats != null)
                {
                    float currentHealth = characterEntity.RuntimeStats.currentHealth;
                    if (Mathf.Abs(currentHealth - syncedHealth) > 0.1f)
                    {
                        CmdUpdateHealth(currentHealth, characterEntity.RuntimeStats.maxHealth);
                    }

                    bool isAlive = characterEntity.RuntimeStats.IsAlive;
                    if (isAlive != syncedIsAlive)
                    {
                        CmdUpdateAlive(isAlive);
                    }
                }
            }
        }

        #endregion

        #region Remote Client (Non-Owner)

        /// <summary>
        /// Remote clients interpolate position and apply synced state.
        /// </summary>
        private void UpdateRemoteClient()
        {
            // Smooth position interpolation
            transform.position = Vector3.Lerp(
                transform.position,
                syncedPosition,
                Time.deltaTime * interpolationSpeed
            );

            // Apply facing direction
            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = !syncedFacingRight;
            }
        }

        #endregion

        #region Commands (Client -> Server)

        [Command]
        private void CmdUpdatePosition(Vector3 position)
        {
            syncedPosition = position;
            // Also update server-side transform for physics
            transform.position = position;
        }

        [Command]
        private void CmdUpdateFacing(bool facingRight)
        {
            syncedFacingRight = facingRight;
        }

        [Command]
        private void CmdUpdateHealth(float health, float maxHealth)
        {
            syncedHealth = health;
            syncedMaxHealth = maxHealth;
        }

        [Command]
        private void CmdUpdateAlive(bool alive)
        {
            syncedIsAlive = alive;
        }

        #endregion

        #region SyncVar Hooks

        /// <summary>
        /// Called on all clients when health changes.
        /// Updates the health bar for remote characters.
        /// </summary>
        private void OnHealthChanged(float oldHealth, float newHealth)
        {
            if (isOwned) return; // Owner manages their own health bar

            if (characterEntity != null && characterEntity.RuntimeStats != null)
            {
                characterEntity.RuntimeStats.currentHealth = newHealth;
            }
        }

        /// <summary>
        /// Called on all clients when alive state changes.
        /// Triggers death for remote characters.
        /// </summary>
        private void OnAliveChanged(bool oldAlive, bool newAlive)
        {
            if (isOwned) return;

            if (!newAlive && characterEntity != null)
            {
                // Remote character died - play death animation
                if (characterAnimator != null)
                {
                    characterAnimator.PlayDeath();
                }
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Apply damage to this character from the network.
        /// Only the server should call this.
        /// </summary>
        [Server]
        public void ServerApplyDamage(float attackPower, CharacterEntity attacker = null)
        {
            if (characterEntity != null)
            {
                characterEntity.TakeDamage(attackPower, attacker);

                // Update synced values
                syncedHealth = characterEntity.RuntimeStats.currentHealth;
                syncedIsAlive = characterEntity.RuntimeStats.IsAlive;
            }
        }

        #endregion
    }
}
