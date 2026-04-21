# 需求文档

## 引言

当前 AI 战斗系统存在一个严重的视觉 Bug：敌人和玩家在战斗时会走到一起完全重叠，然后在重叠位置疯狂左右翻转。

### 根因分析

该问题由三个缺陷共同导致：

1. **Engage 阶段缺少最小间距保护**：`BTCombat.TickEngage()` 中双方角色都通过 `pos.x += dir * speed * Time.deltaTime` 直接修改位置走向对方，没有任何最小安全距离检查，导致两个角色可以完全重叠在同一位置。

2. **重叠后 FaceTowards 导致每帧翻转**：当两个角色 X 坐标几乎相同时，`CharacterAnimator.FaceTowards()` 中 `targetPosition.x - transform.position.x` 的值在 0 附近因浮点精度而正负跳变，导致 `SetFacingDirection` 每帧在 +1/-1 之间切换，产生疯狂左右翻转。

3. **翻转导致攻击范围检查不稳定**：`CombatSystem.TryNormalAttack()` 使用朝向相关的形状范围检查（`IsTargetInAttackRange`），角色每帧翻转导致攻击范围形状也跟着翻转，攻击时而命中时而不命中，进一步加剧抖动循环。

## 需求

### 需求 1：Engage 阶段最小安全间距

**用户故事：** 作为一名玩家，我希望我的角色和敌人在战斗追击时保持合理的物理间距，以便角色不会穿过敌人或与敌人重叠。

#### 验收标准

1. WHEN 角色处于 Engage 状态向目标移动 THEN `BTCombat.TickEngage()` SHALL 在每帧移动前检查与目标的水平距离，IF 移动后距离将小于 `engageDistance` THEN SHALL 将角色位置钳制到恰好 `engageDistance` 处并立即切换到 Strike 状态，而非继续移动穿过目标。
2. WHEN 角色处于 Engage 状态 AND 与目标的当前距离已经 ≤ `engageDistance` THEN 系统 SHALL 立即进入 Strike 状态而不执行任何移动。

### 需求 2：Strike 状态朝向锁定（防抖动）

**用户故事：** 作为一名玩家，我希望角色在攻击状态下面朝方向稳定不抖动，以便战斗动画看起来自然流畅。

#### 验收标准

1. WHEN 角色处于 Strike 状态 AND 与目标的水平距离 < 一个极小阈值（如 0.05） THEN `BTCombat.TickStrike()` SHALL 跳过 `FaceTowards` 调用，保持当前朝向不变，避免因浮点精度导致的每帧翻转。
2. WHEN 角色处于 Strike 状态 AND 与目标的水平距离 ≥ 该阈值 THEN 系统 SHALL 正常调用 `FaceTowards` 面向目标。

### 需求 3：Engage 移动过冲保护

**用户故事：** 作为一名玩家，我希望角色在追击敌人时不会因为移动步长过大而穿过敌人身体，以便战斗定位始终合理。

#### 验收标准

1. WHEN 角色处于 Engage 状态执行水平移动 THEN 系统 SHALL 在应用位移后检查是否"穿过"了目标（即移动前在目标左侧、移动后到了目标右侧，或反之），IF 发生穿越 THEN SHALL 将角色 X 坐标钳制到目标 X 坐标减去（或加上）`engageDistance` 的位置。
2. WHEN 角色移速极高或帧率极低导致单帧位移 > 与目标的距离 THEN 系统 SHALL 确保角色不会越过目标位置。

### 需求 4：双向对称性

**用户故事：** 作为一名玩家，我希望敌人 AI 和玩家 AI 都遵守相同的间距和防抖规则，以便双方不会因为同时追击而重叠。

#### 验收标准

1. WHEN 玩家角色和敌人角色同时处于 Engage 状态互相追击 THEN 双方 SHALL 各自独立执行最小间距检查，最终双方都会在各自的 `engageDistance` 处停下进入 Strike。
2. WHEN 双方都进入 Strike 状态 THEN 双方 SHALL 各自独立执行朝向防抖逻辑，不会出现同步翻转。

### 需求 5：边界情况处理

**用户故事：** 作为一名开发者，我希望修复方案能正确处理各种边界情况，以便不引入新的 Bug。

#### 验收标准

1. IF `engageDistance` 配置为 0 或极小值 THEN 系统 SHALL 使用一个最小安全间距兜底值（如 0.1），防止角色完全重叠。
2. WHEN 目标在战斗中被击退或位移 THEN 系统 SHALL 在下一帧重新计算距离并正确决定 Engage/Strike 状态，不会因为之前的钳制位置而卡住。
3. WHEN 角色被墙壁阻挡无法到达 `engageDistance` 位置 THEN 系统 SHALL 保持在墙前 Idle，不会尝试穿墙。
