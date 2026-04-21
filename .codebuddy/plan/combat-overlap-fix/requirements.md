# 需求文档 — 修复敌我重叠来回跑抖动问题

## 引言

在挂机（行为树控制）战斗过程中，玩家与敌人相遇后会出现"两人重叠在一起来回跑"的抖动现象，体验极差。

### 问题根因

经代码分析定位到 **AI 行为树停止距离判定** 与 **带朝向偏移的攻击范围判定** 的组合逻辑存在缺陷：

1. **攻击范围带朝向偏移**：`AttackRangeShape.offset` 会根据 `FacingDirection` 做镜像，攻击范围集中在角色正前方。
2. **追击停止条件用的就是"是否进入攻击范围"**：`BTMoveToTarget` 在 `IsTargetInAttackRange() == true` 时立即停下，但此时双方距离往往已经非常近，甚至部分重叠。
3. **没有物理阻挡 / `MIN_STOPPING_DISTANCE` 太小**：两个角色可以相互穿越。
4. **穿越瞬间攻击范围从"命中"翻转为"不命中"**：当一方跨过另一方后，目标跑到身后，攻击范围 offset 在正前方，判定瞬间失败。
5. **`BTAttack` 判定失败后立刻退出战斗状态**：`context.IsInCombat = false` → 回到 `BTMoveToTarget` → 朝向翻转 → 攻击范围再次覆盖目标 → 又停下 → 又被穿越 → 循环。
6. **每帧按移动方向设置朝向**：在靠近和穿越过程中，朝向持续翻转，进一步放大抖动。

最终表现：两人重叠后 Idle ↔ Walk 高频切换、不停左右横跳，实际攻击命中率极低。

### 目标

- 修复"重叠后来回跑"的抖动循环。
- 保持攻击范围判定的语义正确（前向攻击 / 多形状组合依然有效）。
- 保证战斗过程中角色面对目标稳定，不因短暂抖动切断战斗状态。
- 不引入额外的物理系统复杂度（尽量复用现有组件）。

## 需求

### 需求 1：追击停止距离使用"攻击触达距离"保守判定，避免重叠

**用户故事：** 作为一名玩家，我希望 AI 角色在追到敌人时能停在一个合理的距离外，而不是与敌人重叠在一起，这样战斗画面才不会抖动。

#### 验收标准

1. WHEN AI 控制的角色追向目标 THEN `BTMoveToTarget` SHALL 使用"到目标中心的直线距离"与"自身最大攻击触达距离（`GetMaxAttackDistance`）"进行停止判定，而不是依赖带朝向翻转的 `IsTargetInAttackRange`。
2. WHEN 当前到目标的距离已经小于等于自身最大攻击触达距离 THEN 移动节点 SHALL 立即停止移动、播放 Idle、朝向目标，并返回 Success。
3. WHEN 两个角色水平相对位置差的绝对值小于一个可配置的最小安全间距（默认 0.4，且不小于 `MIN_STOPPING_DISTANCE`） THEN 移动节点 SHALL 停止前进，即使目标仍未进入攻击范围（避免穿越）。
4. WHEN 角色由于目标位于身后（攻击范围带 offset 导致暂时命中失败）而重新进入 Move 阶段 THEN 重新向目标行进的距离计算 SHALL 使用相同的"到目标中心距离"标准，不得出现 Move → Attack → Move 的瞬时抖动。

### 需求 2：BTAttack 对"暂时飞出攻击范围"具备容差，不立即退出战斗

**用户故事：** 作为一名玩家，我希望战斗中即使发生短暂抖动（朝向翻转、目标微位移），攻击状态也不要立刻被打断，让战斗更稳定。

#### 验收标准

1. WHEN `BTAttack` 检测到目标当前不在多形状攻击范围内 THEN 系统 SHALL 额外退化检查：若到目标直线距离 ≤ `GetMaxAttackDistance + 容差（默认 0.2）`，则保持战斗状态，继续走 Attack 分支并在必要时先 `FaceTowards(target)` 后再检测。
2. WHEN 目标已死亡或不存在 THEN `BTAttack` SHALL 立即退出战斗状态并返回 Failure（保留现有行为）。
3. WHEN 目标确实在最大攻击触达距离 + 容差之外 THEN `BTAttack` SHALL 退出战斗状态、返回 Failure（回到追击），不得错误地锁住战斗。
4. IF 在进入 Attack 之前，目标位于身后（即 `target.x` 与 `owner.FacingDirection` 不同侧） THEN 系统 SHALL 先调用 `FaceTowards(target)` 翻正朝向，再进行范围检测，以消除因朝向偏移导致的假失败。

### 需求 3：战斗站定后稳定朝向，避免每帧翻转

**用户故事：** 作为一名玩家，我希望角色进入攻击距离后面对着敌人稳稳站定，而不是每帧都在翻转朝向。

#### 验收标准

1. WHEN `BTMoveToTarget` 返回 Success（已在攻击范围内或已到达安全间距） THEN 系统 SHALL 调用一次 `FaceTowards(target)`，此后在未离开攻击距离之前不得再按移动方向覆盖朝向。
2. WHEN `context.IsInCombat == true` THEN `BTMoveToTarget` SHALL 直接 Success 并且不得再调用 `SetFacingDirection(direction)` 覆盖战斗中的朝向。
3. WHEN 角色处于 Attack 动画中（`CombatSystem.IsAttacking == true` 或 `CharAnimator.IsInHitState == true`） THEN 行为树 SHALL 不得改变朝向和位置。

### 需求 4：两个实体之间具备基本的水平"软推开"，防止贴脸重叠

**用户故事：** 作为一名玩家，我希望当两个角色贴到一起时不会互相穿透，视觉上能分辨两人位置。

#### 验收标准

1. WHEN 两个水平相对位置差的绝对值小于最小安全间距 THEN 移动节点 SHALL 不得再向目标方向继续移动（仅在当前帧不推进 x，不需要真实的物理回弹）。
2. IF 对方在自己正前方且已经非常接近（距离 < 安全间距） THEN 当前角色 SHALL 保持原地并播放 Idle，将攻击时机交给 `BTAttack` 处理。
3. 改动 SHALL 只修改 `Assets/Scripts/AI/Nodes` 下的行为树节点，不引入新的 Rigidbody/Collider 组件，不影响非战斗场景。

### 需求 5：可配置参数与兼容性

**用户故事：** 作为一名开发者，我希望相关阈值可调，且不破坏既有系统（多形状攻击范围、朝向镜像、Hit 保护期等）。

#### 验收标准

1. 最小安全间距、攻击范围容差 SHALL 以常量或 `BTContext` 字段形式暴露，方便后续策划调参（不要求一定放入 Inspector，常量即可）。
2. WHEN 角色 `CharacterData.attackRangeShapes` 为空（走 fallback 圆形）时 THEN 新逻辑 SHALL 仍然可用，停止距离退化为 `attackRange`。
3. 改动 SHALL 不影响 `CharacterAnimator.IsInHitState` 保护期的行为。
4. 改动 SHALL 不影响手动控制（`ManualController`）下的攻击流程。
