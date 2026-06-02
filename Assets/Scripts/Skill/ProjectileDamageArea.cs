using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Determines how the projectile triggers its damage detection.
    /// </summary>
    public enum DamageTriggerMode
    {
        /// <summary>Damage is triggered by an animation frame event calling OnDamageEvent().</summary>
        AnimationEvent,
        /// <summary>Damage is triggered on collision with an enemy, or as fallback when reaching the end point.</summary>
        OnCollision
    }

    /// <summary>
    /// Attach to a projectile prefab to define its damage area and trigger mode.
    /// Supports configurable AttackRangeShape[] for flexible damage zones,
    /// and two trigger modes: AnimationEvent or OnCollision.
    /// </summary>
    public class ProjectileDamageArea : MonoBehaviour
    {
        [Header("Damage Trigger")]
        [Tooltip("How damage is triggered: AnimationEvent (via animation frame event) or OnCollision (on collider hit / end-of-flight fallback).")]
        public DamageTriggerMode triggerMode = DamageTriggerMode.OnCollision;

        [Header("Damage Range Shapes")]
        [Tooltip("Composable damage range shapes for this projectile. " +
                 "Union of all shapes defines the final damage area. " +
                 "Each shape supports independent offset and size.")]
        public AttackRangeShape[] damageShapes;

        [Tooltip("Whether the projectile sprite faces right by default. " +
                 "Used to correctly mirror shape offsets at runtime.")]
        public bool defaultFacesRight = true;

        // --- Runtime state (set by ProjectileController at launch) ---
        private float damage;
        private CharacterType casterType;
        private bool hasDamaged;

        // --- Cached reference ---
        private ProjectileController controller;

        /// <summary>
        /// Whether damage has already been dealt (prevents duplicate damage).
        /// </summary>
        public bool HasDamaged => hasDamaged;

        private void Awake()
        {
            controller = GetComponent<ProjectileController>();
        }

        /// <summary>
        /// Initialize damage parameters. Called by ProjectileController at launch time.
        /// </summary>
        /// <param name="dmg">Damage value to deal.</param>
        /// <param name="casterCharType">Caster's character type, used to determine enemy targets.</param>
        public void Initialize(float dmg, CharacterType casterCharType)
        {
            damage = dmg;
            casterType = casterCharType;
            hasDamaged = false;
        }

        /// <summary>
        /// Reset damage state for object pool reuse.
        /// </summary>
        public void ResetDamageState()
        {
            hasDamaged = false;
            damage = 0f;
        }

        // ==================== Animation Event Trigger ====================

        /// <summary>
        /// Animation frame event callback. Add this as an Animation Event in the explosion animation
        /// to trigger damage at a precise frame.
        /// Only works when triggerMode is set to AnimationEvent.
        /// </summary>
        public void OnDamageEvent()
        {
            if (triggerMode != DamageTriggerMode.AnimationEvent) return;

            if (damageShapes == null || damageShapes.Length == 0)
            {
                Debug.LogError($"[ProjectileDamageArea] {gameObject.name}: OnDamageEvent called but no damageShapes configured!");
                return;
            }

            DealDamage();
        }

        // ==================== Collision Trigger ====================

        /// <summary>
        /// Collision detection for OnCollision trigger mode.
        /// When the projectile's trigger collider hits an enemy, deal damage and enter explosion state.
        /// </summary>
        private void OnTriggerEnter2D(Collider2D other)
        {
            if (triggerMode != DamageTriggerMode.OnCollision) return;
            if (hasDamaged) return;

            // Only trigger on enemy targets
            string targetTag = (casterType == CharacterType.Player) ? "Enemy" : "Player";
            if (!other.CompareTag(targetTag)) return;

            // Verify the collided object is a valid alive character
            CharacterEntity targetEntity = other.GetComponent<CharacterEntity>();
            if (targetEntity == null || !targetEntity.RuntimeStats.IsAlive) return;

            // Deal damage using configured shapes
            DealDamage();

            // Notify controller to enter explosion state (stop flight, play explosion anim)
            if (controller != null)
            {
                controller.EnterExplosionFromCollision();
            }
        }

        // ==================== Fallback Trigger (End of Flight) ====================

        /// <summary>
        /// Called by ProjectileController when the projectile reaches its end point.
        /// Acts as a fallback for OnCollision mode (in case no collision occurred during flight).
        /// For AnimationEvent mode, this does nothing (damage is handled by the animation event).
        /// </summary>
        public void OnReachedEndPoint()
        {
            if (triggerMode == DamageTriggerMode.OnCollision)
            {
                DealDamage();
            }
            // AnimationEvent mode: do nothing here, wait for animation event
        }

        // ==================== Core Damage Logic ====================

        /// <summary>
        /// Execute damage detection using configured damage shapes.
        /// Finds all enemy targets within the union of all shapes and deals damage.
        /// </summary>
        public void DealDamage()
        {
            if (hasDamaged) return;
            hasDamaged = true;

            if (damageShapes == null || damageShapes.Length == 0)
            {
                Debug.LogWarning($"[ProjectileDamageArea] {gameObject.name}: No damageShapes configured, skipping damage.");
                return;
            }

            // Determine which tag to search for based on caster type
            string targetTag = (casterType == CharacterType.Player) ? "Enemy" : "Player";

            // Use facing sign = 1 (right) since projectile shapes are defined in local space
            // and the projectile itself doesn't flip
            float facingSign = defaultFacesRight ? 1f : -1f;
            Vector2 center = transform.position;

            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            int hitCount = 0;

            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity target = candidates[i].GetComponent<CharacterEntity>();
                if (target == null || !target.RuntimeStats.IsAlive) continue;

                Vector2 targetPos = target.transform.position;

                if (AttackRangeHelper.IsTargetInRange(center, facingSign, damageShapes, targetPos))
                {
                    target.TakeDamage(damage, null);
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                Debug.Log($"[ProjectileDamageArea] {gameObject.name} at {transform.position} hit {hitCount} target(s) for {damage} damage.");
            }
            else
            {
                Debug.Log($"[ProjectileDamageArea] {gameObject.name} at {transform.position} hit no targets.");
            }
        }

        // ==================== Gizmo Visualization ====================

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (damageShapes == null || damageShapes.Length == 0) return;

            float facingSign = defaultFacesRight ? 1f : -1f;
            Vector2 center = transform.position;

            Gizmos.color = new Color(1f, 0.3f, 0f, 0.7f); // Orange for damage area

            for (int i = 0; i < damageShapes.Length; i++)
            {
                AttackRangeShape shape = damageShapes[i];
                if (shape == null) continue;

                Vector2 shapeCenter = center + new Vector2(shape.offset.x * facingSign, shape.offset.y);

                switch (shape.shapeType)
                {
                    case AttackShapeType.Circle:
                        // Draw wire sphere for circle shape
                        Gizmos.DrawWireSphere(shapeCenter, shape.radius);
                        // Draw semi-transparent filled circle
                        Gizmos.color = new Color(1f, 0.3f, 0f, 0.15f);
                        Gizmos.DrawSphere(shapeCenter, shape.radius);
                        Gizmos.color = new Color(1f, 0.3f, 0f, 0.7f);
                        break;

                    case AttackShapeType.Box:
                        // Draw wire cube for box shape
                        Vector3 boxSize = new Vector3(shape.size.x, shape.size.y, 0.01f);
                        Gizmos.DrawWireCube(shapeCenter, boxSize);
                        // Draw semi-transparent filled box
                        Gizmos.color = new Color(1f, 0.3f, 0f, 0.15f);
                        Gizmos.DrawCube(shapeCenter, boxSize);
                        Gizmos.color = new Color(1f, 0.3f, 0f, 0.7f);
                        break;
                }
            }
        }
#endif
    }
}
