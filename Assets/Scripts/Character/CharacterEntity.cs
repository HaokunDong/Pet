using UnityEngine;

namespace PetGame
{
    /// <summary>
    /// Core component attached to every character GameObject (player, enemy, boss).
    /// Holds the static data reference and runtime stats instance.
    /// </summary>
    public class CharacterEntity : MonoBehaviour
    {
        [Header("Configuration")]
        [Tooltip("Static character data template (ScriptableObject)")]
        public CharacterData characterData;

        /// <summary>
        /// Runtime stats instance, independent per character.
        /// </summary>
        public RuntimeCharacterStats RuntimeStats { get; private set; }

        /// <summary>
        /// Reference to the CharacterAnimator component (set during init or via GetComponent).
        /// </summary>
        public CharacterAnimator CharAnimator { get; private set; }

        /// <summary>
        /// Reference to the FlashEffect component for hit flash visual feedback.
        /// </summary>
        public FlashEffect FlashFx { get; private set; }

        /// <summary>
        /// Reference to the OutlineEffect component for enemy highlight outline.
        /// </summary>
        public OutlineEffect OutlineFx { get; private set; }

        /// <summary>
        /// Reference to the KnockbackController component for hit knockback physics.
        /// </summary>
        public KnockbackController Knockback { get; private set; }

        /// <summary>
        /// Reference to the HealthBar component displayed above this character.
        /// </summary>
        private HealthBar healthBar;

        /// <summary>
        /// Reference to the PlayerNameTag component displayed above the health bar.
        /// </summary>
        private PlayerNameTag nameTag;
        private string networkPlayerName;

        /// <summary>
        /// Whether this entity has been initialized.
        /// </summary>
        public bool IsInitialized { get; private set; }

        /// <summary>
        /// Event fired when this character dies.
        /// </summary>
        public event System.Action<CharacterEntity> OnDeath;

        /// <summary>
        /// Event fired when this character takes damage.
        /// </summary>
        public event System.Action<CharacterEntity, float> OnDamageTaken;

        /// <summary>
        /// Current velocity of the character, computed from per-frame position delta.
        /// Used by projectile skills for target movement prediction.
        /// </summary>
        public Vector2 Velocity { get; private set; }

        /// <summary>
        /// Cached reference to the character's Collider2D component.
        /// Used for attack range detection against collider center instead of transform position.
        /// </summary>
        public Collider2D CharCollider { get; private set; }

        /// <summary>
        /// Returns the center of the character's collider bounds in world space.
        /// Falls back to transform.position if no collider is present.
        /// Used as the target point for attack range checks.
        /// </summary>
        public Vector2 ColliderCenter
        {
            get
            {
                if (CharCollider != null)
                    return CharCollider.bounds.center;
                return transform.position;
            }
        }

        /// <summary>
        /// Returns half the width of the character's collider bounds on the X axis.
        /// Used for attack range overlap checks against the target's collider X interval.
        /// Returns 0 if no collider is present.
        /// </summary>
        public float ColliderHalfExtentX
        {
            get
            {
                if (CharCollider != null)
                    return CharCollider.bounds.extents.x;
                return 0f;
            }
        }

        /// <summary>
        /// The last entity that dealt damage to this character.
        /// Used to award experience on death.
        /// </summary>
        private CharacterEntity lastAttacker;

        private Vector3 lastFramePosition;

        private void Awake()
        {
            if (characterData != null)
            {
                Initialize(characterData);
            }
        }

        /// <summary>
        /// Initialize the entity with a CharacterData template.
        /// Creates a runtime stats copy so multiple instances don't share state.
        /// </summary>
        public void Initialize(CharacterData data)
        {
            characterData = data;
            RuntimeStats = new RuntimeCharacterStats();
            RuntimeStats.InitFromData(data);

            CharAnimator = GetComponent<CharacterAnimator>();
            CharCollider = GetComponent<Collider2D>();

            // Sync sprite orientation from CharacterData to CharacterAnimator
            if (CharAnimator != null)
            {
                CharAnimator.SyncDefaultFacing(data.defaultFacesRight);

                // Set skill count so CharacterAnimator knows whether to use
                // Skill Trigger (single skill) or Skills Int (multi-skill) mode
                int skillCount = (data.skills != null) ? data.skills.Length : 0;
                CharAnimator.SetSkillCount(skillCount);

                // Initialize the FSM after animator controller and skill count are set
                CharAnimator.InitializeStateMachine(data.characterType, skillCount);
            }

            // Ensure FlashEffect component exists and SpriteRenderer uses the outline+flash material
            InitializeFlashEffect();

            // Ensure OutlineEffect component exists
            OutlineFx = GetComponent<OutlineEffect>();
            if (OutlineFx == null)
            {
                OutlineFx = gameObject.AddComponent<OutlineEffect>();
            }

            // Ensure KnockbackController component exists
            Knockback = GetComponent<KnockbackController>();
            if (Knockback == null)
            {
                Knockback = gameObject.AddComponent<KnockbackController>();
            }

            // Create or re-initialize the health bar above this character
            InitializeHealthBar();

            // Create or re-initialize the name tag above the health bar
            InitializeNameTag();

            // Initialize velocity tracking
            lastFramePosition = transform.position;
            Velocity = Vector2.zero;

            // Ensure Rigidbody2D has FreezeRotation constraint to prevent physics
            // collisions from rotating the character sprite (critical for multiplayer
            // where MirrorEnemy/MirrorCharacter collisions could cause rotation).
            // Also reset physics state in case the object was recycled from pool.
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.constraints |= RigidbodyConstraints2D.FreezeRotation;
                rb.angularVelocity = 0f;
                transform.rotation = Quaternion.identity;
            }

