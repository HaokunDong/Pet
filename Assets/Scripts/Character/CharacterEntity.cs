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

            // Sync sprite orientation from CharacterData to CharacterAnimator
            if (CharAnimator != null)
            {
                CharAnimator.SyncDefaultFacing(data.defaultFacesRight);
            }

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
        /// Get the current facing sign for Gizmo drawing.
        /// Returns 1 (right) or -1 (left).
        /// </summary>
        private float GetFacingSign()
        {
            if (CharAnimator != null)
            {
                float rawFacing = CharAnimator.FacingDirection;
                // Convert to effective facing sign relative to sprite's native orientation
                bool facesRight = CharAnimator.DefaultFacesRight;
                return facesRight ? rawFacing : -rawFacing;
            }
            return 1f;
        }

        /// <summary>
        /// Draw attack range gizmo when the character is selected in the Scene view.
        /// Shows prominent semi-transparent shapes with wire outlines.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            if (RuntimeStats == null) return;
            DrawAttackRangeGizmos(selected: true);
        }

        /// <summary>
        /// Draw attack range gizmo when the character is NOT selected (faint outline).
        /// </summary>
        private void OnDrawGizmos()
        {
            if (RuntimeStats == null) return;
            DrawAttackRangeGizmos(selected: false);
        }

        /// <summary>
        /// Draw all attack range shapes as Gizmos.
        /// Also draws minAttackDistance as a green circle and engageDistance as a yellow circle.
        /// </summary>
        private void DrawAttackRangeGizmos(bool selected)
        {
            float fillAlpha = selected ? 0.15f : 0.05f;
            float wireAlpha = selected ? 0.5f : 0.15f;
            Color fillColor = new Color(1f, 0f, 0f, fillAlpha);
            Color wireColor = new Color(1f, 0f, 0f, wireAlpha);

            AttackRangeShape[] shapes = RuntimeStats.attackRangeShapes;
            float facingSign = GetFacingSign();
            Vector3 pos = transform.position;

            if (shapes != null)
            {
                for (int i = 0; i < shapes.Length; i++)
                {
                    if (shapes[i] == null) continue;

                    AttackRangeShape shape = shapes[i];
                    Vector2 center = (Vector2)pos + new Vector2(shape.offset.x * facingSign, shape.offset.y);

                    switch (shape.shapeType)
                    {
                        case AttackShapeType.Circle:
                            Gizmos.color = wireColor;
                            Gizmos.DrawWireSphere(center, shape.radius);
                            if (selected)
                            {
                                UnityEditor.Handles.color = fillColor;
                                UnityEditor.Handles.DrawSolidDisc(center, Vector3.forward, shape.radius);
                            }
                            break;

                        case AttackShapeType.Box:
                            Vector3 boxCenter = new Vector3(center.x, center.y, pos.z);
                            Vector3 boxSize = new Vector3(shape.size.x, shape.size.y, 0f);
                            Gizmos.color = wireColor;
                            Gizmos.DrawWireCube(boxCenter, boxSize);
                            if (selected)
                            {
                                Gizmos.color = fillColor;
                                Gizmos.DrawCube(boxCenter, boxSize);
                            }
                            break;
                    }
                }
            }

            // Draw minAttackDistance as a green circle
            float minAtkDist = RuntimeStats.minAttackDistance;
            if (minAtkDist > 0f)
            {
                Color minDistWire = new Color(0f, 1f, 0f, selected ? 0.6f : 0.2f);
                Gizmos.color = minDistWire;
                Gizmos.DrawWireSphere(pos, minAtkDist);
                if (selected)
                {
                    UnityEditor.Handles.color = new Color(0f, 1f, 0f, 0.08f);
                    UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, minAtkDist);
                }
            }

            // Draw engageDistance as a yellow circle
            float engDist = RuntimeStats.engageDistance;
            if (engDist > 0f)
            {
                Color engDistWire = new Color(1f, 1f, 0f, selected ? 0.5f : 0.15f);
                Gizmos.color = engDistWire;
                Gizmos.DrawWireSphere(pos, engDist);
            }
        }
#endif
    }
}
