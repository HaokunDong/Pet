namespace PetGame.AI
{
    /// <summary>
    /// Behavior tree runner. Holds the root node and drives execution via Tick().
    /// </summary>
    public class BehaviorTree
    {
        private readonly BTNode root;

        /// <summary>
        /// Whether the tree is currently enabled and should tick.
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        public BehaviorTree(BTNode root)
        {
            this.root = root;
        }

        /// <summary>
        /// Execute one tick of the behavior tree.
        /// Call this from Update() or a fixed interval.
        /// </summary>
        public BTState Tick()
        {
            if (!IsEnabled || root == null)
                return BTState.Failure;

            return root.Execute();
        }
    }
}
