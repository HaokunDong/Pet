using UnityEngine;
using Steamworks;
using System;

namespace PetGame.Network
{
    /// <summary>
    /// Manages Steam Lobby operations: create, join, invite, leave.
    /// Handles all Steam callbacks related to lobby lifecycle.
    /// Uses SingletonMono pattern for global access.
    /// </summary>
    public class SteamLobbyManager : SingletonMono<SteamLobbyManager>
    {
        #region Constants

        /// <summary>Maximum players allowed in a lobby.</summary>
        public const int MAX_PLAYERS = 2;

        /// <summary>Lobby data key for storing the host's Steam ID.</summary>
        private const string LOBBY_KEY_HOST_ID = "HostSteamId";

        /// <summary>Lobby data key for game identification.</summary>
        private const string LOBBY_KEY_GAME_ID = "PetGame_Lobby";

        #endregion

        #region Events

        /// <summary>Fired when a lobby is successfully created. Provides the lobby code (string).</summary>
        public event Action<string> OnLobbyCreated;

        /// <summary>Fired when successfully entered a lobby (as host or client).</summary>
        public event Action OnLobbyEntered;

        /// <summary>Fired when joining a lobby fails. Provides error message.</summary>
        public event Action<string> OnLobbyJoinFailed;

        /// <summary>Fired when the player count in the lobby changes.</summary>
        public event Action<int> OnPlayerCountChanged;

        /// <summary>Fired when the host disconnects or lobby is closed.</summary>
        public event Action OnHostDisconnected;

        /// <summary>Fired when an error occurs. Provides error message.</summary>
        public event Action<string> OnError;

        #endregion

        #region Properties

        /// <summary>Current Steam Lobby ID. Invalid if not in a lobby.</summary>
        public CSteamID CurrentLobbyId { get; private set; } = CSteamID.Nil;

        /// <summary>Whether we are currently in a lobby.</summary>
        public bool InLobby => CurrentLobbyId.IsValid() && CurrentLobbyId != CSteamID.Nil;

        /// <summary>Whether we are the host of the current lobby.</summary>
        public bool IsHost { get; private set; }

        /// <summary>Current lobby code (string representation of lobby ID).</summary>
        public string CurrentLobbyCode { get; private set; } = "";

        /// <summary>Number of players currently in the lobby.</summary>
        public int PlayerCount => InLobby ? SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId) : 0;

        /// <summary>Whether Steam is initialized and ready.</summary>
        public bool IsSteamReady => SteamManager.Initialized;

        #endregion

        #region Steam Callbacks

        private Callback<LobbyCreated_t> lobbyCreatedCallback;
        private Callback<LobbyEnter_t> lobbyEnteredCallback;
        private Callback<GameLobbyJoinRequested_t> lobbyJoinRequestedCallback;
        private Callback<LobbyChatUpdate_t> lobbyChatUpdateCallback;
        private Callback<LobbyDataUpdate_t> lobbyDataUpdateCallback;

        #endregion

        #region Lifecycle

        protected override void OnAwake()
        {
            if (!SteamManager.Initialized)
            {
                Debug.LogWarning("[SteamLobbyManager] Steam is not initialized. Lobby features will be unavailable.");
                return;
            }

            RegisterCallbacks();
        }

        protected override void OnStart()
        {
            // Nothing needed here
        }

