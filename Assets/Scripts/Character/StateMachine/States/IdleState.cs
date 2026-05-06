using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Idle state: character is standing still, fully interruptible.
    /// Sets isIdle = true and canBeInterrupted = true on entry.
    /// Uses Bool parameter "Idle" in Animator Controller.
    /// 
    /// In Entry/Exit mode, Idle is the DEFAULT path from Entry (no condition).
    /// When no other Bool is true, the Animator automatically enters Idle.
    /// When Idle Bool becomes false (OnExit), the Animator exits to Exit node
    /// and re-evaluates from Entry to enter the next active state.
    /// </summary>
    public class IdleState : StateBase
    {
        private static readonly int HashIdle = Animator.StringToHash("Idle");

        public IdleState(EntityStateMachine stateMachine) : base(stateMachine) { }

        public override void OnEnter()
        {
            machine.isIdle = true;
            machine.canBeInterrupted = true;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashIdle, true);
                Debug.Log($"[IdleState] {machine.CharAnimator.gameObject.name}: OnEnter - Idle Bool set to TRUE");
            }
            else
            {
                Debug.LogWarning($"[IdleState] OnEnter - Animator is NULL! isIdle set to true but Bool not applied!");
            }
        }

        public override void OnExit()
        {
            machine.isIdle = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashIdle, false);
                Debug.Log($"[IdleState] {machine.CharAnimator.gameObject.name}: OnExit - Idle Bool set to FALSE");
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
