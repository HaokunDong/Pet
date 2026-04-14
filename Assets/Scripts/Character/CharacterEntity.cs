using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Core component attached to every character GameObject (player, enemy, boss).
    /// Holds the static data reference and runtime stats instance.
    /// </summary>
    public class CharacterEntity : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Static character data template (ScriptableObject)")]
        public CharacterData characterData;

        /// <summary>
        /// Runtime stats instance, independent per character.
        /// </summary>
        public RuntimeCharacterStats RuntimeStats { get; private set; }

        /// <summary>
        /// Reference to the CharacterAnimator component (set during init or via GetComponent).
        /// </summary>
        public CharacterAnimator CharAnimator { get; private set; }

        /// <summary>
        /// Whether this entity has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Event fired when this character dies.
        /// </summary>
        public event System.Action<CharacterEntity> OnDeath;

        /// <summary>
        /// Event fired when this character takes damage.
        /// </summary>
        public event System.Action<CharacterEntity, float> OnDamageTaken;

        private void Awake()
        {
            if (characterData != null)
            {
                Initialize(characterData);
            }
        }

        /// <summary>
        /// Initialize the entity with a CharacterData template.
        /// Creates a runtime stats copy so multiple instances don't share state.
        /// </summary>
        public void Initialize(CharacterData data)
        {
            characterData = data;
            RuntimeStats = new RuntimeCharacterStats();
            RuntimeStats.InitFromData(data);

            CharAnimator = GetComponent<CharacterAnimator>();

            IsInitialized = true;
        }

        private void Update()
        {
            if (!IsInitialized || !RuntimeStats.IsAlive) return;

            // Tick skill cooldowns
            RuntimeStats.UpdateCooldowns(Time.deltaTime);
        }

        /// <summary>
        /// Apply damage to this character.
        /// Actual damage = attackPower - defense (min 1).
        /// </summary>
        public void TakeDamage(float attackPower)
        {
            if (!RuntimeStats.IsAlive) return;

            float actualDamage = Mathf.Max(1f, attackPower - RuntimeStats.defense);
            RuntimeStats.currentHealth -= actualDamage;

            OnDamageTaken?.Invoke(this, actualDamage);

            // Play hit animation
            if (CharAnimator != null)
            {
                CharAnimator.PlayHit();
            }

            if (!RuntimeStats.IsAlive)
            {
                Die();
            }
        }

        /// <summary>
        /// Handle character death: play animation, fire event, recycle via pool.
        /// </summary>
        private void Die()
        {
            OnDeath?.Invoke(this);

            if (CharAnimator != null)
            {
                CharAnimator.PlayDeath();
            }

            // Delay recycle to allow death animation to play
            Invoke(nameof(Recycle), 1f);
        }

        /// <summary>
        /// Recycle this character back to the object pool.
        /// </summary>
        private void Recycle()
        {
            CleanUp();
            PoolMgr.Instance.PutNode(gameObject);
        }

        /// <summary>
        /// Clean up all runtime state and event listeners.
        /// </summary>
        private void CleanUp()
        {
            OnDeath = null;
            OnDamageTaken = null;
            IsInitialized = false;
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

        private void OnDestroy()
        {
            CleanUp();
        }
    }
}
