using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Wraps Animator state transitions for character animations.
    /// Attach alongside CharacterEntity on the character GameObject.
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        // Animator parameter hashes for performance
        private static readonly int HashIdle = Animator.StringToHash("Idle");
        private static readonly int HashWalk = Animator.StringToHash("Walk");
        private static readonly int HashAttack = Animator.StringToHash("Attack");
        private static readonly int HashSkill = Animator.StringToHash("Skill");
        private static readonly int HashHit = Animator.StringToHash("Hit");
        private static readonly int HashDeath = Animator.StringToHash("Death");
        private static readonly int HashSkillIndex = Animator.StringToHash("SkillIndex");

        private Animator animator;
        private SpriteRenderer spriteRenderer;

        /// <summary>
        /// Current facing direction: 1 = right, -1 = left.
        /// </summary>
        public int FacingDirection { get; private set; } = 1;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Set the animator controller at runtime (from CharacterData).
        /// </summary>
        public void SetAnimatorController(RuntimeAnimatorController controller)
        {
            if (animator == null)
                animator = GetComponent<Animator>();
            if (controller != null)
                animator.runtimeAnimatorController = controller;
        }

        /// <summary>
        /// Play idle animation.
        /// </summary>
        public void PlayIdle()
        {
            ResetAllTriggers();
            animator.SetTrigger(HashIdle);
        }

        /// <summary>
        /// Play walk/run animation.
        /// </summary>
        public void PlayWalk()
        {
            ResetAllTriggers();
            animator.SetTrigger(HashWalk);
        }

        /// <summary>
        /// Play normal attack animation.
        /// </summary>
        public void PlayAttack()
        {
            ResetAllTriggers();
            animator.SetTrigger(HashAttack);
        }

        /// <summary>
        /// Play skill animation with a specific skill index.
        /// </summary>
        public void PlaySkill(int skillIndex)
        {
            ResetAllTriggers();
            animator.SetInteger(HashSkillIndex, skillIndex);
            animator.SetTrigger(HashSkill);
        }

        /// <summary>
        /// Play hit/hurt animation.
        /// </summary>
        public void PlayHit()
        {
            animator.SetTrigger(HashHit);
        }

        /// <summary>
        /// Play death animation.
        /// </summary>
        public void PlayDeath()
        {
            ResetAllTriggers();
            animator.SetTrigger(HashDeath);
        }

        /// <summary>
        /// Flip the sprite to face the movement direction.
        /// Positive moveDirection = face right, negative = face left.
        /// </summary>
        public void SetFacingDirection(float moveDirection)
        {
            if (Mathf.Approximately(moveDirection, 0f)) return;

            FacingDirection = moveDirection > 0f ? 1 : -1;
            spriteRenderer.flipX = FacingDirection > 0;
        }

        /// <summary>
        /// Face towards a world position.
        /// </summary>
        public void FaceTowards(Vector3 targetPosition)
        {
            float direction = targetPosition.x - transform.position.x;
            SetFacingDirection(direction);
        }

        /// <summary>
        /// Reset all animation triggers to prevent queued transitions.
        /// </summary>
        private void ResetAllTriggers()
        {
            animator.ResetTrigger(HashIdle);
            animator.ResetTrigger(HashWalk);
            animator.ResetTrigger(HashAttack);
            animator.ResetTrigger(HashSkill);
            animator.ResetTrigger(HashHit);
            animator.ResetTrigger(HashDeath);
        }
    }
}
