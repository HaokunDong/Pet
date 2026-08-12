using UnityEngine;
using Mirror;

namespace PetGame.Network
{
    /// <summary>
    /// Network-aware enemy spawner that runs only on the server (host).
    /// Wraps the existing EnemySpawner logic and ensures enemies are spawned
    /// across the network so all clients can see and interact with them.
    /// Attach this alongside EnemySpawner on the same GameObject.
    /// </summary>
    public class NetworkEnemySpawner : NetworkBehaviour
    {
        [Header("References")]
        [Tooltip("Reference to the local EnemySpawner component")]
        [SerializeField] private EnemySpawner localSpawner;

        /// <summary>
        /// Whether this spawner is active in multiplayer mode.
        /// Only the host/server should spawn enemies.
        /// </summary>
        public bool IsActiveInMultiplayer => isServer;

        private void Awake()
        {
            if (localSpawner == null)
                localSpawner = GetComponent<EnemySpawner>();
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("[NetworkEnemySpawner] Server started. Enemy spawning is active on host.");
            // The local EnemySpawner handles spawning logic.
            // In multiplayer, enemies are spawned locally on the host and
            // their state is synced via NetworkCharacterSync (if needed in future).
            // For now, since the scene is "host's scene", enemies exist on host
            // and clients see them through standard Mirror object sync.
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // If we are a client (not host), disable the local spawner
            // because the host manages all enemy spawning
            if (!isServer && localSpawner != null)
            {
                localSpawner.enabled = false;
                Debug.Log("[NetworkEnemySpawner] Client mode: local EnemySpawner disabled (host manages spawning).");
            }
        }

        /// <summary>
        /// Spawn a networked enemy that all clients can see.
        /// Called by the server only.
        /// </summary>
        [Server]
        public void SpawnNetworkedEnemy(CharacterData data, Vector3 position)
        {
            if (data == null) return;

            // Create enemy object
            GameObject enemyObj = null;
            if (data.prefab != null)
            {
                enemyObj = Instantiate(data.prefab, position, Quaternion.identity);
            }
            else
            {
                enemyObj = new GameObject($"NetEnemy_{data.characterName}");
                enemyObj.transform.position = position;
            }

            enemyObj.tag = "Enemy";

            // Add NetworkIdentity
            if (enemyObj.GetComponent<NetworkIdentity>() == null)
                enemyObj.AddComponent<NetworkIdentity>();

            // Add NetworkCharacterSync for state sync
            if (enemyObj.GetComponent<NetworkCharacterSync>() == null)
                enemyObj.AddComponent<NetworkCharacterSync>();

            // Initialize character components
            CharacterEntity entity = enemyObj.GetComponent<CharacterEntity>();
            if (entity == null)
                entity = enemyObj.AddComponent<CharacterEntity>();
            entity.Initialize(data);

            // Spawn on network (no owner - server authoritative)
            NetworkServer.Spawn(enemyObj);

            Debug.Log($"[NetworkEnemySpawner] Spawned networked enemy: {data.characterName}");
        }
    }
}
