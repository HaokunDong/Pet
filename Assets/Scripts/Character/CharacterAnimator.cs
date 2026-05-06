using UnityEngine;
using PetGame.States;

namespace PetGame
{
    /// <summary>
    /// Wraps Animator state transitions for character animations.
    /// Attach alongside CharacterEntity on the character GameObject.
    /// Internally uses a Finite State Machine (EntityStateMachine) to manage
    /// all animation state transitions with Bool condition-driven logic.
    /// 
    /// Uses Entry/Exit mode in Animator Controller:
    /// - Each state has a Bool parameter (Idle, Walk, Attack, Hit, Death, SkillOne~Four).
    /// - Entry evaluates Bool conditions to route to the correct state.
    /// - When a state's Bool becomes false, it transitions to Exit.
    /// - Exit loops back to Entry for re-evaluation.
    /// - Idle is the default path from Entry (no condition / fallback).
    /// - State transitions rely on OnExit() clearing the current Bool and OnEnter() setting the new Bool.
    /// - ResetAllBools() is only used for forced scenarios (Death, Reset).
    /// 
    /// === ANIMATOR CONTROLLER CONFIGURATION GUIDE ===
    /// 
    /// Parameters (all Bool type):
    ///   Idle, Walk, Attack, Hit, Death, SkillOne, SkillTwo, SkillThree, SkillFour
    /// 
    /// Entry → State transitions (left side):
    ///   - Entry → Idle:      (default, no condition — lowest priority / fallback)
    ///   - Entry → Walk:      condition Walk == true
    ///   - Entry → Attack:    condition Attack == true
    ///   - Entry → Hit:       condition Hit == true
    ///   - Entry → Death:     condition Death == true
    ///   - Entry → SkillOne:  condition SkillOne == true
    ///   - Entry → SkillTwo:  condition SkillTwo == true
    ///   - Entry → SkillThree: condition SkillThree == true
    ///   - Entry → SkillFour: condition SkillFour == true
    /// 
    /// State → Exit transitions (right side):
    ///   - Idle → Exit:       condition Idle == false
    ///   - Walk → Exit:       condition Walk == false
    ///   - Attack → Exit:     condition Attack == false
    ///   - Hit → Exit:        condition Hit == false
    ///   - Death → Exit:      condition Death == false
    ///   - SkillOne → Exit:   condition SkillOne == false
    ///   - SkillTwo → Exit:   condition SkillTwo == false
    ///   - SkillThree → Exit: condition SkillThree == false
    ///   - SkillFour → Exit:  condition SkillFour == false
    /// 
    /// All transitions settings:
    ///   - Has Exit Time = false (unchecked)
    ///   - Transition Duration = 0 (instant)
    ///   - Can Transition To Self = false (for non-Hit states)
    /// 
    /// IMPORTANT Animator Controller setup:
    /// - All transitions MUST have Has Exit Time = false.
    /// - Hit animation state MUST have Loop Time = false (non-looping).
    /// - Attack animation clips MUST have Animation Events configured:
    ///   add an event at the hit frame calling "OnAttackHit" (normal attack) or "OnSkillHit" (skill).
    /// </summary>
    [RequireComponent(typeof(Animator))]
    [RequireComponent(typeof(SpriteRenderer))]
    public class CharacterAnimator : MonoBehaviour
    {
        // Animator parameter hashes for performance (used by ResetAllBools)
        private static readonly int HashIdle = Animator.StringToHash("Idle");
        private static readonly int HashWalk = Animator.StringToHash("Walk");
        private static readonly int HashAttack = Animator.StringToHash("Attack");
        private static readonly int HashSkill = Animator.StringToHash("Skill");
        private static readonly int HashSkillOne = Animator.StringToHash("SkillOne");
        private static readonly int HashSkillTwo = Animator.StringToHash("SkillTwo");
        private static readonly int HashSkillThree = Animator.StringToHash("SkillThree");
        private static readonly int HashSkillFour = Animator.StringToHash("SkillFour");
        private static readonly int HashHit = Animator.StringToHash("Hit");
        private static readonly int HashDeath = Animator.StringToHash("Death");

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
        private bool hasSkillOneParam;
        private bool hasSkillTwoParam;
        private bool hasSkillThreeParam;
        private bool hasSkillFourParam;
        private bool hasDeathParam;

