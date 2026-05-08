using UnityEngine;
using UnityEngine.UI;
using TMPro;
using PetGame.Network;

namespace PetGame.UI
{
    /// <summary>
    /// UI controller for the multiplayer lobby panel.
    /// Manages display states: initial view, host view, client view.
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
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);

            // Start hidden
            gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            // Ensure network managers exist
            EnsureNetworkManagersExist();

            // Subscribe to LobbyManager events
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnStateChanged += HandleStateChanged;
                LobbyManager.Instance.OnPlayerCountChanged += HandlePlayerCountChanged;
                LobbyManager.Instance.OnError += HandleError;
                LobbyManager.Instance.OnLobbyCreated += HandleLobbyCreated;
                LobbyManager.Instance.OnLobbyJoined += HandleLobbyJoined;
                LobbyManager.Instance.OnGameStarting += HandleGameStarting;
            }

            // Reset to initial state
            ShowInitialState();
        }

        /// <summary>
        /// Ensures all required network manager singletons exist in the scene.
        /// Creates them if they don't exist.
        /// </summary>
        private void EnsureNetworkManagersExist()
        {
            // Ensure NetworkBootstrap exists
            if (NetworkBootstrap.Instance == null)
            {
                GameObject bootstrapObj = new GameObject("[NetworkBootstrap]");
                bootstrapObj.AddComponent<NetworkBootstrap>();
                Debug.Log("[LobbyPanel] Created NetworkBootstrap instance.");
            }

            // Ensure RelayManager exists
            if (RelayManager.Instance == null)
            {
                GameObject relayObj = new GameObject("[RelayManager]");
                relayObj.AddComponent<RelayManager>();
                Debug.Log("[LobbyPanel] Created RelayManager instance.");
            }

            // Ensure LobbyManager exists
            if (LobbyManager.Instance == null)
            {
                GameObject lobbyObj = new GameObject("[LobbyManager]");
                lobbyObj.AddComponent<LobbyManager>();
                Debug.Log("[LobbyPanel] Created LobbyManager instance.");
            }
        }

        private void OnDisable()
        {
            // Unsubscribe from LobbyManager events
            if (LobbyManager.Instance != null)
            {
                LobbyManager.Instance.OnStateChanged -= HandleStateChanged;
                LobbyManager.Instance.OnPlayerCountChanged -= HandlePlayerCountChanged;
                LobbyManager.Instance.OnError -= HandleError;
                LobbyManager.Instance.OnLobbyCreated -= HandleLobbyCreated;
                LobbyManager.Instance.OnLobbyJoined -= HandleLobbyJoined;
                LobbyManager.Instance.OnGameStarting -= HandleGameStarting;
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

        private void ShowLobbyState(string joinCode, bool isHost)
        {
            if (initialPanel != null) initialPanel.SetActive(false);
            if (lobbyPanel != null) lobbyPanel.SetActive(true);

            if (lobbyCodeText != null)
                lobbyCodeText.text = joinCode;

            if (statusText != null)
                statusText.text = isHost ? "Waiting for player..." : "Connected!";
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
        }

        // --- Button Handlers ---

        private void OnCreateLobbyClicked()
        {
            if (LobbyManager.Instance == null)
            {
                EnsureNetworkManagersExist();
            }

            if (LobbyManager.Instance == null)
            {
                ShowError("Network system not available.");
                return;
            }

            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = "Creating lobby...";
            LobbyManager.Instance.CreateLobby();
        }

        private void OnJoinClicked()
        {
            string code = joinCodeInput != null ? joinCodeInput.text : "";
            if (string.IsNullOrWhiteSpace(code))
            {
                ShowError("Please enter a lobby code.");
                return;
            }

            if (LobbyManager.Instance == null)
            {
                EnsureNetworkManagersExist();
            }

            if (LobbyManager.Instance == null)
            {
                ShowError("Network system not available.");
                return;
            }

            SetButtonsInteractable(false);
            if (statusText != null) statusText.text = "Joining lobby...";
            LobbyManager.Instance.JoinLobby(code);
        }

        private void OnCloseClicked()
        {
            // If in lobby, leave first
            if (LobbyManager.Instance != null &&
                LobbyManager.Instance.CurrentState != LobbyManager.LobbyState.None)
            {
                LobbyManager.Instance.LeaveLobby();
            }
            Hide();
        }

        private void OnCopyCodeClicked()
        {
            LobbyManager.Instance?.CopyJoinCodeToClipboard();
            if (statusText != null) statusText.text = "Code copied!";

            // Reset status text after a moment
            Invoke(nameof(ResetStatusText), 1.5f);
        }

        private void OnLeaveLobbyClicked()
        {
            LobbyManager.Instance?.LeaveLobby();
            ShowInitialState();
        }

        private void ResetStatusText()
        {
            if (statusText != null && LobbyManager.Instance != null)
            {
                if (LobbyManager.Instance.CurrentState == LobbyManager.LobbyState.InLobbyAsHost)
                    statusText.text = "Waiting for player...";
                else if (LobbyManager.Instance.CurrentState == LobbyManager.LobbyState.InLobbyAsClient)
                    statusText.text = "Connected!";
            }
        }

        // --- Event Handlers ---

        private void HandleStateChanged(LobbyManager.LobbyState state)
        {
            switch (state)
            {
                case LobbyManager.LobbyState.None:
                    ShowInitialState();
                    break;
                case LobbyManager.LobbyState.Creating:
                    if (statusText != null) statusText.text = "Creating lobby...";
                    break;
                case LobbyManager.LobbyState.Joining:
                    if (statusText != null) statusText.text = "Joining lobby...";
                    break;
                case LobbyManager.LobbyState.StartingGame:
                    if (statusText != null) statusText.text = "Starting game...";
                    break;
            }
        }

        private void HandlePlayerCountChanged(int count)
        {
            if (playerCountText != null)
            {
                int max = LobbyManager.Instance != null ? LobbyManager.Instance.MaxPlayers : 2;
                playerCountText.text = $"Lobby ({count}/{max})";
            }
        }

        private void HandleError(string error)
        {
            ShowError(error);
            SetButtonsInteractable(true);
        }

        private void HandleLobbyCreated(string joinCode)
        {
            ShowLobbyState(joinCode, true);
        }

        private void HandleLobbyJoined()
        {
            string code = LobbyManager.Instance != null ? LobbyManager.Instance.CurrentJoinCode : "";
            ShowLobbyState(code, false);
        }

        private void HandleGameStarting()
        {
            if (statusText != null) statusText.text = "Game starting...";
            Hide();
        }
    }
}
