using System.Collections.Generic;

namespace PetGame.AI
{
    /// <summary>
    /// Sequence (AND) node: executes children in order until one fails.
    /// Returns Failure if any child fails, Success if all succeed.
    /// </summary>
    public class BTSequence : BTNode
    {
        private readonly List<BTNode> children = new List<BTNode>();
        private int currentIndex = 0;

        public BTSequence(params BTNode[] nodes)
        {
            children.AddRange(nodes);
        }

        public void AddChild(BTNode node)
        {
            children.Add(node);
        }

        public override BTState Execute()
        {
            for (; currentIndex < children.Count; currentIndex++)
            {
                BTState state = children[currentIndex].Execute();

                if (state == BTState.Running)
                    return BTState.Running;

                if (state == BTState.Failure)
                {
                    currentIndex = 0;
                    return BTState.Failure;
                }
            }

            currentIndex = 0;
            return BTState.Success;
        }
    }
}
