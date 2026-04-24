using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Projectile skill effect: spawns a projectile that flies in a parabolic arc
    /// toward the predicted target position, then explodes dealing AOE damage.
    /// </summary>
    [CreateAssetMenu(fileName = "NewProjectileSkillEffect", menuName = "Game/SkillEffect/Projectile")]
    public class ProjectileSkillEffectData : SkillEffectData
    {
        [Header("Projectile Settings")]
        [Tooltip("Sprite image for the projectile. Assign your projectile art here.")]
        public Sprite projectileSprite;

        [Tooltip("Total flight time from launch to landing (seconds).")]
        [Min(0.01f)]
        public float projectileFlightDuration = 0.6f;

        [Tooltip("Peak height of the parabolic arc above the start-end line.")]
        [Min(0f)]
        public float projectileArcHeight = 1.5f;

        [Tooltip("Scale of the projectile GameObject.")]
        [Min(0.01f)]
        public float projectileScale = 1.0f;

        [Header("Explosion Settings")]
        [Tooltip("RuntimeAnimatorController for the explosion animation. Assign a sprite sheet animation.")]
        public RuntimeAnimatorController explosionAnimatorController;

        [Tooltip("Radius of the AOE damage circle at the landing point.")]
        [Min(0f)]
        public float explosionRadius = 1.0f;

        [Tooltip("Scale of the explosion effect GameObject.")]
        [Min(0.01f)]
        public float explosionScale = 1.0f;

        // --- Pool keys for object pool integration ---
        private const string PROJECTILE_POOL_KEY = "SkillProjectile";
        private const string EXPLOSION_POOL_KEY = "SkillExplosion";

        // --- Prefab templates (created once, registered with PoolMgr) ---
        private static bool projectilePrefabRegistered;
        private static bool explosionPrefabRegistered;

        /// <summary>
        /// Execute the projectile skill effect.
        /// Spawns a projectile at the caster's position, flies toward the predicted target position,
        /// then spawns an explosion at the landing point dealing AOE damage.
        /// </summary>
        public override void Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)
        {
            if (caster == null) return;

            // Ensure prefab templates are registered with the pool
            EnsurePrefabsRegistered();

            // Calculate predicted landing position based on target's current velocity
            Vector3 targetPos = target != null ? target.transform.position : caster.transform.position + Vector3.right;
            Vector2 targetVelocity = target != null ? target.Velocity : Vector2.zero;
            Vector3 predictedPos = targetPos + (Vector3)(targetVelocity * projectileFlightDuration);

            // Spawn projectile from pool
            GameObject projectileObj = PoolMgr.Instance.GetNode(PROJECTILE_POOL_KEY);
            if (projectileObj == null)
            {
                Debug.LogError("[ProjectileSkillEffectData] Failed to get projectile from pool.");
                return;
            }

            // Configure projectile appearance
            projectileObj.transform.position = caster.transform.position;
            projectileObj.transform.localScale = Vector3.one * projectileScale;
            projectileObj.transform.rotation = Quaternion.identity;
            projectileObj.SetActive(true);

            SpriteRenderer sr = projectileObj.GetComponent<SpriteRenderer>();
            if (sr != null && projectileSprite != null)
            {
                sr.sprite = projectileSprite;
            }

            // Reset and launch the projectile controller
            ProjectileController controller = projectileObj.GetComponent<ProjectileController>();
            if (controller != null)
            {
                controller.ResetState();

                // Capture values for the closure
                float damage = skillData.damage;
                CharacterType casterType = caster.GetComponent<CharacterEntity>().RuntimeStats.characterType;
                Vector3 landingPos = predictedPos;

                controller.Launch(
                    caster.transform.position,
                    landingPos,
                    projectileFlightDuration,
                    projectileArcHeight,
                    () => OnProjectileArrived(projectileObj, landingPos, damage, casterType)
                );
            }
        }

        /// <summary>
        /// Called when the projectile reaches its landing position.
        /// Recycles the projectile and spawns the explosion effect.
        /// </summary>
        private void OnProjectileArrived(GameObject projectileObj, Vector3 landingPos, float damage, CharacterType casterType)
        {
            // Recycle projectile back to pool
            PoolMgr.Instance.PutNode(projectileObj);

            // Spawn explosion from pool
            if (explosionAnimatorController == null)
            {
                Debug.LogWarning("[ProjectileSkillEffectData] No explosion animator controller assigned. Skipping explosion.");
                return;
            }

            GameObject explosionObj = PoolMgr.Instance.GetNode(EXPLOSION_POOL_KEY);
            if (explosionObj == null)
            {
                Debug.LogError("[ProjectileSkillEffectData] Failed to get explosion from pool.");
                return;
            }

            explosionObj.transform.position = landingPos;
            explosionObj.transform.localScale = Vector3.one * explosionScale;
            explosionObj.SetActive(true);

            ExplosionController explosion = explosionObj.GetComponent<ExplosionController>();
            if (explosion != null)
            {
                explosion.ResetState();
                explosion.Play(
                    explosionAnimatorController,
                    explosionRadius,
                    damage,
                    casterType,
                    () => PoolMgr.Instance.PutNode(explosionObj)
                );
            }
        }

        /// <summary>
        /// Ensure projectile and explosion prefab templates are created and registered with PoolMgr.
        /// Uses static flags so templates are only created once across all instances.
        /// </summary>
        private static void EnsurePrefabsRegistered()
        {
            if (!projectilePrefabRegistered)
            {
                // Create projectile template: SpriteRenderer + ProjectileController
                GameObject projectileTemplate = new GameObject(PROJECTILE_POOL_KEY);
                projectileTemplate.AddComponent<SpriteRenderer>();
                projectileTemplate.AddComponent<ProjectileController>();
                projectileTemplate.SetActive(false);

                PoolMgr.Instance.SetPrefab(PROJECTILE_POOL_KEY, projectileTemplate);
                // Hide the template so it doesn't appear in the scene
                Object.DontDestroyOnLoad(projectileTemplate);
                projectilePrefabRegistered = true;
            }

            if (!explosionPrefabRegistered)
            {
                // Create explosion template: Animator + ExplosionController
                GameObject explosionTemplate = new GameObject(EXPLOSION_POOL_KEY);
                explosionTemplate.AddComponent<SpriteRenderer>();
                explosionTemplate.AddComponent<Animator>();
                explosionTemplate.AddComponent<ExplosionController>();
                explosionTemplate.SetActive(false);

                PoolMgr.Instance.SetPrefab(EXPLOSION_POOL_KEY, explosionTemplate);
                Object.DontDestroyOnLoad(explosionTemplate);
                explosionPrefabRegistered = true;
            }
        }

#if UNITY_EDITOR
        /// <summary>
        /// Reset static registration flags when entering play mode in the editor.
        /// This ensures templates are re-created after domain reload.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticFlags()
        {
            projectilePrefabRegistered = false;
            explosionPrefabRegistered = false;
        }
#endif
    }
}
