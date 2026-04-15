using UnityEngine;

namespace PetGame.AI
{
    /// <summary>
    /// AI controller component. Builds and drives the behavior tree for a character.
    /// Supports different tree structures for Player AI, MinorEnemy, and Boss.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    public class AIController : MonoBehaviour
    {
        [Header("Settings")]
        [Tooltip("Detection range for finding enemies")]
        public float detectionRange = 10f;

        [Tooltip("Patrol range (distance from spawn point)")]
        public float patrolRange = 5f;

        [Tooltip("Wall detection raycast distance")]
        public float wallDetectDistance = 0.5f;

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
            if (entity.IsInitialized)
            {
                BuildTree();
            }
        }

        /// <summary>
        /// Initialize the AI controller after the entity is ready.
        /// </summary>
        public void InitializeAI()
        {
            if (entity == null)
                entity = GetComponent<CharacterEntity>();
            BuildTree();
        }

        /// <summary>
        /// Build the behavior tree based on character type.
        /// </summary>
        private void BuildTree()
        {
            context = new BTContext(entity)
            {
                PatrolRange = patrolRange,
                WallDetectDistance = wallDetectDistance
            };

            BTNode root;

            switch (entity.RuntimeStats.characterType)
            {
                case CharacterType.MinorEnemy:
                    root = BuildMinorEnemyTree();
                    break;
                case CharacterType.Boss:
                    root = BuildBossTree();
                    break;
                case CharacterType.Player:
                default:
                    root = BuildPlayerAITree();
                    break;
            }

            behaviorTree = new BehaviorTree(root);
        }

        /// <summary>
        /// Player AI tree: Patrol → Find Enemy → (Skill Attack | Normal Attack)
        /// </summary>
        private BTNode BuildPlayerAITree()
        {
            var findEnemy = new BTFindNearestEnemy(context, detectionRange);
            var checkInRange = new BTCheckEnemyInRange(context);
            var moveToTarget = new BTMoveToTarget(context);
            var attack = new BTAttack(context);
            var patrol = new BTPatrol(context);
            var checkSkill = new BTCheckSkillReady(context);
            var useSkill = new BTUseSkill(context, checkSkill);

            // Skill attack sequence: check skill ready → check in range → use skill
            var skillAttackSeq = new BTSequence(checkSkill, checkInRange, useSkill);

            // Normal attack sequence: check in range → attack
            var normalAttackSeq = new BTSequence(checkInRange, attack);

            // Attack selector: try skill first, then normal attack
            var attackSelector = new BTSelector(skillAttackSeq, normalAttackSeq);

            // Combat sequence: find enemy → move to target → attack
            var combatSeq = new BTSequence(findEnemy, moveToTarget, attackSelector);

            // Root selector: try combat, fallback to patrol
            return new BTSelector(combatSeq, patrol);
        }

        /// <summary>
        /// Minor enemy tree: Find Player → Move → Normal Attack only (no skills)
        /// </summary>
        private BTNode BuildMinorEnemyTree()
        {
            var findEnemy = new BTFindNearestEnemy(context, detectionRange);
            var checkInRange = new BTCheckEnemyInRange(context);
            var moveToTarget = new BTMoveToTarget(context);
            var attack = new BTAttack(context);
            var patrol = new BTPatrol(context);

            // Normal attack sequence: check in range → attack
            var normalAttackSeq = new BTSequence(checkInRange, attack);

            // Combat sequence: find player → move → attack
            var combatSeq = new BTSequence(findEnemy, moveToTarget, normalAttackSeq);

            // Root: try combat, fallback to patrol
            return new BTSelector(combatSeq, patrol);
        }

        /// <summary>
        /// Boss tree: same as player AI (find → move → skill/normal attack)
        /// </summary>
        private BTNode BuildBossTree()
        {
            return BuildPlayerAITree();
        }

        private void Update()
        {
            if (!IsActive || behaviorTree == null) return;
            if (entity == null || !entity.RuntimeStats.IsAlive) return;

            behaviorTree.Tick();
        }

        /// <summary>
        /// Pause the AI (stop ticking the behavior tree).
        /// </summary>
        public void PauseAI()
        {
            IsActive = false;
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.PlayIdle();
            }
        }

        /// <summary>
        /// Resume the AI (start ticking the behavior tree again).
        /// </summary>
        public void ResumeAI()
        {
            IsActive = true;
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
