using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Attack state: character is performing a normal attack.
    /// Sets isAttacking = true and canBeInterrupted = false on entry (protection active).
    /// The protection is cleared by frame event callbacks via ClearProtection().
    /// Uses Bool parameter "Attack" in Animator Controller.
    /// </summary>
    public class AttackState : StateBase
    {
        private static readonly int HashAttack = Animator.StringToHash("Attack");

        public AttackState(EntityStateMachine stateMachine) : base(stateMachine) { }

        public override void OnEnter()
        {
            machine.isAttacking = true;
            machine.canBeInterrupted = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashAttack, true);
            }
        }

        public override void OnExit()
        {
            machine.isAttacking = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetBool(HashAttack, false);
            }
        }

        public override bool CanEnter()
        {
            // Can only enter Attack if current state allows interruption
            return machine.canBeInterrupted;
        }

        public override bool CanExit()
        {
            return machine.canBeInterrupted;
        }
    }
}
