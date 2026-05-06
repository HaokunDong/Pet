using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Hit state: character is being hit/hurt.
    /// Sets isHit = true and canBeInterrupted = false on entry (protection active).
    /// Supports re-entry: in Entry/Exit mode, the state machine calls OnExit() (Bool=false → Exit)
    /// then OnEnter() (Bool=true → Entry → Hit), causing the animation to replay from the beginning.
    /// The protection is cleared by frame event callbacks via ClearProtection().
    /// Uses Bool parameter "Hit" in Animator Controller.
    /// </summary>
    public class HitState : StateBase
    {
        private static readonly int HashHit = Animator.StringToHash("Hit");

        public HitState(EntityStateMachine stateMachine) : base(stateMachine) { }

        public override void OnEnter()
        {
            // In Entry/Exit mode, re-entry is handled naturally:
            // OnExit() sets Hit Bool = false (exits to Exit node),
            // then OnEnter() sets Hit Bool = true (re-enters from Entry node).
            // The Animator replays the Hit animation from the beginning automatically.

            machine.isHit = true;
            machine.canBeInterrupted = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashHit, true);
            }
        }

        public override void OnExit()
        {
            machine.isHit = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashHit, false);
            }
        }

        /// <summary>
        /// Hit state can always be entered as long as the current state allows exit.
        /// This enables re-entry (hit while already in hit state).
        /// </summary>
        public override bool CanEnter()
        {
            return true;
        }

        /// <summary>
        /// Hit state exit is controlled by canBeInterrupted flag.
        /// However, Hit can always be interrupted by another Hit (re-entry).
        /// The state machine handles this special case in ChangeState.
        /// </summary>
        public override bool CanExit()
        {
            return machine.canBeInterrupted;
        }
    }
}
