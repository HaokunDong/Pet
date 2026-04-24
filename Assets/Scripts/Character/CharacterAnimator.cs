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

        /// <summary>
        /// Duration of the skill animation protection period (seconds).
        /// During this time, behavior tree ticks and hit animations will not interrupt the Skill animation.
        /// This is set dynamically when PlaySkill is called, based on the animation clip length.
        /// </summary>
        private const float SKILL_STATE_MAX_DURATION = 5f;

        [Header("Sprite Orientation")]
        [Tooltip("Whether the sprite asset faces right by default. " +
                 "This value is synced from CharacterData at runtime. " +
                 "Only used as fallback if no CharacterData is assigned.")]
        [SerializeField]
        private bool defaultFacesRight = true;

        private Animator animator;
        private SpriteRenderer spriteRenderer;
        private CombatSystem combatSystem;

        // Flags indicating whether the Animator Controller has optional parameters
        private bool hasSkillParam;
        private bool hasSkillIndexParam;
        private bool hasDeathParam;

        /// <summary>
        /// Whether the sprite asset faces right by default.
        /// Exposed for external systems (e.g. Gizmo drawing) that need to know the native orientation.
        /// </summary>
        public bool DefaultFacesRight => defaultFacesRight;

        private bool isInHitState;
        private float hitStateTimer;

        private bool isInSkillState;
        private float skillStateTimer;

        /// <summary>
        /// Whether the character is currently in the Hit animation protection period.
        /// When true, behavior tree nodes should avoid overriding the current animation.
        /// </summary>
        public bool IsInHitState => isInHitState;

        /// <summary>
        /// Whether the character is currently in the Skill animation protection period.
        /// When true, behavior tree ticks and hit animations should not interrupt the skill.
        /// </summary>
        public bool IsInSkillState => isInSkillState;

        /// <summary>
        /// Current facing direction: 1 = right, -1 = left.
        /// </summary>
        public int FacingDirection { get; private set; } = 1;

        /// <summary>
        /// Tracks the current animation state to avoid re-triggering the same state every frame,
        /// which would cause AnyState self-transitions to restart the animation (visual jitter).
        /// </summary>
        private enum AnimState { None, Idle, Walk, Attack, Skill, Hit, Death }
        private AnimState currentAnimState = AnimState.None;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            spriteRenderer = GetComponent<SpriteRenderer>();
            combatSystem = GetComponent<CombatSystem>();
        }

        /// <summary>
        /// Cache which optional parameters exist in the current Animator Controller.
        /// Called after SetAnimatorController or on first use.
        /// </summary>
        private void CacheParameterFlags()
        {
            hasSkillParam = false;
            hasSkillIndexParam = false;
            hasDeathParam = false;

            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (var param in animator.parameters)
            {
                switch (param.nameHash)
                {
                    case var h when h == HashSkill:
                        hasSkillParam = true;
                        break;
                    case var h when h == HashSkillIndex:
                        hasSkillIndexParam = true;
                        break;
                    case var h when h == HashDeath:
                        hasDeathParam = true;
                        break;
                }
            }
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
                    // Reset anim state so behavior tree can transition to Idle/Walk
                    // (the Animator Controller's Exit Time transition handles the actual clip change)
                    currentAnimState = AnimState.None;
                }
            }

            // Count down skill state protection timer
            if (isInSkillState)
            {
                skillStateTimer -= Time.deltaTime;
                if (skillStateTimer <= 0f)
                {
                    isInSkillState = false;
                    currentAnimState = AnimState.None;
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
            {
                animator.runtimeAnimatorController = controller;
                CacheParameterFlags();
            }
        }

        /// <summary>
        /// Play idle animation. Clears any pending attack state.
        /// Skipped if the character is in the Hit animation protection period.
        /// </summary>
        public void PlayIdle()
        {
            // Do not interrupt Hit or Skill animation during protection period
            if (isInHitState) return;
            if (isInSkillState) return;

            // Skip if already in Idle state to prevent AnyState self-transition restart
            if (currentAnimState == AnimState.Idle) return;

            ClearAttackStateIfNeeded();
            ResetAllTriggers();
            animator.SetTrigger(HashIdle);
            currentAnimState = AnimState.Idle;
        }

        /// <summary>
        /// Play walk/run animation. Clears any pending attack state.
        /// </summary>
        public void PlayWalk()
        {
            // Do not interrupt Skill animation during protection period
            if (isInSkillState) return;

            // Skip if already in Walk state to prevent AnyState self-transition restart
            if (currentAnimState == AnimState.Walk) return;

            ClearAttackStateIfNeeded();
            ResetAllTriggers();
            animator.SetTrigger(HashWalk);
            currentAnimState = AnimState.Walk;
        }

        /// <summary>
        /// Play normal attack animation.
        /// </summary>
        public void PlayAttack()
        {
            ResetAllTriggers();
            animator.SetTrigger(HashAttack);
            currentAnimState = AnimState.Attack;
        }

        /// <summary>
        /// Play skill animation with a specific skill index.
        /// </summary>
        public void PlaySkill(int skillIndex)
        {
            if (!hasSkillParam)
            {
                // Animator Controller does not have Skill parameter; fall back to Attack
                PlayAttack();
                return;
            }

            ResetAllTriggers();
            if (hasSkillIndexParam)
                animator.SetInteger(HashSkillIndex, skillIndex);
            animator.SetTrigger(HashSkill);
            currentAnimState = AnimState.Skill;

            // Set skill state protection period so the animation is not interrupted
            isInSkillState = true;
            skillStateTimer = SKILL_STATE_MAX_DURATION;
        }

        /// <summary>
        /// Called when the skill animation finishes (via animation event or CombatSystem).
        /// Clears the skill state protection so the character can transition to other states.
        /// </summary>
        public void ClearSkillState()
        {
            isInSkillState = false;
            skillStateTimer = 0f;
            currentAnimState = AnimState.None;
        }

        /// <summary>
        /// Play hit/hurt animation.
        /// Does NOT clear pending attack state — an ongoing attack should still
        /// deal damage even if the attacker is hit mid-swing (animation event fires normally).
        /// If already in Hit state, replays the animation from the beginning.
        /// Sets a protection period so behavior tree ticks don't interrupt the animation.
        /// </summary>
        public void PlayHit()
        {
            // Do not interrupt Skill animation during protection period
            if (isInSkillState) return;

            // NOTE: intentionally NOT calling ClearAttackStateIfNeeded() here.
            // The attack's damage frame event should still fire even if we get hit.
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
            currentAnimState = AnimState.Hit;
        }

        /// <summary>
        /// Play death animation. Clears any pending attack state.
        /// </summary>
        public void PlayDeath()
        {
            ClearAttackStateIfNeeded();
            ResetAllTriggers();
            if (hasDeathParam)
                animator.SetTrigger(HashDeath);
            currentAnimState = AnimState.Death;
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
            if (hasSkillParam) animator.ResetTrigger(HashSkill);
            animator.ResetTrigger(HashHit);
            if (hasDeathParam) animator.ResetTrigger(HashDeath);
        }
    }
}
