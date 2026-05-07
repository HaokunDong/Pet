using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

namespace PetGame.Network
{
    /// <summary>
    /// Monitors network connection status and displays UI indicators.
    /// Handles disconnection events and provides visual feedback for network quality.
    /// </summary>
    public class NetworkStatusUI : MonoBehaviour
    {
        [Header("UI References")]
        [Tooltip("Icon displayed when network latency is high")]
        [SerializeField] private GameObject lagIndicator;

        [Tooltip("Panel displayed when disconnected")]
        [SerializeField] private GameObject disconnectPanel;

        [Tooltip("Text showing disconnect reason")]
        [SerializeField] private TextMeshProUGUI disconnectReasonText;

        [Tooltip("Button to return to main menu after disconnect")]
        [SerializeField] private Button returnToMenuButton;

        [Header("Settings")]
        [Tooltip("RTT threshold (ms) above which the lag indicator is shown")]
        public float lagThresholdMs = 200f;

        [Tooltip("How often to check network quality (seconds)")]
        public float checkInterval = 2f;

        private float checkTimer;
        private bool isMonitoring;

        private void Awake()
        {
            if (lagIndicator != null) lagIndicator.SetActive(false);
            if (disconnectPanel != null) disconnectPanel.SetActive(false);

            if (returnToMenuButton != null)
                returnToMenuButton.onClick.AddListener(OnReturnToMenuClicked);
        }

        private void OnEnable()
        {
            // Subscribe to network events
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnect;
                isMonitoring = true;
            }
        }

        private void OnDisable()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnect;
            }
            isMonitoring = false;
        }

        private void Update()
        {
            if (!isMonitoring) return;
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening) return;

            checkTimer += Time.deltaTime;
            if (checkTimer >= checkInterval)
            {
                checkTimer = 0f;
                CheckNetworkQuality();
            }
        }

        /// <summary>
        /// Check network quality and update lag indicator.
        /// </summary>
        private void CheckNetworkQuality()
        {
            if (NetworkManager.Singleton == null) return;

            // For clients, check RTT to server
            if (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost)
            {
                var transport = NetworkManager.Singleton.NetworkConfig.NetworkTransport;
                if (transport != null)
                {
                    // Unity Transport provides RTT via NetworkManager
                    float rtt = NetworkManager.Singleton.NetworkConfig.NetworkTransport.GetCurrentRtt(
                        NetworkManager.ServerClientId);

                    bool isLagging = rtt > lagThresholdMs;
                    if (lagIndicator != null)
                        lagIndicator.SetActive(isLagging);
                }
            }
        }

        /// <summary>
        /// Handle client disconnection.
        /// </summary>
        private void OnClientDisconnect(ulong clientId)
        {
            // If we are the one who got disconnected (client side)
            if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsHost)
            {
                // We got disconnected from the host
                if (clientId == NetworkManager.Singleton.LocalClientId)
                {
                    ShowDisconnectPanel("Connection to host lost.");
                }
            }
            else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost)
            {
                // Host: a client disconnected
                // Remove the disconnected player's character
                RemoveDisconnectedPlayerCharacter(clientId);
            }
        }

        /// <summary>
        /// Remove the character belonging to a disconnected player.
        /// </summary>
        private void RemoveDisconnectedPlayerCharacter(ulong clientId)
        {
            // Find and destroy NetworkObjects owned by the disconnected client
            var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjectsList;
            foreach (var netObj in spawnedObjects)
            {
                if (netObj.OwnerClientId == clientId && netObj.CompareTag("Player"))
                {
                    netObj.Despawn(true);
                    Debug.Log($"[NetworkStatusUI] Removed player character for disconnected client {clientId}");
                    break;
                }
            }
        }

        /// <summary>
        /// Show the disconnect panel with a reason message.
        /// </summary>
        private void ShowDisconnectPanel(string reason)
        {
            if (disconnectPanel != null)
                disconnectPanel.SetActive(true);
            if (disconnectReasonText != null)
                disconnectReasonText.text = reason;
            if (lagIndicator != null)
                lagIndicator.SetActive(false);

            Debug.Log($"[NetworkStatusUI] Disconnected: {reason}");
        }

        /// <summary>
        /// Return to main menu after disconnection.
        /// </summary>
        private void OnReturnToMenuClicked()
        {
            // Cleanup network
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.LeaveLobby();

            // Load main scene
            SceneManager.LoadScene("MainScene");
        }
    }
}
