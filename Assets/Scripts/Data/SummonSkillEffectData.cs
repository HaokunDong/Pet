using UnityEngine;
using Mirror;
using PetGame.Network;

namespace PetGame
{
    /// <summary>
    /// Summon skill effect: spawns a configurable number of summoned entities around the caster.
    /// Each summoned entity uses its own prefab's CharacterData for stats, its own animator controller,
    /// and is fully controlled by its AIController behavior tree.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSummonSkillEffect", menuName = "Game/SkillEffect/Summon")]
    public class SummonSkillEffectData : SkillEffectData
    {
        [Header("Summon Settings")]
        [Tooltip("Prefab for the summoned entity. Must have CharacterEntity and AIController components.")]
        public GameObject summonPrefab;

        [Tooltip("Number of summoned entities to spawn per skill cast.")]
        [Min(1)]
        public int summonCount = 1;

        [Tooltip("Duration (seconds) each summoned entity stays alive before auto-destruction.")]
        [Min(0.1f)]
        public float summonDuration = 10f;

        [Tooltip("If true, multiple waves of summons can coexist. " +
                 "If false, casting again will destroy existing summons before spawning new ones.")]
        public bool allowMultipleWaves = false;

        [Tooltip("Radius around the caster within which summons are randomly placed.")]
        [Min(0.5f)]
        public float spawnRadius = 2f;

        /// <summary>
        /// Execute the summon skill effect.
        /// Spawns summonCount entities around the caster with independent stats and AI.
        /// </summary>
        public override void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)
        {
            if (caster == null) return;

            if (summonPrefab == null)
            {
                Debug.LogError("[SummonSkillEffectData] summonPrefab is not assigned!");
                return;
            }

            CharacterEntity casterEntity = caster.GetComponent<CharacterEntity>();
            if (casterEntity == null) return;

            // If not allowing multiple waves, destroy existing summons for this caster first
            if (!allowMultipleWaves)
            {
                SummonOwnerRegistry.DestroyAllForOwner(casterEntity);
            }

            // Calculate spawn positions
            Vector3 casterPos = caster.transform.position;
            Vector3[] spawnPositions = CalculateSpawnPositions(casterPos, summonCount, spawnRadius);

            // Determine the tag for summoned entities (same faction as caster)
            string summonTag = casterEntity.gameObject.tag;

            // Spawn each summoned entity
            for (int i = 0; i < summonCount; i++)
            {
                GameObject summonObj = Instantiate(summonPrefab, spawnPositions[i], Quaternion.identity);
                InitializeSummonedEntity(summonObj, casterEntity, summonTag);
                // Enemy summons already belong to the host's world snapshot.
                if (summonTag != "Enemy" && NetworkClient.active && MirrorNetworkManager.singleton != null)
                {
                    var localPlayer = MirrorNetworkManager.singleton.LocalPlayer;
                    if (localPlayer != null)
                        localPlayer.RegisterLocalSummon(summonObj, summonPrefab.name, summonTag);
                }
            }
        }

        /// <summary>
        /// Initialize a newly spawned summoned entity with proper tag, stats, AI, and lifecycle tracking.
        /// </summary>
        private void InitializeSummonedEntity(GameObject summonObj, CharacterEntity owner, string factionTag)
        {
            // Set faction tag
            summonObj.tag = factionTag;

            // Initialize CharacterEntity (creates independent RuntimeCharacterStats)
            CharacterEntity summonEntity = summonObj.GetComponent<CharacterEntity>();
            if (summonEntity == null)
            {
                Debug.LogError($"[SummonSkillEffectData] Summoned prefab '{summonPrefab.name}' is missing CharacterEntity component!");
                Destroy(summonObj);
                return;
            }

            // If CharacterEntity wasn't auto-initialized in Awake (e.g. characterData was null on prefab),
            // ensure it's initialized now
            if (!summonEntity.IsInitialized && summonEntity.characterData != null)
            {
                summonEntity.Initialize(summonEntity.characterData);
            }

            // Initialize AI controller
            PetGame.AI.AIController aiController = summonObj.GetComponent<PetGame.AI.AIController>();
            if (aiController == null)
            {
                Debug.LogError($"[SummonSkillEffectData] Summoned prefab '{summonPrefab.name}' is missing AIController component!");
                Destroy(summonObj);
                return;
            }
            aiController.InitializeAI();

            // Attach lifecycle tracker
            SummonedEntityTracker tracker = summonObj.AddComponent<SummonedEntityTracker>();
            tracker.Initialize(owner, summonDuration);

            // Register with the owner registry
            SummonOwnerRegistry.Register(owner, summonObj);
        }

        /// <summary>
        /// Calculate random spawn positions around a center point, avoiding overlap.
        /// Uses uniform angle distribution with random radius offset.
        /// Falls back to nearby positions if obstacles are detected.
        /// </summary>
        private Vector3[] CalculateSpawnPositions(Vector3 center, int count, float radius)
        {
            Vector3[] positions = new Vector3[count];
            float angleStep = 360f / count;

            for (int i = 0; i < count; i++)
            {
                float angle = angleStep * i + Random.Range(-angleStep * 0.3f, angleStep * 0.3f);
                float distance = Random.Range(radius * 0.5f, radius);
                float rad = angle * Mathf.Deg2Rad;

                Vector3 offset = new Vector3(Mathf.Cos(rad) * distance, 0f, 0f);
                // For 2D game, use X offset only (Y is vertical)
                Vector3 candidatePos = center + new Vector3(offset.x, 0f, 0f);

                // Check for obstacles at the candidate position using Physics2D
                Collider2D obstacle = Physics2D.OverlapCircle(
                    new Vector2(candidatePos.x, candidatePos.y),
                    0.3f,
                    LayerMask.GetMask("Obstacle", "Wall")
                );

                if (obstacle != null)
                {
                    // Try a fallback position closer to center
                    float fallbackDistance = distance * 0.5f;
                    candidatePos = center + new Vector3(Mathf.Cos(rad) * fallbackDistance, 0f, 0f);
                }

                positions[i] = candidatePos;
            }

            return positions;
        }
    }
}