            IsInitialized = true;
        }

        /// <summary>
        /// Ensure FlashEffect component is attached and SpriteRenderer uses the CharacterFlash shader material.
        /// </summary>
        private void InitializeFlashEffect()
        {
            FlashFx = GetComponent<FlashEffect>();
            if (FlashFx == null)
            {
                FlashFx = gameObject.AddComponent<FlashEffect>();
            }

            // Ensure the SpriteRenderer uses the CharacterFlash shader material.
            // If the material already uses the correct shader, skip to avoid breaking shared material references.
            SpriteRenderer sr = GetComponent<SpriteRenderer>();
            if (sr != null && (sr.sharedMaterial == null || sr.sharedMaterial.shader.name != "Game/SpriteOutline"))
            {
                // Try to load the pre-made material from Resources first (works in builds)
                Material outlineMat = Resources.Load<Material>("SpriteOutline_Reference");
                if (outlineMat != null)
                {
                    // Use the pre-made material instance
                    sr.material = new Material(outlineMat);
                    sr.material.name = "SpriteOutline_Runtime";
                }
                else
                {
                    // Fallback to Shader.Find for editor mode
                    Shader outlineShader = Shader.Find("Game/SpriteOutline");
                    if (outlineShader != null)
                    {
                        // Create a runtime material instance with the outline+flash shader
                        Material runtimeMat = new Material(outlineShader);
                        runtimeMat.name = "SpriteOutline_Runtime";
                        sr.material = runtimeMat;
                    }
                }
            }
        }
        /// <summary>
        /// Create or re-initialize the HealthBar child object.
        /// Handles both first-time creation and object pool reuse.
        /// </summary>
        private void InitializeHealthBar()
        {
            // Check if a HealthBar child already exists (object pool reuse)
            healthBar = GetComponentInChildren<HealthBar>(true);

            if (healthBar == null)
            {
                // Create a new HealthBar child object
                GameObject hbObj = new GameObject("HealthBar");
                hbObj.transform.SetParent(transform, false);
                healthBar = hbObj.AddComponent<HealthBar>();

                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                healthBar.Initialize(sr);
            }

            // Update to full health and show
            healthBar.UpdateHealth(RuntimeStats.currentHealth, RuntimeStats.maxHealth);
            healthBar.Show();
        }

        /// <summary>
        /// Create or re-initialize the PlayerNameTag child object.
        /// Handles both first-time creation and object pool reuse.
        /// The name tag is hidden by default and only shown when SetPlayerName is called (online mode).
        /// </summary>
        private void InitializeNameTag()
        {
            // Check if a PlayerNameTag child already exists (object pool reuse)
            nameTag = GetComponentInChildren<PlayerNameTag>(true);

            if (nameTag == null)
            {
                // Create a new PlayerNameTag child object
                GameObject ntObj = new GameObject("PlayerNameTag");
                ntObj.transform.SetParent(transform, false);
                nameTag = ntObj.AddComponent<PlayerNameTag>();

                SpriteRenderer sr = GetComponent<SpriteRenderer>();
                nameTag.Initialize(sr);
            }

            // Hide by default - only shown when SetPlayerName is called (online mode)
            if (!string.IsNullOrEmpty(networkPlayerName))
            {
                nameTag.SetName(networkPlayerName);
                nameTag.Show();
            }
            else nameTag.Hide();
        }

