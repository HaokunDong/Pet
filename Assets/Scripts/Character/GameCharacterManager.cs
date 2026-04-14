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

        private void Start()
        {
            // Load save data
            GameSaveData saveData = SaveManager.Instance.LoadGame();

            // Spawn player characters
            SpawnPlayerCharacters(saveData);

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
        /// Create a single player character with all required components.
        /// </summary>
        private CharacterEntity CreatePlayerCharacter(CharacterData data, Vector3 position)
        {
            GameObject playerObj = PoolMgr.Instance.GetNode(playerPrefabName);
            if (playerObj == null)
            {
                // Fallback: create a new GameObject if pool doesn't have one
                playerObj = new GameObject($"Player_{data.characterName}");
            }

            playerObj.transform.position = position;
            playerObj.SetActive(true);
            playerObj.tag = "Player";

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

            return entity;
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
    }
}
