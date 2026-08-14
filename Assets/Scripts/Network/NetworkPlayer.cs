using UnityEngine;
using Mirror;
using Steamworks;
using System.Collections;

namespace PetGame.Network
{
    /// <summary>
    /// Network player proxy spawned for each connected player.
    /// Instead of spawning networked character objects, this component:
    /// 1. Keeps the local player's existing character untouched
    /// 2. Syncs character info (ID, position, facing, health) to remote players
    /// 3. Remote players create a local "mirror character" based on synced data
    /// This avoids the need for NetworkIdentity on character prefabs.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        #region SyncVars

        /// <summary>
        /// The character ID selected by this player. Empty means no character.
        /// </summary>
        [SyncVar(hook = nameof(OnCharacterIdChanged))]
        private string selectedCharacterId = "";

        /// <summary>
        /// The Steam display name of this player.
        /// </summary>
        [SyncVar]
        private string playerName = "";

        /// <summary>
        /// Whether this player has an active character.
        /// </summary>
        [SyncVar(hook = nameof(OnHasCharacterChanged))]
        private bool hasCharacter = false;

        /// <summary>
        /// Synced position of this player's character.
        /// </summary>
        [SyncVar]
        private Vector3 syncedPosition;

        /// <summary>
        /// Synced facing direction (true = facing right).
        /// </summary>
        [SyncVar]
        private bool syncedFacingRight = true;

        /// <summary>
        /// Synced current health.
        /// </summary>
        [SyncVar]
        private float syncedHealth;

        /// <summary>
        /// Synced max health.
        /// </summary>
        [SyncVar]
        private float syncedMaxHealth;

        /// <summary>
        /// Synced alive state.
        /// </summary>
        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool syncedIsAlive = true;

        #endregion

        #region Properties

        public string SelectedCharacterId => selectedCharacterId;
        public string PlayerName => playerName;
        public bool HasCharacter => hasCharacter;

        /// <summary>
        /// The local character entity that this player controls (only valid for local player).
        /// </summary>
        public CharacterEntity LocalCharacter { get; private set; }

        /// <summary>
        /// The mirror character entity created on remote clients to represent this player.
        /// </summary>
        public CharacterEntity MirrorCharacter { get; private set; }

        #endregion

        #region Settings

        [Header("Sync Settings")]
        [SerializeField] private float syncRate = 20f;
        [SerializeField] private float positionThreshold = 0.01f;
        [SerializeField] private float interpolationSpeed = 15f;

        /// <summary>Offset for the remote player's mirror character so they don't overlap with the host.</summary>
        [SerializeField] private float remotePlayerXOffset = 2f;

        #endregion

        #region Private Fields

        private float syncTimer;
        private Vector3 lastSentPosition;
        private bool mirrorCharacterCreated = false;

        #endregion

        #region Lifecycle

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            Debug.Log("[NetworkPlayer] Local player started.");

