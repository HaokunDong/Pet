using System;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// State of the projectile lifecycle.
    /// </summary>
    public enum ProjectileState
    {
        Idle,
        Flying,
        Exploding
    }

    /// <summary>
    /// Controls a projectile's full lifecycle: parabolic flight → explosion → recycle.
    /// Damage is now handled by the ProjectileDamageArea component on the same GameObject.
    /// Attach to a projectile prefab with SpriteRenderer and Animator components.
    /// Call Launch() to begin; the onFinish callback fires after the explosion animation completes.
    /// </summary>
    public class ProjectileController : MonoBehaviour
    {
        // --- Flight parameters ---
        private Vector3 startPos;
        private Vector3 endPos;
        private float flightDuration;
        private float arcHeight;

        // --- Callbacks ---
        private Action onFinish;

        // --- Internal state ---
        private float elapsed;
        private ProjectileState state = ProjectileState.Idle;
        private bool explosionAnimStarted;

        // --- Cached components ---
        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private ProjectileDamageArea damageArea;

        // --- Original sprite saved at Awake for restoration after explosion ---
        private Sprite originalSprite;

        /// <summary>
        /// Current state of this projectile.
        /// </summary>
        public ProjectileState State => state;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            damageArea = GetComponent<ProjectileDamageArea>();
            if (spriteRenderer != null)
            {
                originalSprite = spriteRenderer.sprite;
            }
        }

        /// <summary>
        /// Begin parabolic flight toward the predicted landing position, then explode on arrival.
        /// </summary>
        /// <param name="start">World position where the projectile spawns.</param>
        /// <param name="predictedEnd">Predicted landing position (accounts for target movement).</param>
        /// <param name="duration">Total flight time in seconds.</param>
        /// <param name="arc">Peak height of the parabolic arc above the start-end line.</param>
        /// <param name="dmg">Damage to deal to each enemy in range.</param>
        /// <param name="casterCharType">The caster's character type, used to determine enemy targets.</param>
        /// <param name="onFinishCallback">Invoked after the explosion animation finishes (for pool recycle).</param>
        public void Launch(Vector3 start, Vector3 predictedEnd, float duration, float arc,
            float dmg, CharacterType casterCharType, Action onFinishCallback)
        {
            startPos = start;
            endPos = predictedEnd;
            flightDuration = Mathf.Max(duration, 0.01f);
            arcHeight = arc;
            onFinish = onFinishCallback;

            elapsed = 0f;
            explosionAnimStarted = false;
            state = ProjectileState.Flying;

            transform.position = start;
            transform.rotation = Quaternion.identity;

            // Initialize damage area component with damage parameters
            if (damageArea == null) damageArea = GetComponent<ProjectileDamageArea>();
            if (damageArea != null)
            {
                damageArea.Initialize(dmg, casterCharType);
            }
            else
            {
                Debug.LogWarning("[ProjectileController] No ProjectileDamageArea component found! Damage will not be dealt.");
            }

            // Ensure sprite is visible during flight
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null) spriteRenderer.enabled = true;

            // Ensure animator is available
            if (animator == null) animator = GetComponent<Animator>();

            // Disable Animator during flight — it will be enabled on arrival
            // so it plays the explosion animation from Entry automatically
            if (animator != null) animator.enabled = false;
        }

        private void Update()
        {
            switch (state)
            {
                case ProjectileState.Flying:
                    UpdateFlying();
                    break;
                case ProjectileState.Exploding:
                    UpdateExploding();
                    break;
            }
        }

        // ==================== Flying State ====================

        private void UpdateFlying()
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / flightDuration);

            // Horizontal: linear interpolation from start to end
            Vector3 flatPos = Vector3.Lerp(startPos, endPos, t);

            // Vertical: parabolic arc offset — peaks at t=0.5
            float arcOffset = arcHeight * 4f * t * (1f - t);
            flatPos.y += arcOffset;

            // Calculate tangent direction for rotation
            if (t < 1f)
            {
                float tNext = Mathf.Clamp01((elapsed + Time.deltaTime) / flightDuration);
                Vector3 flatNext = Vector3.Lerp(startPos, endPos, tNext);
                float arcNext = arcHeight * 4f * tNext * (1f - tNext);
                flatNext.y += arcNext;

                Vector3 tangent = flatNext - flatPos;
                if (tangent.sqrMagnitude > 0.0001f)
                {
                    float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }
            }

            transform.position = flatPos;

            // Arrived at destination — transition to explosion
            if (t >= 1f)
            {
                transform.position = endPos;
                transform.rotation = Quaternion.identity;
                EnterExplosionState();
            }
        }

        // ==================== Explosion State ====================

        /// <summary>
        /// Transition from flying to exploding: notify damage area and play explosion animation.
        /// The Animator is configured with Entry → Explosion state, so simply enabling
        /// the Animator will start the explosion animation automatically.
        /// </summary>
        private void EnterExplosionState()
        {
            state = ProjectileState.Exploding;
            explosionAnimStarted = false;

            // Notify damage area that projectile has reached end point (fallback damage for OnCollision mode)
            if (damageArea != null)
            {
                damageArea.OnReachedEndPoint();
            }

            // Enable Animator — it goes from Entry directly to the explosion animation
            if (animator != null)
            {
                animator.enabled = true;
                animator.Play(0, 0, 0f);
                explosionAnimStarted = true;
            }
            else
            {
                // No animator — finish immediately
                Debug.LogWarning("[ProjectileController] No Animator found, skipping explosion animation.");
                FinishAndRecycle();
            }
        }

        /// <summary>
        /// Called externally by ProjectileDamageArea when a collision triggers explosion.
        /// Stops flight immediately and enters explosion state.
        /// </summary>
        public void EnterExplosionFromCollision()
        {
            if (state != ProjectileState.Flying) return;

            // Stop at current position
            transform.rotation = Quaternion.identity;

            state = ProjectileState.Exploding;
            explosionAnimStarted = false;

            // Enable Animator for explosion animation
            if (animator != null)
            {
                animator.enabled = true;
                animator.Play(0, 0, 0f);
                explosionAnimStarted = true;
            }
            else
            {
                Debug.LogWarning("[ProjectileController] No Animator found, skipping explosion animation.");
                FinishAndRecycle();
            }
        }

        private void UpdateExploding()
        {
            if (!explosionAnimStarted) return;
            if (animator == null)
            {
                FinishAndRecycle();
                return;
            }

            // Check if the explosion animation has finished
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // Ensure the clip has valid length and has reached or passed the end
            if (stateInfo.length > 0f && stateInfo.normalizedTime >= 1f)
            {
                FinishAndRecycle();
            }
        }

        // ==================== Lifecycle ====================

        /// <summary>
        /// Called when the explosion animation finishes (or immediately if no animator).
        /// Invokes the onFinish callback so the external system can recycle this object.
        /// </summary>
        private void FinishAndRecycle()
        {
            state = ProjectileState.Idle;
            onFinish?.Invoke();
        }

        /// <summary>
        /// Reset all state when retrieved from object pool.
        /// IMPORTANT: Must be called AFTER SetActive(true) so that component operations take effect.
        /// </summary>
        public void ResetState()
        {
            StopAllCoroutines();

            state = ProjectileState.Idle;
            elapsed = 0f;
            explosionAnimStarted = false;
            onFinish = null;

            transform.rotation = Quaternion.identity;

            // Re-acquire components in case Awake was not called (pooled objects)
            if (animator == null) animator = GetComponent<Animator>();
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            if (damageArea == null) damageArea = GetComponent<ProjectileDamageArea>();

            // Reset damage area state
            if (damageArea != null)
            {
                damageArea.ResetDamageState();
            }

            // Disable animator FIRST so it stops rendering explosion frames
            if (animator != null)
            {
                animator.enabled = false;
            }

            // Restore the original sprite (explosion animation may have changed it)
            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                if (originalSprite != null)
                {
                    spriteRenderer.sprite = originalSprite;
                }
                // Ensure full visibility
                spriteRenderer.color = Color.white;
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            state = ProjectileState.Idle;
            onFinish = null;
        }
    }
}
