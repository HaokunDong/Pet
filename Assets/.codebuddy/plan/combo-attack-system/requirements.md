# 需求文档：多段普攻衔接系统（Combo Attack）

## 引言

本功能为手动操控模式下的角色实现多段普攻衔接机制。采用 **方案一：CombatSystem 层级的 Combo 计数器** 方案，在 `CombatSystem` 中维护 combo 状态，通过 Animator 的 `AttackIndex` Int 参数区分不同段攻击动画，并通过动画帧事件 `OnComboWindowOpen` 标记可衔接窗口。

### 核心设计思路

- `CombatSystem` 新增 combo 计数器（`_comboStep`）和衔接窗口标志（`_comboWindowOpen`）
- 动画中新增帧事件 `OnComboWindowOpen`，在攻击动画快结束前触发，标记"可接受下一段输入"
- `OnStateEnd` 时检查：如果有缓冲输入 → 直接播放下一段；没有 → 回到 Idle 并重置 combo
- Animator Controller 中用 `AttackIndex` Int 参数 + 多个 Attack 状态（Attack1, Attack2...）
- `ManualController` 中检测攻击输入时，如果在 combo 窗口内则标记输入缓冲

### 现有架构概述

当前普攻流程：
1. `ManualController` 检测右键点击敌人 → 进入 `isAttacking` 持续攻击模式
2. `HandleSustainedAttack()` 每帧调用 `combatSystem.TryNormalAttack(target)`
3. `CombatSystem.TryNormalAttack()` → 锁定朝向 → `CharAnimator.PlayAttack()` → 进入 `AttackState`
4. `AttackState` 设置 `canBeInterrupted = false`，播放攻击动画
5. 动画帧事件 `OnAttackHit` → 造成伤害
6. 动画帧事件 `OnStateEnd` → `ClearCurrentState()` → 解锁朝向 → 回到 `IdleState`
7. 下一帧 `HandleSustainedAttack()` 再次尝试攻击

---

## 需求

### 需求 1：CombatSystem Combo 状态管理

**用户故事：** 作为一名开发者，我希望 CombatSystem 能够管理多段普攻的 combo 状态（当前段数、衔接窗口、输入缓冲），以便在一个集中的位置控制连击逻辑，同时支持手动和 AI 模式复用。

#### 验收标准

1. WHEN CombatSystem 发起普攻 THEN CombatSystem SHALL 记录当前 combo 段数（从 0 开始），并将该段数传递给 Animator
2. WHEN combo 窗口打开（`OnComboWindowOpen` 帧事件触发）THEN CombatSystem SHALL 将 `_comboWindowOpen` 标志设为 true
3. WHEN 在 combo 窗口内收到下一段攻击请求 AND 当前段数未达到最大段数 THEN CombatSystem SHALL 将 `_comboInputBuffered` 设为 true
4. WHEN `OnStateEnd` 触发 AND `_comboInputBuffered` 为 true THEN CombatSystem SHALL 立即发起下一段攻击（combo 段数 +1），而不回到 Idle
5. WHEN `OnStateEnd` 触发 AND `_comboInputBuffered` 为 false THEN CombatSystem SHALL 重置 combo 段数为 0，回到 Idle
6. WHEN combo 段数达到最大值（最后一段攻击结束）THEN CombatSystem SHALL 重置 combo 段数为 0，回到 Idle，下次攻击从第 1 段开始
7. WHEN 角色被打断（受击/死亡）THEN CombatSystem SHALL 立即重置 combo 状态（段数归 0、窗口关闭、缓冲清除）

### 需求 2：动画帧事件 OnComboWindowOpen

**用户故事：** 作为一名开发者，我希望在攻击动画快结束前有一个"衔接窗口"帧事件，以便系统知道何时可以接受下一段攻击输入。

#### 验收标准

1. WHEN 攻击动画播放到衔接窗口帧（`OnComboWindowOpen` 事件）THEN AnimEventReceiver SHALL 通知 CombatSystem 打开 combo 窗口
2. IF 攻击动画中未配置 `OnComboWindowOpen` 事件 THEN 系统 SHALL 仍然正常工作（等同于没有衔接窗口，`OnStateEnd` 时直接回 Idle）
3. WHEN 新的攻击段开始播放 THEN CombatSystem SHALL 自动关闭上一段的 combo 窗口标志

### 需求 3：Animator Controller 多段攻击状态

**用户故事：** 作为一名开发者，我希望 Animator Controller 支持通过 `AttackIndex` Int 参数区分多段攻击动画，以便不同段的攻击播放不同的动画。

#### 验收标准

1. WHEN AttackState 进入 THEN Animator SHALL 根据 `AttackIndex` 参数值播放对应段数的攻击动画（0 = Attack1, 1 = Attack2, ...）
2. WHEN 角色只有一段攻击动画（未配置多段）THEN 系统 SHALL 保持向后兼容，行为与当前单段攻击一致
3. IF CharacterData 中配置了 `comboCount`（连击段数）THEN CombatSystem SHALL 使用该值作为最大 combo 段数上限

