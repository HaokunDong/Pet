using UnityEngine;
using Mirror;
using PetGame.AI;
using PetGame.Network;

namespace PetGame
{
    /// <summary>
    /// Central manager that coordinates the entire Boss fight flow.
    /// Handles: pausing enemy spawner, clearing existing enemies, spawning Boss,
    /// listening for Boss death, granting rewards, and resuming normal gameplay.
    /// 
    /// Setup:
    ///   1. Attach to a GameObject in the scene (must have NetworkIdentity for multiplayer).
    ///   2. Assign bossCharacterData, bossSpawnPoint, enemySpawner, and cardDistributor in Inspector.
    ///   3. Call StartBossFight() to initiate the Boss encounter (triggered by PortalManager).
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class BossFightManager : NetworkBehaviour
    {
        [Header("Boss Configuration")]
        [Tooltip("CharacterData asset for the Boss character")]
        [SerializeField] private CharacterData bossCharacterData;

        [Tooltip("Spawn point transform for the Boss. If null, uses Vector3.zero with a warning.")]
        [SerializeField] private Transform bossSpawnPoint;

        [Tooltip("Direct reference to the Boss prefab GameObject")]
        [SerializeField] private GameObject bossPrefab;

        [Header("References")]
        [Tooltip("Reference to the EnemySpawner in the scene")]
        [SerializeField] private EnemySpawner enemySpawner;

        [Tooltip("Reference to the CardSplineDistributor for reward delivery")]
        [SerializeField] private CardSplineDistributor cardDistributor;

        [Header("AI Settings")]
        [Tooltip("Detection range for Boss AI")]
        [SerializeField] private float bossDetectionRange = 15f;

        [Tooltip("Patrol range for Boss AI")]
        [SerializeField] private float bossPatrolRange = 5f;

        /// <summary>
        /// Event fired when the Boss is defeated. Other systems can subscribe to react.
        /// </summary>
        public event System.Action<CharacterData> OnBossDefeated;

        /// <summary>
        /// Whether a Boss fight is currently in progress.
        /// </summary>
        public bool IsBossFightActive { get; private set; }

        /// <summary>
        /// The portalId that triggered the current Boss fight.
        /// </summary>
        public uint CurrentPortalId => currentPortalId;
        public bool CanStartBossFight => !IsBossFightActive && bossCharacterData != null && bossPrefab != null;

        /// <summary>
        /// Reference to the currently active Boss entity (null if no fight is active).
        /// </summary>
        private CharacterEntity currentBossEntity;

        /// <summary>
        /// The portalId that triggered this Boss fight.
        /// Used to notify clients to destroy their local portal on Boss defeat.
        /// </summary>
        private uint currentPortalId;

        /// <summary>
        /// Reference to the player entity being monitored for death during Boss fight.
        /// </summary>
        private CharacterEntity monitoredPlayer;

        // =====================================================================
        // Public API
        // =====================================================================

        /// <summary>
        /// Starts the Boss fight sequence:
        /// 1. Pause enemy spawner
        /// 2. Clear all existing enemies
        /// 3. Spawn Boss at configured spawn point
        /// 4. Listen for Boss death
        /// In multiplayer mode, notifies all clients to also start the boss fight locally.
        /// </summary>
        /// <param name="portalObj">The portal GameObject that triggered this fight (can be null in multiplayer if portal is on another client).</param>
        /// <param name="portalId">The portal ID for network identification. Used to destroy the portal on all clients after Boss defeat.</param>
        public void StartBossFight(GameObject portalObj = null, uint portalId = 0)
        {
            if (NetworkClient.active && !NetworkServer.active) return;
            if (IsBossFightActive)
            {
                Debug.LogWarning("[BossFightManager] Boss fight already in progress!");
                return;
            }

            if (bossCharacterData == null)
            {
                Debug.LogError("[BossFightManager] Boss CharacterData is not assigned!");
                return;
            }

            IsBossFightActive = true;

            // Store portal ID for cleanup on Boss defeat
            currentPortalId = portalId;

            // If we have a local portal reference, destroy it now (it's on this client's Canvas)
            if (portalObj != null)
            {
                Destroy(portalObj);
            }

            // Execute the boss fight locally
            ExecuteBossFightLocally();

            // In multiplayer mode, notify all clients to also start the boss fight
            if (NetworkServer.active)
            {
                RpcStartBossFightOnClients(portalId);
            }

            Debug.Log("[BossFightManager] Boss fight started!");
        }

        /// <summary>
        /// RPC to notify all clients to start the boss fight locally.
        /// Also destroys the local portal (if it exists on this client) by portalId.
        /// </summary>
        [ClientRpc]
        private void RpcStartBossFightOnClients(uint portalId)
        {
            // Host already executed locally, skip
            if (NetworkServer.active) return;

            Debug.Log("[BossFightManager] Client received RPC to start boss fight.");
            IsBossFightActive = true;
            currentPortalId = portalId;

            // Destroy the local portal if it exists on this client's Canvas
            if (portalId != 0)
            {
                DestroyLocalPortalById(portalId);
            }

            ExecuteBossFightLocally();
        }

        /// <summary>
        /// Executes the boss fight sequence locally (both server and client).
        /// On clients, the Boss is NOT spawned here — it will be synced from the host
        /// via NetworkEnemySpawner as a MirrorEnemy. This avoids duplicate Boss spawning.
        /// </summary>
        private void ExecuteBossFightLocally()
        {
            // Step 1: Pause enemy spawning
            if (enemySpawner != null)
            {
                enemySpawner.PauseSpawning();
            }
            else
            {
                Debug.LogWarning("[BossFightManager] EnemySpawner reference is null. Attempting to find one.");
                enemySpawner = FindObjectOfType<EnemySpawner>();
                if (enemySpawner != null)
                    enemySpawner.PauseSpawning();
            }

            // Step 2: Clear all existing enemies
            if (enemySpawner != null)
            {
                enemySpawner.ClearAllEnemies();
            }

            // Step 3: Spawn Boss (host/server only)
            // Clients do NOT spawn the Boss locally — the host's Boss is tagged "Enemy"
            // and will be automatically detected and synced to clients by NetworkEnemySpawner
            // as a MirrorEnemy. Spawning here on clients would create a duplicate.
            bool isClientOnly = !NetworkServer.active && NetworkClient.active;
            if (!isClientOnly)
            {
                SpawnBoss();
            }
            else
            {
                Debug.Log("[BossFightManager] Client: Skipping local Boss spawn. Boss will be synced from host via NetworkEnemySpawner.");
            }

            // Step 4: Monitor player death for retry logic
            SubscribePlayerDeath();
        }

        /// <summary>
        /// Finds the player entity and subscribes to its death event.
        /// </summary>
        private void SubscribePlayerDeath()
        {
            // A client's personal death must not terminate the shared encounter.
            if (NetworkClient.active && !NetworkServer.active) return;
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj != null)
            {
                monitoredPlayer = playerObj.GetComponent<CharacterEntity>();
                if (monitoredPlayer != null)
                {
                    monitoredPlayer.OnDeath += OnPlayerDeath;
                }
            }
        }

