using UnityEngine;
using UnityEngine.SceneManagement;
using Mirror;
using Mirror.FizzySteam;
using Steamworks;
using System;
using System.Collections.Generic;

namespace PetGame.Network
{
    /// <summary>
    /// Custom Mirror NetworkManager that integrates with Steam via FizzySteamworks transport.
    /// Manages the Mirror networking lifecycle (Host/Client start/stop).
    /// </summary>
    public class MirrorNetworkManager : NetworkManager
    {
        #region Singleton

        public static new MirrorNetworkManager singleton { get; private set; }

        #endregion

        #region Events

        /// <summary>Fired when a client connects to the server.</summary>
        public event Action<NetworkConnectionToClient> OnClientConnectedEvent;

        /// <summary>Fired when a client disconnects from the server.</summary>
        public event Action<NetworkConnectionToClient> OnClientDisconnectedEvent;

        /// <summary>Fired when this client connects to a server.</summary>
        public event Action OnConnectedToServer;

        /// <summary>Fired when this client disconnects from a server.</summary>
        public event Action OnDisconnectedFromServer;

        /// <summary>Fired on all clients (and host) after a networked scene change completes.
        /// Parameter: the new scene name.</summary>
        public event Action<string> OnNetworkSceneChanged;

        #endregion

        #region Properties

        /// <summary>Whether the network is currently active (as host or client).</summary>
        public bool IsNetworkActive => NetworkServer.active || NetworkClient.active;

        /// <summary>Whether we are running as host.</summary>
        public bool IsHostActive => NetworkServer.active && NetworkClient.active;

        /// <summary>Whether we are running as client only.</summary>
        public bool IsClientOnly => !NetworkServer.active && NetworkClient.active;

        /// <summary>All connected NetworkPlayer instances (server-side tracking).</summary>
        public List<NetworkPlayer> ConnectedPlayers { get; private set; } = new List<NetworkPlayer>();

        /// <summary>The local player's NetworkPlayer instance.</summary>
        public NetworkPlayer LocalPlayer { get; set; }

        /// <summary>The current game scene name tracked by the server.</summary>
        public string CurrentGameScene { get; private set; } = "Game";

        /// <summary>Whether a networked scene change is currently in progress.</summary>
        public bool IsChangingScene { get; private set; }
        private bool switchingToSteamClient;
        private bool leavingRoom;
        private bool applicationQuitting;
        private Coroutine singlePlayerRecovery;

        #endregion

        public override void Awake()
        {
            // Ensure singleton
            if (singleton != null && singleton != this)
            {
                Destroy(gameObject);
                return;
            }
            singleton = this;
            DontDestroyOnLoad(gameObject);

            // Disable auto create player - we handle it manually
            autoCreatePlayer = false;

            // Ensure FizzySteamworks transport is set
            EnsureTransport();

            // Ensure we have a player prefab
            EnsurePlayerPrefab();

            // Ensure maxConnections supports 4 players at runtime
            maxConnections = SteamLobbyManager.MAX_PLAYERS;
            Debug.Log($"[MirrorNetworkManager] maxConnections set to {maxConnections}.");

            base.Awake();
        }

        public override void Start()
        {
            base.Start();

            // Auto-start as Host in single player mode so that all NetworkBehaviour
            // objects (e.g. BossFightManager) are activated by Mirror.
            // Only start if not already in a lobby (multiplayer handles its own startup).
            var lobbyMgr = SteamLobbyManager.Instance;
            bool inLobby = lobbyMgr != null && lobbyMgr.InLobby;

            if (!inLobby && !IsNetworkActive)
            {
                Debug.Log("[MirrorNetworkManager] Single player mode detected. Auto-starting Host...");
                StartHost();
            }
        }

        public override void OnDestroy()
        {
            if (singleton == this)
                singleton = null;
            base.OnDestroy();
        }

        #region Transport & Prefab Setup

        /// <summary>
        /// Ensures FizzySteamworks transport component exists and is assigned.
        /// </summary>
        private void EnsureTransport()
        {
            FizzySteamworks fizzyTransport = GetComponent<FizzySteamworks>();
            if (fizzyTransport == null)
            {
                fizzyTransport = gameObject.AddComponent<FizzySteamworks>();
                Debug.Log("[MirrorNetworkManager] Added FizzySteamworks transport component.");
            }

            transport = fizzyTransport;
            Transport.active = fizzyTransport;
        }