        /// <summary>
        /// The internal state machine managing all animation state transitions.
        /// </summary>
        private EntityStateMachine stateMachine;

        /// <summary>
        /// Whether this character has multiple skills (> 1).
        /// If true, uses SkillOne/SkillTwo/SkillThree/SkillFour Bool parameters.
        /// If false, uses single Skill Bool parameter.
        /// </summary>
        private bool useMultiSkillBools;

        /// <summary>
        /// Whether the sprite asset faces right by default.
        /// Exposed for external systems (e.g. Gizmo drawing) that need to know the native orientation.
        /// </summary>
        public bool DefaultFacesRight => defaultFacesRight;

        /// <summary>
        /// Whether the character is currently in the Hit animation protection period.
        /// When true, behavior tree nodes should avoid overriding the current animation.
        /// </summary>
        public bool IsInHitState => stateMachine != null && stateMachine.IsInHitState;

        /// <summary>
        /// Whether the character is currently in the Skill animation protection period.
        /// When true, behavior tree ticks and hit animations should not interrupt the skill.
        /// </summary>
        public bool IsInSkillState => stateMachine != null && stateMachine.IsInSkillState;

        /// <summary>
        /// Whether the character is currently attacking.
        /// </summary>
        public bool IsAttacking => stateMachine != null && stateMachine.IsAttacking;

        /// <summary>
        /// Current facing direction: 1 = right, -1 = left.
        /// </summary>
        public int FacingDirection { get; private set; } = 1;

        /// <summary>
        /// Expose the state machine for external systems that need direct access.
        /// </summary>
        public EntityStateMachine StateMachine => stateMachine;

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
            hasSkillOneParam = false;
            hasSkillTwoParam = false;
            hasSkillThreeParam = false;
            hasSkillFourParam = false;
            hasDeathParam = false;

            if (animator == null || animator.runtimeAnimatorController == null) return;

            foreach (var param in animator.parameters)
            {
                switch (param.nameHash)
                {
                    case var h when h == HashSkill:
                        hasSkillParam = true;
                        break;
                    case var h when h == HashSkillOne:
                        hasSkillOneParam = true;
                        break;
                    case var h when h == HashSkillTwo:
                        hasSkillTwoParam = true;
                        break;
                    case var h when h == HashSkillThree:
                        hasSkillThreeParam = true;
                        break;
                    case var h when h == HashSkillFour:
                        hasSkillFourParam = true;
                        break;
                    case var h when h == HashDeath:
                        hasDeathParam = true;
                        break;
                }
            }
        }

