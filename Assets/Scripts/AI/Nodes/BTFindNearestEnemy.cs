using UnityEngine;
using PetGame.Network;

namespace PetGame.AI
{
    /// <summary>
    /// Condition + target-maintenance node: keeps <see cref="BTContext.CurrentTarget"/> up to date.
    /// 
    /// Rules:
    /// - If the current target is dead / missing, clear it and search for a new nearest enemy
    ///   within detectionRange (by tag: Player→Enemy, otherwise→Player).
    /// - If the current target is alive and still within detectionRange + DisengageHysteresis,
    ///   keep it as the target (no jitter-prone re-selection every frame).
    /// - If the current target has drifted beyond detectionRange + DisengageHysteresis, treat it
    ///   as disengaged: clear it and search for a new nearest enemy; if none found, report Failure
    ///   so the root Selector falls through to PostCombat / Wander.
    /// 
    /// When acquiring a new target (either fresh or replacing a dead/disengaged one):
    /// - Reset per-encounter combat flags: HasFiredFirstStrike = false.
    /// - Compute TargetWasBehindOnEngage based on the owner's current facing vs the target's
    ///   horizontal position, so the Combat node's first-strike-behind rule can act on it.
    /// 
    /// Returns Success when a valid target is set, Failure otherwise.
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

            // Validate the existing target first — only replace it if it has died, been
            // deactivated/recycled (e.g. player switched character), or disengaged.
            if (context.CurrentTarget != null)
            {
                bool dead = context.CurrentTarget.gameObject == null
                            || !context.CurrentTarget.gameObject.activeInHierarchy
                            || !context.CurrentTarget.RuntimeStats.IsAlive;

                float distToCurrent = dead
                    ? float.MaxValue
                    : Vector2.Distance(owner.transform.position, context.CurrentTarget.transform.position);

                float disengageDist = detectionRange + context.DisengageHysteresis;

                if (!dead && distToCurrent <= disengageDist)
                {
                    // Keep current target — stable, no jitter from every-frame re-selection.
                    return BTState.Success;
                }

                // Current target is dead or too far — clear and re-scan.
                context.CurrentTarget = null;
            }

            // Scan for the nearest valid enemy within detectionRange.
            CharacterEntity newTarget = FindNearest(owner);

            if (newTarget == null)
            {
                // Log only occasionally to avoid spam (use frame count)
                if (Time.frameCount % 60 == 0)
                {
                    Debug.Log($"[BTFindNearestEnemy] '{owner.gameObject.name}': No enemy found within range={detectionRange}. Pos={owner.transform.position}");
                }
                return BTState.Failure;
            }

            // New target acquired — initialize per-encounter combat flags.
            context.CurrentTarget = newTarget;
            context.HasFiredFirstStrike = false;
            context.TargetWasBehindOnEngage = ComputeTargetBehind(owner, newTarget);

            return BTState.Success;
        }

        /// <summary>
        /// Tag-based nearest enemy search within detectionRange.
        /// Player → Enemy, otherwise → Player.
        /// </summary>
        private CharacterEntity FindNearest(CharacterEntity owner)
        {
            // Use GameObject tag instead of characterType so Boss characters
            // controlled by the player correctly target enemies.
            string targetTag = owner.gameObject.CompareTag("Player")
                ? "Enemy"
                : "Player";

            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            float closestDist = float.MaxValue;
            CharacterEntity closest = null;

            for (int i = 0; i < candidates.Length; i++)
            {
                GameObject go = candidates[i];
                // Skip deactivated/recycled objects (e.g. a previously controlled
                // character that was returned to the pool during a character switch).
                if (!go.activeInHierarchy) continue;

                // Skip MirrorCharacters — they are visual proxies for remote players
                // on the host. Boss AI should only target real LocalCharacters.
                if (go.GetComponent<MirrorCharacterTag>() != null) continue;

                CharacterEntity entity = go.GetComponent<CharacterEntity>();
                if (entity == null || !entity.RuntimeStats.IsAlive) continue;

                float dist = Vector2.Distance(owner.transform.position, go.transform.position);
                if (dist < detectionRange && dist < closestDist)
                {
                    closestDist = dist;
                    closest = entity;
                }
            }

            return closest;
        }

        /// <summary>
        /// True if the target sits on the opposite side of owner's current facing direction.
        /// </summary>
        private bool ComputeTargetBehind(CharacterEntity owner, CharacterEntity target)
        {
            if (owner.CharAnimator == null) return false;

            float dx = target.ColliderCenter.x - owner.ColliderCenter.x;
            if (Mathf.Approximately(dx, 0f)) return false;

            int targetDir = dx > 0f ? 1 : -1;
            return owner.CharAnimator.FacingDirection != targetDir;
        }
    }
}
