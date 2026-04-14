using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manual control component for player-controlled characters.
    /// Handles right-click movement and attack input.
    /// Only active when the character is in Manual control mode.
    /// </summary>
    [RequireComponent(typeof(CharacterEntity))]
    [RequireComponent(typeof(CombatSystem))]
    public class ManualController : MonoBehaviour
    {
        private CharacterEntity entity;
        private CombatSystem combatSystem;

        private bool isActive;
        private bool isMoving;
        private float moveDirection; // 1 = right, -1 = left
        private CharacterEntity attackTarget;
        private bool isChasing; // Moving towards an enemy to attack

        private void Awake()
        {
            entity = GetComponent<CharacterEntity>();
            combatSystem = GetComponent<CombatSystem>();
        }

        /// <summary>
        /// Enable or disable manual control.
        /// </summary>
        public void SetActive(bool active)
        {
            isActive = active;

            if (!active)
            {
                StopMoving();
                attackTarget = null;
                isChasing = false;
            }
            else
            {
                // Play idle when first entering manual mode
                if (entity.CharAnimator != null)
                {
                    entity.CharAnimator.PlayIdle();
                }
            }
        }

        private void Update()
        {
            if (!isActive || entity == null || !entity.RuntimeStats.IsAlive) return;

            HandleRightClickInput();
            HandleMovement();
            HandleChaseAndAttack();
        }

        /// <summary>
        /// Handle right-click input for movement and targeting.
        /// </summary>
        private void HandleRightClickInput()
        {
            if (!Input.GetMouseButtonDown(1)) return; // Right click only

            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // Check if clicked on an enemy
            Collider2D hit = Physics2D.OverlapPoint(mouseWorldPos);
            if (hit != null)
            {
                CharacterEntity clickedEntity = hit.GetComponent<CharacterEntity>();
                if (clickedEntity != null && clickedEntity != entity &&
                    clickedEntity.RuntimeStats.IsAlive &&
                    clickedEntity.RuntimeStats.characterType != CharacterType.Player)
                {
                    // Clicked on an enemy
                    attackTarget = clickedEntity;

                    // Check if already in attack range
                    float dist = Vector2.Distance(transform.position, attackTarget.transform.position);
                    if (dist <= entity.RuntimeStats.attackRange)
                    {
                        // In range: attack immediately
                        isChasing = false;
                        isMoving = false;
                        combatSystem.TryNormalAttack(attackTarget);
                    }
                    else
                    {
                        // Out of range: chase
                        isChasing = true;
                        isMoving = false;
                    }
                    return;
                }
            }

            // Clicked on empty area: move left or right based on click position
            attackTarget = null;
            isChasing = false;

            float clickX = mouseWorldPos.x;
            float charX = transform.position.x;

            if (clickX > charX)
            {
                moveDirection = 1f; // Move right
            }
            else
            {
                moveDirection = -1f; // Move left
            }

            isMoving = true;
        }

        /// <summary>
        /// Handle directional movement.
        /// </summary>
        private void HandleMovement()
        {
            if (!isMoving || isChasing) return;

            float speed = entity.RuntimeStats.moveSpeed;
            Vector3 pos = transform.position;
            pos.x += moveDirection * speed * Time.deltaTime;
            transform.position = pos;

            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.PlayWalk();
                entity.CharAnimator.SetFacingDirection(moveDirection);
            }
        }

        /// <summary>
        /// Handle chasing an enemy and attacking when in range.
        /// </summary>
        private void HandleChaseAndAttack()
        {
            if (!isChasing || attackTarget == null) return;

            // Check if target is still alive
            if (!attackTarget.RuntimeStats.IsAlive)
            {
                attackTarget = null;
                isChasing = false;
                StopMoving();
                return;
            }

            float dist = Vector2.Distance(transform.position, attackTarget.transform.position);

            if (dist <= entity.RuntimeStats.attackRange)
            {
                // In range: stop and attack
                isChasing = false;
                combatSystem.TryNormalAttack(attackTarget);

                if (entity.CharAnimator != null)
                {
                    entity.CharAnimator.FaceTowards(attackTarget.transform.position);
                }
            }
            else
            {
                // Move towards target
                float speed = entity.RuntimeStats.moveSpeed;
                float direction = attackTarget.transform.position.x > transform.position.x ? 1f : -1f;

                Vector3 pos = transform.position;
                pos.x += direction * speed * Time.deltaTime;
                transform.position = pos;

                if (entity.CharAnimator != null)
                {
                    entity.CharAnimator.PlayWalk();
                    entity.CharAnimator.SetFacingDirection(direction);
                }
            }
        }

        /// <summary>
        /// Stop all movement and play idle.
        /// </summary>
        private void StopMoving()
        {
            isMoving = false;
            if (entity.CharAnimator != null)
            {
                entity.CharAnimator.PlayIdle();
            }
        }
    }
}