            // Register as the local player in the network manager
            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.LocalPlayer = this;
            }

            // Set player name from Steam
            if (SteamManager.Initialized)
            {
                string steamName = SteamFriends.GetPersonaName();
                CmdSetPlayerName(steamName);
            }

            // Find and register the existing local character
            StartCoroutine(RegisterLocalCharacterDelayed());
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            // If this is a remote player and they already have a character, create the mirror
            if (!isOwned && hasCharacter && !string.IsNullOrEmpty(selectedCharacterId))
            {
                StartCoroutine(CreateMirrorCharacterDelayed(selectedCharacterId));
            }
        }

        /// <summary>
        /// Wait for GameCharacterManager to be ready before creating mirror character.
        /// </summary>
        private IEnumerator CreateMirrorCharacterDelayed(string characterId)
        {
            // Wait until GameCharacterManager is available and has initialized
            float timeout = 5f;
            float elapsed = 0f;
            while (Object.FindObjectOfType<GameCharacterManager>() == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            // Additional frame wait to ensure Start() has completed
            yield return null;
            yield return null;

            if (!mirrorCharacterCreated)
            {
                CreateMirrorCharacter(characterId);
            }
        }

        public override void OnStopClient()
        {
            base.OnStopClient();

            // Clean up mirror character when disconnecting
            DestroyMirrorCharacter();

            // If local player, unregister
            if (isOwned && MirrorNetworkManager.singleton != null)
            {
                if (MirrorNetworkManager.singleton.LocalPlayer == this)
                {
                    MirrorNetworkManager.singleton.LocalPlayer = null;
                }
            }
        }

        private void Update()
        {
            if (isOwned)
            {
                // Local player: send position/state updates to server
                UpdateLocalPlayerSync();
            }
            else
            {
                // Remote player: interpolate mirror character to synced position
                UpdateMirrorCharacter();
            }
        }

        #endregion

        #region Local Player - Character Registration

        /// <summary>
        /// Wait for GameCharacterManager to be ready and have a player character, then register it.
        /// In client-only mode, creates the character since GameCharacterManager.Start() skips it.
        /// </summary>
        private IEnumerator RegisterLocalCharacterDelayed()
        {
            // Wait for GameCharacterManager to exist
            GameCharacterManager gcm = null;
            float timeout = 5f;
            float elapsed = 0f;
            while (elapsed < timeout)
            {
                gcm = Object.FindObjectOfType<GameCharacterManager>();
                if (gcm != null)
                    break;
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (gcm == null)
            {
                Debug.LogWarning("[NetworkPlayer] GameCharacterManager not found after timeout.");
                CmdRegisterNoCharacter();
                yield break;
            }

            // In client-only mode, GameCharacterManager.Start() skips character creation.
            // We need to create the character here.
            bool isClientOnly = MirrorNetworkManager.singleton != null && MirrorNetworkManager.singleton.IsClientOnly;

            if (isClientOnly && gcm.PlayerCharacters.Count == 0)
            {
                // Create the local player's character in the host's scene
                if (gcm.playerCharacterDataList != null && gcm.playerCharacterDataList.Length > 0)
                {
                    CharacterData data = gcm.playerCharacterDataList[0];
                    Vector3 spawnPos = gcm.GetPlayerSpawnPosition();

                    CharacterEntity entity = gcm.CreatePlayerCharacter(data, spawnPos);
                    gcm.PlayerCharacters.Add(entity);

                    Debug.Log($"[NetworkPlayer] Client-only mode: Created local character '{data.characterId}' in host's scene.");
                }
            }

            // Now wait for the character to be ready
            elapsed = 0f;
            while (elapsed < timeout)
            {
                if (gcm.PlayerCharacters.Count > 0)
                    break;
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

            if (gcm.PlayerCharacters.Count > 0)
            {
                CharacterEntity currentChar = gcm.PlayerCharacters[0];
                if (currentChar != null && currentChar.characterData != null)
                {
                    LocalCharacter = currentChar;
                    string charId = currentChar.characterData.characterId;
                    Debug.Log($"[NetworkPlayer] Registering existing local character: {charId}");

                    // Tell the server about our character
                    CmdRegisterCharacter(charId,
                        currentChar.transform.position,
                        currentChar.RuntimeStats.currentHealth,
                        currentChar.RuntimeStats.maxHealth);
                }
                else
                {
                    Debug.Log("[NetworkPlayer] Player character exists but has no data.");
                    CmdRegisterNoCharacter();
                }
            }
            else
            {
                Debug.Log("[NetworkPlayer] No local character active after timeout.");
                CmdRegisterNoCharacter();
            }
        }

        /// <summary>
        /// Called when the local player switches character (e.g., via card selection).
        /// </summary>
        public void OnLocalCharacterSwitched(string newCharacterId)
        {
            if (!isOwned) return;
            if (string.IsNullOrEmpty(newCharacterId)) return;

            // Update local reference
            GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm != null && gcm.PlayerCharacters.Count > 0)
            {
                LocalCharacter = gcm.PlayerCharacters[0];
            }

            Debug.Log($"[NetworkPlayer] Local character switched to: {newCharacterId}");

            float health = LocalCharacter != null ? LocalCharacter.RuntimeStats.currentHealth : 100f;
            float maxHealth = LocalCharacter != null ? LocalCharacter.RuntimeStats.maxHealth : 100f;
            Vector3 pos = LocalCharacter != null ? LocalCharacter.transform.position : Vector3.zero;

            CmdSwitchCharacter(newCharacterId, pos, health, maxHealth);
        }

        #endregion

        #region Local Player - State Sync

        /// <summary>
        /// Periodically send local character state to the server.
        /// </summary>
        private void UpdateLocalPlayerSync()
        {
            if (LocalCharacter == null) return;

            syncTimer += Time.deltaTime;
            if (syncTimer < 1f / syncRate) return;
            syncTimer = 0f;

            // Position
            Vector3 currentPos = LocalCharacter.transform.position;
            if (Vector3.Distance(currentPos, lastSentPosition) > positionThreshold)
            {
                CmdUpdatePosition(currentPos);
                lastSentPosition = currentPos;
            }

            // Facing direction
            SpriteRenderer sr = LocalCharacter.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                bool facingRight = !sr.flipX;
                CmdUpdateFacing(facingRight);
            }

            // Health
            if (LocalCharacter.RuntimeStats != null)
            {
                float currentHealth = LocalCharacter.RuntimeStats.currentHealth;
                float maxHealth = LocalCharacter.RuntimeStats.maxHealth;
                bool isAlive = LocalCharacter.RuntimeStats.IsAlive;

                CmdUpdateHealth(currentHealth, maxHealth, isAlive);
            }
        }

        #endregion

        #region Mirror Character - Remote Representation

        /// <summary>
        /// Create a local "mirror" character to represent a remote player.
        /// Uses GameCharacterManager.CreatePlayerCharacter to ensure consistent visuals.
        /// </summary>
        private void CreateMirrorCharacter(string characterId)
        {
            if (mirrorCharacterCreated) return;
            if (string.IsNullOrEmpty(characterId)) return;

            // Find the CharacterData
            CharacterData charData = FindCharacterDataById(characterId);
            if (charData == null)
            {
                Debug.LogWarning($"[NetworkPlayer] Cannot create mirror character: CharacterData not found for ID '{characterId}'");
                return;
            }

            // Determine spawn position
            Vector3 spawnPos = syncedPosition;
            if (spawnPos == Vector3.zero)
            {
                // Fallback: offset from the local player's position
                GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
                if (gcm != null)
                {
                    spawnPos = gcm.GetPlayerSpawnPosition() + new Vector3(remotePlayerXOffset, 0f, 0f);
                }
            }

            // Create the mirror character using GameCharacterManager's method
            GameCharacterManager gcm2 = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm2 != null)
            {
                MirrorCharacter = gcm2.CreatePlayerCharacter(charData, spawnPos);

                // Disable AI and manual control on mirror characters - they are driven by network sync
                DisableMirrorCharacterControllers();

                // Mark the mirror character's tag to differentiate from local player
                MirrorCharacter.gameObject.name = $"MirrorPlayer_{charData.characterName}_{playerName}";

                mirrorCharacterCreated = true;
                Debug.Log($"[NetworkPlayer] Mirror character created: {charData.characterName} for player '{playerName}'");
            }
            else
            {
                Debug.LogError("[NetworkPlayer] GameCharacterManager not found, cannot create mirror character.");
            }
        }

        /// <summary>
        /// Destroy the mirror character when the remote player disconnects or switches character.
        /// </summary>
        private void DestroyMirrorCharacter()
        {
            if (MirrorCharacter != null)
            {
                // Use pool recycling if possible
                PoolMgr poolMgr = PoolMgr.Instance;
                if (poolMgr != null)
                {
                    poolMgr.PutNode(MirrorCharacter.gameObject);
                }
                else
                {
                    Destroy(MirrorCharacter.gameObject);
                }
                MirrorCharacter = null;
                mirrorCharacterCreated = false;
                Debug.Log("[NetworkPlayer] Mirror character destroyed.");
            }
        }

        /// <summary>
        /// Disable AI and manual controllers on the mirror character so it doesn't act on its own.
        /// </summary>
        private void DisableMirrorCharacterControllers()
        {
            if (MirrorCharacter == null) return;

            var ai = MirrorCharacter.GetComponent<PetGame.AI.AIController>();
            if (ai != null) ai.enabled = false;

            var manual = MirrorCharacter.GetComponent<ManualController>();
            if (manual != null) manual.enabled = false;

            var controlMode = MirrorCharacter.GetComponent<ControlModeManager>();
            if (controlMode != null) controlMode.enabled = false;
        }

        /// <summary>
        /// Update the mirror character's position and state from synced data.
        /// </summary>
        private void UpdateMirrorCharacter()
        {
            if (MirrorCharacter == null) return;

            // Smooth position interpolation
            MirrorCharacter.transform.position = Vector3.Lerp(
                MirrorCharacter.transform.position,
                syncedPosition,
                Time.deltaTime * interpolationSpeed
            );

            // Apply facing direction
            SpriteRenderer sr = MirrorCharacter.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.flipX = !syncedFacingRight;
            }

            // Update health
            if (MirrorCharacter.RuntimeStats != null)
            {
                MirrorCharacter.RuntimeStats.currentHealth = syncedHealth;
                MirrorCharacter.RuntimeStats.maxHealth = syncedMaxHealth;
            }
        }

        #endregion

        #region Commands (Client -> Server)

        [Command]
        private void CmdSetPlayerName(string name)
        {
            playerName = name;
            Debug.Log($"[NetworkPlayer] Player name set: {name}");
        }

        /// <summary>
        /// Register that this player has an active character.
        /// </summary>
        [Command]
        private void CmdRegisterCharacter(string characterId, Vector3 position, float health, float maxHealth)
        {
            selectedCharacterId = characterId;
            hasCharacter = true;
            syncedPosition = position;
            syncedHealth = health;
            syncedMaxHealth = maxHealth;
            syncedIsAlive = true;

            Debug.Log($"[NetworkPlayer] Server: Player registered character '{characterId}'");

            // Notify all clients to create the mirror character
            RpcCreateMirrorCharacter(characterId);
        }

        /// <summary>
        /// Register that this player has no character.
        /// </summary>
        [Command]
        private void CmdRegisterNoCharacter()
        {
            selectedCharacterId = "";
            hasCharacter = false;
            Debug.Log("[NetworkPlayer] Server: Player has no character.");
        }

        /// <summary>
        /// Player switched to a different character.
        /// </summary>
        [Command]
        private void CmdSwitchCharacter(string newCharacterId, Vector3 position, float health, float maxHealth)
        {
            string oldId = selectedCharacterId;
            selectedCharacterId = newCharacterId;
            hasCharacter = true;
            syncedPosition = position;
            syncedHealth = health;
            syncedMaxHealth = maxHealth;
            syncedIsAlive = true;

            Debug.Log($"[NetworkPlayer] Server: Player switched character from '{oldId}' to '{newCharacterId}'");

            // Notify all clients to recreate the mirror character
            RpcSwitchMirrorCharacter(newCharacterId);
        }

        [Command]
        private void CmdUpdatePosition(Vector3 position)
        {
            syncedPosition = position;
        }

        [Command]
        private void CmdUpdateFacing(bool facingRight)
        {
            syncedFacingRight = facingRight;
        }

        [Command]
        private void CmdUpdateHealth(float health, float maxHealth, bool isAlive)
        {
            syncedHealth = health;
            syncedMaxHealth = maxHealth;
            syncedIsAlive = isAlive;
        }

        #endregion

        #region ClientRpc (Server -> All Clients)

        /// <summary>
        /// Tell all clients to create a mirror character for this player.
        /// </summary>
        [ClientRpc]
        private void RpcCreateMirrorCharacter(string characterId)
        {
            // Don't create a mirror for our own character
            if (isOwned) return;

            Debug.Log($"[NetworkPlayer] RPC: Creating mirror character '{characterId}' for remote player '{playerName}'");
            StartCoroutine(CreateMirrorCharacterDelayed(characterId));
        }

        /// <summary>
        /// Tell all clients to switch the mirror character for this player.
        /// </summary>
        [ClientRpc]
        private void RpcSwitchMirrorCharacter(string newCharacterId)
        {
            // Don't affect our own character
            if (isOwned) return;

            Debug.Log($"[NetworkPlayer] RPC: Switching mirror character to '{newCharacterId}' for remote player '{playerName}'");

            // Destroy old mirror and create new one
            DestroyMirrorCharacter();
            StartCoroutine(CreateMirrorCharacterDelayed(newCharacterId));
        }

        #endregion

        #region SyncVar Hooks

        private void OnCharacterIdChanged(string oldId, string newId)
        {
            Debug.Log($"[NetworkPlayer] Character ID changed: {oldId} -> {newId}");
        }

        private void OnHasCharacterChanged(bool oldValue, bool newValue)
        {
            if (!isOwned && newValue && !string.IsNullOrEmpty(selectedCharacterId))
            {
                // Remote player now has a character - create mirror if not already done
                if (!mirrorCharacterCreated)
                {
                    StartCoroutine(CreateMirrorCharacterDelayed(selectedCharacterId));
                }
            }
            else if (!isOwned && !newValue)
            {
                // Remote player no longer has a character - destroy mirror
                DestroyMirrorCharacter();
            }
        }

        private void OnAliveChanged(bool oldAlive, bool newAlive)
        {
            if (isOwned) return;

            if (!newAlive && MirrorCharacter != null)
            {
                // Remote character died - play death animation
                if (MirrorCharacter.CharAnimator != null)
                {
                    MirrorCharacter.CharAnimator.PlayDeath();
                }
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// Find a CharacterData ScriptableObject by its characterId.
        /// Searches GameCharacterManager's list, then all loaded ScriptableObjects.
        /// </summary>
        private CharacterData FindCharacterDataById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;

            // First check GameCharacterManager's configured list
            GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm != null && gcm.playerCharacterDataList != null)
            {
                foreach (CharacterData data in gcm.playerCharacterDataList)
                {
                    if (data != null && data.characterId == characterId)
                        return data;
                }
            }

            // Fallback: search all loaded CharacterData ScriptableObjects in memory
            CharacterData[] allLoaded = Resources.FindObjectsOfTypeAll<CharacterData>();
            foreach (CharacterData data in allLoaded)
            {
                if (data != null && data.characterId == characterId)
                    return data;
            }

            Debug.LogWarning($"[NetworkPlayer] CharacterData not found for ID: {characterId}");
            return null;
        }

        #endregion
    }
}
