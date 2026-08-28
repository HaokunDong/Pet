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
    ///   1. Attach to a GameObject in the scene.
    ///   2. Assign bossCharacterData, bossSpawnPoint, enemySpawner, and cardDistributor in Inspector.
    ///   3. Call StartBossFight() to initiate the Boss encounter (triggered by PortalManager).
    /// </summary>
    public class BossFightManager : MonoBehaviour
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
        /// Reference to the currently active Boss entity (null if no fight is active).
        /// </summary>
        private CharacterEntity currentBossEntity;

        /// <summary>
        /// Reference to the portal GameObject that triggered this Boss fight.
        /// Will be destroyed upon Boss defeat.
        /// </summary>
        private GameObject currentPortal;

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
        /// </summary>
        /// <param name="portalObj">The portal GameObject that triggered this fight. Will be destroyed on Boss defeat.</param>
        public void StartBossFight(GameObject portalObj = null)
        {
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

            // Store portal reference for destruction on Boss defeat
            currentPortal = portalObj;

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

            // Step 3: Spawn Boss
            SpawnBoss();

            // Step 4: Monitor player death for retry logic
            SubscribePlayerDeath();

            Debug.Log("[BossFightManager] Boss fight started!");
        }

        /// <summary>
        /// Finds the player entity and subscribes to its death event.
        /// </summary>
        private void SubscribePlayerDeath()
        {
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
        /// Spawns the Boss at the configured spawn point using the object pool.
        /// </summary>
        private void SpawnBoss()
        {
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

            // Setup AI controller
            AIController aiController = bossObj.GetComponent<AIController>();
            if (aiController == null)
                aiController = bossObj.AddComponent<AIController>();

            aiController.detectionRange = bossDetectionRange;
            aiController.patrolRange = bossPatrolRange;
            aiController.InitializeAI();

            // Step 4: Listen for Boss death
            currentBossEntity.OnDeath += OnBossDeath;

            Debug.Log($"[BossFightManager] Boss '{bossCharacterData.characterName}' spawned at {spawnPosition}");
        }

        // =====================================================================
        // Boss Death Handling & Rewards
        // =====================================================================

        /// <summary>
        /// Called when the Boss dies. Handles reward distribution and spawner resumption.
        /// </summary>
        private void OnBossDeath(CharacterEntity bossEntity)
        {
            // Unsubscribe from death event
            bossEntity.OnDeath -= OnBossDeath;
            currentBossEntity = null;
            IsBossFightActive = false;

            // Unsubscribe from player death monitoring
            UnsubscribePlayerDeath();

            Debug.Log($"[BossFightManager] Boss '{bossCharacterData.characterName}' defeated!");

            // Notify SpecialLevelListManager to remove the option for this portal (multiplayer)
            if (currentPortal != null)
            {
                var netIdentity = currentPortal.GetComponent<NetworkIdentity>();
                if (netIdentity != null && NetworkServer.active)
                {
                    var levelListManager = SpecialLevelListManager.Instance;
                    if (levelListManager == null)
                        levelListManager = FindObjectOfType<SpecialLevelListManager>();
                    if (levelListManager != null)
                    {
                        levelListManager.RemoveOptionAfterBossDefeat(netIdentity.netId);
                    }
                }
            }

            // Destroy the portal that triggered this fight
            if (currentPortal != null)
            {
                Destroy(currentPortal);
                currentPortal = null;
                Debug.Log("[BossFightManager] Associated portal destroyed.");
            }

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
    }
}