        /// <summary>
        /// Ensures a NetworkPlayer prefab is assigned as the player prefab.
        /// If none is assigned, creates one at runtime.
        /// </summary>
        private void EnsurePlayerPrefab()
        {
            if (playerPrefab != null) return;

            // Try to load from Resources
            GameObject prefab = Resources.Load<GameObject>("Prefabs/Network/NetworkPlayerPrefab");
            if (prefab == null)
            {
                // Fallback: try without "Prefab" suffix
                prefab = Resources.Load<GameObject>("Prefabs/Network/NetworkPlayer");
            }

            if (prefab != null)
            {
                playerPrefab = prefab;
                Debug.Log("[MirrorNetworkManager] Loaded NetworkPlayer prefab from Resources.");
            }
            else
            {
                Debug.LogWarning("[MirrorNetworkManager] NetworkPlayer prefab not found in Resources. " +
                    "Please create it at Resources/Prefabs/Network/NetworkPlayerPrefab.");
            }
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Start as Host (server + client). Called after lobby creation succeeds.
        /// If a single-player Host is already running (auto-started in Start()),
        /// we keep it alive — it already uses FizzySteamworks transport and can
        /// accept incoming Steam connections.
        /// </summary>
        public void StartHostWithSteam()
        {
            if (singlePlayerRecovery != null)
            {
                StopCoroutine(singlePlayerRecovery);
                singlePlayerRecovery = null;
            }
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[MirrorNetworkManager] Cannot start host: Steam not initialized.");
                return;
            }

            // Always ensure maxConnections is correct for multiplayer
            maxConnections = SteamLobbyManager.MAX_PLAYERS;

            if (IsNetworkActive)
            {
                // Single-player Host was auto-started in Start().
                // Verify the transport layer is correctly configured for remote connections.
                var fizzy = GetComponent<Mirror.FizzySteam.FizzySteamworks>();
                if (fizzy == null || Transport.active != fizzy)
                {
                    // Transport is misconfigured — restart the host with correct transport
                    Debug.LogWarning("[MirrorNetworkManager] Transport misconfigured on existing host. Restarting...");
                    StopHost();
                    EnsureTransport();
                    StartHost();
                }
                else
                {
                    Debug.Log($"[MirrorNetworkManager] Host already active (single-player auto-start). " +
                        $"Reusing for multiplayer. maxConnections={maxConnections}, transport={Transport.active?.GetType().Name}");
                }
                return;
            }

            Debug.Log($"[MirrorNetworkManager] Starting host with Steam... maxConnections={maxConnections}");
            StartHost();
        }

        /// <summary>
        /// Start as Client and connect to the specified host Steam ID.
        /// If a single-player Host is already running (auto-started in Start()),
        /// it is stopped first so we can reconnect as a client to the remote host.
        /// </summary>
        public void StartClientWithSteam(CSteamID hostSteamId)
        {
            if (singlePlayerRecovery != null)
            {
                StopCoroutine(singlePlayerRecovery);
                singlePlayerRecovery = null;
            }
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[MirrorNetworkManager] Cannot start client: Steam not initialized.");
                return;
            }

            // If a single-player Host was auto-started, stop it first.
            // We need to switch from Host mode to Client-only mode to connect
            // to the remote host.
            if (IsNetworkActive)
            {
                Debug.Log("[MirrorNetworkManager] Stopping existing network (single-player host) before connecting as client...");
                switchingToSteamClient = true;
                try
                {
                    StopNetwork();
                }
                finally
                {
                    switchingToSteamClient = false;
                }
            }

            // Set the network address to the host's Steam ID
            networkAddress = hostSteamId.m_SteamID.ToString();

            Debug.Log($"[MirrorNetworkManager] Starting client, connecting to host: {hostSteamId}");
            StartClient();
        }

        /// <summary>
        /// Change the scene for all connected clients (server-side only).
        /// Uses Mirror's built-in ServerChangeScene which handles:
        /// - Notifying all clients to load the new scene
        /// - Pausing message processing during scene load
        /// - Re-spawning player objects in the new scene
        /// </summary>
        /// <param name="sceneName">The scene to load (must be in Build Settings).</param>
        public void ChangeSceneForAll(string sceneName)
        {
            if (!NetworkServer.active)
            {
                Debug.LogWarning("[MirrorNetworkManager] ChangeSceneForAll can only be called on the server/host.");
                return;
            }

            if (IsChangingScene)
            {
                Debug.LogWarning($"[MirrorNetworkManager] Scene change already in progress. Ignoring request for '{sceneName}'.");
                return;
            }

            Debug.Log($"[MirrorNetworkManager] Changing scene for all clients to: {sceneName}");
            IsChangingScene = true;
            CurrentGameScene = sceneName;
            ServerChangeScene(sceneName);
        }