        protected override void BeforeOnDestroy()
        {
            // Leave lobby if still in one
            if (InLobby)
            {
                LeaveLobby();
            }

            UnregisterCallbacks();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Create a new Steam Lobby.
        /// </summary>
        public void CreateLobby()
        {
            if (!IsSteamReady)
            {
                OnError?.Invoke("Steam is not initialized.");
                return;
            }

            if (InLobby)
            {
                OnError?.Invoke("Already in a lobby. Leave first.");
                return;
            }

            Debug.Log("[SteamLobbyManager] Creating lobby...");
            SteamMatchmaking.CreateLobby(ELobbyType.k_ELobbyTypePublic, MAX_PLAYERS);
        }

        /// <summary>
        /// Join a lobby by lobby code (string representation of CSteamID).
        /// </summary>
        public void JoinLobby(string lobbyCode)
        {
            if (!IsSteamReady)
            {
                OnLobbyJoinFailed?.Invoke("Steam is not initialized.");
                return;
            }

            if (InLobby)
            {
                OnLobbyJoinFailed?.Invoke("Already in a lobby. Leave first.");
                return;
            }

            if (string.IsNullOrWhiteSpace(lobbyCode))
            {
                OnLobbyJoinFailed?.Invoke("Please enter a lobby code.");
                return;
            }

            if (!ulong.TryParse(lobbyCode.Trim(), out ulong lobbyId))
            {
                OnLobbyJoinFailed?.Invoke("Invalid lobby code format.");
                return;
            }

            CSteamID steamLobbyId = new CSteamID(lobbyId);
            Debug.Log($"[SteamLobbyManager] Joining lobby: {lobbyCode}");
            SteamMatchmaking.JoinLobby(steamLobbyId);
        }

        /// <summary>
        /// Open the Steam Overlay invite dialog for the current lobby.
        /// </summary>
        public void InviteFriends()
        {
            if (!InLobby)
            {
                OnError?.Invoke("Not in a lobby. Create or join one first.");
                return;
            }

            Debug.Log("[SteamLobbyManager] Opening Steam invite dialog...");
            SteamFriends.ActivateGameOverlayInviteDialog(CurrentLobbyId);
        }

        /// <summary>
        /// Leave the current lobby.
        /// </summary>
        public void LeaveLobby()
        {
            if (!InLobby) return;

            Debug.Log($"[SteamLobbyManager] Leaving lobby: {CurrentLobbyId}");
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);

            CurrentLobbyId = CSteamID.Nil;
            CurrentLobbyCode = "";
            IsHost = false;
        }

        /// <summary>
        /// Copy the current lobby code to the system clipboard.
        /// </summary>
        public void CopyLobbyCodeToClipboard()
        {
            if (!InLobby || string.IsNullOrEmpty(CurrentLobbyCode))
            {
                OnError?.Invoke("No lobby code to copy.");
                return;
            }

            GUIUtility.systemCopyBuffer = CurrentLobbyCode;
            Debug.Log($"[SteamLobbyManager] Lobby code copied: {CurrentLobbyCode}");
        }

        /// <summary>
        /// Get the Steam ID of the lobby host.
        /// </summary>
        public CSteamID GetHostSteamId()
        {
            if (!InLobby) return CSteamID.Nil;
            return SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
        }

        #endregion

        #region Steam Callback Registration

        private void RegisterCallbacks()
        {
            lobbyCreatedCallback = Callback<LobbyCreated_t>.Create(OnSteamLobbyCreated);
            lobbyEnteredCallback = Callback<LobbyEnter_t>.Create(OnSteamLobbyEntered);
            lobbyJoinRequestedCallback = Callback<GameLobbyJoinRequested_t>.Create(OnGameLobbyJoinRequested);
            lobbyChatUpdateCallback = Callback<LobbyChatUpdate_t>.Create(OnLobbyChatUpdate);
            lobbyDataUpdateCallback = Callback<LobbyDataUpdate_t>.Create(OnLobbyDataUpdate);

            Debug.Log("[SteamLobbyManager] Steam callbacks registered.");
        }

        private void UnregisterCallbacks()
        {
            lobbyCreatedCallback?.Dispose();
            lobbyEnteredCallback?.Dispose();
            lobbyJoinRequestedCallback?.Dispose();
            lobbyChatUpdateCallback?.Dispose();
            lobbyDataUpdateCallback?.Dispose();

            lobbyCreatedCallback = null;
            lobbyEnteredCallback = null;
            lobbyJoinRequestedCallback = null;
            lobbyChatUpdateCallback = null;
            lobbyDataUpdateCallback = null;
        }

        #endregion

        #region Steam Callback Handlers

        /// <summary>
        /// Called when a lobby creation request completes.
        /// </summary>
        private void OnSteamLobbyCreated(LobbyCreated_t result)
        {
            if (result.m_eResult != EResult.k_EResultOK)
            {
                string error = $"Failed to create lobby: {result.m_eResult}";
                Debug.LogError($"[SteamLobbyManager] {error}");
                OnError?.Invoke(error);
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentLobbyCode = result.m_ulSteamIDLobby.ToString();
            IsHost = true;

            // Set lobby metadata
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_GAME_ID, "1");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_HOST_ID,
                SteamUser.GetSteamID().m_SteamID.ToString());

