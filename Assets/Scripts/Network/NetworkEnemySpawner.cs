using UnityEngine;
using Mirror;
using System.Collections.Generic;
using PetGame.AI;

namespace PetGame.Network
{
    #region Network Messages

    public struct EnemyAnimatorParameter
    {
        public int hash;
        public int type;
        public float floatValue;
        public int intValue;
        public bool boolValue;
    }

    public struct EnemySnapshot
    {
        public uint id;
        public string characterId;
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
        public bool facingRight;
        public string statsJson;
        public int[] cooldownKeys;
        public float[] cooldownValues;
        public int animationHash;
        public float animationTime;
        public float animationSpeed;
        public EnemyAnimatorParameter[] animationParameters;
    }

    public struct WorldSnapshotMessage : NetworkMessage
    {
        public string scene;
        public EnemySnapshot[] enemies;
        public bool bossFightActive;
        public uint bossPortalId;
    }

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
        public float health;
        public float maxHealth;
    }

    /// <summary>Message sent from server to clients when an enemy dies.</summary>
    public struct EnemyDeathMessage : NetworkMessage
    {
        public uint enemyNetId;
    }

    /// <summary>Message sent from server to clients when an enemy takes a hit (for animation sync).</summary>
    public struct EnemyHitMessage : NetworkMessage
    {
        public uint enemyNetId;
        public float currentHealth;
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
        private bool clientScenePrepared;
        private readonly Dictionary<uint, Vector3> clientTargetPositions = new Dictionary<uint, Vector3>();
        private struct AnimationProgress { public int hash; public float time; }
        private readonly Dictionary<uint, AnimationProgress> clientAnimationProgress = new Dictionary<uint, AnimationProgress>();

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
            if (localSpawner == null)
                localSpawner = FindObjectOfType<EnemySpawner>();
        }

        private void OnEnable()
        {
            // Handlers are now registered by MirrorNetworkManager.OnStartClient()
            // to ensure they are available before any messages arrive.
        }

        private void OnDisable()
        {
            // Handlers are now unregistered by MirrorNetworkManager.OnStopClient()
            CleanupMirrorEnemies();
        }

        /// <summary>
        /// Register network message handlers on the client side.
        /// NOTE: This is now called by MirrorNetworkManager as a fallback.
        /// Primary registration happens in MirrorNetworkManager.OnStartClient().
        /// </summary>
        private void RegisterHandlers()
        {
            // Handlers are registered by MirrorNetworkManager.OnStartClient() now.
            // This method is kept for compatibility but does nothing.
            isRegistered = true;
        }

        /// <summary>
        /// Get the network ID of a mirror enemy by its GameObject.
        /// Used by CombatSystem on clients to route damage to the server.
        /// </summary>
        public uint GetMirrorEnemyNetId(GameObject enemyObj)
        {
            if (enemyObj == null) return 0;
            MirrorEnemyTag tag = enemyObj.GetComponent<MirrorEnemyTag>();
            if (tag != null) return tag.enemyNetId;
            return 0;
        }

        private void UnregisterHandlers()
        {
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
            }

            if (NetworkServer.active)
            {
                Debug.Log("[NetworkEnemySpawner] Server started. Enemy sync active on host.");
            }
        }

        private void Update()
        {
            if (!NetworkServer.active)
            {
                float blend = 1f - Mathf.Exp(-20f * Time.deltaTime);
                foreach (var pair in clientTargetPositions)
                    if (clientMirrorEnemies.TryGetValue(pair.Key, out GameObject obj) && obj != null)
                        obj.transform.position = Vector3.Lerp(obj.transform.position, pair.Value, blend);
                return;
            }

            // Server: Periodically scan for new enemies and sync their state
            syncTimer += Time.deltaTime;
            if (syncTimer >= 1f / Mathf.Max(1f, syncRate))
            {
                syncTimer = 0f;
                ScanAndSyncEnemies();
                if (NetworkServer.connections.Count > 1)
                    SendToRemoteClients(CreateWorldSnapshot());
            }
        }

        /// <summary>
        /// Server: Scan the scene for active enemies and sync their state to clients.
        /// </summary>
        private void ScanAndSyncEnemies()
        {
            if (!NetworkServer.active) return;

            // Don't scan/send if there are no remote clients connected

            // Find all active enemies in the scene
            GameObject[] enemyObjects = GameObject.FindGameObjectsWithTag("Enemy");

            // Track which enemies are still alive
            HashSet<uint> aliveEnemyIds = new HashSet<uint>();

            foreach (GameObject enemyObj in enemyObjects)
            {
                if (enemyObj == null || !enemyObj.activeInHierarchy) continue;

                CharacterEntity entity = enemyObj.GetComponent<CharacterEntity>();
                if (entity == null) continue;
                if (entity.RuntimeStats == null || !entity.RuntimeStats.IsAlive) continue;
                if (enemyObj.GetComponent<MirrorEnemyTag>() != null) continue;

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

                    // Check if enemy died
                    if (entity.RuntimeStats != null && !entity.RuntimeStats.IsAlive)
                    {
                        SendToRemoteClients(new EnemyDeathMessage { enemyNetId = existingId });
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
                SendToRemoteClients(new EnemyDeathMessage { enemyNetId = id });
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

            // Notify remote clients to spawn this enemy (skip host's local client)
            float health = entity.RuntimeStats != null ? entity.RuntimeStats.currentHealth : 100f;
            float maxHealth = entity.RuntimeStats != null ? entity.RuntimeStats.maxHealth : 100f;

            var msg = new EnemySpawnMessage
            {
                enemyNetId = id,
                characterDataId = characterDataId,
                position = enemyObj.transform.position,
                health = health,
                maxHealth = maxHealth
            };

            SendToRemoteClients(msg);
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

        /// <summary>
        /// Send a network message to only remote clients (not the host's local client).
        /// This avoids unnecessary message processing on the host.
        /// </summary>
        private void SendToRemoteClients<T>(T msg) where T : struct, NetworkMessage
        {
            foreach (var kvp in NetworkServer.connections)
            {
                NetworkConnectionToClient conn = kvp.Value;
                // Skip the host's local connection (connectionId 0)
                if (conn != null && conn.connectionId != 0 && conn.isReady)
                {
                    conn.Send(msg);
                }
            }
        }

        #region Public Message Handlers (called by MirrorNetworkManager)

        private WorldSnapshotMessage CreateWorldSnapshot()
        {
            var enemies = new List<EnemySnapshot>();
            foreach (EnemyNetData data in serverEnemies.Values)
            {
                if (data.entity == null || !data.gameObject.activeInHierarchy || !data.entity.RuntimeStats.IsAlive)
                    continue;
                var animator = data.gameObject.GetComponent<Animator>();
                bool hasAnimator = animator != null && animator.runtimeAnimatorController != null;
                AnimatorStateInfo state = hasAnimator
                    ? (animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0)) : default;
                var stats = data.entity.RuntimeStats;
                var sprite = data.gameObject.GetComponent<SpriteRenderer>();
                enemies.Add(new EnemySnapshot
                {
                    id = data.netId,
                    characterId = data.entity.characterData.characterId,
                    position = data.gameObject.transform.position,
                    rotation = data.gameObject.transform.rotation,
                    scale = data.gameObject.transform.localScale,
                    facingRight = sprite == null || !sprite.flipX,
                    statsJson = JsonUtility.ToJson(stats),
                    cooldownKeys = stats.skillCooldowns == null ? new int[0] : new List<int>(stats.skillCooldowns.Keys).ToArray(),
                    cooldownValues = stats.skillCooldowns == null ? new float[0] : new List<float>(stats.skillCooldowns.Values).ToArray(),
                    animationHash = state.fullPathHash,
                    animationTime = state.normalizedTime,
                    animationSpeed = hasAnimator ? animator.speed : 1f,
                    animationParameters = CaptureAnimationParameters(animator)
                });
            }
            BossFightManager boss = FindObjectOfType<BossFightManager>();
            return new WorldSnapshotMessage
            {
                scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path,
                enemies = enemies.ToArray(),
                bossFightActive = boss != null && boss.IsBossFightActive,
                bossPortalId = boss != null ? boss.CurrentPortalId : 0
            };
        }

        public void HandleWorldSnapshot(WorldSnapshotMessage msg)
        {
            if (NetworkServer.active || msg.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene().path)
                return;

            // Clearing local simulation never destroys a server replica.
            if (!clientScenePrepared && localSpawner != null)
            {
                localSpawner.PauseSpawning();
                localSpawner.ClearAllEnemies();
                clientScenePrepared = true;
            }
            var alive = new HashSet<uint>();
            foreach (EnemySnapshot enemy in msg.enemies)
            {
                alive.Add(enemy.id);
                if (clientMirrorEnemies.TryGetValue(enemy.id, out GameObject existing) &&
                    (existing == null || existing.GetComponent<CharacterEntity>().characterData.characterId != enemy.characterId))
                {
                    if (existing != null) Destroy(existing);
                    clientMirrorEnemies.Remove(enemy.id);
                }
                CreateMirrorEnemy(enemy.id, enemy.characterId, enemy.position, 1f, 1f);
                if (!clientMirrorEnemies.TryGetValue(enemy.id, out GameObject obj) || obj == null) continue;
                var entity = obj.GetComponent<CharacterEntity>();
                JsonUtility.FromJsonOverwrite(enemy.statsJson, entity.RuntimeStats);
                entity.RuntimeStats.skillCooldowns.Clear();
                for (int i = 0; i < enemy.cooldownKeys.Length; i++)
                    entity.RuntimeStats.skillCooldowns[enemy.cooldownKeys[i]] = enemy.cooldownValues[i];
                clientTargetPositions[enemy.id] = enemy.position;
                if (Vector3.Distance(obj.transform.position, enemy.position) > 3f)
                    obj.transform.position = enemy.position;
                obj.transform.rotation = enemy.rotation;
                obj.transform.localScale = enemy.scale;
                obj.GetComponent<SpriteRenderer>().flipX = !enemy.facingRight;
                var bar = obj.GetComponentInChildren<HealthBar>(true);
                if (bar != null) bar.UpdateHealth(entity.RuntimeStats.currentHealth, entity.RuntimeStats.maxHealth);
                var animator = obj.GetComponent<Animator>();
                if (animator != null && animator.runtimeAnimatorController != null && enemy.animationHash != 0)
                {
                    ApplyAnimationParameters(animator, enemy.animationParameters);
                    animator.speed = enemy.animationSpeed;
                    AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
                    // Let the clip run between snapshots. Replaying every 100ms rewinds
                    // playback and fights transitions, especially during attack/hit clips.
                    bool restarted = clientAnimationProgress.TryGetValue(enemy.id, out AnimationProgress previous) &&
                        previous.hash == enemy.animationHash && enemy.animationTime + 0.05f < previous.time;
                    if (current.fullPathHash != enemy.animationHash || restarted ||
                        Mathf.Abs(current.normalizedTime - enemy.animationTime) * current.length > 0.2f)
                        animator.Play(enemy.animationHash, 0, enemy.animationTime);
                    clientAnimationProgress[enemy.id] = new AnimationProgress { hash = enemy.animationHash, time = enemy.animationTime };
                }
            }
            foreach (uint id in new List<uint>(clientMirrorEnemies.Keys))
            {
                if (alive.Contains(id)) continue;
                Destroy(clientMirrorEnemies[id]);
                clientMirrorEnemies.Remove(id);
                clientTargetPositions.Remove(id);
                clientAnimationProgress.Remove(id);
            }
            var boss = FindObjectOfType<BossFightManager>();
            if (boss != null) boss.ApplyNetworkFightState(msg.bossFightActive, msg.bossPortalId);
        }

        internal static EnemyAnimatorParameter[] CaptureAnimationParameters(Animator animator)
        {
            var result = new List<EnemyAnimatorParameter>();
            if (animator == null || animator.runtimeAnimatorController == null) return result.ToArray();
            foreach (var parameter in animator.parameters)
            {
                var value = new EnemyAnimatorParameter { hash = parameter.nameHash, type = (int)parameter.type };
                switch (parameter.type)
                {
                    case AnimatorControllerParameterType.Bool: value.boolValue = animator.GetBool(parameter.nameHash); break;
                    case AnimatorControllerParameterType.Int: value.intValue = animator.GetInteger(parameter.nameHash); break;
                    case AnimatorControllerParameterType.Float: value.floatValue = animator.GetFloat(parameter.nameHash); break;
                    default: continue;
                }
                result.Add(value);
            }
            return result.ToArray();
        }

        internal static void ApplyAnimationParameters(Animator animator, EnemyAnimatorParameter[] parameters)
        {
            if (parameters == null) return;
            foreach (var parameter in parameters)
            {
                switch ((AnimatorControllerParameterType)parameter.type)
                {
                    case AnimatorControllerParameterType.Bool: animator.SetBool(parameter.hash, parameter.boolValue); break;
                    case AnimatorControllerParameterType.Int: animator.SetInteger(parameter.hash, parameter.intValue); break;
                    case AnimatorControllerParameterType.Float: animator.SetFloat(parameter.hash, parameter.floatValue); break;
                }
            }
        }

        /// <summary>Handle EnemySpawnMessage forwarded from MirrorNetworkManager.</summary>
        public void HandleEnemySpawnMessage(EnemySpawnMessage msg)
        {
            OnClientEnemySpawn(msg);
        }

        /// <summary>Handle EnemyPositionMessage forwarded from MirrorNetworkManager.</summary>
        public void HandleEnemyPositionMessage(EnemyPositionMessage msg)
        {
            OnClientEnemyPosition(msg);
        }

        /// <summary>Handle EnemyDeathMessage forwarded from MirrorNetworkManager.</summary>
        public void HandleEnemyDeathMessage(EnemyDeathMessage msg)
        {
            OnClientEnemyDeath(msg);
        }

        /// <summary>Handle EnemyHitMessage forwarded from MirrorNetworkManager.</summary>
        public void HandleEnemyHitMessage(EnemyHitMessage msg)
        {
            OnClientEnemyHit(msg);
        }

        #endregion

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
                clientTargetPositions[msg.enemyNetId] = msg.position;

                SpriteRenderer sr = mirrorObj.GetComponent<SpriteRenderer>();
                if (sr != null)
                    sr.flipX = !msg.facingRight;

                // Update real-time health from position sync
                CharacterEntity entity = mirrorObj.GetComponent<CharacterEntity>();
                if (entity != null && entity.RuntimeStats != null)
                {
                    entity.RuntimeStats.currentHealth = msg.health;
                    entity.RuntimeStats.maxHealth = msg.maxHealth;

                    // Update the HealthBar UI to reflect the synced health
                    HealthBar healthBar = mirrorObj.GetComponentInChildren<HealthBar>();
                    if (healthBar != null)
                    {
                        healthBar.UpdateHealth(msg.health, msg.maxHealth);
                    }
                }
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
                clientTargetPositions.Remove(msg.enemyNetId);
                clientAnimationProgress.Remove(msg.enemyNetId);
                Debug.Log($"[NetworkEnemySpawner] Mirror enemy #{msg.enemyNetId} died.");
            }
        }

        private void OnClientEnemyHit(EnemyHitMessage msg)
        {
            if (NetworkServer.active) return;

            if (clientMirrorEnemies.TryGetValue(msg.enemyNetId, out GameObject mirrorObj) && mirrorObj != null)
            {
                // Animation state comes exclusively from world snapshots. This message
                // only updates damage feedback; do not start a competing local state.

                // Update health
                CharacterEntity entity = mirrorObj.GetComponent<CharacterEntity>();
                if (entity != null && entity.RuntimeStats != null)
                {
                    entity.RuntimeStats.currentHealth = msg.currentHealth;

                    // Update the HealthBar UI to reflect the synced health
                    HealthBar healthBar = mirrorObj.GetComponentInChildren<HealthBar>();
                    if (healthBar != null)
                    {
                        healthBar.UpdateHealth(msg.currentHealth, entity.RuntimeStats.maxHealth);
                    }
                }

                // Trigger flash effect
                FlashEffect flash = mirrorObj.GetComponent<FlashEffect>();
                if (flash != null)
                {
                    flash.TriggerFlash();
                }
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

            // Set Rigidbody2D to Kinematic on mirror enemies to prevent physics
            // simulation (gravity, collisions) from interfering with network-synced position.
            // MirrorEnemy position is driven entirely by EnemyPositionMessage.
            Rigidbody2D rb = enemyObj.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.simulated = true; // Keep simulated so OverlapCircle queries still detect it
            }

            // Kinematic bodies still push dynamic bodies through solid contacts.
            // Replicas are queryable hit targets only; the host owns collision response.
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            foreach (Collider2D collider in enemyObj.GetComponentsInChildren<Collider2D>(true))
            {
                collider.isTrigger = true;
                if (enemyLayer >= 0) collider.gameObject.layer = enemyLayer;
            }
            foreach (Rigidbody2D body in enemyObj.GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.useFullKinematicContacts = false;
            }

            // Disable AI on mirror enemies - they are driven by network sync
            AIController ai = enemyObj.GetComponent<AIController>();
            if (ai != null) ai.enabled = false;

            // Disable CombatSystem on mirror enemies - host handles combat
            CombatSystem combat = enemyObj.GetComponent<CombatSystem>();
            if (combat != null) combat.enabled = false;
            var events = enemyObj.GetComponent<AnimEventReceiver>();
            if (events != null) events.enabled = false;
            var knockback = enemyObj.GetComponent<KnockbackController>();
            if (knockback != null) knockback.enabled = false;
            // Animator playback is driven by the host, not the local state machine.
            if (charAnim != null) charAnim.enabled = false;
            var visualAnimator = enemyObj.GetComponent<Animator>();
            if (visualAnimator != null) visualAnimator.fireEvents = false;
            entity.enabled = false;

            // Store the network ID on the enemy for damage routing
            MirrorEnemyTag tag = enemyObj.GetComponent<MirrorEnemyTag>();
            if (tag == null) tag = enemyObj.AddComponent<MirrorEnemyTag>();
            tag.enemyNetId = enemyNetId;

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
        /// Clean up all mirror enemies and clear the tracking dictionary.
        /// Called externally by EnemySpawner.ClearAllEnemies() to keep dictionary in sync.
        /// </summary>
        public void CleanupMirrorEnemies()
        {
            clientScenePrepared = false;
            clientTargetPositions.Clear();
            clientAnimationProgress.Clear();
            foreach (var kvp in clientMirrorEnemies)
            {
                if (kvp.Value != null)
                    Destroy(kvp.Value);
            }
            clientMirrorEnemies.Clear();
        }

        #endregion

        #region Server - Damage Routing

        /// <summary>
        /// Server: Apply damage to a tracked enemy by its network ID.
        /// Called from NetworkPlayer.CmdRequestDamageEnemy.
        /// </summary>
        public void ApplyDamageToEnemy(uint enemyNetId, float damage, CharacterEntity attacker)
        {
            if (!NetworkServer.active) return;

            if (serverEnemies.TryGetValue(enemyNetId, out EnemyNetData data))
            {
                if (data.entity != null && data.entity.RuntimeStats != null && data.entity.RuntimeStats.IsAlive)
                {
                    data.entity.TakeDamage(damage, attacker);

                    // Sync hit animation to clients
                    float currentHealth = data.entity.RuntimeStats.currentHealth;
                    SendToRemoteClients(new EnemyHitMessage
                    {
                        enemyNetId = enemyNetId,
                        currentHealth = currentHealth
                    });

                    Debug.Log($"[NetworkEnemySpawner] Server: Enemy #{enemyNetId} took {damage} damage. HP: {currentHealth}");
                }
            }
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
            if (conn == null || conn.connectionId == 0 || !conn.isReady) return;
            ScanAndSyncEnemies();
            conn.Send(CreateWorldSnapshot());

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

        /// <summary>
        /// Called by MirrorNetworkManager.OnServerSceneChanged() after a networked scene change.
        /// Clears old tracking data and re-scans the new scene for enemies.
        /// </summary>
        public void OnSceneChanged()
        {
            Debug.Log("[NetworkEnemySpawner] Scene changed. Clearing old data and re-scanning...");

            // Clear server-side tracking (old enemies from previous scene are gone)
            serverEnemies.Clear();

            // Clear client-side mirror enemies
            CleanupMirrorEnemies();

            // Re-acquire local spawner reference (may be a new instance in the new scene)
            localSpawner = GetComponent<EnemySpawner>();
            if (localSpawner == null)
                localSpawner = FindObjectOfType<EnemySpawner>();

            // If we are a client, disable the local spawner
            if (MirrorNetworkManager.singleton != null && MirrorNetworkManager.singleton.IsClientOnly)
            {
                if (localSpawner != null)
                {
                    localSpawner.enabled = false;
                    Debug.Log("[NetworkEnemySpawner] Client mode: local EnemySpawner disabled after scene change.");
                }
            }
        }

        #endregion
    }
}
