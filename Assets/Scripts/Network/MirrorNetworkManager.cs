using UnityEngine;
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

            base.Awake();
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
        /// </summary>
        public void StartHostWithSteam()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[MirrorNetworkManager] Cannot start host: Steam not initialized.");
                return;
            }

            if (IsNetworkActive)
            {
                Debug.LogWarning("[MirrorNetworkManager] Network already active. Stop first.");
                return;
            }

            Debug.Log("[MirrorNetworkManager] Starting host with Steam...");
            StartHost();
        }

        /// <summary>
        /// Start as Client and connect to the specified host Steam ID.
        /// </summary>
        public void StartClientWithSteam(CSteamID hostSteamId)
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogError("[MirrorNetworkManager] Cannot start client: Steam not initialized.");
                return;
            }

            if (IsNetworkActive)
            {
                Debug.LogWarning("[MirrorNetworkManager] Network already active. Stop first.");
                return;
            }

            // Set the network address to the host's Steam ID
            networkAddress = hostSteamId.m_SteamID.ToString();

            Debug.Log($"[MirrorNetworkManager] Starting client, connecting to host: {hostSteamId}");
            StartClient();
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
                Debug.Log($"[MirrorNetworkManager] NetworkPlayer spawned for connection: {conn.connectionId}");
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
            NetworkClient.AddPlayer();

            OnConnectedToServer?.Invoke();
        }

        public override void OnClientDisconnect()
        {
            Debug.Log("[MirrorNetworkManager] Disconnected from server.");
            OnDisconnectedFromServer?.Invoke();
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
            Debug.Log("[MirrorNetworkManager] Client started.");
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            LocalPlayer = null;
            Debug.Log("[MirrorNetworkManager] Client stopped.");
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            ConnectedPlayers.Clear();
            Debug.Log("[MirrorNetworkManager] Server stopped. Player list cleared.");
        }

        #endregion
    }
}
