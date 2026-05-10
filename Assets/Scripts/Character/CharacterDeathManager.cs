using UnityEngine;
using System.Collections.Generic;

namespace PetGame
{
    /// <summary>
    /// Manages character death cooldown logic.
    /// When a player character dies, this manager tracks the cooldown timer
    /// independently of the card UI visibility, ensuring cooldown progresses
    /// even when the card selection panel is closed.
    /// The corresponding CharacterCard's visuals are synced every frame
    /// (no-op when the card GameObject is inactive, refreshed when shown again).
    /// Attach to a persistent GameObject in the scene (or use DontDestroyOnLoad).
    /// </summary>
    public class CharacterDeathManager : MonoBehaviour
    {
        public static CharacterDeathManager Instance { get; private set; }

        [Header("Cooldown Settings")]
        [Tooltip("Global respawn cooldown in seconds. Used when CharacterData.respawnCooldown is 0.")]
        [SerializeField] private float globalCooldown = 30f;

        /// <summary>
        /// Cached reference to the CardSplineDistributor for finding cards.
        /// </summary>
        private CardSplineDistributor cardDistributor;

        /// <summary>
        /// Active cooldown entries: CharacterData -> remaining seconds.
        /// Maintained by this manager (always active), independent of card UI visibility.
        /// </summary>
        private readonly Dictionary<CharacterData, float> activeCooldowns = new Dictionary<CharacterData, float>();

        /// <summary>
        /// Reusable buffer for keys to avoid allocations during iteration.
        /// </summary>
        private readonly List<CharacterData> cooldownKeysBuffer = new List<CharacterData>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        /// <summary>
        /// Get the effective cooldown duration for a character.
        /// Uses per-character value if set, otherwise falls back to global.
        /// </summary>
        public float GetCooldownDuration(CharacterData data)
        {
            if (data != null && data.respawnCooldown > 0f)
                return data.respawnCooldown;
            return globalCooldown;
        }

        /// <summary>
        /// Returns true if the given character is currently in cooldown.
        /// </summary>
        public bool IsInCooldown(CharacterData data)
        {
            if (data == null) return false;
            return activeCooldowns.ContainsKey(data);
        }

        /// <summary>
        /// Get the remaining cooldown seconds for the given character (0 if not in cooldown).
        /// </summary>
        public float GetRemainingCooldown(CharacterData data)
        {
            if (data == null) return 0f;
            return activeCooldowns.TryGetValue(data, out float remaining) ? remaining : 0f;
        }

        /// <summary>
        /// Called when a player character dies.
        /// Registers a cooldown entry for the character; the timer ticks down
        /// every frame regardless of card UI state.
        /// </summary>
        public void OnPlayerCharacterDeath(CharacterEntity deadEntity)
        {
            if (deadEntity == null || deadEntity.characterData == null) return;

            CharacterData data = deadEntity.characterData;
            float cooldown = GetCooldownDuration(data);

            activeCooldowns[data] = cooldown;

            // Immediately apply cooldown visual to the matching card (if it exists).
            // Even if the card is inactive, the visual state is set so that the next
            // Show() will display the correct grey portrait + countdown text.
            CharacterCard card = FindCardForCharacter(data);
            if (card != null)
            {
                card.ApplyCooldownVisual(cooldown);
            }

            Debug.Log($"[CharacterDeathManager] '{data.characterName}' died. Cooldown started: {cooldown}s");
        }

        private void Update()
        {
            if (activeCooldowns.Count == 0) return;

            // Snapshot keys to allow modification during iteration
            cooldownKeysBuffer.Clear();
            foreach (var kv in activeCooldowns)
            {
                cooldownKeysBuffer.Add(kv.Key);
            }

            for (int i = 0; i < cooldownKeysBuffer.Count; i++)
            {
                CharacterData data = cooldownKeysBuffer[i];
                float remaining = activeCooldowns[data] - Time.deltaTime;

                if (remaining <= 0f)
                {
                    // Cooldown finished
                    activeCooldowns.Remove(data);

                    CharacterCard finishedCard = FindCardForCharacter(data);
                    if (finishedCard != null)
                    {
                        finishedCard.ClearCooldownVisual();
                    }
                }
                else
                {
                    activeCooldowns[data] = remaining;

                    // Sync visual on the card every frame.
                    // Setting text/colors on inactive UI is a no-op cost-wise and ensures
                    // the latest value is shown the moment the card panel is opened.
                    CharacterCard card = FindCardForCharacter(data);
                    if (card != null)
                    {
                        card.ApplyCooldownVisual(remaining);
                    }
                }
            }
        }

        /// <summary>
        /// Find the CharacterCard that corresponds to the given CharacterData.
        /// </summary>
        private CharacterCard FindCardForCharacter(CharacterData data)
        {
            if (cardDistributor == null)
                cardDistributor = FindObjectOfType<CardSplineDistributor>();

            if (cardDistributor == null) return null;

            foreach (CharacterCard card in cardDistributor.Cards)
            {
                if (card != null && card.Data == data)
                    return card;
            }

            return null;
        }
    }
}