        /// <summary>
        /// Stop all network activity and clean up.
        /// </summary>
        public void StopNetwork()
        {
            if (IsHostActive)
            {
                StopHost();
            }
            else if (IsClientOnly)
            {
                StopClient();
            }
            else if (NetworkServer.active)
            {
                StopServer();
            }

            Debug.Log("[MirrorNetworkManager] Network stopped.");
        }

        public void LeaveRoomAndResumeSinglePlayer()
        {
            if (leavingRoom || applicationQuitting) return;
            leavingRoom = true;
            try
            {
                SteamLobbyManager.Instance.LeaveLobby();
                StopNetwork();
                QueueSinglePlayerRecovery();
            }
            finally { leavingRoom = false; }
        }

        private void QueueSinglePlayerRecovery()
        {
            if (singlePlayerRecovery == null && !applicationQuitting && !switchingToSteamClient)
                singlePlayerRecovery = StartCoroutine(RecoverSinglePlayer());
        }

        private System.Collections.IEnumerator RecoverSinglePlayer()
        {
            // Mirror invokes disconnect callbacks before shutting its client down.
            yield return null;
            while (IsNetworkActive || loadingSceneAsync != null) yield return null;
            singlePlayerRecovery = null;
            if (applicationQuitting || SteamLobbyManager.Instance.InLobby) yield break;

            CleanupClientState();
            foreach (BossFightManager boss in FindObjectsOfType<BossFightManager>(true))
                boss.ResetAfterLeavingRoom();
            foreach (PortalController portal in FindObjectsOfType<PortalController>())
                Destroy(portal.gameObject);
            foreach (EnemySpawner spawner in FindObjectsOfType<EnemySpawner>(true))
            {
                spawner.ClearAllEnemies();
                spawner.enabled = true;
                spawner.ResumeSpawning();
            }
            StartHost();
        }

        public void DisconnectSteamMember(CSteamID steamId)
        {
            if (!NetworkServer.active) return;
            string address = steamId.m_SteamID.ToString();
            foreach (var conn in new List<NetworkConnectionToClient>(NetworkServer.connections.Values))
                if (conn != null && conn.connectionId != 0 && conn.address == address)
                    conn.Disconnect();
        }

        public void RemoveDepartedLobbyConnections(SteamLobbyManager lobby)
        {
            if (!NetworkServer.active || !lobby.InLobby) return;
            foreach (var conn in new List<NetworkConnectionToClient>(NetworkServer.connections.Values))
                if (conn != null && conn.connectionId != 0 && ulong.TryParse(conn.address, out ulong id) &&
                    !lobby.IsLobbyMember(new CSteamID(id)))
                    conn.Disconnect();
        }

        public override void OnApplicationQuit()
        {
            applicationQuitting = true;
            SteamLobbyManager.Instance.LeaveLobby();
            base.OnApplicationQuit();
        }

        /// <summary>
        /// Clean up all client-side network state: mirror characters, mirror enemies, etc.
        /// Called when disconnecting from the server or stopping the client.
        /// </summary>
        private void CleanupClientState()
        {
            // Destroy all MirrorCharacters (remote player representations)
            MirrorCharacterTag[] mirrorChars = FindObjectsOfType<MirrorCharacterTag>();
            foreach (var mc in mirrorChars)
            {
                if (mc != null && mc.gameObject != null)
                {
                    Destroy(mc.gameObject);
                }
            }

            // Clean up mirror enemies via NetworkEnemySpawner
            NetworkEnemySpawner enemySpawner = FindObjectOfType<NetworkEnemySpawner>();
            if (enemySpawner != null)
            {
                enemySpawner.CleanupMirrorEnemies();
            }

            Debug.Log("[MirrorNetworkManager] Client state cleaned up (mirror characters, mirror enemies).");
        }

        #endregion

