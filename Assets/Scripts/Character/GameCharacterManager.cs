using System.Collections.Generic;
using UnityEngine;
using PetGame.AI;

namespace PetGame
{
    /// <summary>
    /// Scene-level manager that initializes and coordinates the character & enemy system.
    /// Attach to a GameObject in the game scene to bootstrap everything.
    /// </summary>
    public class GameCharacterManager : MonoBehaviour
    {
        /// <summary>
        /// Name of the physics layer used for all character entities.
        /// Must be created in Unity Editor: Edit → Project Settings → Tags and Layers.
        /// </summary>
        private const string CHARACTER_LAYER_NAME = "Character";

        [Header("Player Characters")]
        [Tooltip("CharacterData templates for player characters to spawn at start")]
        public CharacterData[] playerCharacterDataList;

        [Tooltip("Prefab name registered in PoolMgr for player character GameObjects")]
        public string playerPrefabName = "PlayerPrefab";

        [Tooltip("Spawn positions for player characters")]
        public Transform[] playerSpawnPoints;

        [Header("Enemy Spawner")]
        [Tooltip("Reference to the EnemySpawner in the scene")]
        public EnemySpawner enemySpawner;

        [Header("Save System")]
        [Tooltip("Auto-save interval in seconds (0 = disabled)")]
        public float autoSaveInterval = 60f;

        /// <summary>
        /// All active player character entities in the scene.
        /// </summary>
        public List<CharacterEntity> PlayerCharacters { get; private set; } = new List<CharacterEntity>();

        private float saveTimer;

        /// <summary>
        /// Name of the physics layer used for enemy entities.
        /// </summary>
        private const string ENEMY_LAYER_NAME = "Enemy";

        private void Awake()
        {
            // Disable collision between Character layer and Enemy layer
            int characterLayer = LayerMask.NameToLayer(CHARACTER_LAYER_NAME);
            int enemyLayer = LayerMask.NameToLayer(ENEMY_LAYER_NAME);

            if (characterLayer != -1 && enemyLayer != -1)
            {
                Physics2D.IgnoreLayerCollision(characterLayer, enemyLayer, true);
                // Also disable collision between same layers
                Physics2D.IgnoreLayerCollision(characterLayer, characterLayer, true);
                Physics2D.IgnoreLayerCollision(enemyLayer, enemyLayer, true);
            }
            else
            {
                Debug.LogError($"[GameCharacterManager] Physics layer not found! " +
                    $"Character layer: {characterLayer}, Enemy layer: {enemyLayer}. " +
                    "Please ensure both 'Character' and 'Enemy' layers exist in " +
                    "Edit → Project Settings → Tags and Layers.");
            }
        }

        private void Start()
        {
            // Load save data
            //GameSaveData saveData = SaveManager.Instance.LoadGame();
            CharacterData data = playerCharacterDataList[0];
            Vector3 spawnPos = Vector3.zero;
            if (playerSpawnPoints != null && playerSpawnPoints[0] != null)
            {
                spawnPos = playerSpawnPoints[0].position;
            }

            CharacterEntity entity = CreatePlayerCharacter(data, spawnPos);
            PlayerCharacters.Add(entity);
            //SpawnPlayerCharacters(saveData);

            saveTimer = 0f;
        }

        private void Update()
        {
            // Auto-save
            if (autoSaveInterval > 0f)
            {
                saveTimer += Time.deltaTime;
                if (saveTimer >= autoSaveInterval)
                {
                    SaveGame();
                    saveTimer = 0f;
                }
            }
        }

        /// <summary>
        /// Spawn all configured player characters.
        /// </summary>
        private void SpawnPlayerCharacters(GameSaveData saveData)
        {
            if (playerCharacterDataList == null) return;

            for (int i = 0; i < playerCharacterDataList.Length; i++)
            {
                CharacterData data = playerCharacterDataList[i];
                if (data == null) continue;

                // Get spawn position
                Vector3 spawnPos = Vector3.zero;
                if (playerSpawnPoints != null && i < playerSpawnPoints.Length && playerSpawnPoints[i] != null)
                {
                    spawnPos = playerSpawnPoints[i].position;
                }

                // Create player character
                CharacterEntity entity = CreatePlayerCharacter(data, spawnPos);

                // Apply save data if available
                if (saveData != null)
                {
                    SaveManager.Instance.ApplySaveData(entity, saveData);
                }

                PlayerCharacters.Add(entity);
            }
        }

