using UnityEngine;
using Mirror;
using System;
using System.Collections.Generic;

namespace PetGame.Network
{
    /// <summary>
    /// Serializable data structure representing a single special level option/request.
    /// Synced across all clients via SyncList.
    /// </summary>
    public struct SpecialLevelOptionData
    {
        /// <summary>NetworkIdentity netId of the Portal GameObject.</summary>
        public uint portalNetId;

        /// <summary>NetworkIdentity netId of the requesting player's NetworkPlayer.</summary>
        public uint requesterNetId;

        /// <summary>Index into the registered SpecialLevelData array for lookup.</summary>
        public int specialLevelDataIndex;
    }

    /// <summary>
    /// Network manager for the SpecialLevelListForMultiplayer system.
    /// Handles adding/approving/rejecting level requests with full network synchronization.
    /// Must be placed on a NetworkIdentity object in the scene (server-authoritative).
    /// </summary>
    [RequireComponent(typeof(NetworkIdentity))]
    public class SpecialLevelListManager : NetworkBehaviour
    {
        #region Constants

        /// <summary>Maximum number of options allowed in the list at once.</summary>
        public const int MAX_OPTIONS = 4;

        #endregion

        #region Serialized Fields

        [Header("Special Level Data Registry")]
        [Tooltip("All available SpecialLevelData assets. Index is used for network sync.")]
        [SerializeField] private SpecialLevelData[] registeredLevelData;

        #endregion

        #region SyncList

        /// <summary>
        /// Synchronized list of all current level options/requests.
        /// Automatically synced to all clients by Mirror.
        /// </summary>
        public readonly SyncList<SpecialLevelOptionData> OptionList = new SyncList<SpecialLevelOptionData>();

        #endregion

        #region Events

        /// <summary>Fired on all clients when the option list changes. UI should refresh.</summary>
        public event Action OnOptionListChanged;

        /// <summary>Fired when a request is rejected due to validation failure. Provides error message.</summary>
        public event Action<string> OnRequestRejected;

        /// <summary>Fired when an option is approved and boss fight starts.</summary>
        public event Action<uint> OnOptionApproved;

        #endregion

        #region Properties

        /// <summary>Singleton-like access (found via FindObjectOfType if needed).</summary>
        public static SpecialLevelListManager Instance { get; private set; }

        #endregion

        #region Private Fields

        // Server-side tracking of which players currently have a pending request
        private readonly HashSet<uint> _playersWithPendingRequest = new HashSet<uint>();

        #endregion

        #region Lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("[SpecialLevelListManager] Duplicate instance found. Destroying this one.");
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // Register SyncList callback for UI updates
            OptionList.Callback += OnSyncListChanged;
            // Trigger initial UI refresh for late joiners
            OnOptionListChanged?.Invoke();
        }