        #region Mirror Callbacks

        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"[MirrorNetworkManager] Client connected to server: {conn.connectionId}");
            OnClientConnectedEvent?.Invoke(conn);
        }

        /// <summary>
        /// Called on the server when a client requests to add a player.
        /// We manually instantiate the NetworkPlayer prefab and spawn it.
        /// </summary>
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // Instantiate the NetworkPlayer prefab
            GameObject playerObj = Instantiate(playerPrefab);
            playerObj.name = $"NetworkPlayer [connId={conn.connectionId}]";

            // Spawn it on the network with the connection's authority
            NetworkServer.AddPlayerForConnection(conn, playerObj);

            // Track the player
            NetworkPlayer netPlayer = playerObj.GetComponent<NetworkPlayer>();
            if (netPlayer != null)
            {
                ConnectedPlayers.Add(netPlayer);
                Debug.Log($"[MirrorNetworkManager] NetworkPlayer spawned for connection: {conn.connectionId}. " +
                    $"Total players: {ConnectedPlayers.Count}/{maxConnections}");
            }

            // === Late Joiner Support ===

            // 1. Sync existing enemies to the new client
            NetworkEnemySpawner enemySpawner = FindObjectOfType<NetworkEnemySpawner>();
            if (enemySpawner != null)
            {
                enemySpawner.SyncAllEnemiesToNewClient(conn);
            }

            // 2. Sync Boss fight state if a fight is in progress
            BossFightManager bossMgr = FindObjectOfType<BossFightManager>();
            if (bossMgr != null && bossMgr.IsBossFightActive)
            {
                bossMgr.SyncBossFightToNewClient(conn);
            }
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            // Remove from tracked players
            NetworkPlayer disconnectedPlayer = null;
            foreach (var player in ConnectedPlayers)
            {
                if (player != null && player.connectionToClient == conn)
                {
                    disconnectedPlayer = player;
                    break;
                }
            }

            if (disconnectedPlayer != null)
            {
                ConnectedPlayers.Remove(disconnectedPlayer);
                Debug.Log($"[MirrorNetworkManager] NetworkPlayer removed for connection: {conn.connectionId}");
            }

            Debug.Log($"[MirrorNetworkManager] Client disconnected from server: {conn.connectionId}");
            OnClientDisconnectedEvent?.Invoke(conn);
            base.OnServerDisconnect(conn);
        }

        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log("[MirrorNetworkManager] Connected to server.");

            // Request the server to add our player object
            if (!clientLoadedScene && NetworkClient.localPlayer == null)
                NetworkClient.AddPlayer();

            OnConnectedToServer?.Invoke();
        }

        public override void OnClientDisconnect()
        {
            Debug.Log("[MirrorNetworkManager] Disconnected from server.");

            // Clean up all mirror characters and mirror enemies on this client
            CleanupClientState();

            // Joining a remote host deliberately stops the local single-player host.
            if (!switchingToSteamClient && !applicationQuitting)
            {
                SteamLobbyManager.Instance.LeaveLobby();
                QueueSinglePlayerRecovery();
                if (!leavingRoom) OnDisconnectedFromServer?.Invoke();
            }
            base.OnClientDisconnect();
        }

        public override void OnStartHost()
        {
            base.OnStartHost();
            Debug.Log("[MirrorNetworkManager] Host started.");
        }

        public override void OnStopHost()
        {
            base.OnStopHost();
            Debug.Log("[MirrorNetworkManager] Host stopped.");
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // Register custom message handlers BEFORE any messages can arrive.
            // This must happen here (not in Update) because the server may send
            // custom messages (like EnemySpawnMessage) immediately after AddPlayer,
            // which arrives before NetworkEnemySpawner.Update() runs.
            RegisterCustomClientHandlers();

            Debug.Log("[MirrorNetworkManager] Client started.");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            UnregisterCustomClientHandlers();

            // Clean up client-side network state
            CleanupClientState();

            LocalPlayer = null;
            Debug.Log("[MirrorNetworkManager] Client stopped.");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ConnectedPlayers.Clear();

            // Reset NetworkEnemySpawner state
            NetworkEnemySpawner enemySpawner = FindObjectOfType<NetworkEnemySpawner>();
            if (enemySpawner != null)
            {
                enemySpawner.OnSceneChanged();
            }

            IsChangingScene = false;
            Debug.Log("[MirrorNetworkManager] Server stopped. Player list and state cleared.");
        }

        /// <summary>
        /// Called on the server after a scene change completes.
        /// Re-initializes network objects in the new scene.
        /// </summary>
        public override void OnServerSceneChanged(string sceneName)
        {
            base.OnServerSceneChanged(sceneName);
            IsChangingScene = false;
            CurrentGameScene = sceneName;
            Debug.Log($"[MirrorNetworkManager] Server scene changed to: {sceneName}");

            // Re-scan and sync enemies in the new scene for all clients
            NetworkEnemySpawner enemySpawner = FindObjectOfType<NetworkEnemySpawner>();
            if (enemySpawner != null)
            {
                enemySpawner.OnSceneChanged();
            }

            OnNetworkSceneChanged?.Invoke(sceneName);
        }

        /// <summary>
        /// Called on clients (including host-client) after a scene change completes.
        /// Re-registers local character and restores state.
        /// </summary>
        public override void OnClientSceneChanged()
        {
            base.OnClientSceneChanged();
            if (NetworkClient.ready && NetworkClient.localPlayer == null)
                NetworkClient.AddPlayer();
            IsChangingScene = false;

            string sceneName = SceneManager.GetActiveScene().name;
            Debug.Log($"[MirrorNetworkManager] Client scene changed to: {sceneName}");

            // Re-register local character and recreate mirror characters after scene change.
            // Find all NetworkPlayer instances in the scene (they survive scene changes
            // because they are spawned by Mirror and marked DontDestroyOnLoad).
            NetworkPlayer[] allPlayers = FindObjectsOfType<NetworkPlayer>();
            foreach (var player in allPlayers)
            {
                player.OnSceneChanged();
            }

            OnNetworkSceneChanged?.Invoke(sceneName);
        }

        #endregion

        #region Custom Message Handler Registration

        /// <summary>
        /// Register all custom network message handlers on the client.
        /// This MUST be called in OnStartClient (before any messages arrive)
        /// because the server may send custom messages immediately after spawning
        /// the player object (e.g., EnemySpawnMessage in SyncAllEnemiesToNewClient).
        /// If we wait for NetworkEnemySpawner.Update(), the messages arrive first
        /// and cause "Unknown message id" disconnection.
        /// </summary>
        private void RegisterCustomClientHandlers()
        {
            // Register EnemySpawnMessage, EnemyPositionMessage, EnemyDeathMessage
            // These are handled by NetworkEnemySpawner, but must be registered
            // before any messages arrive. We use forwarding handlers that delegate
            // to NetworkEnemySpawner when it's available.
            NetworkClient.RegisterHandler<EnemySpawnMessage>(OnEnemySpawnMessageReceived);
            NetworkClient.RegisterHandler<EnemyPositionMessage>(OnEnemyPositionMessageReceived);
            NetworkClient.RegisterHandler<EnemyDeathMessage>(OnEnemyDeathMessageReceived);
            NetworkClient.RegisterHandler<EnemyHitMessage>(OnEnemyHitMessageReceived);
            NetworkClient.RegisterHandler<WorldSnapshotMessage>(OnWorldSnapshotReceived);
            Debug.Log("[MirrorNetworkManager] Custom client message handlers registered.");
        }

        private void UnregisterCustomClientHandlers()
        {
            if (NetworkClient.active)
            {
                NetworkClient.UnregisterHandler<EnemySpawnMessage>();
                NetworkClient.UnregisterHandler<EnemyPositionMessage>();
                NetworkClient.UnregisterHandler<EnemyDeathMessage>();
                NetworkClient.UnregisterHandler<EnemyHitMessage>();
                NetworkClient.UnregisterHandler<WorldSnapshotMessage>();
            }
        }

        // Forwarding handlers - these get called by Mirror and forward to NetworkEnemySpawner
        private void OnWorldSnapshotReceived(WorldSnapshotMessage msg)
        {
            NetworkEnemySpawner spawner = FindObjectOfType<NetworkEnemySpawner>();
            if (spawner != null) spawner.HandleWorldSnapshot(msg);
        }

        private void OnEnemySpawnMessageReceived(EnemySpawnMessage msg)
        {
            NetworkEnemySpawner spawner = FindObjectOfType<NetworkEnemySpawner>();
            if (spawner != null)
            {
                spawner.HandleEnemySpawnMessage(msg);
            }
        }

        private void OnEnemyPositionMessageReceived(EnemyPositionMessage msg)
        {
            NetworkEnemySpawner spawner = FindObjectOfType<NetworkEnemySpawner>();
            if (spawner != null)
            {
                spawner.HandleEnemyPositionMessage(msg);
            }
        }

        private void OnEnemyDeathMessageReceived(EnemyDeathMessage msg)
        {
            NetworkEnemySpawner spawner = FindObjectOfType<NetworkEnemySpawner>();
            if (spawner != null)
            {
                spawner.HandleEnemyDeathMessage(msg);
            }
        }

        private void OnEnemyHitMessageReceived(EnemyHitMessage msg)
        {
            NetworkEnemySpawner spawner = FindObjectOfType<NetworkEnemySpawner>();
            if (spawner != null)
            {
                spawner.HandleEnemyHitMessage(msg);
            }
        }

        #endregion
    }
}
