# 实施计划

- [ ] 1. 修复 CharacterAnimator 中 Hit 动画的触发逻辑
   - 修改 `Assets/Scripts/Character/CharacterAnimator.cs` 的 `PlayHit()` 方法
   - 在 `PlayHit()` 中先调用 `ResetAllTriggers()` 清除所有待处理的Trigger，再设置Hit Trigger，确保Hit动画能从头重新播放
   - 添加 `isInHitState` 标志位和 `hitStateTimer` 计时器，用于标记角色当前处于Hit动画保护期
   - 添加公共属性 `IsInHitState` 供行为树节点查询，避免在Hit动画期间被打断
   - 在 `Update()` 中递减 `hitStateTimer`，当计时器归零时自动清除 `isInHitState` 标志
   - Hit保护期时长设为可配置常量（建议0.3~0.5秒，与Hit动画时长匹配）
   - _需求：1.1、1.2、1.3、1.5_

- [ ] 2. 修改行为树攻击节点，在Hit动画期间不打断动画
   - 修改 `Assets/Scripts/AI/Nodes/BTAttack.cs` 的 `Execute()` 方法
   - 在调用 `PlayIdle()` 之前，检查 `owner.CharAnimator.IsInHitState`，如果为true则跳过 `PlayIdle()` 调用
   - 同样在 `TryNormalAttack()` 调用前检查Hit状态，如果正在播放Hit动画则暂不发起攻击，直接返回Running
   - _需求：1.5_

- [ ] 3. 修复 EnemySpawner 中缺少 AnimEventReceiver 组件的问题
   - 修改 `Assets/Scripts/Character/EnemySpawner.cs` 的 `SpawnEnemy()` 和 `SpawnSpecificEnemy()` 方法
   - 在确保 `CombatSystem` 组件存在之后，添加对 `AnimEventReceiver` 组件的检查和添加逻辑：`if (enemyObj.GetComponent<AnimEventReceiver>() == null) enemyObj.AddComponent<AnimEventReceiver>();`
   - 这是敌人不播放Hit动画的根本原因：没有 `AnimEventReceiver` → 帧事件不触发 → `ApplyNormalAttackDamage()` 不被调用 → `TakeDamage()` 不被调用 → 敌人不播放Hit动画
   - _需求：2.1、2.2、2.3、2.5_

- [ ] 4. 增强 CharacterEntity 的 TakeDamage 方法，添加防御性检查和日志
   - 修改 `Assets/Scripts/Character/CharacterEntity.cs` 的 `TakeDamage()` 方法
   - 在调用 `CharAnimator.PlayHit()` 前添加 Debug.Log 输出，记录受伤角色名称和伤害值，方便调试
   - 确保 `CharAnimator` 为null时输出警告日志，帮助排查初始化问题
   - _需求：2.2_

- [ ] 5. 在 CharacterEntity 中添加攻击范围 Gizmo 可视化
   - 修改 `Assets/Scripts/Character/CharacterEntity.cs`
   - 添加 `OnDrawGizmosSelected()` 方法：当角色被选中时，以红色半透明圆（`Gizmos.color = new Color(1, 0, 0, 0.3f)`）绘制攻击范围，使用 `Gizmos.DrawWireSphere()` 绘制线框圆
   - 添加 `OnDrawGizmos()` 方法：当角色未被选中时，以淡色（`Gizmos.color = new Color(1, 0, 0, 0.1f)`）绘制攻击范围线框
   - 在绘制前检查 `RuntimeStats != null`，避免未初始化时的空引用异常
   - 使用 `RuntimeStats.attackRange` 作为圆的半径，数值变化时自动更新
   - _需求：3.1、3.2、3.3、3.4、3.5_

- [ ] 6. 创建 HealthBar 血条组件
   - 新建 `Assets/Scripts/UI/HealthBar.cs`
   - 使用纯代码方式创建血条（不依赖预制体），在 `Awake()` 或 `Initialize()` 中动态创建两个子 SpriteRenderer：背景条（深灰色）和前景条（绿色）
   - 使用 Unity 内置的白色方块精灵（`Sprite.Create` 或 `Resources.GetBuiltinResource`）作为血条素材
   - 实现 `UpdateHealth(float current, float max)` 方法：根据比例缩放前景条的 `localScale.x`，并根据比例切换颜色（>50%绿色，25%~50%黄色，<25%红色）
   - 血条位置通过 `offset` 参数控制，默认在角色头顶上方（Y轴偏移约0.8~1.0）
   - 在 `LateUpdate()` 中处理翻转补偿：检测父物体的 `SpriteRenderer.flipX`，如果翻转则将血条的 `localScale.x` 取反，确保血条始终正向显示
   - _需求：4.1、4.2、4.3、4.4、4.5、4.6、4.8_

- [ ] 7. 将 HealthBar 集成到角色生命周期中
   - 修改 `Assets/Scripts/Character/CharacterEntity.cs`
   - 在 `Initialize()` 方法末尾，动态创建 HealthBar 子物体并初始化（或获取已有的 HealthBar 组件）
   - 在 `TakeDamage()` 方法中，伤害计算后调用 `healthBar.UpdateHealth(RuntimeStats.currentHealth, RuntimeStats.maxHealth)` 更新血条
   - 在 `Die()` 方法中隐藏或销毁血条
   - 在 `CleanUp()` 方法中确保血条被正确清理，避免对象池复用时残留旧血条
   - _需求：4.1、4.2、4.7_

- [ ] 8. 修改 EnemySpawner 确保生成的敌人拥有 HealthBar
   - 修改 `Assets/Scripts/Character/EnemySpawner.cs` 的 `SpawnEnemy()` 和 `SpawnSpecificEnemy()` 方法
   - 由于 HealthBar 在 `CharacterEntity.Initialize()` 中自动创建，此步骤主要验证对象池复用时血条状态是否正确重置
   - 如果 `Initialize()` 中的自动创建逻辑已覆盖复用场景，则此步骤仅需验证无需额外代码
   - _需求：4.1、4.7_

- [ ] 9. Animator Controller 配置说明（Unity Editor 手动操作）
   - 在 `CharacterAnimator.cs` 的类注释中添加配置说明
   - 说明 Hit 动画状态的 Loop Time 必须设为 false（非循环）
   - 说明 Hit 状态应配置自动过渡回 Idle（Exit Time 过渡，无需条件）
   - 说明攻击动画片段必须配置 Animation Event：在命中帧添加事件，回调方法名为 `OnAttackHit`（普通攻击）或 `OnSkillHit`（技能）
   - _需求：1.2、1.4、2.4_
