using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Runtime controller that handles skill displacement movement on a character.
    /// Smoothly moves the character in a direction over a specified duration.
    /// Supports continuous damage detection during displacement (dash-slash style).
    /// Auto-added by SkillEffectData.ApplyDisplacement() when needed.
    /// </summary>
    public class SkillDisplacementController : MonoBehaviour
    {
        /// <summary>
        /// Whether the character is currently being displaced by a skill.
        /// When true, AI movement should be suppressed to avoid overriding the displacement.
        /// </summary>
        public bool IsDisplacing { get; private set; }

        private float direction;       // +1 = right, -1 = left
        private float totalDistance;
        private float duration;
        private float elapsedTime;
        private float distanceMoved;

        // --- Lock-on displacement ---
        private bool isLockOn;
        private Vector2 lockOnStartPos;
        private Vector2 lockOnTargetPos;

        // --- Continuous damage detection during displacement ---
        private bool continuousDamage;
        private AttackRangeShape[] damageShapes;
        private float damageFacingSign;
        private string damageTargetTag;
        private float damageAmount;
        private CharacterEntity damageCasterEntity;
        private HashSet<int> alreadyHitInstanceIDs;

        /// <summary>
        /// Start a displacement movement (no continuous damage).
        /// If already displacing, the new displacement overrides the current one.
        /// </summary>
        /// <param name="dir">Horizontal direction: +1 for right, -1 for left.</param>
        /// <param name="distance">Total distance to travel (world units).</param>
        /// <param name="dur">Duration of the movement (seconds).</param>
        public void StartDisplacement(float dir, float distance, float dur)
        {
            direction = dir >= 0f ? 1f : -1f;
            totalDistance = Mathf.Max(0f, distance);
            duration = Mathf.Max(0.01f, dur);
            elapsedTime = 0f;
            distanceMoved = 0f;
            continuousDamage = false;
            isLockOn = false;
            IsDisplacing = true;
        }

        /// <summary>
        /// Start a displacement movement with continuous damage detection.
        /// Enemies entering the skill range shapes during movement will be hit (each only once).
        /// </summary>
        public void StartDisplacementWithDamage(float dir, float distance, float dur,
            AttackRangeShape[] shapes, float facingSign, string targetTag, float damage, CharacterEntity casterEntity)
        {
            direction = dir >= 0f ? 1f : -1f;
            totalDistance = Mathf.Max(0f, distance);
            duration = Mathf.Max(0.01f, dur);
            elapsedTime = 0f;
            distanceMoved = 0f;
            IsDisplacing = true;

            // Setup continuous damage
            continuousDamage = true;
            isLockOn = false;
            damageShapes = shapes;
            damageFacingSign = facingSign;
            damageTargetTag = targetTag;
            damageAmount = damage;
            damageCasterEntity = casterEntity;

            if (alreadyHitInstanceIDs == null)
                alreadyHitInstanceIDs = new HashSet<int>();
            else
                alreadyHitInstanceIDs.Clear();

            // Record enemies already hit at the starting position (handled by MeleeSkillEffectData)
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            Vector2 currentPos = transform.position;
            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                Vector2 candidatePos = candidateEntity.transform.position;
                if (AttackRangeHelper.IsTargetInRange(currentPos, damageFacingSign, damageShapes, candidatePos))
                {
                    alreadyHitInstanceIDs.Add(candidates[i].GetInstanceID());
                }
            }
        }

        /// <summary>
        /// Cancel any ongoing displacement immediately.
        /// </summary>
        public void CancelDisplacement()
        {
            IsDisplacing = false;
            elapsedTime = 0f;
            distanceMoved = 0f;
            continuousDamage = false;
            isLockOn = false;
        }

        /// <summary>
        /// Start a lock-on displacement: move toward a locked target position over duration.
        /// Distance is auto-calculated from current position to target.
        /// </summary>
        public void StartLockOnDisplacement(Vector2 targetPos, float dur)
        {
            lockOnStartPos = transform.position;
            lockOnTargetPos = targetPos;
            totalDistance = Vector2.Distance(lockOnStartPos, lockOnTargetPos);
            duration = Mathf.Max(0.01f, dur);
            elapsedTime = 0f;
            distanceMoved = 0f;
            continuousDamage = false;
            isLockOn = true;
            IsDisplacing = true;
            Debug.Log($"[SkillDisplacement] {gameObject.name}: StartLockOnDisplacement from {lockOnStartPos} to {lockOnTargetPos}, distance={totalDistance}, duration={duration}");
        }

        /// <summary>
        /// Start a lock-on displacement with continuous damage detection.
        /// Moves toward the locked target position while detecting enemies along the path.
        /// </summary>
        public void StartLockOnDisplacementWithDamage(Vector2 targetPos, float dur,
            AttackRangeShape[] shapes, float facingSign, string targetTag, float damage, CharacterEntity casterEntity)
        {
            lockOnStartPos = transform.position;
            lockOnTargetPos = targetPos;
            totalDistance = Vector2.Distance(lockOnStartPos, lockOnTargetPos);
            duration = Mathf.Max(0.01f, dur);
            elapsedTime = 0f;
            distanceMoved = 0f;
            isLockOn = true;
            IsDisplacing = true;

            // Setup continuous damage
            continuousDamage = true;
            damageShapes = shapes;
            damageFacingSign = facingSign;
            damageTargetTag = targetTag;
            damageAmount = damage;
            damageCasterEntity = casterEntity;

            if (alreadyHitInstanceIDs == null)
                alreadyHitInstanceIDs = new HashSet<int>();
            else
                alreadyHitInstanceIDs.Clear();

            // Record enemies already in range at starting position
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            Vector2 currentPos = transform.position;
            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                Vector2 candidatePos = candidateEntity.transform.position;
                if (AttackRangeHelper.IsTargetInRange(currentPos, damageFacingSign, damageShapes, candidatePos))
                {
                    alreadyHitInstanceIDs.Add(candidates[i].GetInstanceID());
                }
            }
        }

        private void Update()
        {
            if (!IsDisplacing) return;

            float dt = Time.deltaTime;
            elapsedTime += dt;

            if (isLockOn)
            {
                // Lock-on displacement: lerp from start to locked target position
                float t = Mathf.Clamp01(elapsedTime / duration);
                Vector2 newPos = Vector2.Lerp(lockOnStartPos, lockOnTargetPos, t);
                transform.position = new Vector3(newPos.x, newPos.y, transform.position.z);
            }
            else
            {
                // Fixed displacement: move in a single direction
                float targetDistance = Mathf.Lerp(0f, totalDistance, Mathf.Clamp01(elapsedTime / duration));
                float frameDelta = targetDistance - distanceMoved;

                if (frameDelta > 0f)
                {
                    Vector3 pos = transform.position;
                    pos.x += direction * frameDelta;
                    transform.position = pos;
                    distanceMoved = targetDistance;
                }
            }

            // Continuous damage detection during displacement
            if (continuousDamage && damageCasterEntity != null)
            {
                DetectDamageDuringDisplacement();
            }

            // Check if displacement is complete
            if (elapsedTime >= duration)
            {
                IsDisplacing = false;
                continuousDamage = false;
                isLockOn = false;
            }
        }

        /// <summary>
        /// Check for new enemies entering the skill range shapes during displacement.
        /// Each enemy is only hit once per displacement.
        /// </summary>
        private void DetectDamageDuringDisplacement()
        {
            if (damageShapes == null || damageShapes.Length == 0) return;

            Vector2 currentPos = transform.position;
            GameObject[] candidates = GameObject.FindGameObjectsWithTag(damageTargetTag);

            for (int i = 0; i < candidates.Length; i++)
            {
                int instanceID = candidates[i].GetInstanceID();

                // Skip already hit enemies
                if (alreadyHitInstanceIDs.Contains(instanceID)) continue;

                CharacterEntity candidateEntity = candidates[i].GetComponent<CharacterEntity>();
                if (candidateEntity == null || !candidateEntity.RuntimeStats.IsAlive) continue;

                Vector2 candidatePos = candidateEntity.transform.position;

                if (AttackRangeHelper.IsTargetInRange(currentPos, damageFacingSign, damageShapes, candidatePos))
                {
                    candidateEntity.TakeDamage(damageAmount, damageCasterEntity);
                    alreadyHitInstanceIDs.Add(instanceID);
                }
            }
        }

        private void OnDisable()
        {
            CancelDisplacement();
        }
    }
}
