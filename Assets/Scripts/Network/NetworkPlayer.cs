using UnityEngine;
using Mirror;
using Steamworks;
using System.Collections;
using System.Collections.Generic;

namespace PetGame.Network
{
    /// <summary>
    /// Network player proxy spawned for each connected player.
    /// Syncs character info, position, animation state, and routes combat damage.
    /// Remote players create a local "mirror character" based on synced data.
    /// </summary>
    public class NetworkPlayer : NetworkBehaviour
    {
        #region SyncVars

        [SyncVar(hook = nameof(OnCharacterIdChanged))]
        private string selectedCharacterId = "";

        [SyncVar(hook = nameof(OnPlayerNameChanged))]
        private string playerName = "";

        [SyncVar(hook = nameof(OnHasCharacterChanged))]
        private bool hasCharacter = false;

        [SyncVar]
        private Vector3 syncedPosition;

        [SyncVar]
        private bool syncedFacingRight = true;

        [SyncVar]
        private float syncedHealth;

        [SyncVar]
        private float syncedMaxHealth;

        [SyncVar(hook = nameof(OnAliveChanged))]
        private bool syncedIsAlive = true;

        /// <summary>Synced animation state name (Idle, Walk, Attack, Hit, Death, etc.)</summary>
        [SyncVar(hook = nameof(OnAnimStateChanged))]
        private string syncedAnimState = "Idle";

        /// <summary>Synced attack index for combo system.</summary>
        [SyncVar]
        private int syncedAttackIndex = 0;

        #endregion

        #region Properties

        public string SelectedCharacterId => selectedCharacterId;
        public string PlayerName => playerName;
        public bool HasCharacter => hasCharacter;
        public CharacterEntity LocalCharacter { get; private set; }
        public CharacterEntity MirrorCharacter { get; private set; }

        #endregion

        #region Settings

        [Header("Sync Settings")]
        [SerializeField] private float syncRate = 20f;
        [SerializeField] private float positionThreshold = 0.01f;
        [SerializeField] private float interpolationSpeed = 15f;
        [SerializeField] private float remotePlayerXOffset = 2f;

        #endregion

        #region Private Fields

        private float syncTimer;
        private Vector3 lastSentPosition;
        private bool mirrorCharacterCreated = false;
        private string lastSentAnimState = "";
        private int lastSentAttackIndex = 0;
        private bool lastSentFacingRight = true;
        private bool _mirrorInKnockback = false;
        private float lastSentHealth;
        private float lastSentMaxHealth;
        private bool lastSentIsAlive = true;
        private bool lastSentKnockbackState = false;

        /// <summary>Server-side counter for generating unique portal IDs.</summary>
        private static uint nextPortalId = 1;

        #endregion

        #region Lifecycle

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            Debug.Log("[NetworkPlayer] Local player started.");

            if (MirrorNetworkManager.singleton != null)
            {
                MirrorNetworkManager.singleton.LocalPlayer = this;
            }

            if (SteamManager.Initialized)
            {
                string steamName = SteamFriends.GetPersonaName();
                CmdSetPlayerName(steamName);
            }

            StartCoroutine(RegisterLocalCharacterDelayed());
        }

        public override void OnStartClient()
        {
            base.OnStartClient();

            if (!isOwned && hasCharacter && !string.IsNullOrEmpty(selectedCharacterId))
            {
                StartCoroutine(CreateMirrorCharacterDelayed(selectedCharacterId));
            }
        }