        /// <summary>
        /// Unsubscribes from the player death event.
        /// </summary>
        private void UnsubscribePlayerDeath()
        {
            if (monitoredPlayer != null)
            {
                monitoredPlayer.OnDeath -= OnPlayerDeath;
                monitoredPlayer = null;
            }
        }

        /// <summary>
        /// Callback when the player dies during a Boss fight.
        /// </summary>
        private void OnPlayerDeath(CharacterEntity playerEntity)
        {
            UnsubscribePlayerDeath();
            OnPlayerDefeated();
        }

        // =====================================================================
        // Boss Spawning
        // =====================================================================

        /// <summary>
        /// Spawns the Boss at the configured spawn point.
        /// Only called on the server/host. The Boss has full AI and combat capabilities.
        /// Clients receive the Boss via NetworkEnemySpawner sync (as a MirrorEnemy).
        /// </summary>
        private void SpawnBoss()
        {
            if (NetworkClient.active && !NetworkServer.active) return;
            // Determine spawn position
            Vector3 spawnPosition;
            if (bossSpawnPoint != null)
            {
                spawnPosition = bossSpawnPoint.position;
            }
            else
            {
                spawnPosition = Vector3.zero;
                Debug.LogWarning("[BossFightManager] Boss spawn point not configured! Using Vector3.zero as default.");
            }

            // Instantiate Boss from prefab
            if (bossPrefab == null)
            {
                Debug.LogError("[BossFightManager] Boss prefab is not assigned!");
                IsBossFightActive = false;
                return;
            }

            GameObject bossObj = Instantiate(bossPrefab, spawnPosition, Quaternion.identity);

            // Position and activate
            bossObj.transform.position = spawnPosition;
            bossObj.SetActive(true);
            bossObj.tag = "Enemy";

            // Initialize CharacterEntity
            currentBossEntity = bossObj.GetComponent<CharacterEntity>();
            if (currentBossEntity == null)
                currentBossEntity = bossObj.AddComponent<CharacterEntity>();
            currentBossEntity.Initialize(bossCharacterData);

            // Ensure required components
            if (bossObj.GetComponent<CharacterAnimator>() == null)
                bossObj.AddComponent<CharacterAnimator>();

            if (bossObj.GetComponent<CombatSystem>() == null)
                bossObj.AddComponent<CombatSystem>();

            if (bossObj.GetComponent<AnimEventReceiver>() == null)
                bossObj.AddComponent<AnimEventReceiver>();

            if (bossObj.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D col = bossObj.AddComponent<BoxCollider2D>();
                col.isTrigger = false;
            }

            // Setup animator controller from data
            CharacterAnimator charAnim = bossObj.GetComponent<CharacterAnimator>();
            if (bossCharacterData.animatorController != null)
                charAnim.SetAnimatorController(bossCharacterData.animatorController);

            // Setup AI controller (server/host only)
            AIController aiController = bossObj.GetComponent<AIController>();
            if (aiController == null)
                aiController = bossObj.AddComponent<AIController>();

            aiController.detectionRange = bossDetectionRange;
            aiController.patrolRange = bossPatrolRange;
            aiController.InitializeAI();

            // Listen for Boss death (server only)
            currentBossEntity.OnDeath += OnBossDeath;

            Debug.Log($"[BossFightManager] Host: Boss '{bossCharacterData.characterName}' spawned at {spawnPosition} with full AI.");
        }

