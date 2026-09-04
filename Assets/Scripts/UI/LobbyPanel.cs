using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetGame.Network;
using Steamworks;

namespace PetGame.UI
{
    /// <summary>
    /// UI controller for the multiplayer lobby panel.
    /// Uses Mirror + Steamworks for networking.
    /// Manages display states: initial view, in-lobby view.
    /// </summary>
    public class LobbyPanel : MonoBehaviour
    {
        [Header("Initial State UI")]
        [SerializeField] private GameObject initialPanel;
        [SerializeField] private Button createLobbyButton;
        [SerializeField] private TMP_InputField joinCodeInput;
        [SerializeField] private Button joinButton;
        [SerializeField] private Button closeButton;

        [Header("In-Lobby State UI")]
        [SerializeField] private GameObject lobbyPanel;
        [SerializeField] private TextMeshProUGUI lobbyCodeText;
        [SerializeField] private Button copyCodeButton;
        [SerializeField] private Button inviteFriendsButton;
        [SerializeField] private TextMeshProUGUI playerCountText;
        [SerializeField] private Button leaveLobbyButton;
        [SerializeField] private TextMeshProUGUI statusText;

        [Header("Error Display")]
        [SerializeField] private TextMeshProUGUI errorText;
        [SerializeField] private float errorDisplayDuration = 3f;

        private float errorTimer;

        private void Awake()
        {
            // Bind button events
            if (createLobbyButton != null)
                createLobbyButton.onClick.AddListener(OnCreateLobbyClicked);
            if (joinButton != null)
                joinButton.onClick.AddListener(OnJoinClicked);
            if (closeButton != null)
                closeButton.onClick.AddListener(OnCloseClicked);
            if (copyCodeButton != null)
                copyCodeButton.onClick.AddListener(OnCopyCodeClicked);
            if (inviteFriendsButton != null)
                inviteFriendsButton.onClick.AddListener(OnInviteFriendsClicked);
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);

            // Subscribe to network events here (before SetActive(false)) so that
            // events are never lost when the panel is hidden via OnDisable.
            SubscribeToEvents();

