# 实施计划：战斗行为与碰撞系统改写

> 基于需求文档 `requirements.md`，以下为按顺序执行的编码任务清单。

---

- [ ] 1. 配置 Physics Layer 并在实体生成时设置层级，取消实体间碰撞
  - 在 `GameCharacterManager.CreatePlayerCharacter` 和 `EnemySpawner.SpawnEnemy` / `SpawnSpecificEnemy` 中，为生成的 GameObject 设置 `layer` 为 "Character" 层（需在 Unity Editor 中预先创建该层，代码中使用 `LayerMask.NameToLayer("Character")`）
  - 在 `GameCharacterManager.Start` 或一个初始化方法中，调用 `Physics2D.IgnoreLayerCollision(characterLayer, characterLayer, true)` 取消 Character 层与自身的碰撞
  - 确保地形保持在 Default 层，Character 层与 Default 层碰撞正常
  - 验证 `ControlModeManager` 的 `Physics2D.OverlapPoint` 鼠标点击检测不受影响（OverlapPoint 不受碰撞矩阵限制，无需额外处理）
  - _需求：3.1、3.2、3.3、3.4_

- [ ] 2. 为 `BTContext` 添加攻击状态标记字段
  - 在 `BTContext` 中新增 `bool IsInCombat` 属性，表示当前实体是否正处于攻击状态（已进入攻击范围并正在攻击目标）
  - 新增 `bool WallDetectedAhead` 属性，供墙体检测使用
  - 新增 `float WallDetectDistance` 属性（默认 0.5f），配置射线检测距离
  - _需求：2.1、2.2、4.4_

- [ ] 3. 修改 `BTAttack` 节点，攻击时标记战斗状态并停止移动
  - 在 `BTAttack.Execute` 开始攻击时，设置 `context.IsInCombat = true`
  - 当目标死亡或超出攻击范围时，设置 `context.IsInCombat = false` 并返回 `Failure`
  - 攻击期间确保不执行任何位移操作（当前实现已无位移，只需确认）
  - _需求：2.1、2.2、2.3、2.4_

- [ ] 4. 修改 `BTMoveToTarget` 节点，战斗状态下跳过移动并添加墙体检测
  - 在 `Execute` 方法开头检查 `context.IsInCombat`，若为 `true` 则直接返回 `Success`（跳过移动，让行为树继续执行攻击节点）
  - 在移动前添加 `Physics2D.Raycast` 墙体检测：从实体位置向移动方向发射射线，检测 Default 层（地形），距离使用 `context.WallDetectDistance`
  - 若检测到墙体，返回 `Failure`（放弃追踪该目标，行为树回退到巡逻）
  - _需求：2.2、4.2、4.4_

- [ ] 5. 修改 `BTPatrol` 节点，添加墙体检测实现遇墙转身
  - 在每帧移动前，使用 `Physics2D.Raycast` 向当前移动方向发射短距离射线，检测 Default 层（地形）
  - 若检测到墙体，立即反转 `context.IsMovingRight` 方向
  - 转身后同步更新 `CharacterAnimator` 的朝向
  - 射线检测距离使用 `context.WallDetectDistance`，射线起点应略高于地面（避免检测到地面本身）
  - _需求：4.1、4.3、4.4_

- [ ] 6. 重构 `AIController` 中敌人行为树结构，确保有玩家时不巡逻
  - 当前敌人行为树结构为 `BTSelector(combatSeq, patrol)`，`combatSeq` 中 `BTFindNearestEnemy` 找不到玩家时返回 `Failure`，会回退到巡逻——这已经符合"无玩家时巡逻"的逻辑
  - 验证 `BTFindNearestEnemy` 在有存活玩家时始终返回 `Success`，确保不会误回退到巡逻
  - 在 `BTFindNearestEnemy` 中增加对目标存活状态的二次校验：如果 `context.CurrentTarget` 已死亡，清除目标并重新搜索
  - 确保敌人在目标死亡后能立即切换到下一个最近的存活玩家，或在无玩家时切换到巡逻
  - _需求：1.1、1.2、1.3、1.4、1.5_

- [ ] 7. 修改 `ManualController`，攻击时停止移动直到新指令
  - 在 `HandleChaseAndAttack` 中，当进入攻击范围并执行攻击后，设置 `isMoving = false` 并播放 Idle 动画（当前已有部分逻辑，需确认攻击后持续停止而非单次）
  - 添加持续攻击逻辑：当 `attackTarget` 存活且在攻击范围内时，持续按攻速间隔执行攻击，期间角色保持静止
  - 仅当玩家发出新的右键移动指令时才中断攻击状态
  - _需求：2.5_

- [ ] 8. 在 `BTContext` 中初始化 `WallDetectDistance` 并通过 `AIController` 传递配置
  - 在 `AIController` 中新增 `[SerializeField] float wallDetectDistance = 0.5f` 配置项
  - 在 `BuildTree` 方法中将该值赋给 `context.WallDetectDistance`
  - 确保巡逻和追踪节点都能读取到正确的墙体检测距离
  - _需求：4.4_

- [ ] 9. 整体集成验证与边界情况处理
  - 验证多玩家场景下敌人始终追踪最近存活玩家（`BTFindNearestEnemy` 每帧重新搜索最近目标）
  - 验证攻击范围不对称时的行为：A 在 B 范围内但 B 不在 A 范围内，只有 B 攻击，A 继续移动
  - 验证对象池回收后重新取出的实体 layer 保持为 Character 层
  - 验证手动操控模式下不触发自动转身（`ManualController` 不使用 Raycast 转身逻辑）
  - 验证 `ControlModeManager` 的鼠标点击检测在新 Layer 设置下正常工作
  - _需求：1.1-1.5、2.1-2.5、3.1-3.4、4.1-4.5_
