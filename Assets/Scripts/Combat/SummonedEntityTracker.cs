using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Attached to each summoned entity to manage its lifetime.
    /// Handles auto-destruction after duration expires, and cleanup when killed.
    /// </summary>
    public class SummonedEntityTracker : MonoBehaviour
    {
        /// <summary>
        /// The entity that summoned this creature.
        /// </summary>
        public CharacterEntity Owner { get; private set; }

        /// <summary>
        /// Maximum lifetime in seconds.
        /// </summary>
        public float Duration { get; private set; }

        private float elapsedTime;
        private CharacterEntity selfEntity;
        private bool isDestroying;

        /// <summary>
        /// Initialize the tracker with owner reference and duration.
        /// </summary>
        /// <param name="owner">The caster who summoned this entity.</param>
        /// <param name="duration">How long (seconds) this summon stays alive.</param>
        public void Initialize(CharacterEntity owner, float duration)
        {
            Owner = owner;
            Duration = duration;
            elapsedTime = 0f;
            isDestroying = false;

            selfEntity = GetComponent<CharacterEntity>();
            if (selfEntity != null)
            {
                selfEntity.OnDeath += OnSummonDeath;
            }
        }

        private void Update()
        {
            if (isDestroying) return;

            elapsedTime += Time.deltaTime;
            if (elapsedTime >= Duration)
            {
                ForceDestroy();
            }
        }

        /// <summary>
        /// Called when the summoned entity is killed in combat.
        /// </summary>
        private void OnSummonDeath(CharacterEntity entity)
        {
            if (isDestroying) return;
            ForceDestroy();
        }

        /// <summary>
        /// Force destroy this summoned entity immediately.
        /// Called by SummonOwnerRegistry when refreshing summons (allowMultipleWaves = false),
        /// or when duration expires, or when killed.
        /// </summary>
        public void ForceDestroy()
        {
            if (isDestroying) return;
            isDestroying = true;

            // Unsubscribe from death event
            if (selfEntity != null)
            {
                selfEntity.OnDeath -= OnSummonDeath;
            }

            // Unregister from owner registry
            SummonOwnerRegistry.Unregister(Owner, gameObject);

            // Destroy the game object
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // Safety cleanup: ensure we're unregistered even if destroyed externally
            if (!isDestroying)
            {
                if (selfEntity != null)
                {
                    selfEntity.OnDeath -= OnSummonDeath;
                }
                SummonOwnerRegistry.Unregister(Owner, gameObject);
            }
        }
    }
}