        private IEnumerator CreateMirrorCharacterDelayed(string characterId)
        {
            float timeout = 5f;
            float elapsed = 0f;
            while (Object.FindObjectOfType<GameCharacterManager>() == null && elapsed < timeout)
            {
                yield return new WaitForSeconds(0.1f);
                elapsed += 0.1f;
            }

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
            DestroyMirrorCharacter();

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
                UpdateLocalPlayerSync();
            }
            else
            {
                UpdateMirrorCharacter();
            }
        }

        #endregion

        #region Local Player - Character Registration

        private IEnumerator RegisterLocalCharacterDelayed()
        {
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
            bool isClientOnly = MirrorNetworkManager.singleton != null && MirrorNetworkManager.singleton.IsClientOnly;

            if (isClientOnly)
            {
                // Pause local enemy spawner and clear existing local enemies
                // Client will see mirror enemies synced from the host instead
                EnemySpawner localSpawner = Object.FindObjectOfType<EnemySpawner>();
                if (localSpawner != null)
                {
                    localSpawner.PauseSpawning();
                    localSpawner.ClearAllEnemies();
                    Debug.Log("[NetworkPlayer] Client-only mode: Local EnemySpawner paused and enemies cleared.");
                }

                if (gcm.PlayerCharacters.Count == 0)
                {
                    if (gcm.playerCharacterDataList != null && gcm.playerCharacterDataList.Length > 0)
                    {
                        CharacterData data = gcm.playerCharacterDataList[0];
                        Vector3 spawnPos = gcm.GetPlayerSpawnPosition();

                        CharacterEntity entity = gcm.CreatePlayerCharacter(data, spawnPos);
                        gcm.PlayerCharacters.Add(entity);

                        Debug.Log($"[NetworkPlayer] Client-only mode: Created local character '{data.characterId}' in host's scene.");
                    }
                }
            }

            // Wait for the character to be ready
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

                    CmdRegisterCharacter(charId,
                        currentChar.transform.position,
                        currentChar.RuntimeStats.currentHealth,
                        currentChar.RuntimeStats.maxHealth);

                    // Set the local player's Steam name on the character name tag
                    if (SteamManager.Initialized)
                    {
                        string steamName = SteamFriends.GetPersonaName();
                        LocalCharacter.SetPlayerName(steamName);
                    }
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

        public void OnLocalCharacterSwitched(string newCharacterId)
        {
            if (!isOwned) return;
            if (string.IsNullOrEmpty(newCharacterId)) return;

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

            // Facing direction (only send when changed)
            SpriteRenderer sr = LocalCharacter.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                bool facingRight = !sr.flipX;
                if (facingRight != lastSentFacingRight)
                {
                    lastSentFacingRight = facingRight;
                    CmdUpdateFacing(facingRight);
                }
            }

            // Health (only send when changed)
            if (LocalCharacter.RuntimeStats != null)
            {
                float currentHealth = LocalCharacter.RuntimeStats.currentHealth;
                float maxHealth = LocalCharacter.RuntimeStats.maxHealth;
                bool isAlive = LocalCharacter.RuntimeStats.IsAlive;
                if (Mathf.Abs(currentHealth - lastSentHealth) > 0.1f ||
                    Mathf.Abs(maxHealth - lastSentMaxHealth) > 0.1f ||
                    isAlive != lastSentIsAlive)
                {
                    lastSentHealth = currentHealth;
                    lastSentMaxHealth = maxHealth;
                    lastSentIsAlive = isAlive;
                    CmdUpdateHealth(currentHealth, maxHealth, isAlive);
                }
            }

            // Animation state sync
            SyncAnimationState();

            // Knockback state sync (Bug4 fix: prevent mirror character jitter during knockback)
            SyncKnockbackState();
        }

        /// <summary>
        /// Sync knockback state to server so mirror characters can simulate knockback locally
        /// instead of relying on position interpolation (which causes jitter).
        /// </summary>
        private void SyncKnockbackState()
        {
            if (LocalCharacter == null) return;

            KnockbackController kb = LocalCharacter.GetComponent<KnockbackController>();
            if (kb == null) return;

            bool isInKnockback = kb.IsInKnockback;
            if (isInKnockback != lastSentKnockbackState)
            {
                lastSentKnockbackState = isInKnockback;
                if (isInKnockback)
                {
                    // Send current position as the knockback start position
                    // Mirror character will use the synced position to start its own knockback simulation
                    CmdStartKnockback(LocalCharacter.transform.position);
                }
                else
                {
                    CmdEndKnockback(LocalCharacter.transform.position);
                }
            }
        }

        /// <summary>
        /// Read the current animation state from the local character's Animator and send to server.
        /// </summary>
        private void SyncAnimationState()
        {
            if (LocalCharacter == null) return;

            Animator animator = LocalCharacter.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            // Get current state info from base layer
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            string currentState = GetAnimStateName(animator, stateInfo);

            // Get attack index if in attack state
            int attackIndex = 0;
            if (animator.parameterCount > 0)
            {
                foreach (var param in animator.parameters)
                {
                    if (param.nameHash == Animator.StringToHash("AttackIndex") && param.type == AnimatorControllerParameterType.Int)
                    {
                        attackIndex = animator.GetInteger("AttackIndex");
                        break;
                    }
                }
            }

            // Only send if changed
            if (currentState != lastSentAnimState || attackIndex != lastSentAttackIndex)
            {
                lastSentAnimState = currentState;
                lastSentAttackIndex = attackIndex;
                CmdUpdateAnimState(currentState, attackIndex);
            }
        }

        /// <summary>
        /// Determine the animation state name from AnimatorStateInfo.
        /// Checks common state names used in the project.
        /// </summary>
        private string GetAnimStateName(Animator animator, AnimatorStateInfo stateInfo)
        {
            // Check Bool parameters to determine state
            if (animator.GetBool("Death")) return "Death";
            if (animator.GetBool("Hit")) return "Hit";
            if (animator.GetBool("Attack")) return "Attack";

            // Check skill bools
            if (HasParam(animator, "SkillOne") && animator.GetBool("SkillOne")) return "SkillOne";
            if (HasParam(animator, "SkillTwo") && animator.GetBool("SkillTwo")) return "SkillTwo";
            if (HasParam(animator, "SkillThree") && animator.GetBool("SkillThree")) return "SkillThree";
            if (HasParam(animator, "SkillFour") && animator.GetBool("SkillFour")) return "SkillFour";
            if (HasParam(animator, "Skill") && animator.GetBool("Skill")) return "Skill";

            if (animator.GetBool("Walk")) return "Walk";
            if (animator.GetBool("Idle")) return "Idle";

            return "Idle";
        }

        private bool HasParam(Animator animator, string paramName)
        {
            foreach (var param in animator.parameters)
            {
                if (param.name == paramName) return true;
            }
            return false;
        }

        #endregion

        #region Mirror Character - Remote Representation

        private void CreateMirrorCharacter(string characterId)
        {
            if (mirrorCharacterCreated) return;
            if (string.IsNullOrEmpty(characterId)) return;

            CharacterData charData = FindCharacterDataById(characterId);
            if (charData == null)
            {
                Debug.LogWarning($"[NetworkPlayer] Cannot create mirror character: CharacterData not found for ID '{characterId}'");
                return;
            }

            Vector3 spawnPos = syncedPosition;
            if (spawnPos == Vector3.zero)
            {
                GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
                if (gcm != null)
                {
                    spawnPos = gcm.GetPlayerSpawnPosition() + new Vector3(remotePlayerXOffset, 0f, 0f);
                }
            }

            GameCharacterManager gcm2 = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm2 != null)
            {
                MirrorCharacter = gcm2.CreatePlayerCharacter(charData, spawnPos);
                DisableMirrorCharacterControllers();
                MirrorCharacter.gameObject.name = $"MirrorPlayer_{charData.characterName}_{playerName}";
                mirrorCharacterCreated = true;

                // Tag this as a mirror character so combat systems can skip or
                // route damage through the network instead of applying it directly.
                MirrorCharacterTag mirrorTag = MirrorCharacter.GetComponent<MirrorCharacterTag>();
                if (mirrorTag == null)
                    mirrorTag = MirrorCharacter.gameObject.AddComponent<MirrorCharacterTag>();
                // connectionToClient is only valid on the server side.
                // On clients, use netId as a fallback identifier.
                if (connectionToClient != null)
                    mirrorTag.ownerConnectionId = (int)connectionToClient.connectionId;
                else
                    mirrorTag.ownerConnectionId = (int)netId;

                // Set the remote player's Steam name on the mirror character name tag
                MirrorCharacter.SetPlayerName(playerName);

                Debug.Log($"[NetworkPlayer] Mirror character created: {charData.characterName} for player '{playerName}'");
            }
            else
            {
                Debug.LogError("[NetworkPlayer] GameCharacterManager not found, cannot create mirror character.");
            }
        }

        private void DestroyMirrorCharacter()
        {
            if (MirrorCharacter != null)
            {
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

        private void DisableMirrorCharacterControllers()
        {
            if (MirrorCharacter == null) return;

            var ai = MirrorCharacter.GetComponent<PetGame.AI.AIController>();
            if (ai != null) ai.enabled = false;

            var manual = MirrorCharacter.GetComponent<ManualController>();
            if (manual != null) manual.enabled = false;

            var controlMode = MirrorCharacter.GetComponent<ControlModeManager>();
            if (controlMode != null) controlMode.enabled = false;

            // Disable CombatSystem on mirror characters - they don't attack locally
            var combat = MirrorCharacter.GetComponent<CombatSystem>();
            if (combat != null) combat.enabled = false;

            // Disable KnockbackController on mirror characters - knockback is synced via RPC,
            // not driven by local TakeDamage. This prevents local Boss attacks from causing
            // jitter/knockback on mirror characters.
            var knockback = MirrorCharacter.GetComponent<KnockbackController>();
            if (knockback != null) knockback.enabled = false;

            // Disable AnimEventReceiver so attack animation events don't trigger damage
            var animEvent = MirrorCharacter.GetComponent<AnimEventReceiver>();
            if (animEvent != null) animEvent.enabled = false;

            // Set Rigidbody2D to Kinematic so MirrorCharacter doesn't participate in
            // physics simulation. Its position is driven entirely by network sync
            // (Vector3.Lerp in UpdateMirrorCharacter), not by the physics engine.
            // This prevents MirrorEnemy/Boss collisions from rotating or pushing the sprite.
            var rb = MirrorCharacter.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
                MirrorCharacter.transform.rotation = Quaternion.identity;
            }
        }

        private void UpdateMirrorCharacter()
        {
            if (MirrorCharacter == null) return;

            // During knockback, skip position interpolation to avoid jitter.
            // The mirror character's position is driven by RpcStartKnockback/RpcEndKnockback.
            if (!_mirrorInKnockback)
            {
                // Smooth position interpolation
                MirrorCharacter.transform.position = Vector3.Lerp(
                    MirrorCharacter.transform.position,
                    syncedPosition,
                    Time.deltaTime * interpolationSpeed
                );
            }

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

                // Update the HealthBar UI to reflect the synced health
                HealthBar healthBar = MirrorCharacter.GetComponentInChildren<HealthBar>();
                if (healthBar != null)
                {
                    healthBar.UpdateHealth(syncedHealth, syncedMaxHealth);
                }
            }
        }

        /// <summary>
        /// Apply animation state to the mirror character's Animator.
        /// </summary>
        private void ApplyAnimStateToMirror(string oldState, string newState)
        {
            if (MirrorCharacter == null) return;

            Animator animator = MirrorCharacter.GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null) return;

            // Reset all bools first
            SetBoolSafe(animator, "Idle", false);
            SetBoolSafe(animator, "Walk", false);
            SetBoolSafe(animator, "Attack", false);
            SetBoolSafe(animator, "Hit", false);
            SetBoolSafe(animator, "Death", false);
            SetBoolSafe(animator, "Skill", false);
            SetBoolSafe(animator, "SkillOne", false);
            SetBoolSafe(animator, "SkillTwo", false);
            SetBoolSafe(animator, "SkillThree", false);
            SetBoolSafe(animator, "SkillFour", false);

            // Set the target state bool
            switch (newState)
            {
                case "Idle":
                    SetBoolSafe(animator, "Idle", true);
                    break;
                case "Walk":
                    SetBoolSafe(animator, "Walk", true);
                    break;
                case "Attack":
                    SetBoolSafe(animator, "Attack", true);
                    SetIntSafe(animator, "AttackIndex", syncedAttackIndex);
                    break;
                case "Hit":
                    SetBoolSafe(animator, "Hit", true);
                    break;
                case "Death":
                    SetBoolSafe(animator, "Death", true);
                    break;
                case "Skill":
                    SetBoolSafe(animator, "Skill", true);
                    break;
                case "SkillOne":
                    SetBoolSafe(animator, "SkillOne", true);
                    break;
                case "SkillTwo":
                    SetBoolSafe(animator, "SkillTwo", true);
                    break;
                case "SkillThree":
                    SetBoolSafe(animator, "SkillThree", true);
                    break;
                case "SkillFour":
                    SetBoolSafe(animator, "SkillFour", true);
                    break;
            }
        }

        private void SetBoolSafe(Animator animator, string param, bool value)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == param && p.type == AnimatorControllerParameterType.Bool)
                {
                    animator.SetBool(param, value);
                    return;
                }
            }
        }

        private void SetIntSafe(Animator animator, string param, int value)
        {
            foreach (var p in animator.parameters)
            {
                if (p.name == param && p.type == AnimatorControllerParameterType.Int)
                {
                    animator.SetInteger(param, value);
                    return;
                }
            }
        }

        #endregion

        #region Network Combat - Client Damage Routing

        /// <summary>
        /// Called by client's CombatSystem when it hits a mirror enemy.
        /// Routes the damage request to the server which applies it to the real enemy.
        /// </summary>
        public void RequestDamageEnemy(uint enemyNetId, float damage)
        {
            if (!isOwned) return;
            CmdRequestDamageEnemy(enemyNetId, damage);
        }

        [Command]
        private void CmdRequestDamageEnemy(uint enemyNetId, float damage)
        {
            // Server-side: find the real enemy and apply damage
            NetworkEnemySpawner spawner = Object.FindObjectOfType<NetworkEnemySpawner>();
            if (spawner != null)
            {
                spawner.ApplyDamageToEnemy(enemyNetId, damage, null);
            }
        }

        #endregion

        #region Network Skill Effects - Projectile/Summon Sync

        /// <summary>
        /// Called by local ProjectileSkillEffectData to sync projectile spawn to all clients.
        /// </summary>
        public void RequestSpawnProjectile(string prefabName, Vector3 startPos, Vector3 targetPos,
            float flightDuration, float arcHeight, float damage, CharacterType casterType)
        {
            if (!isOwned) return;
            CmdSpawnProjectile(prefabName, startPos, targetPos, flightDuration, arcHeight, damage, (int)casterType);
        }

        [Command]
        private void CmdSpawnProjectile(string prefabName, Vector3 startPos, Vector3 targetPos,
            float flightDuration, float arcHeight, float damage, int casterType)
        {
            // Relay to all clients except the sender
            RpcSpawnProjectile(prefabName, startPos, targetPos, flightDuration, arcHeight, damage, casterType);
        }

        [ClientRpc]
        private void RpcSpawnProjectile(string prefabName, Vector3 startPos, Vector3 targetPos,
            float flightDuration, float arcHeight, float damage, int casterType)
        {
            // Skip on the owner (they already spawned it locally)
            if (isOwned) return;

            // Load prefab and spawn projectile locally for visual effect
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/Entity/Characters/{prefabName}");
            if (prefab == null)
            {
                // Try loading from pool registration
                if (PoolMgr.Instance.HasPrefab(prefabName))
                {
                    prefab = PoolMgr.Instance.GetPrefab(prefabName);
                }
            }
            if (prefab == null)
            {
                Debug.LogWarning($"[NetworkPlayer] Cannot find projectile prefab: {prefabName}");
                return;
            }

            // Ensure prefab is registered with pool
            PoolMgr.Instance.SetPrefab(prefabName, prefab);
            GameObject projectileObj = PoolMgr.Instance.GetNode(prefabName);
            if (projectileObj == null) return;

            projectileObj.transform.position = startPos;
            projectileObj.transform.rotation = Quaternion.identity;
            projectileObj.SetActive(true);

            ProjectileController controller = projectileObj.GetComponent<ProjectileController>();
            if (controller == null)
            {
                PoolMgr.Instance.PutNode(projectileObj);
                return;
            }

            controller.ResetState();

            // Launch as visual-only (damage = 0 on remote clients, host handles real damage)
            controller.Launch(
                startPos,
                targetPos,
                flightDuration,
                arcHeight,
                0f, // No damage on remote - host handles damage
                (CharacterType)casterType,
                () => { PoolMgr.Instance.PutNode(projectileObj); }
            );
        }

        /// <summary>
        /// Called by local SummonSkillEffectData to sync summon spawn to all clients.
        /// </summary>
        public void RequestSpawnSummon(string prefabName, Vector3[] positions, string factionTag)
        {
            if (!isOwned) return;
            CmdSpawnSummon(prefabName, positions, factionTag);
        }

        [Command]
        private void CmdSpawnSummon(string prefabName, Vector3[] positions, string factionTag)
        {
            RpcSpawnSummon(prefabName, positions, factionTag);
        }

        [ClientRpc]
        private void RpcSpawnSummon(string prefabName, Vector3[] positions, string factionTag)
        {
            // Skip on the owner (they already spawned it locally)
            if (isOwned) return;

            // Load prefab
            GameObject prefab = Resources.Load<GameObject>($"Prefabs/Entity/Characters/{prefabName}");
            if (prefab == null)
            {
                Debug.LogWarning($"[NetworkPlayer] Cannot find summon prefab: {prefabName}");
                return;
            }

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject summonObj = Object.Instantiate(prefab, positions[i], Quaternion.identity);
                summonObj.tag = factionTag;
                summonObj.name = $"RemoteSummon_{prefabName}_{i}";

                // Initialize entity
                CharacterEntity entity = summonObj.GetComponent<CharacterEntity>();
                if (entity != null && !entity.IsInitialized && entity.characterData != null)
                {
                    entity.Initialize(entity.characterData);
                }

                // Initialize AI
                PetGame.AI.AIController ai = summonObj.GetComponent<PetGame.AI.AIController>();
                if (ai != null) ai.InitializeAI();

                // Auto-destroy after a reasonable duration
                Object.Destroy(summonObj, 15f);
            }
        }

        /// <summary>
        /// Called by PortalManager to request the server to spawn a portal.
        /// Allows both host and client players to spawn portals.
        /// The portal is spawned locally on each client's own Canvas (not via NetworkServer.Spawn),
        /// because Portal is a UI element that must be a child of Canvas to render correctly.
        /// </summary>
        public void RequestSpawnPortal(Vector3 worldPos)
        {
            if (!isOwned) return;
            CmdSpawnPortal(worldPos);
        }

        [Command]
        private void CmdSpawnPortal(Vector3 worldPos)
        {
            // Generate a unique portal ID on the server
            uint portalId = nextPortalId++;

            // Server picks a random level data index so all portals have valid data
            int levelDataIndex = -1;
            var levelListMgr = SpecialLevelListManager.Instance;
            if (levelListMgr != null)
            {
                levelDataIndex = levelListMgr.GetRandomLevelDataIndex();
            }

            // Spawn portal only on the requesting player's client (not on all clients).
            // Portal is a personal UI element that only appears on the spawner's own desktop.
            TargetRpcSpawnPortal(connectionToClient, worldPos, portalId, levelDataIndex);
        }

        /// <summary>
        /// TargetRpc: Spawn a portal only on the requesting player's client.
        /// Portal is a personal UI element that only appears on the player's own Canvas/desktop.
        /// </summary>
        [TargetRpc]
        private void TargetRpcSpawnPortal(NetworkConnection target, Vector3 worldPos, uint portalId, int levelDataIndex)
        {
            PortalManager portalManager = Object.FindObjectOfType<PortalManager>();
            if (portalManager != null)
            {
                portalManager.SpawnPortalLocally(worldPos, portalId, levelDataIndex);
                Debug.Log($"[NetworkPlayer] Portal spawned locally at {worldPos}, portalId={portalId}, levelDataIndex={levelDataIndex}.");
            }
        }

        /// <summary>
        /// TargetRpc: Called by the host to apply Boss damage to a specific client's LocalCharacter.
        /// The host detects that Boss hit a MirrorCharacter and routes the damage here
        /// so the owning client applies it to their real LocalCharacter.
        /// After taking damage, the client syncs updated HP back to the server via CmdUpdateHealth.
        /// </summary>
        [TargetRpc]
        public void TargetTakeDamageFromBoss(float damage)
        {
            if (LocalCharacter == null || LocalCharacter.RuntimeStats == null) return;

            // Apply damage locally on the owning client
            LocalCharacter.TakeDamage(damage, null);

            // Sync updated HP back to the server so all clients see the change
            float currentHealth = LocalCharacter.RuntimeStats.currentHealth;
            float maxHealth = LocalCharacter.RuntimeStats.maxHealth;
            bool isAlive = LocalCharacter.RuntimeStats.IsAlive;
            CmdUpdateHealth(currentHealth, maxHealth, isAlive);

            Debug.Log($"[NetworkPlayer] Received Boss damage={damage} on client. HP={currentHealth}/{maxHealth}");
        }

        #endregion

        #region Commands (Client -> Server)

        [Command]
        private void CmdSetPlayerName(string name)
        {
            playerName = name;
            Debug.Log($"[NetworkPlayer] Player name set: {name}");
        }

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
            RpcCreateMirrorCharacter(characterId);
        }

        [Command]
        private void CmdRegisterNoCharacter()
        {
            selectedCharacterId = "";
            hasCharacter = false;
            Debug.Log("[NetworkPlayer] Server: Player has no character.");
        }

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

        [Command]
        private void CmdUpdateAnimState(string animState, int attackIndex)
        {
            syncedAnimState = animState;
            syncedAttackIndex = attackIndex;
        }

        [Command]
        private void CmdStartKnockback(Vector3 currentPosition)
        {
            syncedPosition = currentPosition;
            RpcStartKnockback(currentPosition);
        }

        [Command]
        private void CmdEndKnockback(Vector3 landingPosition)
        {
            syncedPosition = landingPosition;
            RpcEndKnockback(landingPosition);
        }

        #endregion

        #region ClientRpc (Server -> All Clients)

        [ClientRpc]
        private void RpcCreateMirrorCharacter(string characterId)
        {
            if (isOwned) return;
            Debug.Log($"[NetworkPlayer] RPC: Creating mirror character '{characterId}' for remote player '{playerName}'");
            StartCoroutine(CreateMirrorCharacterDelayed(characterId));
        }

        [ClientRpc]
        private void RpcSwitchMirrorCharacter(string newCharacterId)
        {
            if (isOwned) return;
            Debug.Log($"[NetworkPlayer] RPC: Switching mirror character to '{newCharacterId}' for remote player '{playerName}'");
            DestroyMirrorCharacter();
            StartCoroutine(CreateMirrorCharacterDelayed(newCharacterId));
        }

        /// <summary>
        /// RPC: Start knockback simulation on mirror character.
        /// Instead of relying on position interpolation (which causes jitter),
        /// the mirror character simulates knockback locally using the same physics.
        /// </summary>
        [ClientRpc]
        private void RpcStartKnockback(Vector3 currentPosition)
        {
            if (isOwned) return;
            if (MirrorCharacter == null) return;

            // Snap mirror character to the knockback start position
            MirrorCharacter.transform.position = currentPosition;

            // Enable knockback flag so UpdateMirrorCharacter skips position interpolation
            _mirrorInKnockback = true;
        }

        /// <summary>
        /// RPC: End knockback on mirror character, snap to landing position.
        /// </summary>
        [ClientRpc]
        private void RpcEndKnockback(Vector3 landingPosition)
        {
            if (isOwned) return;
            if (MirrorCharacter == null) return;

            MirrorCharacter.transform.position = landingPosition;
            _mirrorInKnockback = false;
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
                if (!mirrorCharacterCreated)
                {
                    StartCoroutine(CreateMirrorCharacterDelayed(selectedCharacterId));
                }
            }
            else if (!isOwned && !newValue)
            {
                DestroyMirrorCharacter();
            }
        }

        private void OnAliveChanged(bool oldAlive, bool newAlive)
        {
            if (isOwned) return;

            if (!newAlive && MirrorCharacter != null)
            {
                if (MirrorCharacter.CharAnimator != null)
                {
                    MirrorCharacter.CharAnimator.PlayDeath();
                }
            }
        }

        private void OnAnimStateChanged(string oldState, string newState)
        {
            if (isOwned) return;
            ApplyAnimStateToMirror(oldState, newState);
        }

        private void OnPlayerNameChanged(string oldName, string newName)
        {
            if (isOwned) return;

            // Update the mirror character's name tag if it exists
            if (MirrorCharacter != null)
            {
                MirrorCharacter.SetPlayerName(newName);
            }
        }

        #endregion

        #region Utility

        private CharacterData FindCharacterDataById(string characterId)
        {
            if (string.IsNullOrEmpty(characterId)) return null;

            GameCharacterManager gcm = Object.FindObjectOfType<GameCharacterManager>();
            if (gcm != null && gcm.playerCharacterDataList != null)
            {
                foreach (CharacterData data in gcm.playerCharacterDataList)
                {
                    if (data != null && data.characterId == characterId)
                        return data;
                }
            }

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
