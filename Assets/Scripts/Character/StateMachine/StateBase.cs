namespace PetGame
{
    /// <summary>
    /// Abstract base class for all character action states.
    /// Provides a reference back to the owning state machine and default implementations
    /// for lifecycle methods. Concrete states should override the methods they need.
    /// </summary>
    public abstract class StateBase : IState
    {
        /// <summary>
        /// Reference to the owning state machine.
        /// Used by concrete states to read/write Bool conditions and access the Animator.
        /// </summary>
        protected EntityStateMachine machine;

        /// <summary>
        /// Constructor that injects the owning state machine reference.
        /// </summary>
        /// <param name="stateMachine">The state machine that owns this state.</param>
        public StateBase(EntityStateMachine stateMachine)
        {
            machine = stateMachine;
        }

        /// <summary>
        /// Called when entering this state. Override to set Bool conditions and trigger animations.
        /// </summary>
        public abstract void OnEnter();

        /// <summary>
        /// Called when exiting this state. Override to clear Bool conditions.
        /// </summary>
        public abstract void OnExit();

        /// <summary>
        /// Called every frame while this state is active. Default implementation does nothing.
        /// Override for per-frame logic.
        /// </summary>
        public virtual void OnUpdate() { }

        /// <summary>
        /// Determines whether this state can be entered.
        /// Default: returns true (no entry restrictions).
        /// Override in protected states to add Bool condition checks.
        /// </summary>
        public virtual bool CanEnter()
        {
            return true;
        }

        /// <summary>
        /// Determines whether this state can be exited.
        /// Default: returns the machine's canBeInterrupted flag.
        /// Protected states (Attack, Skill, Hit) rely on this to prevent interruption.
        /// </summary>
        public virtual bool CanExit()
        {
            return machine.canBeInterrupted;
        }
    }
}
