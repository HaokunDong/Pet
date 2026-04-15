# 需求文档：修复AI战斗不触发与实体重叠卡住问题

## 引言

当前角色与敌人系统存在两个关键Bug：
1. **玩家角色只来回走路不攻击敌人**：AI行为树在执行过程中，角色能发现敌人并朝敌人移动，但到达攻击范围后未能正确进入攻击状态，而是反复在移动和巡逻之间切换。
2. **玩家和敌人有时会卡在一起**：两个实体移动到同一位置后，由于缺乏有效的间距控制，导致它们重叠在一起无法正常分离。

通过代码分析，发现以下根因：

### Bug 1 根因分析
行为树结构为：`BTSelector(combatSeq, patrol)`，其中 `combatSeq = BTSequence(findEnemy, moveToTarget, attackSelector)`。

核心问题在于 `BTMoveToTarget` 中的**墙体检测 Raycast 误判**：
- Raycast 从实体位置 `y + 0.2f` 处水平发射，使用 `TerrainLayerMask`（Default 层）检测墙体
- 如果地面 Collider（也在 Default 层）的边缘恰好在射线路径上，或者场景中有其他 Default 层物体在角色前方，Raycast 会误判为"前方有墙"
- `BTMoveToTarget` 返回 `Failure` → `combatSeq` 返回 `Failure` → 回退到 `patrol` → 角色开始巡逻
- 下一帧又尝试 `combatSeq`，又被墙体检测拦截 → 形成来回走路的循环

此外，`BTAttack` 攻击成功后返回 `Success`，整棵树重置 `currentIndex`，下一帧从 `findEnemy` 重新开始。如果攻速冷却期间 `BTAttack` 返回 `Running`，`BTSequence` 会记住位置，但如果中间任何节点状态变化（如 `IsInCombat` 标记被意外清除），也会导致攻击链断裂。

### Bug 2 根因分析
- 实体之间的碰撞忽略依赖 Unity Editor 中创建 "Character" 物理层，如果层未创建则 `IgnoreLayerCollision` 不生效
- 即使碰撞被正确忽略，AI 移动逻辑中没有**最小间距**控制，两个实体可以移动到完全相同的位置
- 当两个实体重叠后，它们的 AI 可能互相以对方为目标，在同一点来回微移形成"卡住"的视觉效果

## 需求

### 需求 1：修复墙体检测 Raycast 误判导致的攻击失败

**用户故事：** 作为一名玩家，我希望AI控制的角色能正确追踪并攻击敌人，以便挂机战斗能正常运行

#### 验收标准

1. WHEN AI角色发现敌人并开始追踪 THEN `BTMoveToTarget` SHALL 使用专门的墙体检测层（如 "Terrain" 或 "Wall" 层），而非 Default 层，避免地面 Collider 干扰
2. IF 无法创建专用墙体层 THEN `BTMoveToTarget` SHALL 将 Raycast 起点提高到角色中心高度（而非 `y + 0.2f`），并增加射线长度的合理阈值，确保不会误检地面
3. WHEN AI角色到达敌人攻击范围内 THEN 系统 SHALL 确保角色停止移动并持续执行攻击，不会因为行为树重置而中断攻击循环
4. WHEN `BTAttack` 处于攻速冷却期（返回 Running）THEN 行为树 SHALL 保持在攻击节点上等待，不回退到移动或巡逻节点

### 需求 2：修复实体重叠卡住问题

**用户故事：** 作为一名玩家，我希望角色和敌人不会重叠卡在一起，以便战斗画面清晰可辨

#### 验收标准

1. WHEN 两个实体（玩家或敌人）接近到一定距离 THEN 移动逻辑 SHALL 在到达攻击范围时停止前进，保持一个最小间距，不会穿透到对方位置
2. WHEN AI角色追踪目标时 THEN `BTMoveToTarget` SHALL 在目标进入攻击范围后立即停止移动并返回 Success，而非继续向目标中心点移动
3. WHEN 多个敌人追踪同一个玩家 THEN 各敌人 SHALL 在到达各自的攻击范围后独立停止，不会堆叠在玩家身上
4. IF 实体之间的 Physics Layer 碰撞忽略未正确生效（层未创建）THEN 系统 SHALL 在控制台输出明确的警告信息，并提供备用方案（如通过代码动态创建层或使用 Tag 过滤）

### 需求 3：增强行为树的战斗状态管理

**用户故事：** 作为一名开发者，我希望行为树的战斗状态转换更加健壮，以便后续扩展新角色和敌人时不会出现类似问题

#### 验收标准

1. WHEN 角色进入战斗状态（`IsInCombat = true`）THEN 行为树 SHALL 优先保持在战斗分支中，不会因为单次攻击成功就完全重置树状态
2. WHEN 目标死亡或脱离检测范围 THEN 系统 SHALL 清除战斗状态标记，并在下一帧重新从搜索目标开始
3. WHEN `BTPatrol` 节点执行时 THEN 系统 SHALL 确保 `IsInCombat` 标记已被清除，避免巡逻和战斗状态冲突
4. WHEN 行为树从战斗状态切换到巡逻状态 THEN 角色 SHALL 平滑过渡，不出现抖动或瞬移现象