        private void Update()
        {
            // Delegate per-frame update to the state machine
            if (stateMachine != null)
            {
                stateMachine.Update();
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
        /// Set the skill count to determine which animation parameter mode to use.
        /// Called by CharacterEntity during initialization.
        /// - count == 0: no skills (will fall back to normal attack)
        /// - count == 1: use Skill Bool parameter
        /// - count > 1: use SkillOne/SkillTwo/SkillThree/SkillFour Bool parameters
        /// </summary>
        public void SetSkillCount(int count)
        {
            useMultiSkillBools = count > 1;
        }

        /// <summary>
        /// Initialize the state machine based on character type.
        /// Must be called after SetAnimatorController and SetSkillCount.
        /// </summary>
        /// <param name="characterType">The type of character (Player, MinorEnemy, Boss).</param>
        /// <param name="skillCount">Number of skills the character has.</param>
        public void InitializeStateMachine(CharacterType characterType, int skillCount)
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            // Ensure parameter flags are cached before configuring skills.
            // This handles the case where the Animator already has a controller
            // from the Prefab but SetAnimatorController() was not called beforehand.
            CacheParameterFlags();

            // Create the appropriate state machine based on character type
            if (characterType == CharacterType.Player)
            {
                var playerMachine = new PlayerStateMachine();
                playerMachine.Initialize(animator, this);
                playerMachine.ConfigureSkills(skillCount, hasSkillParam, hasSkillOneParam,
                    hasSkillTwoParam, hasSkillThreeParam, hasSkillFourParam, hasDeathParam);
                stateMachine = playerMachine;
            }
            else
            {
                var enemyMachine = new EnemyStateMachine();
                enemyMachine.Initialize(animator, this);
                enemyMachine.ConfigureSkills(skillCount, hasSkillParam, hasSkillOneParam,
                    hasSkillTwoParam, hasSkillThreeParam, hasSkillFourParam, hasDeathParam);
                stateMachine = enemyMachine;
            }

            // Start in Idle state
            stateMachine.ForceChangeState<IdleState>();
        }

        /// <summary>
        /// Play idle animation. Delegates to state machine.
        /// Skipped if the character is in a protected state (Attack/Skill/Hit).
        /// In Entry/Exit mode, OnExit() of the current state clears its Bool,
        /// then OnEnter() of IdleState sets Idle Bool = true.
        /// </summary>
        public void PlayIdle()
        {
            if (stateMachine == null) return;

            // Skip if already in Idle state (no need to re-enter from Entry)
            if (stateMachine.isIdle)
            {
                // Diagnostic: check if Animator Bool is out of sync with state machine
                if (animator != null && !animator.GetBool(HashIdle))
                {
                    Debug.LogWarning($"[CharacterAnimator] {gameObject.name}: PlayIdle skipped (isIdle=true) but Animator Idle Bool is FALSE! Forcing sync.");
                    animator.SetBool(HashIdle, true);
                }
                return;
            }

            Debug.Log($"[CharacterAnimator] {gameObject.name}: PlayIdle() - transitioning from current state (isWalking={stateMachine.isWalking}, isAttacking={stateMachine.isAttacking}, isUsingSkill={stateMachine.isUsingSkill}, isHit={stateMachine.isHit})");
            ClearAttackStateIfNeeded();
            stateMachine.ChangeState<IdleState>();
        }

        /// <summary>
        /// Play walk/run animation. Delegates to state machine.
        /// In Entry/Exit mode, OnExit() of the current state clears its Bool,
        /// then OnEnter() of WalkState sets Walk Bool = true.
        /// </summary>
        public void PlayWalk()
        {
            if (stateMachine == null) return;

            // Skip if already in Walk state (no need to re-enter from Entry)
            if (stateMachine.isWalking) return;

            Debug.Log($"[CharacterAnimator] {gameObject.name}: PlayWalk() - transitioning from current state (isIdle={stateMachine.isIdle}, isAttacking={stateMachine.isAttacking})");
            ClearAttackStateIfNeeded();
            stateMachine.ChangeState<WalkState>();
        }

        /// <summary>
        /// Play normal attack animation. Delegates to state machine.
        /// In Entry/Exit mode, OnExit() of the current state clears its Bool,
        /// then OnEnter() of AttackState sets Attack Bool = true.
        /// </summary>
        public void PlayAttack()
        {
            if (stateMachine == null) return;

            Debug.Log($"[CharacterAnimator] {gameObject.name}: PlayAttack() - canBeInterrupted={stateMachine.canBeInterrupted}");
            stateMachine.ChangeState<AttackState>(); 
        }

        /// <summary>
        /// Play skill animation with a specific skill index.
        /// All skills use Bool parameters:
        /// - Single-skill characters: uses "Skill" Bool.
        /// - Multi-skill characters: uses "SkillOne", "SkillTwo", "SkillThree", "SkillFour" Bools.
        /// skillIndex is 0-based: 0=SkillOne, 1=SkillTwo, 2=SkillThree, 3=SkillFour.
        /// </summary>
        public void PlaySkill(int skillIndex)
        {
            if (stateMachine == null) return;

            // Configure the skill state with the correct index before transitioning
            var skillState = stateMachine.GetState<SkillState>(); 
            if (skillState != null)
            {
                skillState.SetSkillIndex(skillIndex);
            }

            if (!stateMachine.ChangeState<SkillState>())
            {
                // If state transition failed (e.g. in protection), do nothing
                return;
            }
        }

        /// <summary>
        /// Called when the current state animation finishes (via animation event or CombatSystem).
        /// Clears the protection so the character can transition to other states,
        /// then automatically transitions back to Idle state.
        /// In Entry/Exit mode, this triggers: current state OnExit() (Bool=false → Exit)
        /// then IdleState OnEnter() (Idle Bool=true → Entry → Idle).
        /// Applicable to Attack, Skill, and Hit states.
        /// </summary>
        public void ClearCurrentState()
        {
            if (stateMachine == null) return;

            Debug.Log($"[CharacterAnimator] {gameObject.name}: ClearCurrentState() - isIdle={stateMachine.isIdle}, isWalking={stateMachine.isWalking}, isAttacking={stateMachine.isAttacking}, isUsingSkill={stateMachine.isUsingSkill}, isHit={stateMachine.isHit}, canBeInterrupted={stateMachine.canBeInterrupted}");

            stateMachine.ClearProtection();

            // After protection is cleared, automatically return to Idle
            // (unless the character is already dead or in a non-protected state)
            if (!stateMachine.isDead && !stateMachine.isIdle && !stateMachine.isWalking)
            {
                ClearAttackStateIfNeeded();
                stateMachine.ChangeState<IdleState>();
            }
        }

        /// <summary>
        /// Play hit/hurt animation. Delegates to state machine.
        /// Does NOT clear pending attack state — an ongoing attack should still
        /// deal damage even if the attacker is hit mid-swing (animation event fires normally).
        /// If already in Hit state, replays the animation from the beginning
        /// (in Entry/Exit mode: exits to Exit then re-enters from Entry).
        /// Sets a protection period so behavior tree ticks don't interrupt the animation.
        /// </summary>
        public void PlayHit()
        {
            if (stateMachine == null) return;

            // NOTE: intentionally NOT calling ClearAttackStateIfNeeded() here.
            // The attack's damage frame event should still fire even if we get hit.
            stateMachine.ChangeState<HitState>();
        }

        /// <summary>
        /// Play death animation. Uses ForceChangeState to bypass all protection.
        /// Clears any pending attack state and skill state.
        /// </summary>
        public void PlayDeath()
        {
            if (stateMachine == null) return;

            ClearAttackStateIfNeeded();
            ResetAllBools();
            stateMachine.ForceChangeState<DeathState>();
        }

        /// <summary>
        /// Reset the state machine to Idle. Used when characters are recycled by the object pool.
        /// </summary>
        public void ResetStateMachine()
        {
            if (stateMachine != null)
            {
                ResetAllBools();
                stateMachine.Reset();
            }
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
        /// Reset all animation Bool parameters to false.
        /// In Entry/Exit mode, this is only used for forced scenarios:
        /// - PlayDeath(): force all Bools off before entering Death state.
        /// - ResetStateMachine(): force clean slate when recycling characters.
        /// Normal state transitions rely on OnExit()/OnEnter() to manage Bools.
        /// </summary>
        public void ResetAllBools()
        {
            if (animator == null) return;

            animator.SetBool(HashIdle, false);
            animator.SetBool(HashWalk, false);
            animator.SetBool(HashAttack, false);
            if (hasSkillParam) animator.SetBool(HashSkill, false);
            if (hasSkillOneParam) animator.SetBool(HashSkillOne, false);
            if (hasSkillTwoParam) animator.SetBool(HashSkillTwo, false);
            if (hasSkillThreeParam) animator.SetBool(HashSkillThree, false);
            if (hasSkillFourParam) animator.SetBool(HashSkillFour, false);
            animator.SetBool(HashHit, false);
            if (hasDeathParam) animator.SetBool(HashDeath, false);
        }
    }
}