        /// <summary>
        /// Set the player name displayed above this character.
        /// Called by NetworkPlayer after character creation (online mode only).
        /// This also makes the name tag visible.
        /// </summary>
        /// <param name="name">The player's Steam display name.</param>
        public void SetPlayerName(string name)
        {
            networkPlayerName = name;
            if (nameTag == null) InitializeNameTag();
            if (nameTag != null)
            {
                nameTag.SetName(name);
                nameTag.Show();
            }
        }

        private void Update()
        {
            if (!IsInitialized || !RuntimeStats.IsAlive) return;

            // Track velocity from position delta (characters move via transform, not Rigidbody)
            Vector3 currentPos = transform.position;
            Velocity = (Vector2)(currentPos - lastFramePosition) / Time.deltaTime;
            lastFramePosition = currentPos;

            // Tick skill cooldowns
            RuntimeStats.UpdateCooldowns(Time.deltaTime);
        }

        /// <summary>
        /// Apply damage to this character.
        /// Actual damage = attackPower - defense (min 1).
        /// </summary>
        public void TakeDamage(float attackPower, CharacterEntity attacker = null)
        {
            // Replica health and death are exclusively applied by network state.
            if (GetComponent<PetGame.Network.MirrorEnemyTag>() != null) return;
            if (!RuntimeStats.IsAlive) return;

            float actualDamage = Mathf.Max(1f, attackPower - RuntimeStats.defense);
            RuntimeStats.currentHealth -= actualDamage;

            // Track the last attacker for experience reward on death
            if (attacker != null)
            {
                lastAttacker = attacker;
            }

            OnDamageTaken?.Invoke(this, actualDamage);

            // Update health bar
            if (healthBar != null)
            {
                healthBar.UpdateHealth(RuntimeStats.currentHealth, RuntimeStats.maxHealth);
            }

            // Play hit animation
            if (CharAnimator != null)
            {
                CharAnimator.PlayHit();
            }


            // Trigger flash white effect (even on lethal hit — Die() will not cancel it)
            if (FlashFx != null)
            {
                FlashFx.TriggerFlash();
            }

            // Apply knockback away from attacker
            if (Knockback != null && attacker != null)
            {
                Knockback.ApplyKnockback(attacker.transform.position);
            }

            if (!RuntimeStats.IsAlive)
            {
                // Delay Die() slightly so the flash effect is visible on the killing blow
                float deathDelay = (FlashFx != null) ? FlashFx.flashDuration : 0f;
                Invoke(nameof(Die), deathDelay);
            }
        }

        /// <summary>
        /// Handle character death: play animation, fire event, recycle via pool.
        /// </summary>
        private void Die()
        {
            // Award experience to the killer if this is an enemy killed by a player
            AwardExperienceToKiller();

            // Record kill stats for the killer (player characters only)
            RecordKillStats();

            OnDeath?.Invoke(this);

            // Hide health bar on death
            if (healthBar != null)
            {
                healthBar.Hide();
            }

            // Stop flash effect on death
            if (FlashFx != null)
            {
                FlashFx.ResetFlash();
            }

            // Stop outline effect on death
            if (OutlineFx != null)
            {
                OutlineFx.ResetOutline();
            }

            // Stop knockback on death
            if (Knockback != null)
            {
                Knockback.CancelKnockback();
            }

            if (CharAnimator != null)
            {
                CharAnimator.PlayDeath();
            }

            // Delay recycle to allow death animation to play
            Invoke(nameof(Recycle), 1f);
        }

        /// <summary>
        /// Award experience points to the killer when this enemy dies.
        /// Only awards exp if this character is an enemy and the killer is a player.
        /// </summary>
        private void AwardExperienceToKiller()
        {
            if (lastAttacker == null) return;
            if (RuntimeStats == null || lastAttacker.RuntimeStats == null) return;

            // Only enemies give experience
            if (RuntimeStats.characterType == CharacterType.Player) return;

            // Only player characters receive experience
            if (lastAttacker.RuntimeStats.characterType != CharacterType.Player) return;

            // Skip if the attacker has no characterId (e.g. summoned entities without an ID)
            if (string.IsNullOrEmpty(lastAttacker.RuntimeStats.characterId)) return;

            // Get exp reward from CharacterData
            int expReward = (characterData != null) ? characterData.expReward : 10;
            if (expReward <= 0) expReward = 10; // Default fallback

            CultivationManager.Instance.AddExperience(lastAttacker, expReward);
            Debug.Log($"[CharacterEntity] '{lastAttacker.RuntimeStats.characterId}' gained {expReward} exp from defeating '{RuntimeStats.characterId}'");
        }