            Debug.Log($"[SteamLobbyManager] Lobby created successfully. Code: {CurrentLobbyCode}");
            OnLobbyCreated?.Invoke(CurrentLobbyCode);
        }

        /// <summary>
        /// Called when we enter a lobby (either by creating or joining).
        /// </summary>
        private void OnSteamLobbyEntered(LobbyEnter_t result)
        {
            EChatRoomEnterResponse response = (EChatRoomEnterResponse)result.m_EChatRoomEnterResponse;

            if (response != EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess)
            {
                string error = $"Failed to enter lobby: {response}";
                Debug.LogError($"[SteamLobbyManager] {error}");

                switch (response)
                {
                    case EChatRoomEnterResponse.k_EChatRoomEnterResponseFull:
                        OnLobbyJoinFailed?.Invoke("Room is full.");
                        break;
                    case EChatRoomEnterResponse.k_EChatRoomEnterResponseDoesntExist:
                        OnLobbyJoinFailed?.Invoke("Room does not exist.");
                        break;
                    case EChatRoomEnterResponse.k_EChatRoomEnterResponseBanned:
                        OnLobbyJoinFailed?.Invoke("You are banned from this room.");
                        break;
                    default:
                        OnLobbyJoinFailed?.Invoke(error);
                        break;
                }

                CurrentLobbyId = CSteamID.Nil;
                CurrentLobbyCode = "";
                IsHost = false;
                return;
            }

            CurrentLobbyId = new CSteamID(result.m_ulSteamIDLobby);
            CurrentLobbyCode = result.m_ulSteamIDLobby.ToString();

            // Determine if we are the host
            CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
            IsHost = lobbyOwner == SteamUser.GetSteamID();

            Debug.Log($"[SteamLobbyManager] Entered lobby: {CurrentLobbyCode} (IsHost: {IsHost})");
            OnLobbyEntered?.Invoke();
            OnPlayerCountChanged?.Invoke(PlayerCount);
        }

        /// <summary>
        /// Called when a friend accepts a game invite through Steam Overlay.
        /// This triggers auto-join to the lobby.
        /// </summary>
        private void OnGameLobbyJoinRequested(GameLobbyJoinRequested_t request)
        {
            Debug.Log($"[SteamLobbyManager] Join requested for lobby: {request.m_steamIDLobby}");

            // If already in a lobby, leave first
            if (InLobby)
            {
                LeaveLobby();
            }

            // Join the requested lobby
            SteamMatchmaking.JoinLobby(request.m_steamIDLobby);
        }

        /// <summary>
        /// Called when a lobby member's status changes (join/leave/disconnect/kicked).
        /// </summary>
        private void OnLobbyChatUpdate(LobbyChatUpdate_t update)
        {
            CSteamID lobbyId = new CSteamID(update.m_ulSteamIDLobby);
            if (lobbyId != CurrentLobbyId) return;

            EChatMemberStateChange stateChange = (EChatMemberStateChange)update.m_rgfChatMemberStateChange;
            CSteamID changedUser = new CSteamID(update.m_ulSteamIDUserChanged);

            if ((stateChange & EChatMemberStateChange.k_EChatMemberStateChangeEntered) != 0)
            {
                Debug.Log($"[SteamLobbyManager] Player joined lobby: {changedUser}");
            }
            else if ((stateChange & EChatMemberStateChange.k_EChatMemberStateChangeLeft) != 0 ||
                     (stateChange & EChatMemberStateChange.k_EChatMemberStateChangeDisconnected) != 0 ||
                     (stateChange & EChatMemberStateChange.k_EChatMemberStateChangeKicked) != 0)
            {
                Debug.Log($"[SteamLobbyManager] Player left lobby: {changedUser}");

                // Check if the host left
                CSteamID currentOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
                if (changedUser == currentOwner || !IsLobbyOwnerValid())
                {
                    Debug.LogWarning("[SteamLobbyManager] Host disconnected!");
                    OnHostDisconnected?.Invoke();
                }
            }

            OnPlayerCountChanged?.Invoke(PlayerCount);
        }

        /// <summary>
        /// Called when lobby metadata is updated.
        /// </summary>
        private void OnLobbyDataUpdate(LobbyDataUpdate_t update)
        {
            CSteamID lobbyId = new CSteamID(update.m_ulSteamIDLobby);
            if (lobbyId != CurrentLobbyId) return;

            // Check if lobby owner changed (host migration or disconnect)
            if (!IsHost)
            {
                CSteamID newOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
                if (newOwner == SteamUser.GetSteamID())
                {
                    IsHost = true;
                    Debug.Log("[SteamLobbyManager] We are now the lobby host (host migration).");
                }
            }
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Check if the lobby owner is still a valid member of the lobby.
        /// </summary>
        private bool IsLobbyOwnerValid()
        {
            if (!InLobby) return false;

            CSteamID owner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, i) == owner)
                    return true;
            }

            return false;
        }

        #endregion
    }
}
