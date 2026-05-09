using UnityEngine;
using System.Collections.Generic;

namespace PetGame
{
    /// <summary>
    /// Manages character death cooldown logic.
    /// When a player character dies, this manager triggers the corresponding card's cooldown state.
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
        /// Called when a player character dies.
        /// Finds the corresponding card and starts its cooldown.
        /// </summary>
        public void OnPlayerCharacterDeath(CharacterEntity deadEntity)
        {
            if (deadEntity == null || deadEntity.characterData == null) return;

            CharacterData data = deadEntity.characterData;
            float cooldown = GetCooldownDuration(data);

            // Find the corresponding card
            CharacterCard card = FindCardForCharacter(data);
            if (card != null)
            {
                card.StartCooldown(cooldown);
                Debug.Log($"[CharacterDeathManager] '{data.characterName}' died. Card cooldown started: {cooldown}s");
            }
            else
            {
                Debug.LogWarning($"[CharacterDeathManager] Could not find card for '{data.characterName}'");
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