        public override void OnStopClient()
        {
            base.OnStopClient();
            OptionList.Callback -= OnSyncListChanged;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // Subscribe to client disconnect events
            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.OnClientDisconnectedEvent += HandleClientDisconnected;
            }
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.OnClientDisconnectedEvent -= HandleClientDisconnected;
            }
            _playersWithPendingRequest.Clear();
        }

        #endregion

        #region Public API

        /// <summary>
        /// Get the SpecialLevelData at the given registry index.
        /// </summary>
        public SpecialLevelData GetLevelData(int index)
        {
            if (registeredLevelData == null || index < 0 || index >= registeredLevelData.Length)
                return null;
            return registeredLevelData[index];
        }

        /// <summary>
        /// Number of registered SpecialLevelData assets.
        /// </summary>
        public int LevelDataCount => registeredLevelData != null ? registeredLevelData.Length : 0;

        /// <summary>
        /// Get a random valid index into the registeredLevelData array.
        /// Returns -1 if no data is registered.
        /// </summary>
        public int GetRandomLevelDataIndex()
        {
            if (registeredLevelData == null || registeredLevelData.Length == 0) return -1;
            return UnityEngine.Random.Range(0, registeredLevelData.Length);
        }

        /// <summary>
        /// Get the registry index for a given SpecialLevelData asset.
        /// Returns -1 if not found.
        /// </summary>
        public int GetLevelDataIndex(SpecialLevelData data)
        {
            if (registeredLevelData == null || data == null) return -1;
            for (int i = 0; i < registeredLevelData.Length; i++)
            {
                if (registeredLevelData[i] == data)
                    return i;
            }
            return -1;
        }

        /// <summary>
        /// Check if a player already has a pending request in the list.
        /// Can be called on client side by iterating the synced list.
        /// </summary>
        public bool HasPendingRequest(uint playerNetId)
        {
            foreach (var option in OptionList)
            {
                if (option.requesterNetId == playerNetId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Remove all options associated with a specific portal (by netId).
        /// Called on server when a portal is destroyed.
        /// </summary>
        [Server]
        public void RemoveOptionsByPortal(uint portalNetId)
        {
            for (int i = OptionList.Count - 1; i >= 0; i--)
            {
                if (OptionList[i].portalNetId == portalNetId)
                {
                    uint requesterNetId = OptionList[i].requesterNetId;
                    OptionList.RemoveAt(i);
                    _playersWithPendingRequest.Remove(requesterNetId);
                    Debug.Log($"[SpecialLevelListManager] Removed option for destroyed portal (netId={portalNetId}), freed requester (netId={requesterNetId}).");
                }
            }
        }

        /// <summary>
        /// Remove the option associated with a specific portal after boss defeat.
        /// Called on server when boss fight is won.
        /// </summary>
        [Server]
        public void RemoveOptionAfterBossDefeat(uint portalNetId)
        {
            for (int i = OptionList.Count - 1; i >= 0; i--)
            {
                if (OptionList[i].portalNetId == portalNetId)
                {
                    uint requesterNetId = OptionList[i].requesterNetId;
                    OptionList.RemoveAt(i);
                    _playersWithPendingRequest.Remove(requesterNetId);
                    Debug.Log($"[SpecialLevelListManager] Removed option after boss defeat (portalNetId={portalNetId}), freed requester (netId={requesterNetId}).");
                    break;
                }
            }
        }

        #endregion

        #region Commands (Client -> Server)

        /// <summary>
        /// Client requests to add a new option to the list.
        /// Server validates: list not full, player has no pending request, no boss fight active.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdRequestAddOption(uint portalNetId, uint requesterNetId, int levelDataIndex, NetworkConnectionToClient sender = null)
        {
            // Validation 1: Boss fight in progress
            BossFightManager bossFightManager = FindObjectOfType<BossFightManager>();
            if (bossFightManager != null && bossFightManager.IsBossFightActive)
            {
                TargetRejectRequest(sender, "Boss fight is in progress. Cannot add new requests.");
                return;
            }

            // Validation 2: List is full
            if (OptionList.Count >= MAX_OPTIONS)
            {
                TargetRejectRequest(sender, "Request list is full (max 4).");
                return;
            }

            // Validation 3: Player already has a pending request
            if (_playersWithPendingRequest.Contains(requesterNetId))
            {
                TargetRejectRequest(sender, "You already have a pending request. Wait for it to be processed.");
                return;
            }

            // Validation 4: Valid level data index
            if (registeredLevelData == null || levelDataIndex < 0 || levelDataIndex >= registeredLevelData.Length)
            {
                TargetRejectRequest(sender, "Invalid level data.");
                return;
            }

            // All validations passed - add to list
            SpecialLevelOptionData newOption = new SpecialLevelOptionData
            {
                portalNetId = portalNetId,
                requesterNetId = requesterNetId,
                specialLevelDataIndex = levelDataIndex
            };

            OptionList.Add(newOption);
            _playersWithPendingRequest.Add(requesterNetId);

            Debug.Log($"[SpecialLevelListManager] Option added: portal={portalNetId}, requester={requesterNetId}, levelIndex={levelDataIndex}");
        }

        /// <summary>
        /// Host approves an option - starts the boss fight for that portal.
        /// Only the host should call this.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdApproveOption(int optionIndex, NetworkConnectionToClient sender = null)
        {
            // Validate sender is host
            if (!IsCallerHost(sender))
            {
                TargetRejectRequest(sender, "Only the host can approve requests.");
                return;
            }

            // Validate index
            if (optionIndex < 0 || optionIndex >= OptionList.Count)
            {
                TargetRejectRequest(sender, "Invalid option index.");
                return;
            }

            // Validate no boss fight active
            BossFightManager bossFightManager = FindObjectOfType<BossFightManager>();
            if (bossFightManager != null && bossFightManager.IsBossFightActive)
            {
                TargetRejectRequest(sender, "Boss fight already in progress. Cannot start another.");
                return;
            }

            SpecialLevelOptionData approvedOption = OptionList[optionIndex];

            // Remove only the approved option from the list
            OptionList.RemoveAt(optionIndex);
            _playersWithPendingRequest.Remove(approvedOption.requesterNetId);

            // Start boss fight - pass portalId instead of portal object reference,
            // because the portal only exists on the requesting player's local Canvas.
            if (bossFightManager != null)
            {
                // Find the portal on the host's local Canvas (may be null if another player spawned it)
                PortalController[] allPortals = FindObjectsOfType<PortalController>();
                GameObject localPortalObj = null;
                foreach (var portal in allPortals)
                {
                    if (portal.portalId == approvedOption.portalNetId)
                    {
                        localPortalObj = portal.gameObject;
                        break;
                    }
                }

                bossFightManager.StartBossFight(localPortalObj, approvedOption.portalNetId);
                Debug.Log($"[SpecialLevelListManager] Option approved. Boss fight started for portal (portalId={approvedOption.portalNetId}).");
            }
            else
            {
                Debug.LogError("[SpecialLevelListManager] BossFightManager not found! Cannot start boss fight.");
            }

            // Notify all clients
            RpcOptionApproved(approvedOption.portalNetId);
        }

        /// <summary>
        /// Host rejects an option - removes it from the list but keeps the portal.
        /// Only the host should call this.
        /// </summary>
        [Command(requiresAuthority = false)]
        public void CmdRejectOption(int optionIndex, NetworkConnectionToClient sender = null)
        {
            // Validate sender is host
            if (!IsCallerHost(sender))
            {
                TargetRejectRequest(sender, "Only the host can reject requests.");
                return;
            }

            // Validate index
            if (optionIndex < 0 || optionIndex >= OptionList.Count)
            {
                TargetRejectRequest(sender, "Invalid option index.");
                return;
            }

            SpecialLevelOptionData rejectedOption = OptionList[optionIndex];

            // Remove from list and free the player's request slot
            OptionList.RemoveAt(optionIndex);
            _playersWithPendingRequest.Remove(rejectedOption.requesterNetId);

            Debug.Log($"[SpecialLevelListManager] Option rejected: portal={rejectedOption.portalNetId}, requester={rejectedOption.requesterNetId}. Portal preserved.");
        }

        #endregion

        #region TargetRpc (Server -> Specific Client)

        /// <summary>
        /// Send a rejection message to the requesting client.
        /// </summary>
        [TargetRpc]
        private void TargetRejectRequest(NetworkConnectionToClient target, string reason)
        {
            Debug.LogWarning($"[SpecialLevelListManager] Request rejected: {reason}");
            OnRequestRejected?.Invoke(reason);
        }

        #endregion

        #region ClientRpc (Server -> All Clients)

        /// <summary>
        /// Notify all clients that an option was approved and boss fight started.
        /// </summary>
        [ClientRpc]
        private void RpcOptionApproved(uint portalNetId)
        {
            OnOptionApproved?.Invoke(portalNetId);
        }

        #endregion

        #region Server-Side Helpers

        /// <summary>
        /// Handle client disconnection - remove all options from the disconnected player.
        /// </summary>
        private void HandleClientDisconnected(NetworkConnectionToClient conn)
        {
            if (!isServer) return;

            // Find the NetworkPlayer associated with this connection
            NetworkPlayer disconnectedPlayer = null;
            foreach (var player in MirrorNetworkManager.singleton.ConnectedPlayers)
            {
                if (player != null && player.connectionToClient == conn)
                {
                    disconnectedPlayer = player;
                    break;
                }
            }

            // Also check the identity directly from the connection
            uint disconnectedNetId = 0;
            if (conn.identity != null)
            {
                disconnectedNetId = conn.identity.netId;
            }
            else if (disconnectedPlayer != null)
            {
                disconnectedNetId = disconnectedPlayer.netId;
            }

            if (disconnectedNetId == 0) return;

            // Remove all options from this player
            for (int i = OptionList.Count - 1; i >= 0; i--)
            {
                if (OptionList[i].requesterNetId == disconnectedNetId)
                {
                    OptionList.RemoveAt(i);
                    Debug.Log($"[SpecialLevelListManager] Removed option from disconnected player (netId={disconnectedNetId}).");
                }
            }

            _playersWithPendingRequest.Remove(disconnectedNetId);
        }

        /// <summary>
        /// Check if the caller (sender connection) is the host.
        /// On a host setup, the host's connection is the first one (connectionId 0).
        /// </summary>
        private bool IsCallerHost(NetworkConnectionToClient sender)
        {
            // On a host, the local connection has connectionId 0
            if (sender == null) return true; // null sender means local server call
            return sender.connectionId == 0;
        }

        #endregion

        #region SyncList Callback

        /// <summary>
        /// Called on clients when the SyncList changes.
        /// </summary>
        private void OnSyncListChanged(SyncList<SpecialLevelOptionData>.Operation op, int index, SpecialLevelOptionData oldItem, SpecialLevelOptionData newItem)
        {
            OnOptionListChanged?.Invoke();
        }

        #endregion
    }
}
