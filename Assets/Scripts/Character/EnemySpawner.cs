using UnityEngine;
using PetGame.AI;

namespace PetGame
{
    /// <summary>
    /// Spawns and manages enemies in the scene.
    /// Uses PoolMgr for object pooling and CharacterData templates for configuration.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Configuration")]
        [Tooltip("Enemy data templates to spawn")]
        public CharacterData[] enemyDataList;

        [Tooltip("Prefab name registered in PoolMgr for enemy GameObjects")]
        public string enemyPrefabName = "EnemyPrefab";

        [Tooltip("Spawn area center")]
        public Transform spawnCenter;

        [Tooltip("Horizontal spawn range from center")]
        public float spawnRange = 8f;

        [Tooltip("Time interval between spawns (seconds)")]
        public float spawnInterval = 5f;

        [Tooltip("Maximum number of enemies alive at once")]
        public int maxEnemies = 5;

        [Header("AI Settings")]
        [Tooltip("Detection range for enemy AI")]
        public float aiDetectionRange = 10f;

        [Tooltip("Patrol range for enemy AI")]
        public float aiPatrolRange = 3f;

        private float spawnTimer;
        private int currentEnemyCount;

        private void Start()
        {
            spawnTimer = 0f;
            currentEnemyCount = 0;

            if (spawnCenter == null)
                spawnCenter = transform;
        }

        private void Update()
        {
            spawnTimer += Time.deltaTime;

            if (spawnTimer >= spawnInterval && currentEnemyCount < maxEnemies)
            {
                SpawnEnemy();
                spawnTimer = 0f;
            }
        }

        /// <summary>
        /// Spawn a single enemy using a random template from the list.
        /// </summary>
        public void SpawnEnemy()
        {
            if (enemyDataList == null || enemyDataList.Length == 0) return;
            if (currentEnemyCount >= maxEnemies) return;

            // Pick a random enemy template
            CharacterData data = enemyDataList[Random.Range(0, enemyDataList.Length)];

            // Get or create enemy GameObject from pool
            GameObject enemyObj = PoolMgr.Instance.GetNode(enemyPrefabName);
            if (enemyObj == null)
            {
                Logger.Log("[EnemySpawner] Failed to get enemy from pool: " + enemyPrefabName);
                return;
            }

            // Position randomly within spawn range
            float randomX = spawnCenter.position.x + Random.Range(-spawnRange, spawnRange);
            enemyObj.transform.position = new Vector3(randomX, spawnCenter.position.y, 0f);
            enemyObj.SetActive(true);

            // Set tag for AI targeting
            enemyObj.tag = "Enemy";

            // Initialize CharacterEntity
            CharacterEntity entity = enemyObj.GetComponent<CharacterEntity>();
            if (entity == null)
                entity = enemyObj.AddComponent<CharacterEntity>();
            entity.Initialize(data);

            // Ensure required components exist
            if (enemyObj.GetComponent<CharacterAnimator>() == null)
                enemyObj.AddComponent<CharacterAnimator>();

            if (enemyObj.GetComponent<CombatSystem>() == null)
                enemyObj.AddComponent<CombatSystem>();

            if (enemyObj.GetComponent<Collider2D>() == null)
            {
                BoxCollider2D col = enemyObj.AddComponent<BoxCollider2D>();
                col.isTrigger = false;
            }

            // Setup animator controller from data
            CharacterAnimator charAnim = enemyObj.GetComponent<CharacterAnimator>();
            if (data.animatorController != null)
                charAnim.SetAnimatorController(data.animatorController);

            // Setup AI controller
            AIController aiController = enemyObj.GetComponent<AIController>();
            if (aiController == null)
                aiController = enemyObj.AddComponent<AIController>();

            aiController.detectionRange = aiDetectionRange;
            aiController.patrolRange = aiPatrolRange;
            aiController.InitializeAI();

            // Listen for death to update count
            entity.OnDeath += OnEnemyDeath;

            currentEnemyCount++;
        }

        /// <summary>
        /// Spawn a specific enemy by data template.
        /// </summary>
        public void SpawnSpecificEnemy(CharacterData data, Vector3 position)
        {
            if (data == null) return;

            GameObject enemyObj = PoolMgr.Instance.GetNode(enemyPrefabName);
            if (enemyObj == null) return;

            enemyObj.transform.position = position;
            enemyObj.SetActive(true);
            enemyObj.tag = "Enemy";

            CharacterEntity entity = enemyObj.GetComponent<CharacterEntity>();
            if (entity == null)
                entity = enemyObj.AddComponent<CharacterEntity>();
            entity.Initialize(data);

            if (enemyObj.GetComponent<CharacterAnimator>() == null)
                enemyObj.AddComponent<CharacterAnimator>();

            if (enemyObj.GetComponent<CombatSystem>() == null)
                enemyObj.AddComponent<CombatSystem>();

            if (enemyObj.GetComponent<Collider2D>() == null)
                enemyObj.AddComponent<BoxCollider2D>();

            CharacterAnimator charAnim = enemyObj.GetComponent<CharacterAnimator>();
            if (data.animatorController != null)
                charAnim.SetAnimatorController(data.animatorController);

            AIController aiController = enemyObj.GetComponent<AIController>();
            if (aiController == null)
                aiController = enemyObj.AddComponent<AIController>();

            aiController.detectionRange = aiDetectionRange;
            aiController.patrolRange = aiPatrolRange;
            aiController.InitializeAI();

            entity.OnDeath += OnEnemyDeath;
            currentEnemyCount++;
        }

        private void OnEnemyDeath(CharacterEntity deadEnemy)
        {
            deadEnemy.OnDeath -= OnEnemyDeath;
            currentEnemyCount--;
            if (currentEnemyCount < 0) currentEnemyCount = 0;
        }
    }
}
