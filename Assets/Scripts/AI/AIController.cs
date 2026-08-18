using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// AI controller component. Builds and drives the new Wander → Combat(Engage/Strike) → PostCombat
    /// behavior tree for a character. Player, MinorEnemy and Boss share the same tree structure; skill
    /// priority inside Strike is gated at runtime by the character type (MinorEnemy uses normal attack only).
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class AIController : MonoBehaviour
    {
        [Header("Detection & Wander")]
        [Tooltip("Detection range for finding enemies.")]
        public float detectionRange = 10f;

        [Tooltip("Patrol/wander range (distance from spawn point).")]
        public float patrolRange = 5f;

        [Tooltip("Wall detection raycast distance.")]
        public float wallDetectDistance = 0.5f;

        [Header("Combat Hysteresis")]
        [Tooltip("Extra distance beyond engage distance before Strike falls back to Engage (anti-jitter).")]
        [Min(0f)]
        public float engageExitHysteresis = 0.1f;

        [Tooltip("Extra distance beyond detectionRange before AI disengages from the current target.")]
        [Min(0f)]
        public float disengageHysteresis = 1.0f;

        [Header("PostCombat")]
        [Tooltip("Duration (seconds) of the idle cooldown after combat ends before returning to Wander.")]
        [Min(0f)]
        public float postCombatDuration = 1.5f;

        [Header("Wander Timing")]
        [Tooltip("Random range (seconds) for a single Wander walk segment length.")]
        public Vector2 wanderWalkDuration = new Vector2(1.5f, 3.5f);

        [Tooltip("Random range (seconds) for a single WanderPause idle duration.")]
        public Vector2 wanderPauseDuration = new Vector2(0.5f, 1.5f);

        private CharacterEntity entity;
        private BehaviorTree behaviorTree;
        private BTContext context;

        /// <summary>
        /// Whether the AI is currently active and ticking.
        /// </summary>
        public bool IsActive { get; private set; } = true;

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
        }

        private void Start()
        {
            if (entity != null && entity.IsInitialized)
            {
                BuildTree();
            }
        }

        /// <summary>
        /// Initialize the AI controller after the entity is ready.
        /// Always activates the AI so it begins ticking immediately (important for
        /// pooled objects whose IsActive may have been set to false by PauseAI).
        /// </summary>
        public void InitializeAI()
        {
            if (entity == null)
                entity = GetComponent<CharacterEntity>();
            BuildTree();
            IsActive = true;
        }

        /// <summary>
        /// Build the unified Wander → Combat → PostCombat behavior tree.
        /// </summary>
        private void BuildTree()
        {
            context = new BTContext(entity)
            {
                PatrolRange = patrolRange,
                WallDetectDistance = wallDetectDistance,
                EngageExitHysteresis = engageExitHysteresis,
                DisengageHysteresis = disengageHysteresis,
                PostCombatDuration = postCombatDuration,
                WanderWalkDuration = wanderWalkDuration,
                WanderPauseDuration = wanderPauseDuration,
                CurrentState = AIState.Wander
            };

            behaviorTree = new BehaviorTree(BuildUnifiedTree());
        }

        /// <summary>
        /// Unified tree used by Player / MinorEnemy / Boss:
        ///   Selector(
        ///     Sequence(FindNearestEnemy, Combat),   // highest priority — engage any valid target
        ///     PostCombat,                           // short idle cooldown after a fight ends
        ///     Wander                                // default random wander behavior
        ///   )
        /// MinorEnemy's skill branch is suppressed inside BTCombat based on RuntimeStats.characterType.
        /// </summary>
        private BTNode BuildUnifiedTree()
        {
            var findEnemy = new BTFindNearestEnemy(context, detectionRange);
            var combat = new BTCombat(context);
            var postCombat = new BTPostCombat(context);
            var wander = new BTWander(context);

            var combatSeq = new BTSequence(findEnemy, combat);

            return new BTSelector(combatSeq, postCombat, wander);
        }

        private void Update()
        {
            if (!IsActive || behaviorTree == null) return;
            if (entity == null || !entity.RuntimeStats.IsAlive) return;

            // Keep runtime hysteresis / duration tunables in sync with Inspector tweaks.
            SyncContextTunables();

            behaviorTree.Tick();
        }

        /// <summary>
        /// Propagate Inspector tweaks into the BTContext so designers can iterate at runtime.
        /// </summary>
        private void SyncContextTunables()
        {
            if (context == null) return;
            context.EngageExitHysteresis = engageExitHysteresis;
            context.DisengageHysteresis = disengageHysteresis;
            context.PostCombatDuration = postCombatDuration;
            context.WanderWalkDuration = wanderWalkDuration;
            context.WanderPauseDuration = wanderPauseDuration;
            context.PatrolRange = patrolRange;
            context.WallDetectDistance = wallDetectDistance;
        }

        /// <summary>
        /// Pause the AI (stop ticking the behavior tree).
        /// </summary>
        public void PauseAI()
        {
            IsActive = false;
            if (entity != null && entity.CharAnimator != null)
            {
                entity.CharAnimator.PlayIdle();
            }
        }

        /// <summary>
        /// Resume the AI: reset the state machine to a clean Wander state so no stale
        /// target / combat sub-state is carried over from the manual-control session.
        /// </summary>
        public void ResumeAI()
        {
            if (entity == null)
            {
                entity = GetComponent<CharacterEntity>();
            }

            if (behaviorTree == null || context == null)
            {
                BuildTree();
            }
            else
            {
                context.CurrentTarget = null;
                context.CurrentState = AIState.Wander;
                context.HasFiredFirstStrike = false;
                context.TargetWasBehindOnEngage = false;
            }

            IsActive = true;

            if (entity != null && entity.CharAnimator != null)
            {
                entity.CharAnimator.PlayIdle();
            }
        }

        /// <summary>
        /// Completely stop and rebuild the AI tree (e.g., after mode switch).
        /// </summary>
        public void ResetAI()
        {
            IsActive = true;
            BuildTree();
        }
    }
}