        // =====================================================================
        // Boss Death Handling & Rewards
        // =====================================================================

        /// <summary>
        /// Called when the Boss dies. Handles reward distribution and spawner resumption.
        /// </summary>
        private void OnBossDeath(CharacterEntity bossEntity)
        {
            if (NetworkClient.active && !NetworkServer.active) return;
            if (!IsBossFightActive || bossEntity != currentBossEntity) return;
            // Unsubscribe from death event
            bossEntity.OnDeath -= OnBossDeath;
            currentBossEntity = null;
            IsBossFightActive = false;

            // Unsubscribe from player death monitoring
            UnsubscribePlayerDeath();

            Debug.Log($"[BossFightManager] Boss '{bossCharacterData.characterName}' defeated!");

            // Notify SpecialLevelListManager to remove the option for this portal (multiplayer)
            if (currentPortalId != 0 && NetworkServer.active)
            {
                var levelListManager = SpecialLevelListManager.Instance;
                if (levelListManager == null)
                    levelListManager = FindObjectOfType<SpecialLevelListManager>();
                if (levelListManager != null)
                {
                    levelListManager.RemoveOptionAfterBossDefeat(currentPortalId);
                }

                // Notify all clients to destroy their local portal
                RpcDestroyPortalOnClients(currentPortalId);
            }

            currentPortalId = 0;

            // Every client checks its own deck, independently of the host's ownership.
            if (NetworkServer.active)
                RpcCompleteBossFightOnClients();
            CompleteBossFightLocally();
        }

        [ClientRpc]
        private void RpcCompleteBossFightOnClients()
        {
            // The host runs the same completion path directly exactly once.
            if (NetworkServer.active) return;
            CompleteBossFightLocally();
        }

        private void CompleteBossFightLocally()
        {
            IsBossFightActive = false;
            currentPortalId = 0;
            UnsubscribePlayerDeath();

            // Check if this is the first time defeating this Boss
            bool isFirstDefeat = !HasBossDataInDeck(bossCharacterData);

            if (isFirstDefeat)
            {
                GrantBossReward();
            }
            else
            {
                Debug.Log("[BossFightManager] Boss already in deck. No reward granted.");
            }

            // Resume enemy spawning
            if (enemySpawner != null)
            {
                enemySpawner.ResumeSpawning();
            }

            // Fire event for other systems
            OnBossDefeated?.Invoke(bossCharacterData);
        }

        /// <summary>
        /// Called when the player dies during a Boss fight.
        /// Ends the fight but keeps the portal alive so the player can retry.
        /// </summary>
        public void OnPlayerDefeated()
        {
            if (NetworkClient.active && !NetworkServer.active) return;
            if (!IsBossFightActive) return;

            Debug.Log("[BossFightManager] Player defeated during Boss fight. Boss fight ended, portal remains for retry.");

            // Clean up current Boss
            if (currentBossEntity != null)
            {
                currentBossEntity.OnDeath -= OnBossDeath;
                Destroy(currentBossEntity.gameObject);
                currentBossEntity = null;
            }

            IsBossFightActive = false;

            // Resume enemy spawning
            if (enemySpawner != null)
            {
                enemySpawner.ResumeSpawning();
            }

            // Portal is NOT destroyed — player can click it again to retry
        }