        /// <summary>
        /// Record a kill for the attacker's kill statistics (used for evolution requirements).
        /// Only records when a player character kills an enemy.
        /// </summary>
        private void RecordKillStats()
        {
            if (lastAttacker == null) return;
            if (RuntimeStats == null || lastAttacker.RuntimeStats == null) return;

            // Only enemies count as kills
            if (RuntimeStats.characterType == CharacterType.Player) return;

            // Only player characters record kills
            if (lastAttacker.RuntimeStats.characterType != CharacterType.Player) return;

            // Skip if the attacker has no characterId (e.g. summoned entities without an ID)
            string attackerId = lastAttacker.RuntimeStats.characterId;
            if (string.IsNullOrEmpty(attackerId)) return;

            KillStatsManager.Instance.AddKill(attackerId);
        }

        /// <summary>
        /// Recycle this character back to the object pool.
        /// </summary>
        private void Recycle()
        {
            CleanUp();
            PoolMgr.Instance.PutNode(gameObject);
        }

        /// <summary>
        /// Clean up all runtime state and event listeners.
        /// </summary>
        private void CleanUp()
        {
            networkPlayerName = null;
            OnDeath = null;
            OnDamageTaken = null;
            IsInitialized = false;
            lastAttacker = null;

            // Reset state machine when recycled
            if (CharAnimator != null)
            {
                CharAnimator.ResetStateMachine();
            }

            // Hide health bar when recycled (will be re-shown on next Initialize)
            if (healthBar != null)
            {
                healthBar.Hide();
            }

            // Reset and hide name tag when recycled (will be re-shown on next Initialize)
            if (nameTag != null)
            {
                nameTag.ResetState();
                nameTag.Hide();
            }

            // Reset outline effect when recycled
            if (OutlineFx != null)
            {
                OutlineFx.ResetOutline();
            }
        }

        private void OnDisable()
        {
            CancelInvoke();
        }



        private void OnDestroy()
        {
            CleanUp();
        }

#if UNITY_EDITOR
        /// <summary>
        /// Get the default facing sign for Gizmo drawing.
        /// Based on CharacterData.defaultFacesRight: true = right (1), false = left (-1).
        /// </summary>
        private float GetFacingSign()
        {
            if (characterData != null)
            {
                return characterData.defaultFacesRight ? 1f : -1f;
            }
            return 1f;
        }

        /// <summary>
        /// Draw attack range gizmo when the character is selected in the Scene view.
        /// Shows prominent semi-transparent shapes with wire outlines.
        /// </summary>
        private void OnDrawGizmosSelected()
        {
            DrawAttackRangeGizmos(selected: true);
            DrawSkillRangeGizmos(selected: true);
        }

        /// <summary>
        /// Draw attack range gizmo when the character is NOT selected (faint outline).
        /// </summary>
        private void OnDrawGizmos()
        {
            DrawAttackRangeGizmos(selected: false);
            DrawSkillRangeGizmos(selected: false);
        }

        /// <summary>
        /// Draw attack distance as a horizontal line Gizmo.
        /// Also draws minAttackDistance as a short vertical marker line.
        /// </summary>
        private void DrawAttackRangeGizmos(bool selected)
        {
            // Resolve data source: prefer RuntimeStats at runtime, fallback to characterData in edit mode
            float atkDist;
            float minAtkDist;

            if (RuntimeStats != null)
            {
                atkDist = RuntimeStats.attackDistance;
                minAtkDist = RuntimeStats.minAttackDistance;
            }
            else if (characterData != null)
            {
                atkDist = characterData.attackDistance;
                minAtkDist = characterData.minAttackDistance;
            }
            else
            {
                return;
            }

            float facingSign = GetFacingSign();
            Vector3 pos = transform.position;

            // Draw attack distance as a horizontal line in the facing direction
            if (atkDist > 0f)
            {
                float alpha = selected ? 0.8f : 0.3f;
                Color lineColor = new Color(1f, 0.2f, 0.2f, alpha);
                Gizmos.color = lineColor;

                Vector3 endPos = pos + new Vector3(facingSign * atkDist, 0f, 0f);
                Gizmos.DrawLine(pos, endPos);

                // Draw a small vertical tick at the end point to mark the distance
                float tickHeight = 0.15f;
                Gizmos.DrawLine(endPos + Vector3.up * tickHeight, endPos + Vector3.down * tickHeight);

                if (selected)
                {
                    // Draw label showing the attack distance value
                    Vector3 labelPos = endPos + Vector3.up * 0.2f;
                    UnityEditor.Handles.color = lineColor;
                    UnityEditor.Handles.Label(labelPos, $"AtkDist: {atkDist:F2}");
                }
            }

            // Draw minAttackDistance as a short green vertical marker line
            if (minAtkDist > 0f)
            {
                float alpha = selected ? 0.7f : 0.25f;
                Color minDistColor = new Color(0f, 1f, 0f, alpha);
                Gizmos.color = minDistColor;

                Vector3 minDistPos = pos + new Vector3(facingSign * minAtkDist, 0f, 0f);
                float tickHeight = 0.12f;
                Gizmos.DrawLine(minDistPos + Vector3.up * tickHeight, minDistPos + Vector3.down * tickHeight);

                if (selected)
                {
                    Vector3 labelPos = minDistPos + Vector3.down * 0.25f;
                    UnityEditor.Handles.color = minDistColor;
                    UnityEditor.Handles.Label(labelPos, $"MinDist: {minAtkDist:F2}");
                }
            }

        }

