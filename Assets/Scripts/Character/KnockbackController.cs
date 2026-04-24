using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Controls knockback displacement when a character is hit.
    /// Applies an initial velocity (horizontal away from attacker + vertical upward),
    /// simulates gravity each frame, and ends when the character returns to ground level.
    /// 
    /// Attach alongside CharacterEntity on the character GameObject.
    /// The component is auto-added by CharacterEntity.Initialize() if not already present.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class KnockbackController : MonoBehaviour
    {
        /// <summary>
        /// Whether the character is currently being knocked back.
        /// When true, AI movement should be suppressed to avoid overriding the knockback displacement.
        /// </summary>
        public bool IsInKnockback { get; private set; }

        private CharacterEntity entity;
        private Vector2 velocity;
        private float groundY;

        /// <summary>
        /// Default scene boundary limits for X-axis clamping during knockback.
        /// These can be overridden via SetBounds() if the scene has custom boundaries.
        /// </summary>
        private float boundMinX = -20f;
        private float boundMaxX = 20f;

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
        }

        /// <summary>
        /// Set custom scene boundaries for knockback X-axis clamping.
        /// </summary>
        public void SetBounds(float minX, float maxX)
        {
            boundMinX = minX;
            boundMaxX = maxX;
        }

        /// <summary>
        /// Apply knockback force away from the attacker's position.
        /// If already in knockback, resets velocity to the new knockback direction (allows chain hits).
        /// </summary>
        /// <param name="attackerPosition">World position of the attacker.</param>
        public void ApplyKnockback(Vector2 attackerPosition)
        {
            if (entity == null || entity.RuntimeStats == null) return;

            float hSpeed = entity.RuntimeStats.knockbackHorizontalSpeed;
            float vSpeed = entity.RuntimeStats.knockbackVerticalSpeed;

            // Skip knockback if both speeds are zero (knockback disabled for this character)
            if (hSpeed <= 0f && vSpeed <= 0f) return;

            // Determine horizontal direction: away from attacker
            float dx = transform.position.x - attackerPosition.x;
            float dirX = dx >= 0f ? 1f : -1f;

            // If attacker is at the exact same X position, default to pushing right
            if (Mathf.Abs(dx) < 0.001f)
            {
                dirX = 1f;
            }

            velocity = new Vector2(dirX * hSpeed, vSpeed);

            // Record ground Y only on first knockback (not on chain hits while airborne)
            if (!IsInKnockback)
            {
                groundY = transform.position.y;
            }

            IsInKnockback = true;
        }

        /// <summary>
        /// Immediately cancel knockback and reset state.
        /// Called when the character dies or is recycled.
        /// </summary>
        public void CancelKnockback()
        {
            IsInKnockback = false;
            velocity = Vector2.zero;
        }

        private void Update()
        {
            if (!IsInKnockback) return;

            float dt = Time.deltaTime;
            float gravity = entity != null && entity.RuntimeStats != null
                ? entity.RuntimeStats.knockbackGravity
                : 8f;

            // Apply gravity to vertical velocity
            velocity.y -= gravity * dt;

            // Apply horizontal drag (gentle deceleration so character doesn't slide forever)
            // Using a simple exponential decay
            velocity.x *= Mathf.Exp(-2f * dt);

            // Update position
            Vector3 pos = transform.position;
            pos.x += velocity.x * dt;
            pos.y += velocity.y * dt;

            // Clamp X to scene boundaries
            pos.x = Mathf.Clamp(pos.x, boundMinX, boundMaxX);

            // Check if character has landed (Y back to ground level or below)
            if (pos.y <= groundY && velocity.y <= 0f)
            {
                pos.y = groundY;
                IsInKnockback = false;
                velocity = Vector2.zero;
            }

            transform.position = pos;
        }

        private void OnDisable()
        {
            // Reset knockback state when disabled (e.g. object pool return)
            CancelKnockback();
        }
    }
}
