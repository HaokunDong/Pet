using System;
using Unity.Netcode;
using UnityEngine;

namespace PetGame.Network
{
    /// <summary>
    /// Manages lobby lifecycle: creating, joining, leaving rooms.
    /// Coordinates between RelayManager and UI.
    /// </summary>
    public class LobbyManager : MonoBehaviour
    {
        public static LobbyManager Instance { get; private set; }

        /// <summary>
        /// Current lobby state.
        /// </summary>
        public enum LobbyState
        {
            None,
            Creating,
            InLobbyAsHost,
            Joining,
            InLobbyAsClient,
            StartingGame,
            InGame
        }

        /// <summary>
        /// Current state of the lobby.
        /// </summary>
        public LobbyState CurrentState { get; private set; } = LobbyState.None;

        /// <summary>
        /// Current join code for the active lobby.
        /// </summary>
        public string CurrentJoinCode { get; private set; }

        /// <summary>
        /// Number of connected players in the lobby.
        /// </summary>
        public int ConnectedPlayerCount { get; private set; }

        /// <summary>
        /// Maximum players allowed.
        /// </summary>
        public int MaxPlayers => NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.maxPlayers : 2;

        // Events for UI updates
        public event Action<LobbyState> OnStateChanged;
        public event Action<int> OnPlayerCountChanged;
        public event Action<string> OnError;
        public event Action<string> OnLobbyCreated;
        public event Action OnLobbyJoined;
        public event Action OnGameStarting;

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

        private void Start()
        {
            SubscribeToRelayEvents();
        }

        /// <summary>
        /// Subscribe to RelayManager events. Safe to call multiple times.
        /// </summary>
        private void SubscribeToRelayEvents()
        {
            if (RelayManager.Instance != null)
            {
                // Unsubscribe first to avoid double-subscription
                RelayManager.Instance.OnRelayError -= HandleRelayError;
                RelayManager.Instance.OnHostCreated -= HandleHostCreated;
                RelayManager.Instance.OnClientJoined -= HandleClientJoined;

                RelayManager.Instance.OnRelayError += HandleRelayError;
                RelayManager.Instance.OnHostCreated += HandleHostCreated;
                RelayManager.Instance.OnClientJoined += HandleClientJoined;
            }
        }

        /// <summary>
        /// Initialize the network system and prepare for lobby operations.
        /// </summary>
        public void InitializeNetwork()
        {
            if (NetworkBootstrap.Instance != null)
            {
                NetworkBootstrap.Instance.Initialize();
            }
            else
            {
                Debug.LogError("[LobbyManager] NetworkBootstrap not found! Please ensure it exists in the scene.");
            }
        }

        /// <summary>
        /// Create a new lobby as host.
        /// </summary>
        public async void CreateLobby()
        {
            if (CurrentState != LobbyState.None)
            {
                OnError?.Invoke("Already in a lobby session.");
                return;
            }

            // Ensure relay events are subscribed
            SubscribeToRelayEvents();

            if (RelayManager.Instance == null)
            {
                OnError?.Invoke("RelayManager not found. Network system not properly initialized.");
                return;
            }

            SetState(LobbyState.Creating);
            InitializeNetwork();

            string joinCode = await RelayManager.Instance.CreateRelay(MaxPlayers - 1);

            if (joinCode != null)
            {
                CurrentJoinCode = joinCode;
                ConnectedPlayerCount = 1;
                SetState(LobbyState.InLobbyAsHost);
                OnLobbyCreated?.Invoke(joinCode);
                OnPlayerCountChanged?.Invoke(ConnectedPlayerCount);

                // Register network callbacks
                RegisterNetworkCallbacks();
            }
            else
            {
                SetState(LobbyState.None);
                // Error already fired via OnRelayError
            }
        }

        /// <summary>
        /// Join an existing lobby using a join code.
        /// </summary>
        /// <param name="joinCode">The join code to connect to</param>
        public async void JoinLobby(string joinCode)
        {
            if (CurrentState != LobbyState.None)
            {
                OnError?.Invoke("Already in a lobby session.");
                return;
            }

            if (string.IsNullOrWhiteSpace(joinCode))
            {
                OnError?.Invoke("Please enter a lobby code.");
                return;
            }

            // Ensure relay events are subscribed
            SubscribeToRelayEvents();

            if (RelayManager.Instance == null)
            {
                OnError?.Invoke("RelayManager not found. Network system not properly initialized.");
                return;
            }

            SetState(LobbyState.Joining);
            InitializeNetwork();

            bool success = await RelayManager.Instance.JoinRelay(joinCode.Trim().ToUpper());

            if (success)
            {
                CurrentJoinCode = joinCode.Trim().ToUpper();
                SetState(LobbyState.InLobbyAsClient);
                OnLobbyJoined?.Invoke();

                // Register network callbacks
                RegisterNetworkCallbacks();
            }
            else
            {
                SetState(LobbyState.None);
                // Error already fired via OnRelayError
            }
        }

