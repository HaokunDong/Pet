# 实施计划：修复AI战斗不触发与实体重叠卡住问题

## 代码分析总结

通过对现有代码的详细分析，确认了以下关键问题：

1. **`BTContext.TerrainLayerMask`** 初始化为 `LayerMask.GetMask("Default")`，地面 Collider 也在 Default 层，导致 `BTMoveToTarget` 和 `BTPatrol` 中的墙体检测 Raycast 误判地面为墙壁
2. **`BTMoveToTarget`** 的 Raycast 起点 `y + 0.2f` 过低，容易打到地面 Collider 边缘
3. **`BTAttack`** 攻击成功返回 `Success` → `BTSequence` 的 `currentIndex` 重置为 0 → 下一帧从 `findEnemy` 重新开始，攻速冷却期间角色可能重新进入移动状态
4. **`BTMoveToTarget`** 没有最小间距控制，角色会一直移动到目标中心点，导致实体重叠
5. **Physics Layer "Character"** 需要在 Unity Editor 中手动创建，如果未创建则碰撞忽略不生效

---

- [ ] 1. 修复墙体检测 Raycast 层级配置，避免误判地面为墙壁
   - 修改 `BTContext.cs`：将 `TerrainLayerMask` 的默认值从 `LayerMask.GetMask("Default")` 改为 `LayerMask.GetMask("Wall")`，并添加一个备用逻辑——如果 "Wall" 层不存在则回退到 `LayerMask.GetMask("Default")` 并在控制台输出警告
   - 在 `BTContext` 构造函数中添加层级有效性检查和日志提示
   - _需求：1.1、1.2_

- [ ] 2. 优化 `BTMoveToTarget` 的墙体检测参数和移动停止逻辑
   - 修改 `BTMoveToTarget.cs`：将 Raycast 起点从 `y + 0.2f` 提高到 `y + 0.5f`（角色中心高度），避免射线打到地面 Collider
   - 在距离判断中增加**停止间距**（`stoppingDistance`），当 `dist <= attackRange` 时返回 `Success` 并播放 Idle 动画，确保角色不会穿透到目标位置
   - 当 `IsInCombat` 为 true 时，面向目标并播放 Idle 动画后返回 `Success`，而非仅返回 `Success` 不做任何处理
   - _需求：1.1、1.2、2.1、2.2_

- [ ] 3. 优化 `BTPatrol` 的墙体检测参数
   - 修改 `BTPatrol.cs`：将 Raycast 起点从 `y + 0.2f` 提高到 `y + 0.5f`，与 `BTMoveToTarget` 保持一致
   - _需求：1.1_

- [ ] 4. 重构 `BTAttack` 的返回值逻辑，确保攻击循环不被中断
   - 修改 `BTAttack.cs`：攻击成功后返回 `Running` 而非 `Success`，使行为树保持在攻击节点上持续攻击，不会每次攻击后重置整棵树
   - 仅当目标死亡或脱离攻击范围时才返回 `Failure`，触发行为树重新搜索目标
   - 在攻速冷却等待期间，面向目标并播放 Idle 动画，避免角色在等待时播放错误动画
   - _需求：1.3、1.4、3.1、3.2_

- [ ] 5. 在 `BTMoveToTarget` 中添加最小间距控制，防止实体重叠
   - 修改 `BTMoveToTarget.cs`：定义一个 `minStoppingDistance`（如 0.3f），当角色与目标的距离小于此值时强制停止移动并返回 `Success`
   - 确保 `attackRange` 判断优先于 `minStoppingDistance`，即先检查是否在攻击范围内，再检查是否过近
   - _需求：2.1、2.2、2.3_

- [ ] 6. 增强 Physics Layer 碰撞忽略的健壮性
   - 修改 `GameCharacterManager.cs` 的 `Awake` 方法：当 "Character" 层不存在时，除了输出警告外，额外使用 `Physics2D.IgnoreLayerCollision` 对 Default 层自身碰撞进行忽略作为备用方案（可选，需评估副作用）
   - 修改 `EnemySpawner.cs`：在设置层级时，如果 "Character" 层不存在，输出更明确的操作指引日志
   - _需求：2.4_

- [ ] 7. 优化 `BTPatrol` 节点的战斗状态清理
   - 修改 `BTPatrol.cs`：在 `Execute` 方法开头，将 `context.IsInCombat` 设置为 `false`，确保进入巡逻状态时战斗标记已被清除
   - 将 `context.CurrentTarget` 设置为 `null`，避免巡逻时仍持有过期的目标引用
   - _需求：3.3、3.4_

- [ ] 8. 优化 `BTFindNearestEnemy` 的状态管理
   - 修改 `BTFindNearestEnemy.cs`：当找不到任何目标时（返回 `Failure`），同时清除 `IsInCombat` 标记，确保状态一致性
   - 当找到新目标且与当前目标不同时，重置 `IsInCombat` 为 `false`，让攻击节点重新建立战斗状态
   - _需求：3.1、3.2_

- [ ] 9. 整体集成验证与边界情况处理
   - 检查 `AIController.cs` 中行为树构建逻辑，确保 `combatSeq` 中 `moveToTarget` 返回 `Success` 后能正确进入 `attackSelector`
   - 验证多个敌人追踪同一玩家时，各自独立停在攻击范围边缘不会堆叠
   - 验证角色从战斗状态切换到巡逻状态时无抖动或瞬移
   - 验证 `BTAttack` 返回 `Running` 后，`BTSequence` 的 `currentIndex` 正确保持在攻击节点位置
   - _需求：1.3、1.4、2.3、3.4_