        /// <summary>
        /// Draw skill range and skill attack distance as horizontal lines for each skill slot.
        /// This method reads directly from characterData (serialized field),
        /// so it works both in Edit mode and Play mode.
        /// </summary>
        private void DrawSkillRangeGizmos(bool selected)
        {
            if (characterData == null || characterData.skills == null) return;

            Vector3 pos = transform.position;
            int maxSkills = characterData.GetMaxSkillCount();
            float facingSign = GetFacingSign();

            // Colors for different skill slots: cyan, magenta, orange, purple
            Color[] skillColors = new Color[]
            {
                new Color(0f, 1f, 1f, 1f),    // Cyan - Skill 1
                new Color(1f, 0f, 1f, 1f),    // Magenta - Skill 2
                new Color(1f, 0.5f, 0f, 1f),  // Orange - Skill 3
                new Color(0.6f, 0.2f, 1f, 1f) // Purple - Skill 4
            };

            for (int i = 0; i < characterData.skills.Length && i < maxSkills; i++)
            {
                SkillData skill = characterData.skills[i];
                if (skill == null) continue;

                Color baseColor = skillColors[i % skillColors.Length];
                float yOffset = 0.08f * (i + 1); // Offset each skill line vertically to avoid overlap

                // Draw skillRange as a horizontal line (trigger distance for AI)
                if (skill.skillRange > 0f)
                {
                    float alpha = selected ? 0.6f : 0.2f;
                    Color lineCol = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                    Gizmos.color = lineCol;

                    Vector3 startPos = pos + new Vector3(0f, yOffset, 0f);
                    Vector3 endPos = pos + new Vector3(facingSign * skill.skillRange, yOffset, 0f);
                    Gizmos.DrawLine(startPos, endPos);

                    // Draw a small vertical tick at the end
                    float tickHeight = 0.1f;
                    Gizmos.DrawLine(endPos + Vector3.up * tickHeight, endPos + Vector3.down * tickHeight);

                    if (selected)
                    {
                        string label = string.IsNullOrEmpty(skill.skillName)
                            ? $"Skill {i + 1} Range: {skill.skillRange:F2}"
                            : $"{skill.skillName} Range: {skill.skillRange:F2}";
                        Vector3 labelPos = endPos + Vector3.up * 0.15f;
                        UnityEditor.Handles.color = lineCol;
                        UnityEditor.Handles.Label(labelPos, label);
                    }
                }

                // Draw skill attack distance (actual damage range) from MeleeSkillEffectData
                if (skill.skillEffect is MeleeSkillEffectData meleeEffect && meleeEffect.skillAttackDistance > 0f)
                {
                    float skillDist = meleeEffect.skillAttackDistance;
                    float alpha = selected ? 0.8f : 0.3f;
                    Color effectLineColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                    Gizmos.color = effectLineColor;

                    float effectYOffset = yOffset - 0.04f; // Slightly below the skill range line
                    Vector3 startPos = pos + new Vector3(0f, effectYOffset, 0f);
                    Vector3 endPos = pos + new Vector3(facingSign * skillDist, effectYOffset, 0f);
                    Gizmos.DrawLine(startPos, endPos);

                    // Draw a small vertical tick at the end
                    float tickHeight = 0.08f;
                    Gizmos.DrawLine(endPos + Vector3.up * tickHeight, endPos + Vector3.down * tickHeight);

                    if (selected)
                    {
                        string effectLabel = string.IsNullOrEmpty(skill.skillName)
                            ? $"Skill {i + 1} Dist: {skillDist:F2}"
                            : $"{skill.skillName} Dist: {skillDist:F2}";
                        Vector3 labelPos = endPos + Vector3.down * 0.2f;
                        UnityEditor.Handles.color = effectLineColor;
                        UnityEditor.Handles.Label(labelPos, effectLabel);
                    }
                }
            }
        }
#endif
    }
}
