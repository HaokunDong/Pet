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
        /// Reference to the HealthBar component displayed above this character.
        /// </summary>
        private HealthBar healthBar;

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

            // Create or re-initialize the health bar above this character
            InitializeHealthBar();

            IsInitialized = true;
        }

        /// <summary>
        /// Create or re-initialize the HealthBar child object.
        /// Handles both first-time creation and object pool reuse.
        /// </summary>
        private void InitializeHealthBar()
        {
            // Check if a HealthBar child already exists (object pool reuse)
            healthBar = GetComponentInChildren<HealthBar>(true);

            if (healthBar == null)
            {
                // Create a new HealthBar child object
                GameObject hbObj = new GameObject("HealthBar");
                hbObj.transform.SetParent(transform, false);
                healthBar = hbObj.AddComponent<HealthBar>();

                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                healthBar.Initialize(sr);
            }

            // Update to full health and show
            healthBar.UpdateHealth(RuntimeStats.currentHealth, RuntimeStats.maxHealth);
            healthBar.Show();
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

            Debug.Log($"[Combat] {gameObject.name} took {actualDamage} damage (ATK:{attackPower} - DEF:{RuntimeStats.defense}). " +
                      $"HP: {RuntimeStats.currentHealth}/{RuntimeStats.maxHealth}");

            OnDamageTaken?.Invoke(this, actualDamage);

            // Update health bar
            if (healthBar != null)
            {
                healthBar.UpdateHealth(RuntimeStats.currentHealth, RuntimeStats.maxHealth);
            }

            // Play hit animation
            if (CharAnimator != null)
            {
                CharAnimator.PlayHit();
            }
            else
            {
                Debug.LogWarning($"[Combat] {gameObject.name} has no CharAnimator — cannot play Hit animation. " +
                    "Ensure CharacterAnimator component is attached and Initialize() was called.");
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

            // Hide health bar on death
            if (healthBar != null)
            {
                healthBar.Hide();
            }

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

            // Hide health bar when recycled (will be re-shown on next Initialize)
            if (healthBar != null)
            {
                healthBar.Hide();
            }
        }

        private void OnDisable()
        {
            CancelInvoke();
        }

        private void OnDestroy()
        {
            CleanUp();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Draw attack range gizmo when the character is selected in the Scene view.
        /// Shows a prominent red semi-transparent circle.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (RuntimeStats == null) return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, RuntimeStats.attackRange);
            // Draw a filled disc for better visibility
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.1f);
            UnityEditor.Handles.DrawSolidDisc(transform.position, Vector3.forward, RuntimeStats.attackRange);
        }

        /// <summary>
        /// Draw attack range gizmo when the character is NOT selected (faint outline).
        /// </summary>
        private void OnDrawGizmos()
        {
            if (RuntimeStats == null) return;

            Gizmos.color = new Color(1f, 0f, 0f, 0.1f);
            Gizmos.DrawWireSphere(transform.position, RuntimeStats.attackRange);
        }
#endif
    }
}
