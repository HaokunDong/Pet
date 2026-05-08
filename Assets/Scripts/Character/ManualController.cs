using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Manual control component for player-controlled characters.
    /// Handles right-click point-to-move, attack input, arrow indicators,
    /// and enemy outline highlighting.
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
        private float targetX; // Target X coordinate for point-to-move
        private bool hasTargetX; // Whether we have a valid move target
        private CharacterEntity attackTarget;
        private bool isChasing; // Moving towards an enemy to attack
        private bool isAttacking; // Currently in sustained attack mode

        // Arrow indicator tracking
        private MoveArrowIndicator currentArrow;

        // Mouse hover outline tracking
        private CharacterEntity hoveredEnemy;

        /// <summary>
        /// Threshold distance to consider the character has arrived at target X.
        /// </summary>
        private const float ArrivalThreshold = 0.05f;

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
                isAttacking = false;
                hasTargetX = false;

                // Destroy any active arrow indicator
                DestroyCurrentArrow();

                // Clear all outline effects
                ClearHoveredEnemy();
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

            // If the character is in skill animation, freeze all manual control.
            if (entity.CharAnimator != null && entity.CharAnimator.IsInSkillState) return;

            HandleMouseHover();
            HandleRightClickInput();
            HandleMovement();
            HandleChaseAndAttack();
            HandleSustainedAttack();
        }

        /// <summary>
        /// Handle right-click input for movement and targeting.
        /// </summary>
        private void HandleRightClickInput()
        {
            if (!Input.GetMouseButtonDown(1)) return; // Right click only

            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

            // New right-click input always interrupts sustained attack
            isAttacking = false;

            // Check if clicked on an enemy
            CharacterEntity clickedEnemy = GetEnemyAtPoint(mouseWorldPos);
            if (clickedEnemy != null)
            {
                // Clicked on an enemy — cancel any point-to-move
                hasTargetX = false;
                DestroyCurrentArrow();

                attackTarget = clickedEnemy;

                // Trigger bold pulse outline feedback on the clicked enemy
                if (clickedEnemy.OutlineFx != null)
                {
                    clickedEnemy.OutlineFx.ShouldRemainAfterPulse = (hoveredEnemy == clickedEnemy);
                    clickedEnemy.OutlineFx.TriggerBoldPulse();
                }

                // Check if already in attack range using multi-shape system
                float atkDx = attackTarget.transform.position.x - transform.position.x;
                float facingSign1 = atkDx >= 0f ? 1f : -1f;
                bool inRangeForAttack = entity.RuntimeStats.IsTargetInAttackRange(
                    transform.position, facingSign1, attackTarget.transform.position);

                if (inRangeForAttack)
                {
                    // In range: enter sustained attack mode
                    isChasing = false;
                    isMoving = false;
                    isAttacking = true;

                    // Set facing toward target
                    if (entity.CharAnimator != null)
                    {
                        entity.CharAnimator.SetFacingDirection(atkDx);
                    }
                }
                else
                {
                    // Out of range: chase
                    isChasing = true;
                    isMoving = false;
                }
                return;
            }

            // Clicked on empty area: point-to-move to the click X position
            attackTarget = null;
            isChasing = false;

            float clickX = mouseWorldPos.x;
            float charX = transform.position.x;

            // If click position is very close to current position, don't move
            if (Mathf.Abs(clickX - charX) < ArrivalThreshold)
            {
                hasTargetX = false;
                isMoving = false;
                return;
            }

            targetX = clickX;
            hasTargetX = true;
            isMoving = true;

            // Spawn arrow indicator at click position (destroy old one first)
            DestroyCurrentArrow();
            OutlineSettings settings = OutlineSettings.Instance;
            Vector3 arrowPos = new Vector3(mouseWorldPos.x, settings.arrowYOffset, 0f);
            currentArrow = MoveArrowIndicator.Spawn(arrowPos, settings.arrowColor, settings.arrowScale, settings.arrowDuration);
        }

        /// <summary>
        /// Handle point-to-move: move towards targetX and stop when arrived.
        /// </summary>
        private void HandleMovement()
        {
            if (!isMoving || isChasing || !hasTargetX) return;

            float charX = transform.position.x;
            float distance = Mathf.Abs(targetX - charX);

            // Check if arrived at target
            if (distance < ArrivalThreshold)
            {
                hasTargetX = false;
                isMoving = false;
                StopMoving();
                return;
            }

            // Determine move direction
            float moveDirection = targetX > charX ? 1f : -1f;

            float speed = entity.RuntimeStats.moveSpeed;
            Vector3 pos = transform.position;
            pos.x = Mathf.MoveTowards(pos.x, targetX, speed * Time.deltaTime);
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
                isAttacking = false;
                StopMoving();
                return;
            }

            float dx = attackTarget.transform.position.x - transform.position.x;
            float facingSign2 = dx >= 0f ? 1f : -1f;

            if (entity.RuntimeStats.IsTargetInAttackRange(
                    transform.position, facingSign2, attackTarget.transform.position))
            {
                // In range: stop moving and enter sustained attack mode
                isChasing = false;
                isMoving = false;
                isAttacking = true;

                // Set facing toward target
                if (entity.CharAnimator != null)
                {
                    entity.CharAnimator.SetFacingDirection(dx);
                }
            }
            else
            {
                // Move towards target
                float speed = entity.RuntimeStats.moveSpeed;
                float direction = dx > 0f ? 1f : -1f;

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
        /// Handle sustained attack: keep attacking target at attack speed interval.
        /// Character stays still until player issues a new right-click command.
        /// </summary>
        private void HandleSustainedAttack()
        {
            if (!isAttacking || attackTarget == null) return;

            // Check if target is still alive
            if (!attackTarget.RuntimeStats.IsAlive)
            {
                attackTarget = null;
                isAttacking = false;
                StopMoving();
                return;
            }

            // Check if target moved out of attack range.
            float dx = attackTarget.transform.position.x - transform.position.x;
            float facingSign3 = entity.CharAnimator != null
                ? entity.CharAnimator.FacingDirection
                : (dx >= 0f ? 1f : -1f);
            bool inRange = entity.RuntimeStats.IsTargetInAttackRange(
                transform.position, facingSign3, attackTarget.transform.position);

            if (!inRange)
            {
                // Target left range — stop attacking, stay idle
                isAttacking = false;
                StopMoving();
                return;
            }

            // Attempt attack — CombatSystem handles attack speed interval internally
            // Note: TryNormalAttack no longer updates facing direction internally.
            // Facing was set once when entering attack mode and remains locked during animations.
            combatSystem.TryNormalAttack(attackTarget);
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

        // =====================================================================
        // Mouse Hover Outline Detection
        // =====================================================================

        /// <summary>
        /// Handle mouse hover detection: highlight enemies under the cursor with an outline.
        /// </summary>
        private void HandleMouseHover()
        {
            Vector2 mouseWorldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            CharacterEntity enemyUnderMouse = GetEnemyAtPoint(mouseWorldPos);

            // Check if hovered enemy died
            if (hoveredEnemy != null && (!hoveredEnemy.RuntimeStats.IsAlive || hoveredEnemy == null))
            {
                ClearHoveredEnemy();
            }

            if (enemyUnderMouse != hoveredEnemy)
            {
                // Mouse moved to a different target (or moved off)
                ClearHoveredEnemy();

                if (enemyUnderMouse != null)
                {
                    hoveredEnemy = enemyUnderMouse;
                    if (hoveredEnemy.OutlineFx != null)
                    {
                        hoveredEnemy.OutlineFx.ShouldRemainAfterPulse = true;
                        // Only set hover outline if not currently in a bold pulse
                        if (!hoveredEnemy.OutlineFx.IsBoldPulsing)
                        {
                            hoveredEnemy.OutlineFx.SetOutline(true, OutlineSettings.Instance.hoverThickness);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Get the topmost enemy CharacterEntity at the given world point.
        /// Returns null if no enemy is found.
        /// </summary>
        private CharacterEntity GetEnemyAtPoint(Vector2 worldPoint)
        {
            // Use OverlapPointAll to handle overlapping enemies, pick the topmost one
            Collider2D[] hits = Physics2D.OverlapPointAll(worldPoint);
            CharacterEntity bestEnemy = null;
            int bestOrder = int.MinValue;

            for (int i = 0; i < hits.Length; i++)
            {
                CharacterEntity ce = hits[i].GetComponent<CharacterEntity>();
                if (ce != null && ce != entity &&
                    ce.RuntimeStats.IsAlive &&
                    ce.RuntimeStats.characterType != CharacterType.Player)
                {
                    // Use SpriteRenderer sorting order to determine "topmost"
                    SpriteRenderer sr = ce.GetComponent<SpriteRenderer>();
                    int order = sr != null ? sr.sortingOrder : 0;
                    if (bestEnemy == null || order > bestOrder)
                    {
                        bestEnemy = ce;
                        bestOrder = order;
                    }
                }
            }

            return bestEnemy;
        }

        /// <summary>
        /// Clear the outline on the currently hovered enemy and reset tracking.
        /// </summary>
        private void ClearHoveredEnemy()
        {
            if (hoveredEnemy != null)
            {
                if (hoveredEnemy.OutlineFx != null)
                {
                    hoveredEnemy.OutlineFx.ShouldRemainAfterPulse = false;
                    // Only clear outline if not in a bold pulse (pulse will handle its own cleanup)
                    if (!hoveredEnemy.OutlineFx.IsBoldPulsing)
                    {
                        hoveredEnemy.OutlineFx.SetOutline(false);
                    }
                }
                hoveredEnemy = null;
            }
        }


        // =====================================================================
        // Arrow Indicator Helpers
        // =====================================================================

        /// <summary>
        /// Destroy the current arrow indicator if it exists.
        /// </summary>
        private void DestroyCurrentArrow()
        {
            if (currentArrow != null)
            {
                currentArrow.DestroyNow();
                currentArrow = null;
            }
        }
    }
}
