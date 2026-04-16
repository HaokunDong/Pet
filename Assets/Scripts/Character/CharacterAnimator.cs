using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Wraps Animator state transitions for character animations.
    /// Attach alongside CharacterEntity on the character GameObject.
    /// When a non-attack animation is played (Idle, Walk), any pending attack state
    /// in CombatSystem is cleared to prevent stale damage events.
    /// 
    /// IMPORTANT Animator Controller setup:
    /// - Hit animation state MUST have Loop Time = false (non-looping).
    /// - Hit state should have an Exit Time transition back to Idle (no condition needed).
    /// - Attack animation clips MUST have Animation Events configured:
    ///   add an event at the hit frame calling "OnAttackHit" (normal attack) or "OnSkillHit" (skill).
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

        /// <summary>
        /// Duration of the hit animation protection period (seconds).
        /// During this time, behavior tree ticks will not interrupt the Hit animation.
        /// </summary>
        private const float HIT_STATE_DURATION = 0.4f;

        [Header("Sprite Orientation")]
        [Tooltip("Whether the sprite asset faces right by default. " +
                 "This value is synced from CharacterData at runtime. " +
                 "Only used as fallback if no CharacterData is assigned.")]
        [SerializeField]
        private bool defaultFacesRight = true;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private CombatSystem combatSystem;

        /// <summary>
        /// Whether the sprite asset faces right by default.
        /// Exposed for external systems (e.g. Gizmo drawing) that need to know the native orientation.
        /// </summary>
        public bool DefaultFacesRight => defaultFacesRight;

        private bool isInHitState;
        private float hitStateTimer;

        /// <summary>
        /// Whether the character is currently in the Hit animation protection period.
        /// When true, behavior tree nodes should avoid overriding the current animation.
        /// </summary>
        public bool IsInHitState => isInHitState;

        /// <summary>
        /// Current facing direction: 1 = right, -1 = left.
        /// </summary>
        public int FacingDirection { get; private set; } = 1;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            combatSystem = GetComponent<CombatSystem>();
        }

        private void Update()
        {
            // Count down hit state protection timer
            if (isInHitState)
            {
                hitStateTimer -= Time.deltaTime;
                if (hitStateTimer <= 0f)
                {
                    isInHitState = false;
                }
            }
        }

        /// <summary>
        /// Sync the defaultFacesRight setting from CharacterData.
        /// Called by CharacterEntity during initialization.
        /// </summary>
        public void SyncDefaultFacing(bool facesRight)
        {
            defaultFacesRight = facesRight;
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
        /// Play idle animation. Clears any pending attack state.
        /// Skipped if the character is in the Hit animation protection period.
        /// </summary>
        public void PlayIdle()
        {
            // Do not interrupt Hit animation during protection period
            if (isInHitState) return;

            ClearAttackStateIfNeeded();
            ResetAllTriggers();
            animator.SetTrigger(HashIdle);
        }

        /// <summary>
        /// Play walk/run animation. Clears any pending attack state.
        /// </summary>
        public void PlayWalk()
        {
            ClearAttackStateIfNeeded();
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
        /// Play hit/hurt animation. Clears any pending attack state
        /// since being hit interrupts the current attack.
        /// If already in Hit state, replays the animation from the beginning.
        /// Sets a protection period so behavior tree ticks don't interrupt the animation.
        /// </summary>
        public void PlayHit()
        {
            ClearAttackStateIfNeeded();
            ResetAllTriggers();

            // If already in hit state, force replay from beginning
            if (isInHitState)
            {
                animator.Play("Hit", 0, 0f);
            }
            else
            {
                animator.SetTrigger(HashHit);
            }

            // Set hit state protection period
            isInHitState = true;
            hitStateTimer = HIT_STATE_DURATION;
        }

        /// <summary>
        /// Play death animation. Clears any pending attack state.
        /// </summary>
        public void PlayDeath()
        {
            ClearAttackStateIfNeeded();
            ResetAllTriggers();
            animator.SetTrigger(HashDeath);
        }

        /// <summary>
        /// Flip the sprite to face the movement direction.
        /// Positive moveDirection = face right, negative = face left.
        /// Respects the defaultFacesRight setting for sprites with different native orientations.
        /// </summary>
        public void SetFacingDirection(float moveDirection)
        {
            if (Mathf.Approximately(moveDirection, 0f)) return;

            FacingDirection = moveDirection > 0f ? 1 : -1;

            // If sprite natively faces right: flip when we want to face left
            // If sprite natively faces left:  flip when we want to face right
            bool wantFaceRight = FacingDirection > 0;
            spriteRenderer.flipX = defaultFacesRight ? !wantFaceRight : wantFaceRight;
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
        /// Notify CombatSystem to clear pending attack state when a non-attack animation interrupts.
        /// </summary>
        private void ClearAttackStateIfNeeded()
        {
            if (combatSystem != null && combatSystem.IsAttacking)
            {
                combatSystem.ClearAttackState();
            }
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
