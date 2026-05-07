using System;
using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Base state machine for all character entities (Player, Enemy, Boss).
    /// Manages state registration, Bool condition variables, and state transitions.
    /// 
    /// === USAGE GUIDE ===
    /// 
    /// 1. HOW TO ADD A NEW STATE:
    ///    - Create a new class inheriting from StateBase in Scripts/Character/StateMachine/States/
    ///    - Override OnEnter() to set relevant Bool conditions and trigger animations
    ///    - Override OnExit() to clear relevant Bool conditions
    ///    - Override CanEnter()/CanExit() if the state has special transition rules
    ///    - Register the state in the appropriate sub-machine's Initialize() method
    ///
    /// 2. HOW TO MODIFY TRANSITION RULES:
    ///    - Adjust the Bool condition checks in CanEnter()/CanExit() of the relevant state class
    ///    - For global rules, modify ChangeState() in this base class
    ///
    /// 3. HOW TO CREATE A NEW CHARACTER TYPE STATE MACHINE:
    ///    - Create a new class inheriting from EntityStateMachine
    ///    - Override Initialize() to register the states needed for that character type
    ///    - Override OnStateChanged() if you need custom logic on transitions
    /// </summary>
    public class EntityStateMachine
    {
        // ==================== Bool Condition Variables ====================
        // These drive all state transition decisions.

        /// <summary>Whether the character is currently in Idle state.</summary>
        public bool isIdle;

        /// <summary>Whether the character is currently in Walk state.</summary>
        public bool isWalking;

        /// <summary>Whether the character is currently in Attack state (protection active).</summary>
        public bool isAttacking;

        /// <summary>Whether the character is currently in Skill state (protection active).</summary>
        public bool isUsingSkill;

        /// <summary>Whether the character is currently in Hit state (protection active).</summary>
        public bool isHit;

        /// <summary>Whether the character is dead.</summary>
        public bool isDead;

        /// <summary>
        /// Whether the current state can be interrupted.
        /// Set to false during Attack/Skill/Hit protection periods.
        /// Set to true by frame event callbacks (ClearProtection) or when entering Idle/Walk.
        /// </summary>
        public bool canBeInterrupted;

        // ==================== State Management ====================

        /// <summary>Dictionary mapping state types to their instances.</summary>
        private Dictionary<Type, IState> states = new Dictionary<Type, IState>();

        /// <summary>The currently active state.</summary>
        private IState currentState;

        /// <summary>The Animator component for triggering animations.</summary>
        protected Animator animator;

        /// <summary>Reference to the owning CharacterAnimator component.</summary>
        protected CharacterAnimator characterAnimator;

        // ==================== Read-Only Properties ====================

        /// <summary>The currently active state instance.</summary>
        public IState CurrentState => currentState;

        /// <summary>Whether the character is in Hit state (for external queries).</summary>
        public bool IsInHitState => isHit;

        /// <summary>Whether the character is in Skill state (for external queries).</summary>
        public bool IsInSkillState => isUsingSkill;

        /// <summary>Whether the character is in Attack state (for external queries).</summary>
        public bool IsAttacking => isAttacking;

        /// <summary>Whether the character is dead (for external queries).</summary>
        public bool IsDead => isDead;

        /// <summary>The Animator component (accessible by states for triggering animations).</summary>
        public Animator Animator => animator;

        /// <summary>The CharacterAnimator component (accessible by states).</summary>
        public CharacterAnimator CharAnimator => characterAnimator;

        // ==================== Initialization ====================

        /// <summary>
        /// Initialize the state machine with required references.
        /// Call this after construction to set up the Animator and CharacterAnimator references.
        /// </summary>
        /// <param name="anim">The Animator component on the character.</param>
        /// <param name="charAnimator">The CharacterAnimator component on the character.</param>
        public virtual void Initialize(Animator anim, CharacterAnimator charAnimator)
        {
            animator = anim;
            characterAnimator = charAnimator;
        }

        // ==================== State Registration ====================

        /// <summary>
        /// Register a state instance in the state machine.
        /// Each state type can only be registered once.
        /// </summary>
        /// <typeparam name="T">The concrete state type.</typeparam>
        /// <param name="state">The state instance to register.</param>
        public void RegisterState<T>(T state) where T : IState
        {
            var type = typeof(T);
            if (!states.ContainsKey(type))
            {
                states[type] = state;
            }
        }

        /// <summary>
        /// Get a registered state instance by type.
        /// Returns null if the state type is not registered.
        /// </summary>
        /// <typeparam name="T">The state type to retrieve.</typeparam>
        public T GetState<T>() where T : class, IState
        {
            if (states.TryGetValue(typeof(T), out IState state))
            {
                return state as T;
            }
            return null;
        }

        // ==================== State Transitions ====================

        /// <summary>
        /// Attempt to change to a new state. Checks both CanExit on current state
        /// and CanEnter on target state. If either returns false, the transition is
        /// silently rejected and the current state remains active.
        /// 
        /// Special case: HitState re-entry is always allowed (Hit can interrupt Hit).
        /// In Entry/Exit mode, re-entry triggers OnExit() (Bool=false → Exit) then
        /// OnEnter() (Bool=true → Entry → Hit), replaying the animation from the start.
        /// </summary>
        /// <typeparam name="T">The target state type.</typeparam>
        /// <returns>True if the transition was successful, false if rejected.</returns>
        public bool ChangeState<T>() where T : class, IState
        {
            // Safety: if dead, reject all normal transitions
            if (isDead) return false;

            var type = typeof(T);
            if (!states.TryGetValue(type, out IState targetState))
            {
                Debug.LogWarning($"[EntityStateMachine] State {type.Name} is not registered.");
                return false;
            }

            // If we're already in this state and it's not HitState (which allows re-entry), skip
            if (currentState == targetState && type != typeof(States.HitState))
            {
                return false;
            }

            // Special case: HitState re-entry bypasses CanExit check
            // (Hit can always interrupt Hit, even during protection period)
            bool isHitReentry = (type == typeof(States.HitState) && currentState is States.HitState);

            // Check transition conditions
            if (currentState != null && !isHitReentry && !currentState.CanExit())
            {
                return false;
            }

            if (!targetState.CanEnter())
            {
                return false;
            }

            // Execute transition
            ExecuteTransition(targetState);
            return true;
        }

        /// <summary>
        /// Force a state change without checking CanExit/CanEnter conditions.
        /// Used for highest-priority transitions like Death.
        /// </summary>
        /// <typeparam name="T">The target state type.</typeparam>
        public void ForceChangeState<T>() where T : class, IState
        {
            var type = typeof(T);
            if (!states.TryGetValue(type, out IState targetState))
            {
                Debug.LogWarning($"[EntityStateMachine] State {type.Name} is not registered.");
                return;
            }

            ExecuteTransition(targetState);
        }

        /// <summary>
        /// Internal method to execute the actual state transition.
        /// Calls OnExit on the old state, updates currentState, then calls OnEnter on the new state.
        /// </summary>
        private void ExecuteTransition(IState targetState)
        {
            IState previousState = currentState;

            if (currentState != null)
            {
                currentState.OnExit();
            }

            currentState = targetState;
            currentState.OnEnter();

            OnStateChanged(previousState, currentState);
        }

        // ==================== Per-Frame Update ====================

        /// <summary>
        /// Called every frame. Delegates to the current state's OnUpdate method.
        /// </summary>
        public void Update()
        {
            if (currentState != null)
            {
                currentState.OnUpdate();
            }
        }

        // ==================== Utility Methods ====================

        /// <summary>
        /// Clear the protection flag, allowing the current state to be exited.
        /// Called by frame event callbacks when an animation finishes its critical section.
        /// </summary>
        public void ClearProtection()
        {
            canBeInterrupted = true;
        }

        /// <summary>
        /// Reset the state machine to its initial state.
        /// Clears all Bool conditions and forces transition to Idle.
        /// Used when characters are returned to the object pool and re-activated.
        /// </summary>
        public void Reset()
        {
            // Clear all Bool conditions
            isIdle = false;
            isWalking = false;
            isAttacking = false;
            isUsingSkill = false;
            isHit = false;
            isDead = false;
            canBeInterrupted = true;

            // Force back to Idle state
            if (states.ContainsKey(typeof(States.IdleState)))
            {
                if (currentState != null)
                {
                    currentState.OnExit();
                }
                currentState = states[typeof(States.IdleState)];
                currentState.OnEnter();
            }
            else
            {
                currentState = null;
            }
        }

        // ==================== Virtual Hooks ====================

        /// <summary>
        /// Called after a state transition completes. Override in subclasses for custom logic.
        /// </summary>
        /// <param name="from">The state that was exited (may be null on first transition).</param>
        /// <param name="to">The state that was entered.</param>
        protected virtual void OnStateChanged(IState from, IState to)
        {
        }
    }
}
