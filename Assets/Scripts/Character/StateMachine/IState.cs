namespace PetGame
{
    /// <summary>
    /// Interface defining the contract for all states in the character action FSM.
    /// Each state must implement lifecycle methods and transition condition checks.
    /// </summary>
    public interface IState
    {
        /// <summary>
        /// Called when the state machine transitions into this state.
        /// Should set relevant Bool conditions and trigger animations.
        /// </summary>
        void OnEnter();

        /// <summary>
        /// Called when the state machine transitions out of this state.
        /// Should clear relevant Bool conditions.
        /// </summary>
        void OnExit();

        /// <summary>
        /// Called every frame while this state is active.
        /// Used for per-frame logic (e.g. checking conditions for auto-transitions).
        /// </summary>
        void OnUpdate();

        /// <summary>
        /// Determines whether this state can be entered based on current Bool conditions.
        /// </summary>
        /// <returns>True if the state machine is allowed to transition into this state.</returns>
        bool CanEnter();

        /// <summary>
        /// Determines whether this state can be exited based on current Bool conditions.
        /// Protected states (Attack, Skill, Hit) return false until their protection is cleared.
        /// </summary>
        /// <returns>True if the state machine is allowed to transition out of this state.</returns>
        bool CanExit();
    }
}