        /// <summary>
        /// Switches the current player character to a new one based on the given CharacterData.
        /// Returns true if the switch was successful, false otherwise.
        /// New character spawns at the configured PlayerSpawn position.
        /// </summary>
        public bool SwitchPlayerCharacter(CharacterData newData)
        {
            if (newData == null)
            {
                Debug.LogError("[GameCharacterManager] Cannot switch character: CharacterData is null!");
                return false;
            }

            // Find the current player character (use the first one in the list, or find by tag)
            CharacterEntity currentEntity = null;
            if (PlayerCharacters.Count > 0)
            {
                currentEntity = PlayerCharacters[0];
            }
            else
            {
                // Try to find by tag as fallback
                GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
                if (playerObj != null)
                    currentEntity = playerObj.GetComponent<CharacterEntity>();
            }

            // Check if the new data is the same as the current character (avoid redundant switch)
            if (currentEntity != null && currentEntity.characterData == newData)
            {
                return false;
            }

            // Destroy or recycle the old character
            if (currentEntity != null)
            {
                PlayerCharacters.Remove(currentEntity);

                // Unsubscribe death event before recycling
                currentEntity.OnDeath -= OnPlayerCharacterDeath;

                // Recycle to pool using the object's name (which matches its prefab key)
                PoolMgr.Instance.PutNode(currentEntity.gameObject);
            }

            // Spawn at PlayerSpawn position
            Vector3 spawnPos = GetPlayerSpawnPosition();

            // Create the new character at the spawn position
            CharacterEntity newEntity = CreatePlayerCharacter(newData, spawnPos);
            PlayerCharacters.Add(newEntity);

            // Apply cultivation bonuses and restore full health
            if (CultivationManager.Instance != null)
            {
                CultivationManager.Instance.ApplyCultivationBonuses(newEntity);
            }
            newEntity.RuntimeStats.currentHealth = newEntity.RuntimeStats.maxHealth;

            return true;
        }

        /// <summary>
        /// Get the PlayerSpawn position. Uses the first spawn point if available, otherwise Vector3.zero.
        /// </summary>
        public Vector3 GetPlayerSpawnPosition()
        {
            if (playerSpawnPoints != null && playerSpawnPoints.Length > 0 && playerSpawnPoints[0] != null)
            {
                return playerSpawnPoints[0].position;
            }
            return Vector3.zero;
        }

