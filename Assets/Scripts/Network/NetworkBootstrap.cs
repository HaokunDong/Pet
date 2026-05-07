using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Bootstraps the NetworkManager with Unity Transport configuration.
    /// Only initializes when multiplayer flow is triggered, ensuring single-player mode is unaffected.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        [Header("Network Settings")]
        [Tooltip("Maximum number of players in a lobby")]
        public int maxPlayers = 2;

        [Header("Network Player Prefab")]
        [Tooltip("Path to the network player prefab in Resources folder")]
        public string networkPlayerPrefabPath = "Prefabs/Network/NetworkPlayerPrefab";

        private NetworkManager networkManager;
        private UnityTransport transport;
        private GameObject networkPlayerPrefab;

        /// <summary>
        /// Whether the network system has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Whether we are currently in a multiplayer session.
        /// </summary>
        public bool IsMultiplayerActive => networkManager != null && networkManager.IsListening;

        /// <summary>
        /// Whether this client is the host.
        /// </summary>
        public bool IsHost => networkManager != null && networkManager.IsHost;

        /// <summary>
        /// Whether this client is a connected client (not host).
        /// </summary>
        public bool IsClient => networkManager != null && networkManager.IsClient && !networkManager.IsHost;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Initialize the network system. Called when player enters multiplayer flow.
        /// </summary>
        public void Initialize()
        {
            if (IsInitialized) return;

            // Get or add NetworkManager
            networkManager = GetComponent<NetworkManager>();
            if (networkManager == null)
                networkManager = gameObject.AddComponent<NetworkManager>();

            // Get or add UnityTransport
            transport = GetComponent<UnityTransport>();
            if (transport == null)
                transport = gameObject.AddComponent<UnityTransport>();

            // Ensure NetworkConfig exists before setting transport
            if (networkManager.NetworkConfig == null)
                networkManager.NetworkConfig = new NetworkConfig();

            // Set transport on NetworkManager
            networkManager.NetworkConfig.NetworkTransport = transport;

            // Load and register the network player prefab
            RegisterNetworkPlayerPrefab();

            IsInitialized = true;
            Debug.Log("[NetworkBootstrap] Network system initialized.");
        }

        /// <summary>
        /// Load the network player prefab from Resources and register it with NetworkManager.
        /// </summary>
        private void RegisterNetworkPlayerPrefab()
        {
            // Load the network player prefab
            networkPlayerPrefab = Resources.Load<GameObject>(networkPlayerPrefabPath);
            if (networkPlayerPrefab == null)
            {
                Debug.LogError($"[NetworkBootstrap] Network player prefab not found at: Resources/{networkPlayerPrefabPath}. " +
                    "Please create a prefab with NetworkObject component at this path. " +
                    "Use menu: Tools/PetGame/Create Network Player Prefab");
                return;
            }

            // Verify it has NetworkObject
            if (networkPlayerPrefab.GetComponent<NetworkObject>() == null)
            {
                Debug.LogError("[NetworkBootstrap] Network player prefab is missing NetworkObject component!");
                return;
            }

            // Set as player prefab (auto-spawn on connect)
            networkManager.NetworkConfig.PlayerPrefab = networkPlayerPrefab;

            // Register the prefab with NetworkManager using the proper API
            networkManager.AddNetworkPrefab(networkPlayerPrefab);

            Debug.Log($"[NetworkBootstrap] Network player prefab registered: {networkPlayerPrefab.name}");
        }

        /// <summary>
        /// Get the NetworkManager instance.
        /// </summary>
        public NetworkManager GetNetworkManager()
        {
            return networkManager;
        }

        /// <summary>
        /// Get the UnityTransport instance.
        /// </summary>
        public UnityTransport GetTransport()
        {
            return transport;
        }

        /// <summary>
        /// Start as Host.
        /// </summary>
        public bool StartHost()
        {
            if (!IsInitialized)
            {
                Debug.LogError("[NetworkBootstrap] Cannot start host: not initialized!");
                return false;
            }
            return networkManager.StartHost();
        }

        /// <summary>
        /// Start as Client.
        /// </summary>
        public bool StartClient()
        {
            if (!IsInitialized)
            {
                Debug.LogError("[NetworkBootstrap] Cannot start client: not initialized!");
                return false;
            }
            return networkManager.StartClient();
        }

        /// <summary>
        /// Shutdown the network session.
        /// </summary>
        public void Shutdown()
        {
            if (networkManager != null && networkManager.IsListening)
            {
                networkManager.Shutdown();
                Debug.Log("[NetworkBootstrap] Network session shut down.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
