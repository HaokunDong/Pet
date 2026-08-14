using UnityEngine;
using Mirror;
using System.Collections.Generic;
using PetGame.AI;

namespace PetGame.Network
{
    #region Network Messages

    /// <summary>Message sent from server to clients to spawn a mirror enemy.</summary>
    public struct EnemySpawnMessage : NetworkMessage
    {
        public uint enemyNetId;
        public string characterDataId;
        public Vector3 position;
        public float health;
        public float maxHealth;
    }

    /// <summary>Message sent from server to clients to update enemy position.</summary>
    public struct EnemyPositionMessage : NetworkMessage
    {
        public uint enemyNetId;
        public Vector3 position;
        public bool facingRight;
    }

    /// <summary>Message sent from server to clients when an enemy dies.</summary>
    public struct EnemyDeathMessage : NetworkMessage
    {
        public uint enemyNetId;
    }

    #endregion

    /// <summary>
    /// Network-aware enemy spawner that synchronizes enemies from host to clients.
    /// Uses Mirror's custom message system (no NetworkIdentity required on this GameObject).
    /// The host runs EnemySpawner normally and this component syncs enemy state to clients.
    /// Clients create local "mirror enemies" that are driven by synced data from the host.
    /// Attach this alongside EnemySpawner on the same GameObject.
    /// </summary>
    public class NetworkEnemySpawner : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the local EnemySpawner component")]
        [SerializeField] private EnemySpawner localSpawner;

        [Header("Sync Settings")]
        [Tooltip("How often to sync enemy positions per second")]
        [SerializeField] private float syncRate = 10f;

        /// <summary>
        /// Whether this spawner is active in multiplayer mode.
        /// Only the host/server should spawn enemies.
        /// </summary>
        public bool IsActiveInMultiplayer => NetworkServer.active;

        // Server-side: track all active enemies with unique IDs
        private Dictionary<uint, EnemyNetData> serverEnemies = new Dictionary<uint, EnemyNetData>();
        private uint nextEnemyId = 1;
        private float syncTimer;

        // Client-side: track mirror enemies
        private Dictionary<uint, GameObject> clientMirrorEnemies = new Dictionary<uint, GameObject>();

        private bool isRegistered = false;

        /// <summary>Data structure to track enemy state on the server.</summary>
        private class EnemyNetData
        {
            public uint netId;
            public string characterDataId;
            public CharacterEntity entity;
            public GameObject gameObject;
            public Vector3 lastSyncedPosition;
            public bool lastFacingRight;
        }

        private void Awake()
        {
            if (localSpawner == null)
                localSpawner = GetComponent<EnemySpawner>();
        }

        private void OnEnable()
        {
            RegisterHandlers();
        }

        private void OnDisable()
        {
            UnregisterHandlers();
            CleanupMirrorEnemies();
        }

        /// <summary>
        /// Register network message handlers on the client side.
        /// </summary>
        private void RegisterHandlers()
        {
            if (isRegistered) return;

            // Client handlers - only register if we're a client (not host)
            if (NetworkClient.active && !NetworkServer.active)
            {
                NetworkClient.RegisterHandler<EnemySpawnMessage>(OnClientEnemySpawn);
                NetworkClient.RegisterHandler<EnemyPositionMessage>(OnClientEnemyPosition);
                NetworkClient.RegisterHandler<EnemyDeathMessage>(OnClientEnemyDeath);
                isRegistered = true;
                Debug.Log("[NetworkEnemySpawner] Client message handlers registered.");
            }
        }

        private void UnregisterHandlers()
        {
            if (!isRegistered) return;

            if (NetworkClient.active)
            {
                NetworkClient.UnregisterHandler<EnemySpawnMessage>();
                NetworkClient.UnregisterHandler<EnemyPositionMessage>();
                NetworkClient.UnregisterHandler<EnemyDeathMessage>();
            }
            isRegistered = false;
        }

