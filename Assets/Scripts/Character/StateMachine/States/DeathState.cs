using UnityEngine;

namespace PetGame.States
{
    /// <summary>
    /// Death state: character has died.
    /// Sets isDead = true on entry and triggers Death animation.
    /// Once entered, this state cannot be exited (CanExit always returns false).
    /// Only ForceChangeState can transition into this state (bypasses CanExit checks).
    /// Uses Bool parameter "Death" in Animator Controller.
    /// </summary>
    public class DeathState : StateBase
    {
        private static readonly int HashDeath = Animator.StringToHash("Death");

        /// <summary>Whether the Animator Controller has a Death parameter.</summary>
        private bool hasDeathParam;

        public DeathState(EntityStateMachine stateMachine) : base(stateMachine) { }

        /// <summary>
        /// Cache whether the Death parameter exists in the Animator Controller.
        /// </summary>
        public void CacheParameterFlags(bool hasDeath)
        {
            hasDeathParam = hasDeath;
        }

        public override void OnEnter()
        {
            machine.isDead = true;
            machine.canBeInterrupted = false;

            if (machine.Animator != null && hasDeathParam)
            {
                machine.Animator.SetBool(HashDeath, true);
            }
        }

        public override void OnExit()
        {
            // Death state should not normally be exited.
            // Only Reset() can bring the character back to life.
            machine.isDead = false;

            if (machine.Animator != null && hasDeathParam)
            {
                machine.Animator.SetBool(HashDeath, false);
            }
        }

        /// <summary>
        /// Death state can always be entered (used with ForceChangeState).
        /// </summary>
        public override bool CanEnter()
        {
            return true;
        }

        /// <summary>
        /// Death state cannot be exited through normal transitions.
        /// Only Reset() bypasses this by directly manipulating state.
        /// </summary>
        public override bool CanExit()
        {
            return false;
        }
    }
}
