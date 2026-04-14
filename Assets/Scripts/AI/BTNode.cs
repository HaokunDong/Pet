namespace PetGame.AI
{
    /// <summary>
    /// Possible return states for a behavior tree node.
    /// </summary>
    public enum BTState
    {
        Success,
        Failure,
        Running
    }

    /// <summary>
    /// Abstract base class for all behavior tree nodes.
    /// </summary>
    public abstract class BTNode
    {
        /// <summary>
        /// Execute this node and return its state.
        /// </summary>
        public abstract BTState Execute();
    }
}