### 需求 4：ManualController 输入缓冲与攻击保护

**用户故事：** 作为一名玩家，我希望在手动操控模式下，普通攻击的操作体验类似英雄联盟——攻击动画播放期间右键点击敌人不会中断当前攻击，只有在动画播放完毕或连击窗口期内才识别下一次攻击输入，以便获得流畅且不被误操作打断的连击体验。

#### 验收标准

1. WHEN 玩家处于持续攻击模式（`isAttacking = true`）AND combo 窗口已打开 THEN ManualController SHALL 通知 CombatSystem 缓冲下一段攻击输入
2. WHEN 玩家在持续攻击模式下 AND combo 窗口未打开（攻击动画仍在保护期内）THEN ManualController SHALL 不发起新攻击（由 `IsFacingLocked` 阻止）
3. WHEN 玩家在攻击动画播放期间右键点击同一个或不同的敌人 THEN ManualController SHALL 不中断当前攻击动画，而是将该输入视为"下一次攻击意图"，等待当前段动画结束或 combo 窗口打开后再处理
4. WHEN 玩家在攻击动画播放期间右键点击敌人 AND combo 窗口已打开 THEN ManualController SHALL 将该点击视为 combo 缓冲输入，衔接下一段攻击
5. WHEN 玩家在攻击动画播放期间右键点击敌人 AND combo 窗口未打开 THEN ManualController SHALL 忽略该点击（不中断、不重置 combo），当前攻击正常播放完毕
6. WHEN 玩家在非攻击状态（Idle/Walk）下右键点击敌人 THEN ManualController SHALL 正常发起攻击（从 combo step 0 开始）
7. WHEN 玩家右键点击空地 THEN ManualController SHALL 取消攻击模式（`isAttacking = false`），但不立即重置 combo 状态
8. WHEN 角色因移动指令等原因实际改变状态（如从 AttackState 退出进入 IdleState/WalkState）THEN CombatSystem SHALL 在状态切换时清除 combo 缓冲并重置 combo 状态

### 需求 5：CharacterData 配置扩展

**用户故事：** 作为一名策划/开发者，我希望能在 CharacterData 中配置每个角色的连击段数和每段的伤害倍率，以便灵活调整不同角色的连击表现。

#### 验收标准

1. WHEN CharacterData 中 `comboCount` 字段值为 1 或未设置 THEN 系统 SHALL 表现为单段攻击（向后兼容）
2. IF CharacterData 中配置了 `comboCount > 1` THEN CombatSystem SHALL 支持最多该段数的连击
3. IF CharacterData 中配置了 `comboDamageMultipliers` 数组 THEN 每段攻击的伤害 SHALL 为 `attackPower * comboDamageMultipliers[comboStep]`
4. IF `comboDamageMultipliers` 未配置或长度不足 THEN 系统 SHALL 使用默认倍率 1.0

### 需求 6：AI 模式连击行为

**用户故事：** 作为一名开发者，我希望 AI 模式（行为树）下角色能自动执行完整连击，当敌人持续在攻击范围内时打完全套 combo，当敌人中途离开范围时在当前段结束后回到 Idle。

#### 验收标准

1. WHEN AI 发起普攻 AND 目标在攻击范围内 THEN CombatSystem SHALL 自动在 combo 窗口内缓冲下一段输入（即 AI 默认打完全套连击）
2. WHEN AI 处于 combo 过程中 AND combo 窗口打开时检测到攻击范围内没有存活敌人 THEN CombatSystem SHALL 不缓冲下一段输入，当前段结束后回到 Idle 并重置 combo
3. WHEN AI 处于 combo 过程中 AND 敌人在当前段 `OnStateEnd` 之前重新进入攻击范围 THEN CombatSystem SHALL 正常缓冲下一段输入，继续连击
4. WHEN AI 处于 combo 过程中 AND 敌人在当前段 `OnStateEnd` 之前一直未进入攻击范围 THEN CombatSystem SHALL 在 `OnStateEnd` 时重置 combo 并回到 Idle
5. IF AI 角色的 `comboCount = 1`（单段攻击）THEN 现有行为树逻辑 SHALL 不受影响，行为与当前完全一致

---

## 边界情况与技术约束

1. **攻击位移（Displacement）兼容**：如果角色配置了攻击位移（Fixed/LockOn），每段攻击的位移应独立计算，不应因 combo 而累积异常位移
2. **受击打断**：角色在任何 combo 段被打断时，应立即重置 combo 状态
3. **目标死亡/离开范围**：
   - 手动模式：如果攻击目标在 combo 过程中死亡，应重置 combo 并回到 Idle
   - AI 模式：如果敌人在 combo 过程中死亡或离开攻击范围，当前段正常播完后不衔接下一段，回到 Idle
4. **动画事件顺序**：每段攻击动画中事件顺序必须为：`OnAttackHit`（伤害帧）→ `OnComboWindowOpen`（衔接窗口）→ `OnStateEnd`（结束）
5. **向后兼容**：未配置多段攻击的角色（`comboCount = 1`）行为应与当前完全一致，无需修改现有动画或 Animator Controller