        /// <summary>
        /// Create a single player character with all required components.
        /// </summary>
        public CharacterEntity CreatePlayerCharacter(CharacterData data, Vector3 position)
        {
            GameObject playerObj = null;
            string prefabKey = data.GetPrefabName();

            if (!string.IsNullOrEmpty(prefabKey))
            {
                // Use character-specific Variant Prefab (lazy registration)
                if (!PoolMgr.Instance.HasPrefab(prefabKey))
                {
                    PoolMgr.Instance.SetPrefab(prefabKey, data.prefab);
                }
                playerObj = PoolMgr.Instance.GetNode(prefabKey);
            }
            else
            {
                // Fallback: use default PlayerPrefab
                playerObj = PoolMgr.Instance.GetNode(playerPrefabName);
            }

            if (playerObj == null)
            {
                // Fallback: create a new GameObject if pool doesn't have one
                playerObj = new GameObject($"Player_{data.characterName}");
            }

            playerObj.transform.position = position;
            playerObj.SetActive(true);
            playerObj.tag = "Player";

            // Set physics layer so characters don't collide with each other
            int characterLayer = LayerMask.NameToLayer(CHARACTER_LAYER_NAME);
            if (characterLayer != -1)
                playerObj.layer = characterLayer;

            // Ensure SpriteRenderer exists
            SpriteRenderer sr = playerObj.GetComponent<SpriteRenderer>();
            if (sr == null)
                sr = playerObj.AddComponent<SpriteRenderer>();
            if (data.sprite != null)
                sr.sprite = data.sprite;

            // Ensure Animator exists
            Animator animator = playerObj.GetComponent<Animator>();
            if (animator == null)
                animator = playerObj.AddComponent<Animator>();

            // Ensure Collider2D exists
            if (playerObj.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D col = playerObj.AddComponent<BoxCollider2D>();
                col.isTrigger = false;
            }

            // Add CharacterAnimator
            CharacterAnimator charAnim = playerObj.GetComponent<CharacterAnimator>();
            if (charAnim == null)
                charAnim = playerObj.AddComponent<CharacterAnimator>();
            if (data.animatorController != null)
                charAnim.SetAnimatorController(data.animatorController);

            // Add CharacterEntity and initialize
            CharacterEntity entity = playerObj.GetComponent<CharacterEntity>();
            if (entity == null)
                entity = playerObj.AddComponent<CharacterEntity>();
            entity.Initialize(data);

            // Add CombatSystem
            if (playerObj.GetComponent<CombatSystem>() == null)
                playerObj.AddComponent<CombatSystem>();

            // Add AnimEventReceiver so attack animation frame events
            // can trigger damage application and clear IsAttacking state
            if (playerObj.GetComponent<AnimEventReceiver>() == null)
                playerObj.AddComponent<AnimEventReceiver>();

            // Add FlashEffect
            if (playerObj.GetComponent<FlashEffect>() == null)
                playerObj.AddComponent<FlashEffect>();

            // Add ManualController (starts inactive)
            ManualController manual = playerObj.GetComponent<ManualController>();
            if (manual == null)
                manual = playerObj.AddComponent<ManualController>();
            manual.SetActive(false);

            // Add AIController (starts active by default)
            AIController ai = playerObj.GetComponent<AIController>();
            if (ai == null)
                ai = playerObj.AddComponent<AIController>();
            ai.InitializeAI();

            // Add ControlModeManager
            if (playerObj.GetComponent<ControlModeManager>() == null)
                playerObj.AddComponent<ControlModeManager>();

            // Subscribe to death event for cooldown management
            entity.OnDeath += OnPlayerCharacterDeath;

            return entity;
        }

        /// <summary>
        /// Check if we are in multiplayer mode.
        /// Uses Mirror NetworkManager to determine if network is active.
        /// </summary>
        public bool IsMultiplayerMode
        {
            get
            {
                return PetGame.Network.MirrorNetworkManager.singleton != null
                    && PetGame.Network.MirrorNetworkManager.singleton.IsNetworkActive;
            }
        }

        /// <summary>
        /// Save the current game state.
        /// </summary>
        public void SaveGame()
        {
            SaveManager.Instance.SaveGame(PlayerCharacters);
        }

        /// <summary>
        /// Called when the application is about to quit.
        /// </summary>
        private void OnApplicationQuit()
        {
            SaveGame();
        }

        /// <summary>
        /// Called when the application loses focus (e.g., minimized).
        /// </summary>
        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
            {
                SaveGame();
            }
        }

        /// <summary>
        /// Called when a player character dies.
        /// Notifies CharacterDeathManager to start card cooldown and removes the character from the active list.
        /// Does NOT auto-open the card selection UI — the player must do so manually.
        /// </summary>
        private void OnPlayerCharacterDeath(CharacterEntity deadEntity)
        {
            if (deadEntity == null) return;

            // Notify CharacterDeathManager to handle cooldown
            if (CharacterDeathManager.Instance != null)
            {
                CharacterDeathManager.Instance.OnPlayerCharacterDeath(deadEntity);
            }

            // Remove from active player characters list
            PlayerCharacters.Remove(deadEntity);

            Debug.Log($"[GameCharacterManager] Player character '{deadEntity.characterData?.characterName}' died. " +
                $"Remaining active characters: {PlayerCharacters.Count}");
        }
    }
}