        /// <summary>
        /// Leave the current lobby.
        /// </summary>
        public void LeaveLobby()
        {
            if (CurrentState == LobbyState.None) return;

            // Shutdown network
            RelayManager.Instance?.Cleanup();

            // Unregister callbacks
            UnregisterNetworkCallbacks();

            CurrentJoinCode = null;
            ConnectedPlayerCount = 0;
            SetState(LobbyState.None);

            Debug.Log("[LobbyManager] Left lobby.");
        }

        /// <summary>
        /// Copy the current join code to clipboard.
        /// </summary>
        public void CopyJoinCodeToClipboard()
        {
            if (!string.IsNullOrEmpty(CurrentJoinCode))
            {
                GUIUtility.systemCopyBuffer = CurrentJoinCode;
                Debug.Log($"[LobbyManager] Join code copied to clipboard: {CurrentJoinCode}");
            }
        }

        /// <summary>
        /// Start the multiplayer game (called when enough players are connected).
        /// </summary>
        private void StartMultiplayerGame()
        {
            if (CurrentState != LobbyState.InLobbyAsHost) return;

            SetState(LobbyState.StartingGame);
            OnGameStarting?.Invoke();

            // Load the game scene via NetworkManager
            NetworkManager.Singleton.SceneManager.LoadScene("GameScene", UnityEngine.SceneManagement.LoadSceneMode.Single);

            SetState(LobbyState.InGame);
            Debug.Log("[LobbyManager] Multiplayer game started!");
        }

        private void RegisterNetworkCallbacks()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback += OnClientConnected;
                nm.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        private void UnregisterNetworkCallbacks()
        {
            var nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        private void OnClientConnected(ulong clientId)
        {
            if (!NetworkManager.Singleton.IsHost) return;

            ConnectedPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
            OnPlayerCountChanged?.Invoke(ConnectedPlayerCount);

            Debug.Log($"[LobbyManager] Client connected. Total players: {ConnectedPlayerCount}");

            // Auto-start game when lobby is full
            if (ConnectedPlayerCount >= MaxPlayers)
            {
                // Small delay to ensure everything is synced
                Invoke(nameof(StartMultiplayerGame), 1f);
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (CurrentState == LobbyState.None) return;

            if (NetworkManager.Singleton == null) return;

            if (NetworkManager.Singleton.IsHost)
            {
                // Host: a client disconnected
                ConnectedPlayerCount = NetworkManager.Singleton.ConnectedClientsIds.Count;
                OnPlayerCountChanged?.Invoke(ConnectedPlayerCount);
                Debug.Log($"[LobbyManager] Client disconnected. Total players: {ConnectedPlayerCount}");
            }
            else
            {
                // Client: we got disconnected from host
                Debug.Log("[LobbyManager] Disconnected from host.");
                OnError?.Invoke("Disconnected from host.");
                LeaveLobby();
            }
        }

        private void HandleRelayError(string error)
        {
            OnError?.Invoke(error);
        }

        private void HandleHostCreated(string joinCode)
        {
            Debug.Log($"[LobbyManager] Host created with code: {joinCode}");
        }

        private void HandleClientJoined()
        {
            Debug.Log("[LobbyManager] Successfully joined lobby as client.");
        }

        private void SetState(LobbyState newState)
        {
            CurrentState = newState;
            OnStateChanged?.Invoke(newState);
        }

        private void OnDestroy()
        {
            if (RelayManager.Instance != null)
            {
                RelayManager.Instance.OnRelayError -= HandleRelayError;
                RelayManager.Instance.OnHostCreated -= HandleHostCreated;
                RelayManager.Instance.OnClientJoined -= HandleClientJoined;
            }

            UnregisterNetworkCallbacks();

            if (Instance == this)
                Instance = null;
        }
    }
}
