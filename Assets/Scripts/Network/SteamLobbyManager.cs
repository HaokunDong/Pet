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
        public const int MAX_PLAYERS = 4;
        public const int LOBBY_CODE_LENGTH = 6;
        private const string LOBBY_KEY_CODE = "RoomCode";
        private CallResult<LobbyMatchList_t> lobbySearchResult;
        private bool searching;
        private const string LOBBY_KEY_CLOSED = "Closed";
        private CSteamID originalHostId = CSteamID.Nil;
        private float nextMembershipCheck;

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

        /// <summary>Six-character shareable room code.</summary>
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

        protected override void OnUpdate()
        {
            if (!InLobby || !IsSteamReady || Time.unscaledTime < nextMembershipCheck) return;
            nextMembershipCheck = Time.unscaledTime + 0.5f;
            if (!IsHost && (SteamMatchmaking.GetLobbyData(CurrentLobbyId, LOBBY_KEY_CLOSED) == "1" ||
                SteamMatchmaking.GetLobbyOwner(CurrentLobbyId) != originalHostId || !IsLobbyOwnerValid()))
            {
                HandleRoomClosed();
                return;
            }
            OnPlayerCountChanged?.Invoke(PlayerCount);
            if (IsHost) MirrorNetworkManager.singleton?.RemoveDepartedLobbyConnections(this);
        }

        protected override void BeforeOnDestroy()
        {
            // Only attempt to leave lobby if Steam is still initialized
            if (SteamManager.Initialized && InLobby)
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
        /// Find and join a lobby by its six-character room code.
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

            string code = lobbyCode.Trim().ToUpperInvariant();
            if (!System.Text.RegularExpressions.Regex.IsMatch(code, @"^(?=.*[A-Z])(?=.*[0-9])[A-Z0-9]{6}$"))
            {
                OnLobbyJoinFailed?.Invoke("Enter a 6-character code containing letters and numbers.");
                return;
            }

            if (searching) return;
            searching = true;
            lobbySearchResult = lobbySearchResult ?? CallResult<LobbyMatchList_t>.Create(OnLobbySearchCompleted);
            SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_KEY_GAME_ID, "1", ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListStringFilter(LOBBY_KEY_CODE, code, ELobbyComparison.k_ELobbyComparisonEqual);
            SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
            SteamMatchmaking.AddRequestLobbyListResultCountFilter(2);
            lobbySearchResult.Set(SteamMatchmaking.RequestLobbyList());
        }

        private void OnLobbySearchCompleted(LobbyMatchList_t result, bool ioFailure)
        {
            searching = false;
            if (InLobby) return;
            if (ioFailure || result.m_nLobbiesMatching != 1)
            {
                OnLobbyJoinFailed?.Invoke(ioFailure ? "Room search failed. Please try again." :
                    result.m_nLobbiesMatching == 0 ? "Room not found or full. Check the code and try again." :
                    "Duplicate room code. Ask the host to recreate the room or send a Steam invite.");
                return;
            }
            SteamMatchmaking.JoinLobby(SteamMatchmaking.GetLobbyByIndex(0));
        }

        private static string GenerateLobbyCode()
        {
            const string letters = "ABCDEFGHJKLMNPQRSTUVWXYZ";
            const string digits = "23456789";
            const string alphabet = letters + digits;
            var random = new System.Random(Guid.NewGuid().GetHashCode());
            var code = new char[LOBBY_CODE_LENGTH];
            code[0] = letters[random.Next(letters.Length)];
            code[1] = digits[random.Next(digits.Length)];
            for (int i = 2; i < code.Length; i++) code[i] = alphabet[random.Next(alphabet.Length)];
            for (int i = code.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                char temporary = code[i];
                code[i] = code[j];
                code[j] = temporary;
            }
            return new string(code);
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
            // Steam automatically migrates ownership. Mark closed before leaving so
            // clients leave instead of turning the room into an unhosted lobby.
            if (IsHost && IsSteamReady)
            {
                SteamMatchmaking.SetLobbyJoinable(CurrentLobbyId, false);
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_CLOSED, "1");
            }
            SteamMatchmaking.LeaveLobby(CurrentLobbyId);

            CurrentLobbyId = CSteamID.Nil;
            CurrentLobbyCode = "";
            IsHost = false;
            originalHostId = CSteamID.Nil;
            OnPlayerCountChanged?.Invoke(0);
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
            return originalHostId;
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
            lobbySearchResult?.Dispose();
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
            CurrentLobbyCode = GenerateLobbyCode();
            IsHost = true;
            originalHostId = SteamUser.GetSteamID();

            // Set lobby metadata
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_GAME_ID, "1");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_CLOSED, "0");
            SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_CODE, CurrentLobbyCode);
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
            CurrentLobbyCode = SteamMatchmaking.GetLobbyData(CurrentLobbyId, LOBBY_KEY_CODE);

            // Determine if we are the host
            CSteamID lobbyOwner = SteamMatchmaking.GetLobbyOwner(CurrentLobbyId);
            string hostId = SteamMatchmaking.GetLobbyData(CurrentLobbyId, LOBBY_KEY_HOST_ID);
            originalHostId = ulong.TryParse(hostId, out ulong hostValue) ? new CSteamID(hostValue) : lobbyOwner;
            IsHost = originalHostId == SteamUser.GetSteamID();
            if (SteamMatchmaking.GetLobbyData(CurrentLobbyId, LOBBY_KEY_CLOSED) == "1" || lobbyOwner != originalHostId)
            {
                HandleRoomClosed();
                return;
            }

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
                if (changedUser == originalHostId || !IsLobbyOwnerValid())
                {
                    HandleRoomClosed();
                    return;
                }
                if (IsHost) MirrorNetworkManager.singleton?.DisconnectSteamMember(changedUser);
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

            if (!IsHost && (SteamMatchmaking.GetLobbyData(CurrentLobbyId, LOBBY_KEY_CLOSED) == "1" ||
                SteamMatchmaking.GetLobbyOwner(CurrentLobbyId) != originalHostId))
            {
                HandleRoomClosed();
            }
        }

        private void HandleRoomClosed()
        {
            if (!InLobby) return;
            // If Steam migrated ownership to us, prevent anyone joining while all
            // remaining members process the closure callback.
            if (SteamMatchmaking.GetLobbyOwner(CurrentLobbyId) == SteamUser.GetSteamID())
            {
                SteamMatchmaking.SetLobbyJoinable(CurrentLobbyId, false);
                SteamMatchmaking.SetLobbyData(CurrentLobbyId, LOBBY_KEY_CLOSED, "1");
            }
            if (MirrorNetworkManager.singleton != null)
                MirrorNetworkManager.singleton.LeaveRoomAndResumeSinglePlayer();
            else LeaveLobby();
            OnHostDisconnected?.Invoke();
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Check if the lobby owner is still a valid member of the lobby.
        /// </summary>
        private bool IsLobbyOwnerValid()
        {
            return IsLobbyMember(originalHostId);
        }

        public bool IsLobbyMember(CSteamID member)
        {
            if (!InLobby) return false;
            int memberCount = SteamMatchmaking.GetNumLobbyMembers(CurrentLobbyId);

            for (int i = 0; i < memberCount; i++)
            {
                if (SteamMatchmaking.GetLobbyMemberByIndex(CurrentLobbyId, i) == member)
                    return true;
            }

            return false;
        }

        #endregion
    }
}
