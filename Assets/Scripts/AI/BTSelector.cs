using System.Collections.Generic;

namespace PetGame.AI
{
    /// <summary>
    /// Selector (OR) node: executes children in order until one succeeds.
    /// Returns Success if any child succeeds, Failure if all fail.
    /// </summary>
    public class BTSelector : BTNode
    {
        private readonly List<BTNode> children = new List<BTNode>();
        private int currentIndex = 0;

        public BTSelector(params BTNode[] nodes)
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

                if (state == BTState.Success)
                {
                    currentIndex = 0;
                    return BTState.Success;
                }
            }

            currentIndex = 0;
            return BTState.Failure;
        }
    }
}