        public void ResetAfterLeavingRoom()
        {
            UnsubscribePlayerDeath();
            if (currentBossEntity != null)
            {
                currentBossEntity.OnDeath -= OnBossDeath;
                Destroy(currentBossEntity.gameObject);
                currentBossEntity = null;
            }
            IsBossFightActive = false;
            currentPortalId = 0;
        }

        /// <summary>
        /// Checks whether the given CharacterData already exists in the card distributor's list.
        /// </summary>
        private bool HasBossDataInDeck(CharacterData data)
        {
            if (cardDistributor == null)
            {
                cardDistributor = FindObjectOfType<CardSplineDistributor>();
            }

            if (cardDistributor == null || cardDistributor.CharacterDataList == null)
                return false;

            foreach (var existing in cardDistributor.CharacterDataList)
            {
                if (existing == data)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Grants the Boss reward by adding its CharacterData to the card deck.
        /// </summary>
        private void GrantBossReward()
        {
            if (cardDistributor == null)
            {
                cardDistributor = FindObjectOfType<CardSplineDistributor>();
            }

            if (cardDistributor != null)
            {
                bool added = cardDistributor.AddCharacterData(bossCharacterData);
                if (added)
                {
                    Debug.Log($"[BossFightManager] Reward granted! '{bossCharacterData.characterName}' added to card deck.");
                }
            }
            else
            {
                Debug.LogError("[BossFightManager] CardSplineDistributor not found! Cannot grant Boss reward.");
            }
        }

        // =====================================================================
        // Portal Cleanup (Network)
        // =====================================================================

        public void ApplyNetworkFightState(bool active, uint portalId)
        {
            if (NetworkServer.active) return;
            bool wasActive = IsBossFightActive;
            IsBossFightActive = active;
            currentPortalId = portalId;
            if (enemySpawner == null) enemySpawner = FindObjectOfType<EnemySpawner>();
            if (enemySpawner != null) enemySpawner.PauseSpawning();
            if (active && portalId != 0) DestroyLocalPortalById(portalId);
            if (wasActive && !active) UnsubscribePlayerDeath();
        }

        /// <summary>
        /// RPC: Notify all clients to destroy their local portal by portalId.
        /// Since portals are local UI elements (not networked objects), each client
        /// must find and destroy its own local copy.
        /// </summary>
        [ClientRpc]
        private void RpcDestroyPortalOnClients(uint portalId)
        {
            DestroyLocalPortalById(portalId);
        }

        /// <summary>
        /// Sync the current Boss fight state to a newly connected client (late joiner).
        /// Called from MirrorNetworkManager.OnServerAddPlayer().
        /// </summary>
        public void SyncBossFightToNewClient(NetworkConnectionToClient conn)
        {
            if (!IsBossFightActive) return;
            if (!NetworkServer.active) return;

            Debug.Log($"[BossFightManager] Syncing Boss fight state to new client: {conn.connectionId}");
            TargetRpcStartBossFightForLateJoiner(conn, currentPortalId);
        }

        /// <summary>
        /// TargetRpc: Start the Boss fight on a specific late-joining client.
        /// The Boss entity itself will be synced via NetworkEnemySpawner.
        /// </summary>
        [TargetRpc]
        private void TargetRpcStartBossFightForLateJoiner(NetworkConnectionToClient target, uint portalId)
        {
            Debug.Log("[BossFightManager] Late joiner: Received Boss fight state sync.");
            IsBossFightActive = true;
            currentPortalId = portalId;

            // Destroy the local portal if it exists
            if (portalId != 0)
            {
                DestroyLocalPortalById(portalId);
            }

            // Pause enemy spawner and clear local enemies (Boss fight mode)
            if (enemySpawner != null)
            {
                enemySpawner.PauseSpawning();
                enemySpawner.ClearAllEnemies();
            }
            else
            {
                enemySpawner = FindObjectOfType<EnemySpawner>();
                if (enemySpawner != null)
                {
                    enemySpawner.PauseSpawning();
                    enemySpawner.ClearAllEnemies();
                }
            }

            // Subscribe to player death for retry logic
            SubscribePlayerDeath();
        }

        /// <summary>
        /// Find and destroy a local portal by its portalId.
        /// Portals are local UI elements on each client's Canvas.
        /// </summary>
        private void DestroyLocalPortalById(uint portalId)
        {
            if (portalId == 0) return;

            PortalController[] allPortals = FindObjectsOfType<PortalController>();
            foreach (var portal in allPortals)
            {
                if (portal.portalId == portalId)
                {
                    Destroy(portal.gameObject);
                    Debug.Log($"[BossFightManager] Local portal (portalId={portalId}) destroyed.");
                    return;
                }
            }
        }
    }
}