        private void Start()
        {
            // If we are a client (not host), disable the local spawner
            if (MirrorNetworkManager.singleton != null && MirrorNetworkManager.singleton.IsClientOnly)
            {
                if (localSpawner != null)
                {
                    localSpawner.enabled = false;
                    Debug.Log("[NetworkEnemySpawner] Client mode: local EnemySpawner disabled (host manages spawning).");
                }

                // Register handlers (might not have been active in OnEnable)
                RegisterHandlers();
            }
            else if (NetworkServer.active)
            {
                Debug.Log("[NetworkEnemySpawner] Server started. Enemy sync active on host.");
            }
        }

        private void Update()
        {
            // Try to register handlers if not yet done (for late initialization)
            if (!isRegistered && NetworkClient.active && !NetworkServer.active)
            {
                RegisterHandlers();
            }

            if (!NetworkServer.active) return;

            // Server: Periodically scan for new enemies and sync their state
            syncTimer += Time.deltaTime;
            if (syncTimer >= 1f / syncRate)
            {
                syncTimer = 0f;
                ScanAndSyncEnemies();
            }
        }

        /// <summary>
        /// Server: Scan the scene for active enemies and sync their state to clients.
        /// </summary>
        private void ScanAndSyncEnemies()
        {
            if (!NetworkServer.active) return;

            // Find all active enemies in the scene
            GameObject[] enemyObjects = GameObject.FindGameObjectsWithTag("Enemy");

            // Track which enemies are still alive
            HashSet<uint> aliveEnemyIds = new HashSet<uint>();

            foreach (GameObject enemyObj in enemyObjects)
            {
                if (enemyObj == null || !enemyObj.activeInHierarchy) continue;

                CharacterEntity entity = enemyObj.GetComponent<CharacterEntity>();
                if (entity == null) continue;

                // Check if this enemy is already tracked
                uint existingId = FindEnemyId(enemyObj);
                if (existingId == 0)
                {
                    // New enemy - register and notify clients
                    RegisterNewEnemy(enemyObj, entity);
                    existingId = FindEnemyId(enemyObj);
                }

                if (existingId != 0)
                {
                    aliveEnemyIds.Add(existingId);

                    // Sync position updates
                    EnemyNetData data = serverEnemies[existingId];
                    Vector3 currentPos = enemyObj.transform.position;
                    SpriteRenderer sr = enemyObj.GetComponent<SpriteRenderer>();
                    bool facingRight = sr != null ? !sr.flipX : true;

                    if (Vector3.Distance(currentPos, data.lastSyncedPosition) > 0.05f || facingRight != data.lastFacingRight)
                    {
                        data.lastSyncedPosition = currentPos;
                        data.lastFacingRight = facingRight;

                        NetworkServer.SendToAll(new EnemyPositionMessage
                        {
                            enemyNetId = existingId,
                            position = currentPos,
                            facingRight = facingRight
                        });
                    }

                    // Check if enemy died
                    if (entity.RuntimeStats != null && !entity.RuntimeStats.IsAlive)
                    {
                        NetworkServer.SendToAll(new EnemyDeathMessage { enemyNetId = existingId });
                        serverEnemies.Remove(existingId);
                        aliveEnemyIds.Remove(existingId);
                    }
                }
            }

            // Remove tracked enemies that are no longer in the scene
            List<uint> toRemove = new List<uint>();
            foreach (var kvp in serverEnemies)
            {
                if (!aliveEnemyIds.Contains(kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }
            foreach (uint id in toRemove)
            {
                NetworkServer.SendToAll(new EnemyDeathMessage { enemyNetId = id });
                serverEnemies.Remove(id);
            }
        }

        /// <summary>
        /// Server: Register a new enemy and notify all clients to create a mirror.
        /// </summary>
        private void RegisterNewEnemy(GameObject enemyObj, CharacterEntity entity)
        {
            uint id = nextEnemyId++;
            string characterDataId = entity.characterData != null ? entity.characterData.characterId : "";

            EnemyNetData data = new EnemyNetData
            {
                netId = id,
                characterDataId = characterDataId,
                entity = entity,
                gameObject = enemyObj,
                lastSyncedPosition = enemyObj.transform.position,
                lastFacingRight = true
            };

            serverEnemies[id] = data;

            // Notify all clients to spawn this enemy
            float health = entity.RuntimeStats != null ? entity.RuntimeStats.currentHealth : 100f;
            float maxHealth = entity.RuntimeStats != null ? entity.RuntimeStats.maxHealth : 100f;

            NetworkServer.SendToAll(new EnemySpawnMessage
            {
                enemyNetId = id,
                characterDataId = characterDataId,
                position = enemyObj.transform.position,
                health = health,
                maxHealth = maxHealth
            });

            Debug.Log($"[NetworkEnemySpawner] Registered enemy #{id}: {characterDataId} at {enemyObj.transform.position}");
        }

        /// <summary>
        /// Find the network ID of a tracked enemy by its GameObject reference.
        /// </summary>
        private uint FindEnemyId(GameObject enemyObj)
        {
            foreach (var kvp in serverEnemies)
            {
                if (kvp.Value.gameObject == enemyObj)
                    return kvp.Key;
            }
            return 0;
        }

        #region Client Message Handlers

        private void OnClientEnemySpawn(EnemySpawnMessage msg)
        {
            // Host already has the real enemy, skip
            if (NetworkServer.active) return;

            CreateMirrorEnemy(msg.enemyNetId, msg.characterDataId, msg.position, msg.health, msg.maxHealth);
        }

        private void OnClientEnemyPosition(EnemyPositionMessage msg)
        {
            if (NetworkServer.active) return;

            if (clientMirrorEnemies.TryGetValue(msg.enemyNetId, out GameObject mirrorObj) && mirrorObj != null)
            {
                // Smooth interpolation
                mirrorObj.transform.position = Vector3.Lerp(mirrorObj.transform.position, msg.position, 0.5f);

                SpriteRenderer sr = mirrorObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.flipX = !msg.facingRight;
            }
        }

        private void OnClientEnemyDeath(EnemyDeathMessage msg)
        {
            if (NetworkServer.active) return;

            if (clientMirrorEnemies.TryGetValue(msg.enemyNetId, out GameObject mirrorObj) && mirrorObj != null)
            {
                // Play death animation if available
                CharacterAnimator charAnim = mirrorObj.GetComponent<CharacterAnimator>();
                if (charAnim != null)
                {
                    charAnim.PlayDeath();
                }

                // Destroy after a short delay for death animation
                Destroy(mirrorObj, 1f);
                clientMirrorEnemies.Remove(msg.enemyNetId);
                Debug.Log($"[NetworkEnemySpawner] Mirror enemy #{msg.enemyNetId} died.");
            }
        }

        #endregion

        #region Client - Mirror Enemy Management

        /// <summary>
        /// Client: Create a local mirror enemy to represent a host-side enemy.
        /// </summary>
        private void CreateMirrorEnemy(uint enemyNetId, string characterDataId, Vector3 position, float health, float maxHealth)
        {
            if (clientMirrorEnemies.ContainsKey(enemyNetId)) return;

            // Find the CharacterData
            CharacterData charData = FindEnemyDataById(characterDataId);
            if (charData == null)
            {
                Debug.LogWarning($"[NetworkEnemySpawner] Cannot create mirror enemy: CharacterData not found for '{characterDataId}'");
                return;
            }

            // Create enemy object from prefab
            GameObject enemyObj = null;
            if (charData.prefab != null)
            {
                enemyObj = Instantiate(charData.prefab, position, Quaternion.identity);
            }
            else
            {
                enemyObj = new GameObject($"MirrorEnemy_{characterDataId}_{enemyNetId}");
            }

            enemyObj.transform.position = position;
            enemyObj.SetActive(true);
            enemyObj.tag = "Enemy";
            enemyObj.name = $"MirrorEnemy_{characterDataId}_{enemyNetId}";

            // Initialize CharacterEntity
            CharacterEntity entity = enemyObj.GetComponent<CharacterEntity>();
            if (entity == null)
                entity = enemyObj.AddComponent<CharacterEntity>();
            entity.Initialize(charData);

            // Set health
            if (entity.RuntimeStats != null)
            {
                entity.RuntimeStats.currentHealth = health;
                entity.RuntimeStats.maxHealth = maxHealth;
            }

            // Ensure visual components
            SpriteRenderer sr = enemyObj.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = enemyObj.AddComponent<SpriteRenderer>();
            if (charData.sprite != null)
                sr.sprite = charData.sprite;

            // Add CharacterAnimator
            CharacterAnimator charAnim = enemyObj.GetComponent<CharacterAnimator>();
            if (charAnim == null)
                charAnim = enemyObj.AddComponent<CharacterAnimator>();
            if (charData.animatorController != null)
                charAnim.SetAnimatorController(charData.animatorController);

            // Ensure Collider2D exists
            if (enemyObj.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D col = enemyObj.AddComponent<BoxCollider2D>();
                col.isTrigger = false;
            }

            // Disable AI on mirror enemies - they are driven by network sync
            AIController ai = enemyObj.GetComponent<AIController>();
            if (ai != null) ai.enabled = false;

            // Disable CombatSystem on mirror enemies - host handles combat
            CombatSystem combat = enemyObj.GetComponent<CombatSystem>();
            if (combat != null) combat.enabled = false;

            clientMirrorEnemies[enemyNetId] = enemyObj;
            Debug.Log($"[NetworkEnemySpawner] Created mirror enemy #{enemyNetId}: {characterDataId}");
        }

        /// <summary>
        /// Find enemy CharacterData by its ID. Searches EnemySpawner's list first.
        /// </summary>
        private CharacterData FindEnemyDataById(string characterDataId)
        {
            if (string.IsNullOrEmpty(characterDataId)) return null;

            // Check local spawner's enemy data list
            if (localSpawner != null && localSpawner.enemyDataList != null)
            {
                foreach (CharacterData data in localSpawner.enemyDataList)
                {
                    if (data != null && data.characterId == characterDataId)
                        return data;
                }
            }

            // Fallback: search all loaded CharacterData
            CharacterData[] allLoaded = Resources.FindObjectsOfTypeAll<CharacterData>();
            foreach (CharacterData data in allLoaded)
            {
                if (data != null && data.characterId == characterDataId)
                    return data;
            }

            return null;
        }

        /// <summary>
        /// Clean up all mirror enemies.
        /// </summary>
        private void CleanupMirrorEnemies()
        {
            foreach (var kvp in clientMirrorEnemies)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            clientMirrorEnemies.Clear();
        }

        #endregion

        #region Server - Late Joiner Support

        /// <summary>
        /// When a new client connects, send them all currently active enemies.
        /// Called from MirrorNetworkManager.OnServerAddPlayer.
        /// </summary>
        public void SyncAllEnemiesToNewClient(NetworkConnectionToClient conn)
        {
            if (!NetworkServer.active) return;

            foreach (var kvp in serverEnemies)
            {
                EnemyNetData data = kvp.Value;
                if (data.gameObject == null || !data.gameObject.activeInHierarchy) continue;

                float health = data.entity != null && data.entity.RuntimeStats != null
                    ? data.entity.RuntimeStats.currentHealth : 100f;
                float maxHealth = data.entity != null && data.entity.RuntimeStats != null
                    ? data.entity.RuntimeStats.maxHealth : 100f;

                conn.Send(new EnemySpawnMessage
                {
                    enemyNetId = data.netId,
                    characterDataId = data.characterDataId,
                    position = data.gameObject.transform.position,
                    health = health,
                    maxHealth = maxHealth
                });
            }

            Debug.Log($"[NetworkEnemySpawner] Synced {serverEnemies.Count} enemies to new client: {conn.connectionId}");
        }

        #endregion
    }
}
