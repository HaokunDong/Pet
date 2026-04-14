namespace PetGame.AI
{
    /// <summary>
    /// Inverter decorator node: inverts the result of its child.
    /// Success becomes Failure and vice versa. Running stays Running.
    /// </summary>
    public class BTInverter : BTNode
    {
        private readonly BTNode child;

        public BTInverter(BTNode child)
        {
            this.child = child;
        }

        public override BTState Execute()
        {
            BTState state = child.Execute();

            switch (state)
            {
                case BTState.Success: return BTState.Failure;
                case BTState.Failure: return BTState.Success;
                default:              return BTState.Running;
            }
        }
    }
}
