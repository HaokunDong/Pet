using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Walk state: character is moving, fully interruptible.
    /// Sets isWalking = true and canBeInterrupted = true on entry.
    /// Uses Bool parameter "Walk" in Animator Controller.
    /// </summary>
    public class WalkState : StateBase
    {
        private static readonly int HashWalk = Animator.StringToHash("Walk");

        public WalkState(EntityStateMachine stateMachine) : base(stateMachine) { }

        public override void OnEnter()
        {
            machine.isWalking = true;
            machine.canBeInterrupted = true;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashWalk, true);
            }
        }

        public override void OnExit()
        {
            machine.isWalking = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashWalk, false);
            }
        }

        public override bool CanEnter()
        {
            return true;
        }

        public override bool CanExit()
        {
            return machine.canBeInterrupted;
        }
    }
}
