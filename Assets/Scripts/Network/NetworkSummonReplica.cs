using UnityEngine;

namespace PetGame.Network
{
    /// <summary>A summon simulated by another player; only network snapshots drive this copy.</summary>
    public class NetworkSummonReplica : MonoBehaviour
    {
        public NetworkPlayer Owner { get; private set; }
        public uint SummonId { get; private set; }
        private CharacterEntity entity;
        private Animator animator;
        private Vector3 targetPosition;
        private int previousAnimationHash;
        private float previousAnimationTime;

        public void Initialize(NetworkPlayer owner, uint id)
        {
            Owner = owner;
            SummonId = id;
            targetPosition = transform.position;
            entity = GetComponent<CharacterEntity>();
            if (entity != null && !entity.IsInitialized && entity.characterData != null)
                entity.Initialize(entity.characterData);

            Disable<PetGame.AI.AIController>();
            Disable<ManualController>();
            Disable<ControlModeManager>();
            Disable<CombatSystem>();
            Disable<AnimEventReceiver>();
            Disable<KnockbackController>();
            Disable<CharacterAnimator>();
            Disable<CharacterEntity>();
            Disable<SummonedEntityTracker>();
            animator = GetComponent<Animator>();
            if (animator != null) animator.fireEvents = false;
            foreach (Rigidbody2D body in GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.bodyType = RigidbodyType2D.Kinematic;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.useFullKinematicContacts = false;
            }
            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
                collider.isTrigger = true;
        }

        private void Disable<T>() where T : Behaviour
        {
            T component = GetComponent<T>();
            if (component != null) component.enabled = false;
        }

        public void ApplySnapshot(SummonSnapshot snapshot)
        {
            targetPosition = snapshot.position;
            if (Vector3.Distance(transform.position, targetPosition) > 3f) transform.position = targetPosition;
            transform.rotation = snapshot.rotation;
            transform.localScale = snapshot.scale;
            var sprite = GetComponent<SpriteRenderer>();
            if (sprite != null) sprite.flipX = snapshot.flipX;
            if (entity != null && entity.RuntimeStats != null)
            {
                entity.RuntimeStats.currentHealth = snapshot.health;
                entity.RuntimeStats.maxHealth = snapshot.maxHealth;
                var bar = GetComponentInChildren<HealthBar>(true);
                if (bar != null) bar.UpdateHealth(snapshot.health, snapshot.maxHealth);
            }
            if (animator == null || animator.runtimeAnimatorController == null || snapshot.animationHash == 0) return;
            NetworkEnemySpawner.ApplyAnimationParameters(animator, snapshot.animationParameters);
            animator.speed = snapshot.animationSpeed;
            AnimatorStateInfo current = animator.GetCurrentAnimatorStateInfo(0);
            bool restarted = previousAnimationHash == snapshot.animationHash && snapshot.animationTime + 0.05f < previousAnimationTime;
            if (current.fullPathHash != snapshot.animationHash || restarted ||
                Mathf.Abs(current.normalizedTime - snapshot.animationTime) * current.length > 0.2f)
                animator.Play(snapshot.animationHash, 0, snapshot.animationTime);
            previousAnimationHash = snapshot.animationHash;
            previousAnimationTime = snapshot.animationTime;
        }

        private void Update()
        {
            transform.position = Vector3.Lerp(transform.position, targetPosition, 1f - Mathf.Exp(-20f * Time.deltaTime));
        }
    }
}