            // Start hidden
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Only restore UI state when the panel becomes visible.
            // Event subscriptions are handled in Awake/OnDestroy to avoid
            // losing events when the panel is hidden.
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null && lobbyMgr.InLobby)
            {
                ShowLobbyState(lobbyMgr.CurrentLobbyCode, lobbyMgr.IsHost);
                HandlePlayerCountChanged(lobbyMgr.PlayerCount);
            }
            else
            {
                ShowInitialState();
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        /// <summary>
        /// Subscribe to all network events. Called once in Awake so that
        /// subscriptions persist regardless of the panel's active state.
        /// </summary>
        private void SubscribeToEvents()
        {
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null)
            {
                lobbyMgr.OnLobbyCreated += HandleLobbyCreated;
                lobbyMgr.OnLobbyEntered += HandleLobbyEntered;
                lobbyMgr.OnLobbyJoinFailed += HandleLobbyJoinFailed;
                lobbyMgr.OnPlayerCountChanged += HandlePlayerCountChanged;
                lobbyMgr.OnHostDisconnected += HandleHostDisconnected;
                lobbyMgr.OnError += HandleError;
            }

            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.OnDisconnectedFromServer += HandleDisconnectedFromServer;
            }
        }

        /// <summary>
        /// Unsubscribe from all network events. Called in OnDestroy.
        /// </summary>
        private void UnsubscribeFromEvents()
        {
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null)
            {
                lobbyMgr.OnLobbyCreated -= HandleLobbyCreated;
                lobbyMgr.OnLobbyEntered -= HandleLobbyEntered;
                lobbyMgr.OnLobbyJoinFailed -= HandleLobbyJoinFailed;
                lobbyMgr.OnPlayerCountChanged -= HandlePlayerCountChanged;
                lobbyMgr.OnHostDisconnected -= HandleHostDisconnected;
                lobbyMgr.OnError -= HandleError;
            }

            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.OnDisconnectedFromServer -= HandleDisconnectedFromServer;
            }
        }

        private void Update()
        {
            // Close panel on Escape key
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                OnCloseClicked();
                return;
            }

            // Auto-hide error text after duration
            if (errorText != null && errorText.gameObject.activeSelf)
            {
                errorTimer -= Time.deltaTime;
                if (errorTimer <= 0f)
                {
                    errorText.gameObject.SetActive(false);
                }
            }
        }

        #region Public Methods

        /// <summary>
        /// Show the lobby panel.
        /// </summary>
        public void Show()
        {
            gameObject.SetActive(true);
        }

        /// <summary>
        /// Hide the lobby panel.
        /// </summary>
        public void Hide()
        {
            gameObject.SetActive(false);
        }

        #endregion

        #region UI State Management

        private void ShowInitialState()
        {
            if (initialPanel != null) initialPanel.SetActive(true);
            if (lobbyPanel != null) lobbyPanel.SetActive(false);
            if (errorText != null) errorText.gameObject.SetActive(false);
            if (statusText != null) statusText.text = "";

            // Clear input
            if (joinCodeInput != null) joinCodeInput.text = "";

            // Enable buttons
            SetButtonsInteractable(true);
        }

        private void ShowLobbyState(string lobbyCode, bool isHost)
        {
            if (initialPanel != null) initialPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);

            if (lobbyCodeText != null)
                lobbyCodeText.text = lobbyCode;

            if (statusText != null)
                statusText.text = isHost ? "Waiting for player..." : "Connected!";

            // Show invite button only for host
            if (inviteFriendsButton != null)
                inviteFriendsButton.gameObject.SetActive(true);
        }

        private void SetButtonsInteractable(bool interactable)
        {
            if (createLobbyButton != null) createLobbyButton.interactable = interactable;
            if (joinButton != null) joinButton.interactable = interactable;
        }

        private void ShowError(string message)
        {
            if (errorText != null)
            {
                errorText.text = message;
                errorText.gameObject.SetActive(true);
                errorTimer = errorDisplayDuration;
            }
            Debug.LogWarning($"[LobbyPanel] Error: {message}");
        }

        #endregion

        #region Button Handlers

        private void OnCreateLobbyClicked()
        {
            if (!SteamManager.Initialized)
            {
                ShowError("Steam is not running. Please start Steam first.");
                return;
            }

            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr == null)
            {
                ShowError("Network system not available.");
                return;
            }

            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = "Creating room...";
            lobbyMgr.CreateLobby();
        }

        private void OnJoinClicked()
        {
            if (!SteamManager.Initialized)
            {
                ShowError("Steam is not running. Please start Steam first.");
                return;
            }

            string code = joinCodeInput != null ? joinCodeInput.text.Trim() : "";
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowError("Please enter a room code.");
                return;
            }

            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr == null)
            {
                ShowError("Network system not available.");
                return;
            }

            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = "Joining room...";
            lobbyMgr.JoinLobby(code);
        }

        private void OnCloseClicked()
        {
            // Just hide the panel without leaving the lobby
            Hide();
        }

        private void OnCopyCodeClicked()
        {
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null)
            {
                lobbyMgr.CopyLobbyCodeToClipboard();
                if (statusText != null) statusText.text = "Code copied!";
                Invoke(nameof(ResetStatusText), 1.5f);
            }
        }

        private void OnInviteFriendsClicked()
        {
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null)
            {
                lobbyMgr.InviteFriends();
                if (statusText != null) statusText.text = "Invite sent!";
                Invoke(nameof(ResetStatusText), 1.5f);
            }
        }

        private void OnLeaveLobbyClicked()
        {
            // Stop Mirror network
            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.StopNetwork();
            }

            // Leave Steam lobby
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null)
            {
                lobbyMgr.LeaveLobby();
            }

            ShowInitialState();
        }

        private void ResetStatusText()
        {
            if (statusText == null) return;

            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null && lobbyMgr.InLobby)
            {
                statusText.text = lobbyMgr.IsHost ? "Waiting for player..." : "Connected!";
            }
        }

        #endregion

        #region Event Handlers

        private void HandleLobbyCreated(string lobbyCode)
        {
            // Start Mirror host
            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.StartHostWithSteam();
            }

            ShowLobbyState(lobbyCode, true);
        }

        private void HandleLobbyEntered()
        {
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr == null) return;

            // If we are not the host, start Mirror client
            if (!lobbyMgr.IsHost)
            {
                CSteamID hostId = lobbyMgr.GetHostSteamId();
                if (MirrorNetworkManager.singleton != null && hostId.IsValid())
                {
                    MirrorNetworkManager.singleton.StartClientWithSteam(hostId);
                }
            }

            ShowLobbyState(lobbyMgr.CurrentLobbyCode, lobbyMgr.IsHost);
        }

        private void HandleLobbyJoinFailed(string error)
        {
            ShowError(error);
            SetButtonsInteractable(true);
            if (statusText != null) statusText.text = "";
        }

        private void HandlePlayerCountChanged(int count)
        {
            if (playerCountText != null)
            {
                playerCountText.text = $"Players: {count}/{SteamLobbyManager.MAX_PLAYERS}";
            }
        }

        private void HandleHostDisconnected()
        {
            ShowError("Host disconnected. Returning to lobby.");

            // Stop Mirror network
            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.StopNetwork();
            }

            ShowInitialState();
        }

        private void HandleError(string error)
        {
            ShowError(error);
            SetButtonsInteractable(true);
        }

        private void HandleDisconnectedFromServer()
        {
            // If we were a client and got disconnected, return to initial state
            var lobbyMgr = SteamLobbyManager.Instance;
            if (lobbyMgr != null && !lobbyMgr.IsHost)
            {
                lobbyMgr.LeaveLobby();
                ShowError("Disconnected from host.");
                ShowInitialState();
            }
        }

        #endregion
    }
}
