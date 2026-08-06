using System.Collections.Generic;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Projectile skill effect: spawns a projectile prefab that flies in a parabolic arc
    /// toward the predicted target position, then explodes dealing AOE damage.
    /// The projectile prefab should have SpriteRenderer, Animator, and ProjectileController components.
    /// </summary>
    [CreateAssetMenu(fileName = "NewProjectileSkillEffect", menuName = "Game/SkillEffect/Projectile")]
    public class ProjectileSkillEffectData : SkillEffectData
    {
        [Header("Projectile Settings")]
        [Tooltip("Prefab for the projectile. Must have SpriteRenderer, Animator, and ProjectileController components.")]
        public GameObject projectilePrefab;

        [Tooltip("Total flight time from launch to landing (seconds).")]
        [Min(0.01f)]
        public float projectileFlightDuration = 0.6f;

        [Tooltip("Peak height of the parabolic arc above the start-end line.")]
        [Min(0f)]
        public float projectileArcHeight = 1.5f;

        // --- Pool registration tracking (static to survive ScriptableObject persistence) ---
        private static HashSet<string> registeredPoolKeys = new HashSet<string>();
        private string poolKey;

        /// <summary>
        /// Get the pool key derived from the prefab name.
        /// </summary>
        private string GetPoolKey()
        {
            if (string.IsNullOrEmpty(poolKey) && projectilePrefab != null)
            {
                poolKey = projectilePrefab.name;
            }
            return poolKey;
        }

        /// <summary>
        /// Ensure the projectile prefab is registered with the object pool.
        /// Uses a static HashSet so registration survives ScriptableObject persistence
        /// but is correctly reset on domain reload.
        /// </summary>
        private void EnsurePrefabRegistered()
        {
            if (projectilePrefab == null)
            {
                Debug.LogError("[ProjectileSkillEffectData] projectilePrefab is not assigned!");
                return;
            }

            string key = GetPoolKey();
            if (registeredPoolKeys.Contains(key)) return;

            PoolMgr.Instance.SetPrefab(key, projectilePrefab);
            registeredPoolKeys.Add(key);
        }

        /// <summary>
        /// Execute the projectile skill effect.
        /// Spawns a projectile prefab at the caster's position, flies toward the predicted target position,
        /// then explodes at the landing point dealing AOE damage.
        /// </summary>
        public override void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)
        {
            if (caster == null) return;
            if (projectilePrefab == null)
            {
                Debug.LogError("[ProjectileSkillEffectData] projectilePrefab is not assigned! Cannot execute skill.");
                return;
            }

            // Ensure prefab is registered with the pool
            EnsurePrefabRegistered();

            // Calculate predicted landing position based on target's current velocity
            Vector3 targetPos = target != null ? (Vector3)target.ColliderCenter : caster.transform.position + Vector3.right;
            Vector2 targetVelocity = target != null ? target.Velocity : Vector2.zero;
            Vector3 predictedPos = targetPos + (Vector3)(targetVelocity * projectileFlightDuration);

            // Get projectile from pool
            string key = GetPoolKey();
            GameObject projectileObj = PoolMgr.Instance.GetNode(key);
            if (projectileObj == null)
            {
                Debug.LogError("[ProjectileSkillEffectData] Failed to get projectile from pool.");
                return;
            }

            // Position and activate FIRST — component operations (SpriteRenderer, Animator)
            // only take effect on an active GameObject.
            projectileObj.transform.position = caster.transform.position;
            projectileObj.transform.rotation = Quaternion.identity;
            projectileObj.SetActive(true);

            // Get the controller and reset state AFTER activating,
            // so that disabling the Animator and restoring the sprite actually take effect.
            ProjectileController controller = projectileObj.GetComponent<ProjectileController>();
            if (controller == null)
            {
                Debug.LogError("[ProjectileSkillEffectData] Projectile prefab is missing ProjectileController component!");
                PoolMgr.Instance.PutNode(projectileObj);
                return;
            }

            controller.ResetState();

            // Capture values for the closure
            float dmg = skillData.damage;
            CharacterType casterCharType = caster.GetComponent<CharacterEntity>().RuntimeStats.characterType;

            controller.Launch(
                caster.transform.position,
                predictedPos,
                projectileFlightDuration,
                projectileArcHeight,
                dmg,
                casterCharType,
                () =>
                {
                    // Recycle projectile back to pool after explosion animation finishes
                    PoolMgr.Instance.PutNode(projectileObj);
                }
            );

            // Apply displacement if configured
            ApplyDisplacement(caster);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reset pool registration flag when entering play mode in the editor.
        /// This ensures the prefab is re-registered after domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFlags()
        {
            registeredPoolKeys.Clear();
        }
#endif
    }
}
