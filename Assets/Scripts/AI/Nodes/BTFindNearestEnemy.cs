using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// Condition node: finds the nearest enemy and sets it as the current target.
    /// Returns Success if an enemy is found, Failure otherwise.
    /// </summary>
    public class BTFindNearestEnemy : BTNode
    {
        private readonly BTContext context;
        private readonly float detectionRange;

        public BTFindNearestEnemy(BTContext context, float detectionRange = 10f)
        {
            this.context = context;
            this.detectionRange = detectionRange;
        }

        public override BTState Execute()
        {
            CharacterEntity owner = context.Owner;
            if (owner == null || !owner.RuntimeStats.IsAlive)
                return BTState.Failure;

            // Validate current target — clear if dead or destroyed
            if (context.CurrentTarget != null &&
                (context.CurrentTarget.gameObject == null || !context.CurrentTarget.RuntimeStats.IsAlive))
            {
                context.CurrentTarget = null;
                context.IsInCombat = false;
            }

            // Determine which tags to search for based on owner type
            string targetTag = (owner.RuntimeStats.characterType == CharacterType.Player)
                ? "Enemy"
                : "Player";

            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            float closestDist = float.MaxValue;
            CharacterEntity closest = null;

            foreach (GameObject go in candidates)
            {
                CharacterEntity entity = go.GetComponent<CharacterEntity>();
                if (entity == null || !entity.RuntimeStats.IsAlive) continue;

                float dist = Vector2.Distance(owner.transform.position, go.transform.position);
                if (dist < detectionRange && dist < closestDist)
                {
                    closestDist = dist;
                    closest = entity;
                }
            }

            if (closest != null)
            {
                // If target changed, reset combat state so attack node re-establishes it
                if (context.CurrentTarget != closest)
                {
                    context.IsInCombat = false;
                }
                context.CurrentTarget = closest;
                return BTState.Success;
            }

            // No target found — clear all combat state
            context.CurrentTarget = null;
            context.IsInCombat = false;
            return BTState.Failure;
        }
    }
}
