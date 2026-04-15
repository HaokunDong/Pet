using System.Collections.Generic;

namespace PetGame.AI
{
    /// <summary>
    /// Selector (OR) node: executes children in priority order until one succeeds.
    /// This is a reactive selector: every tick it reevaluates from the first child,
    /// so higher-priority behaviors (such as combat) can immediately interrupt lower-priority ones (such as patrol).
    /// Returns Success if any child succeeds, Failure if all fail.
    /// </summary>
    public class BTSelector : BTNode
    {
        private readonly List<BTNode> children = new List<BTNode>();

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
            for (int i = 0; i < children.Count; i++)
            {
                BTState state = children[i].Execute();

                if (state == BTState.Running)
                    return BTState.Running;

                if (state == BTState.Success)
                    return BTState.Success;
            }

            return BTState.Failure;
        }
    }
}