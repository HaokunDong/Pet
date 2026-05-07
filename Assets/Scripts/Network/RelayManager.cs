using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using UnityEngine;

// Resolve ambiguity between Unity.Services.Relay and Unity.Services.Multiplayer
using Allocation = Unity.Services.Relay.Models.Allocation;
using JoinAllocation = Unity.Services.Relay.Models.JoinAllocation;
using RelayService = Unity.Services.Relay.RelayService;
using RelayServiceException = Unity.Services.Relay.RelayServiceException;

namespace PetGame.Network
{
    /// <summary>
    /// Manages Unity Relay service for NAT traversal.
    /// Handles creating allocations (host) and joining via join codes (client).
    /// </summary>
    public class RelayManager : MonoBehaviour
    {
        public static RelayManager Instance { get; private set; }

        /// <summary>
        /// The current join code for the active relay session.
        /// </summary>
        public string JoinCode { get; private set; }

        /// <summary>
        /// Whether Unity Services have been initialized.
        /// </summary>
        public bool IsServicesInitialized { get; private set; }

        /// <summary>
        /// Event fired when relay operations encounter an error.
        /// </summary>
        public event Action<string> OnRelayError;

        /// <summary>
        /// Event fired when host allocation is successful.
        /// </summary>
        public event Action<string> OnHostCreated;

        /// <summary>
        /// Event fired when client successfully joins.
        /// </summary>
        public event Action OnClientJoined;

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
        /// Initialize Unity Gaming Services (Authentication + Relay).
        /// Must be called before any relay operations.
        /// </summary>
        public async Task InitializeServices()
        {
            if (IsServicesInitialized) return;

            try
            {
                await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"[RelayManager] Signed in anonymously. Player ID: {AuthenticationService.Instance.PlayerId}");
                }

                IsServicesInitialized = true;
                Debug.Log("[RelayManager] Unity Services initialized successfully.");
            }
            catch (Exception e)
            {
                string error = $"Failed to initialize Unity Services: {e.Message}";
                Debug.LogError($"[RelayManager] {error}");
                OnRelayError?.Invoke(error);
            }
        }

        /// <summary>
        /// Create a Relay allocation and start as Host.
        /// </summary>
        /// <param name="maxConnections">Maximum number of client connections (excluding host)</param>
        /// <returns>The join code if successful, null otherwise</returns>
        public async Task<string> CreateRelay(int maxConnections = 1)
        {
            if (!IsServicesInitialized)
            {
                await InitializeServices();
                if (!IsServicesInitialized) return null;
            }

            try
            {
                // Create allocation
                Allocation allocation = await RelayService.Instance.CreateAllocationAsync(maxConnections);

                // Get join code
                JoinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

                Debug.Log($"[RelayManager] Relay allocation created. Join Code: {JoinCode}");

                // Configure transport with relay server data
                if (NetworkBootstrap.Instance == null)
                {
                    string error = "NetworkBootstrap instance not found.";
                    Debug.LogError($"[RelayManager] {error}");
                    OnRelayError?.Invoke(error);
                    return null;
                }

                UnityTransport transport = NetworkBootstrap.Instance.GetTransport();
                if (transport == null)
                {
                    string error = "UnityTransport not initialized. Call NetworkBootstrap.Initialize() first.";
                    Debug.LogError($"[RelayManager] {error}");
                    OnRelayError?.Invoke(error);
                    return null;
                }

                transport.SetRelayServerData(
                    allocation.RelayServer.IpV4,
                    (ushort)allocation.RelayServer.Port,
                    allocation.AllocationIdBytes,
                    allocation.Key,
                    allocation.ConnectionData
                );

                // Start as host
                bool started = NetworkBootstrap.Instance.StartHost();
                if (started)
                {
                    OnHostCreated?.Invoke(JoinCode);
                    return JoinCode;
                }
                else
                {
                    string error = "Failed to start host after relay allocation.";
                    Debug.LogError($"[RelayManager] {error}");
                    OnRelayError?.Invoke(error);
                    return null;
                }
            }
            catch (Unity.Services.Relay.RelayServiceException e)
            {
                string error = $"Relay service error: {e.Message}";
                Debug.LogError($"[RelayManager] {error}");
                OnRelayError?.Invoke(error);
                return null;
            }
            catch (Exception e)
            {
                string error = $"Unexpected error creating relay: {e.Message}";
                Debug.LogError($"[RelayManager] {error}");
                OnRelayError?.Invoke(error);
                return null;
            }
        }

        /// <summary>
        /// Join an existing Relay allocation using a join code and start as Client.
        /// </summary>
        /// <param name="joinCode">The join code provided by the host</param>
        /// <returns>True if successfully joined</returns>
        public async Task<bool> JoinRelay(string joinCode)
        {
            if (!IsServicesInitialized)
            {
                await InitializeServices();
                if (!IsServicesInitialized) return false;
            }

            if (string.IsNullOrEmpty(joinCode))
            {
                string error = "Join code cannot be empty.";
                Debug.LogError($"[RelayManager] {error}");
                OnRelayError?.Invoke(error);
                return false;
            }

            try
            {
                // Join allocation
                JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

                Debug.Log($"[RelayManager] Joined relay with code: {joinCode}");

                // Configure transport with relay server data
                if (NetworkBootstrap.Instance == null)
                {
                    string error = "NetworkBootstrap instance not found.";
                    Debug.LogError($"[RelayManager] {error}");
                    OnRelayError?.Invoke(error);
                    return false;
                }

                UnityTransport transport = NetworkBootstrap.Instance.GetTransport();
                if (transport == null)
                {
                    string error = "UnityTransport not initialized. Call NetworkBootstrap.Initialize() first.";
                    Debug.LogError($"[RelayManager] {error}");
                    OnRelayError?.Invoke(error);
                    return false;
                }

                transport.SetRelayServerData(
                    joinAllocation.RelayServer.IpV4,
                    (ushort)joinAllocation.RelayServer.Port,
                    joinAllocation.AllocationIdBytes,
                    joinAllocation.Key,
                    joinAllocation.ConnectionData,
                    joinAllocation.HostConnectionData
                );

                // Start as client
                bool started = NetworkBootstrap.Instance.StartClient();
                if (started)
                {
                    JoinCode = joinCode;
                    OnClientJoined?.Invoke();
                    return true;
                }
                else
                {
                    string error = "Failed to start client after joining relay.";
                    Debug.LogError($"[RelayManager] {error}");
                    OnRelayError?.Invoke(error);
                    return false;
                }
            }
            catch (Unity.Services.Relay.RelayServiceException e)
            {
                string error = $"Relay join error: {e.Message}";
                Debug.LogError($"[RelayManager] {error}");
                OnRelayError?.Invoke(error);
                return false;
            }
            catch (Exception e)
            {
                string error = $"Unexpected error joining relay: {e.Message}";
                Debug.LogError($"[RelayManager] {error}");
                OnRelayError?.Invoke(error);
                return false;
            }
        }

        /// <summary>
        /// Cleanup relay session.
        /// </summary>
        public void Cleanup()
        {
            JoinCode = null;
            NetworkBootstrap.Instance?.Shutdown();
            Debug.Log("[RelayManager] Relay session cleaned up.");
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }
    }
}
