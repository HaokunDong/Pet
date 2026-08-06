using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Attack state: character is performing a normal attack.
    /// Sets isAttacking = true and canBeInterrupted = false on entry (protection active).
    /// The protection is cleared by frame event callbacks via ClearProtection().
    /// Uses Bool parameter "Attack" in Animator Controller.
    /// Supports multi-step combo via Int parameter "AttackIndex" (0 = first attack, 1 = second, etc.).
    /// </summary>
    public class AttackState : StateBase
    {
        private static readonly int HashAttack = Animator.StringToHash("Attack");
        private static readonly int HashAttackIndex = Animator.StringToHash("AttackIndex");

        /// <summary>
        /// Current attack index for combo system. 0 = first attack step.
        /// Set by CombatSystem before entering this state.
        /// </summary>
        private int attackIndex = 0;

        public AttackState(EntityStateMachine stateMachine) : base(stateMachine) { }

        /// <summary>
        /// Set the attack index before transitioning into this state.
        /// Called by CharacterAnimator/CombatSystem to specify which combo step to play.
        /// </summary>
        /// <param name="index">Zero-based combo step index.</param>
        public void SetAttackIndex(int index)
        {
            attackIndex = index;
        }

        public override void OnEnter()
        {
            machine.isAttacking = true;
            machine.canBeInterrupted = false;

            if (machine.Animator != null)
            {
                machine.Animator.SetInteger(HashAttackIndex, attackIndex);
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
