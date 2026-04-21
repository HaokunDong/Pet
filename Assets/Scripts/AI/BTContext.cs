using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// High-level AI state for the Wander → Combat(Engage/Strike) → PostCombat state machine.
    /// </summary>
    public enum AIState
    {
        Wander,
        WanderPause,
        Engage,
        Strike,
        PostCombat
    }

    /// <summary>
    /// Shared context/blackboard for behavior tree nodes.
    /// Holds references to the owning entity, current target, and state-machine data.
    /// </summary>
    public class BTContext
    {
        // ---------- Core references ----------
        public CharacterEntity Owner { get; set; }
        public CharacterEntity CurrentTarget { get; set; }

        // ---------- Environment ----------
        public Vector3 PatrolOrigin { get; set; }
        public float PatrolRange { get; set; } = 5f;

        /// <summary>Distance for wall detection raycast. Configurable via AIController.</summary>
        public float WallDetectDistance { get; set; } = 0.5f;

        /// <summary>Layer mask for terrain/wall detection.</summary>
        public int TerrainLayerMask { get; set; }

        // ---------- State machine ----------
        public AIState CurrentState { get; set; } = AIState.Wander;

        // --- Wander sub-state ---
        /// <summary>Current random wander target X coordinate (world space).</summary>
        public float WanderTargetX { get; set; }
        /// <summary>Time at which the current Wander/WanderPause phase ends.</summary>
        public float WanderPhaseEndTime { get; set; }
        /// <summary>Last wander horizontal direction (+1 right / -1 left). Used to bias reverse sampling after hitting a wall.</summary>
        public int LastWanderDirection { get; set; }
        /// <summary>If true, next wander sampling should bias toward the opposite of LastWanderDirection (after a wall hit).</summary>
        public bool WanderReverseBias { get; set; }

        // --- Combat sub-state ---
        /// <summary>Time of the last successful attack trigger.</summary>
        public float LastAttackTime { get; set; }
        /// <summary>Whether the first strike of this combat encounter has been fired.</summary>
        public bool HasFiredFirstStrike { get; set; }
        /// <summary>When AI enters Combat or switches target, whether the target was behind the owner (opposite to facing direction).</summary>
        public bool TargetWasBehindOnEngage { get; set; }

        // --- PostCombat sub-state ---
        /// <summary>Time at which the PostCombat cooldown ends.</summary>
        public float PostCombatEndTime { get; set; }

        // ---------- Tunables (copied from AIController each tick) ----------
        /// <summary>Extra distance beyond engageDistance before Strike falls back to Engage (anti-jitter hysteresis).</summary>
        public float EngageExitHysteresis { get; set; } = 0.25f;
        /// <summary>Extra distance beyond detectionRange before AI disengages completely.</summary>
        public float DisengageHysteresis { get; set; } = 1.0f;
        /// <summary>Duration (seconds) of the PostCombat idle cooldown.</summary>
        public float PostCombatDuration { get; set; } = 1.5f;
        /// <summary>Random range for a single Wander walk segment length (seconds).</summary>
        public Vector2 WanderWalkDuration { get; set; } = new Vector2(1.5f, 3.5f);
        /// <summary>Random range for a single WanderPause idle duration (seconds).</summary>
        public Vector2 WanderPauseDuration { get; set; } = new Vector2(0.5f, 1.5f);

        public BTContext(CharacterEntity owner)
        {
            Owner = owner;
            PatrolOrigin = owner.transform.position;

            // Use "Wall" layer for wall/terrain detection to avoid ground collider interference
            int wallLayer = LayerMask.NameToLayer("Wall");
            if (wallLayer != -1)
            {
                TerrainLayerMask = LayerMask.GetMask("Wall");
            }
            else
            {
                // Fallback to Default layer if "Wall" layer doesn't exist
                TerrainLayerMask = LayerMask.GetMask("Default");
                Debug.LogWarning("[BTContext] Physics layer 'Wall' not found. Falling back to 'Default' layer for wall detection. " +
                    "Please create a 'Wall' layer in Edit → Project Settings → Tags and Layers and assign it to wall/terrain objects for accurate detection.");
            }
        }
    }
}
