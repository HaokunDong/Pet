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
    /// Parameters (Bool type):
    ///   Idle, Walk, Attack, Hit, Death, SkillOne, SkillTwo, SkillThree, SkillFour
    /// 
    /// Parameters (Int type):
    ///   AttackIndex — used for multi-step combo attacks (0 = Attack1, 1 = Attack2, etc.)
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
    /// === MULTI-STEP COMBO ATTACK CONFIGURATION ===
    /// 
    /// For characters with comboCount > 1 in CharacterData:
    /// 
    /// Option A: BlendTree (recommended for simple cases)
    ///   - Create a BlendTree in the Attack state.
    ///   - Set Blend Parameter to "AttackIndex" (Int).
    ///   - Add motion fields: Attack1 clip at 0, Attack2 clip at 1, etc.
    ///   - Each clip must have Animation Events: OnAttackHit → OnComboWindowOpen → OnCancellablePoint → OnStateEnd.
    /// 
    /// Option B: Sub-State Machine with multiple Attack states
    ///   - Create Attack1, Attack2, etc. states inside a Sub-State Machine.
    ///   - Entry → Attack1: condition AttackIndex == 0
    ///   - Entry → Attack2: condition AttackIndex == 1
    ///   - Each state → Exit: condition Attack == false
    ///   - Each clip must have Animation Events: OnAttackHit → OnComboWindowOpen → OnCancellablePoint → OnStateEnd.
    /// 
    /// Animation Events order for each attack clip:
    ///   1. OnAttackHit — damage frame (required)
    ///   2. OnComboWindowOpen — opens combo input window (required for combo, optional for last step)
    ///   3. OnCancellablePoint — marks animation as cancellable / triggers combo skip (optional)
    ///   4. OnStateEnd — ends the animation state (required)
    /// 
    /// === ANIMATION CANCEL / FRAME SKIP ===
    /// 
    /// OnCancellablePoint is an optional Animation Event that enables two features:
    ///   1. Combo Skip Acceleration: if combo input was buffered (player pressed attack during
    ///      combo window), the animation immediately skips to the next attack step at this point.
    ///   2. Non-Attack Cancel: after this point, non-attack inputs (move, skill) can immediately
    ///      cancel the remaining animation frames and transition to the new state.
    /// 
    /// Configuration:
    ///   - Place OnCancellablePoint AFTER OnComboWindowOpen and BEFORE OnStateEnd.
    ///   - OnCancellablePoint and OnComboWindowOpen are independent events:
    ///     * OnComboWindowOpen controls "when can the next attack input be buffered"
    ///     * OnCancellablePoint controls "when can the animation be skipped/cancelled"
    ///   - If OnCancellablePoint is not configured, the animation cannot be cancelled early
    ///     (backward compatible — it will play to OnStateEnd as before).
    ///   - Can also be used in Skill animation clips to allow skill post-cast cancellation.
    /// 
    /// 
    /// For single-attack characters (comboCount = 1):
    ///   - No changes needed. AttackIndex defaults to 0.
    ///   - Existing Attack state with single clip works as before.
    ///   - OnComboWindowOpen event is optional (if absent, no combo chaining occurs).
    ///   - OnCancellablePoint event is optional (if absent, no frame skip/cancel occurs).
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

            // Create the appropriate state machine based on GameObject tag.
            // A Boss character used by the player (tag "Player") should use PlayerStateMachine.
            bool isPlayerControlled = gameObject.CompareTag("Player");
            if (isPlayerControlled)
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

            if (stateMachine.ChangeState<IdleState>())
            {
                ClearAttackStateIfNeeded();
            }
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

            if (stateMachine.ChangeState<WalkState>())
            {
                ClearAttackStateIfNeeded();
            }
        }

        /// <summary>
        /// Play normal attack animation. Delegates to state machine.
        /// In Entry/Exit mode, OnExit() of the current state clears its Bool,
        /// then OnEnter() of AttackState sets Attack Bool = true.
        /// Supports combo system via attackIndex parameter.
        /// </summary>
        /// <param name="attackIndex">Zero-based combo step index (0 = first attack).</param>
        public void PlayAttack(int attackIndex = 0)
        {
            if (stateMachine == null) return;

            // Configure the attack state with the correct index before transitioning
            var attackState = stateMachine.GetState<AttackState>();
            if (attackState != null)
            {
                attackState.SetAttackIndex(attackIndex);
            }

            stateMachine.ChangeState<AttackState>();

            // After entering AttackState, force Animator to play from EmptyState
            // to ensure the correct attack animation is selected based on AttackIndex.
            // This is necessary because if the previous attack just ended and we're
            // starting a new combo from step 0, the Animator might still be in the
            // previous attack state (e.g., Attack2) due to loop timing.
            // Also explicitly set AttackIndex on the Animator in case ChangeState
            // was a no-op (already in AttackState) and OnEnter didn't fire.
            if (animator != null)
            {
                animator.SetInteger(Animator.StringToHash("AttackIndex"), attackIndex);
                animator.Play("Base Layer.Attack.EmptyState", 0, 0f);
                animator.Update(0f);
            }
        }

        /// <summary>
        /// Play the next combo attack step without returning to Idle.
        /// Instead of exiting and re-entering AttackState (which causes same-frame
        /// Attack=false→true issues with Animator), this method keeps the state machine
        /// in AttackState and directly updates the AttackIndex parameter, then uses
        /// Animator.Play() to force the Animator into the target attack animation state.
        /// </summary>
        /// <param name="attackIndex">Zero-based combo step index for the next attack.</param>
        public void PlayComboNextAttack(int attackIndex)
        {
            if (stateMachine == null) return;

            // Update the attack index in the state (for consistency)
            var attackState = stateMachine.GetState<AttackState>();
            if (attackState != null)
            {
                attackState.SetAttackIndex(attackIndex);
            }

            // Clear protection so the state can be exited later by OnStateEnd
            stateMachine.ClearProtection();
            // Re-enable protection for the new attack step
            stateMachine.isAttacking = true;
            stateMachine.canBeInterrupted = false;

            // Directly update the Animator parameter and force-play the target state.
            // This avoids the Exit→Entry→SubStateMachine re-entry issue.
            if (animator != null)
            {
                animator.SetInteger(Animator.StringToHash("AttackIndex"), attackIndex);
                // Force the Animator to re-enter the Attack Sub-State Machine's EmptyState.
                // EmptyState has transitions to Attack1/Attack2 based on AttackIndex value.
                // Using the full path "Base Layer.Attack.EmptyState" for Sub-State Machine states.
                animator.Play("Base Layer.Attack.EmptyState", 0, 0f);
                // Force Animator to evaluate transitions immediately in the same frame,
                // so it transitions from EmptyState to the correct attack state without
                // showing a blank frame.
                animator.Update(0f);
            }
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
        /// Clears the protection so the character can transition to other states.
        /// For attack states, checks with CombatSystem if a combo continuation is buffered.
        /// If combo continues, plays the next attack step without returning to Idle.
        /// Otherwise, transitions back to Idle state.
        /// Applicable to Attack, Skill, and Hit states.
        /// </summary>
        public void ClearCurrentState()
        {
            if (stateMachine == null) return;

            // Only process if we're actually in a protected state (Attack, Skill, Hit).
            // If already in Idle/Walk, this is a stale event from a looping animation — ignore it.
            if (stateMachine.isIdle || stateMachine.isWalking)
            {
                return;
            }

            // Check if this is an attack state ending and combo should continue
            if (stateMachine.isAttacking && combatSystem != null)
            {
                if (combatSystem.HandleComboOnStateEnd())
                {
                    // Combo continues — CombatSystem has already initiated the next step
                    // via PlayComboNextAttack(). Do NOT unlock facing or return to Idle.
                    return;
                }
            }

            // Unlock facing direction now that the animation has fully finished.
            facingLocked = false;

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
        /// Cancel the current animation immediately (skip remaining frames).
        /// Unlike ClearCurrentState(), this does NOT check combo continuation —
        /// it unconditionally ends the current animation, unlocks facing, clears protection,
        /// and transitions to Idle. The caller is responsible for initiating the next state
        /// (e.g., move or skill) after this method returns.
        /// Called by CombatSystem.TryCancelAnimation() when a non-attack input is received
        /// during the cancellable window.
        /// </summary>
        public void CancelCurrentAnimation()
        {
            if (stateMachine == null) return;

            // Only process if we're actually in a protected state (Attack, Skill, Hit).
            if (stateMachine.isIdle || stateMachine.isWalking)
            {
                return;
            }

            // Unlock facing direction
            facingLocked = false;

            // Clear protection so state can be changed
            stateMachine.ClearProtection();

            // Transition to Idle (caller will immediately transition to the desired state)
            if (!stateMachine.isDead && !stateMachine.isIdle && !stateMachine.isWalking)
            {
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
        /// Resets combo state so the combo chain is broken on hit.
        /// </summary>
        public void PlayHit()
        {
            if (stateMachine == null) return;

            // Reset combo state on hit — combo chain is broken.
            // NOTE: intentionally NOT calling ClearAttackStateIfNeeded() here.
            // The attack's damage frame event should still fire even if we get hit.
            if (combatSystem != null)
            {
                combatSystem.ResetComboState();
            }

            stateMachine.ChangeState<HitState>();
        }

        /// <summary>
        /// Play death animation. Uses ForceChangeState to bypass all protection.
        /// Clears any pending attack state and skill state.
        /// </summary>
        public void PlayDeath()
        {
            if (stateMachine == null) return;

            facingLocked = false; // Ensure facing is unlocked
            ClearAttackStateIfNeeded();
            ResetAllBools();
            stateMachine.ForceChangeState<DeathState>();
        }

        /// <summary>
        /// Reset the state machine to Idle. Used when characters are recycled by the object pool.
        /// </summary>
        public void ResetStateMachine()
        {
            facingLocked = false; // Ensure facing is unlocked
            if (stateMachine != null)
            {
                ResetAllBools();
                stateMachine.Reset();
            }
        }

        /// <summary>
        /// When true, facing direction is locked and cannot be changed.
        /// Used during attacks to prevent jitter from overlapping targets.
        /// </summary>
        private bool facingLocked;

        /// <summary>
        /// Lock the current facing direction. While locked, SetFacingDirection and FaceTowards
        /// will be ignored. Call UnlockFacing() when the attack/action completes.
        /// </summary>
        public void LockFacing()
        {
            facingLocked = true;
        }

        /// <summary>
        /// Unlock the facing direction so it can be changed again.
        /// </summary>
        public void UnlockFacing()
        {
            facingLocked = false;
        }

        /// <summary>
        /// Whether facing is currently locked.
        /// </summary>
        public bool IsFacingLocked => facingLocked;

        /// <summary>
        /// Flip the sprite to face the movement direction.
        /// Positive moveDirection = face right, negative = face left.
        /// Respects the defaultFacesRight setting for sprites with different native orientations.
        /// Ignored while facing is locked (during attacks).
        /// </summary>
        public void SetFacingDirection(float moveDirection)
        {
            if (facingLocked) return;
            if (Mathf.Approximately(moveDirection, 0f)) return;

            int newFacing = moveDirection > 0f ? 1 : -1;
            FacingDirection = newFacing;

            // If sprite natively faces right: flip when we want to face left
            // If sprite natively faces left:  flip when we want to face right
            bool wantFaceRight = FacingDirection > 0;
            spriteRenderer.flipX = defaultFacesRight ? !wantFaceRight : wantFaceRight;
        }

        /// <summary>
        /// Face towards a world position.
        /// Uses a small deadzone to prevent flip-flopping when overlapping with the target.
        /// Ignored while facing is locked (during attacks).
        /// </summary>
        public void FaceTowards(Vector3 targetPosition)
        {
            if (facingLocked) return;
            float direction = targetPosition.x - transform.position.x;
            // Deadzone: if the target is extremely close horizontally, don't change facing.
            if (Mathf.Abs(direction) < 0.15f) return;
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
