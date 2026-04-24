using System;
using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Controls an explosion effect: plays an animation and deals AOE damage
    /// to all enemy characters within the explosion radius.
    /// Attach to a GameObject with an Animator component.
    /// </summary>
    public class ExplosionController : MonoBehaviour
    {
        private Animator animator;
        private Action onFinish;
        private bool isPlaying;
        private bool hasDamaged;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        /// <summary>
        /// Play the explosion effect at the current position.
        /// Deals damage immediately and waits for the animation to finish before calling onFinish.
        /// </summary>
        /// <param name="animController">RuntimeAnimatorController for the explosion animation.</param>
        /// <param name="explosionRadius">Radius of the AOE damage circle.</param>
        /// <param name="damage">Damage to deal to each enemy in range.</param>
        /// <param name="casterType">The caster's character type, used to determine enemy targets.</param>
        /// <param name="onFinishCallback">Called when the explosion animation finishes.</param>
        public void Play(RuntimeAnimatorController animController, float explosionRadius, float damage,
            CharacterType casterType, Action onFinishCallback)
        {
            if (animator == null)
                animator = GetComponent<Animator>();

            animator.runtimeAnimatorController = animController;
            onFinish = onFinishCallback;
            isPlaying = true;
            hasDamaged = false;

            // Force play from the beginning
            animator.Play(0, 0, 0f);

            // Deal AOE damage immediately
            DealAOEDamage(explosionRadius, damage, casterType);
        }

        /// <summary>
        /// Deal damage to all enemy characters within the explosion radius.
        /// </summary>
        private void DealAOEDamage(float explosionRadius, float damage, CharacterType casterType)
        {
            if (hasDamaged) return;
            hasDamaged = true;

            // Determine which tag to search for based on caster type
            string targetTag = (casterType == CharacterType.Player) ? "Enemy" : "Player";

            GameObject[] candidates = GameObject.FindGameObjectsWithTag(targetTag);
            int hitCount = 0;

            for (int i = 0; i < candidates.Length; i++)
            {
                CharacterEntity target = candidates[i].GetComponent<CharacterEntity>();
                if (target == null || !target.RuntimeStats.IsAlive) continue;

                float dist = Vector2.Distance(transform.position, target.transform.position);
                if (dist <= explosionRadius)
                {
                    target.TakeDamage(damage, null);
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                Debug.Log($"[ExplosionController] Explosion at {transform.position} hit {hitCount} target(s) for {damage} damage.");
            }
        }

        private void Update()
        {
            if (!isPlaying) return;

            // Check if the explosion animation has finished
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.normalizedTime >= 1f)
            {
                isPlaying = false;
                onFinish?.Invoke();
            }
        }

        /// <summary>
        /// Reset state when retrieved from object pool.
        /// </summary>
        public void ResetState()
        {
            isPlaying = false;
            hasDamaged = false;
            onFinish = null;
        }

        private void OnDisable()
        {
            isPlaying = false;
            onFinish = null;
        }
    }
}
