# 需求文档：Animator Controller 连线改为 Entry/Exit 模式

## 引言

当前角色动画状态机的 Animator Controller 使用 AnyState 出发的转换线来切换动画状态。现在需要改为 **Entry/Exit 模式**：

- **Entry** → 各状态节点（左侧连线），默认进入 Idle
- 各状态节点 → **Exit**（右侧连线），当对应 Bool == false 时退出

这种模式下，Animator 的状态流转为：
1. 从 Entry 进入，根据当前哪个 Bool 为 true 决定进入哪个状态
2. 如果没有任何 Bool 为 true，走默认路径进入 Idle
3. 当状态的 Bool 变为 false 时，状态退出到 Exit
4. 退出到 Exit 后，Animator 重新从 Entry 评估，进入下一个 Bool 为 true 的状态

**Animator Controller 连线结构（参考截图）：**

![Animator Controller](https://zhiyan-ai-agent-with-1258344702.cos.ap-guangzhou.tencentcos.cn/copilot/86ec62fa-a9fa-4e00-a41b-049c0e61e4c8/image-019dfd8e9a52766cb8f130e416dcf486.png)

**参数列表（全部为 Bool 类型）：**
- Walk
- Idle
- Attack
- SkillOne（多技能角色可有 SkillTwo/SkillThree/SkillFour）
- Hit
- Death

## 需求

### 需求 1：状态切换时确保 Bool 参数的正确时序

**用户故事：** 作为一名开发者，我希望代码在切换动画状态时，先将当前状态的 Bool 设为 false（触发退出到 Exit），再将目标状态的 Bool 设为 true（从 Entry 进入新状态），以便 Animator Controller 的 Entry/Exit 连线能正确工作。

#### 验收标准

1. WHEN 状态机执行状态切换 THEN 系统 SHALL 先调用当前状态的 OnExit()（将对应 Bool 设为 false），再调用新状态的 OnEnter()（将对应 Bool 设为 true）
2. WHEN CharacterAnimator 调用 PlayIdle/PlayWalk/PlayAttack/PlaySkill/PlayHit/PlayDeath THEN 系统 SHALL 在状态切换前调用 ResetAllBools() 确保所有 Bool 为 false
3. WHEN 所有 Bool 参数均为 false THEN Animator Controller SHALL 从 Entry 走默认路径进入 Idle 状态

### 需求 2：移除 CharacterAnimator 中多余的 ResetAllBools 调用

**用户故事：** 作为一名开发者，我希望避免在状态切换过程中重复设置 Bool 为 false，以便减少不必要的 Animator 参数操作，防止在同一帧内产生闪烁或意外的状态跳转。

#### 验收标准

1. WHEN 状态机的 OnExit() 已经将当前状态的 Bool 设为 false THEN CharacterAnimator 中的 ResetAllBools() SHALL 不再需要在每次 Play 方法中调用（因为 OnExit 已经处理了当前状态的 Bool 清除）
2. IF 需要强制清除所有 Bool（如 Reset、Death 等场景） THEN 系统 SHALL 保留 ResetAllBools() 方法供这些特殊场景使用
3. WHEN 正常状态切换（非强制） THEN 系统 SHALL 依赖 OnExit() 清除当前 Bool + OnEnter() 设置新 Bool 的自然流程

### 需求 3：Idle 作为默认回退状态

**用户故事：** 作为一名开发者，我希望当没有任何状态 Bool 为 true 时，Animator 自动回到 Idle 状态，以便角色始终有一个安全的默认动画。

#### 验收标准

1. WHEN Entry 节点评估所有转换条件且没有任何 Bool 为 true THEN Animator Controller SHALL 走默认路径（无条件）进入 Idle 状态
2. WHEN 角色初始化完成 THEN 状态机 SHALL 将 Idle Bool 设为 true，使 Animator 从 Entry 进入 Idle
3. WHEN 受保护状态（Attack/Skill/Hit）的动画播放完毕且 ClearProtection 被调用 THEN 系统 SHALL 自动切换回 Idle 状态

### 需求 4：Entry 到各状态的转换条件

**用户故事：** 作为一名开发者，我希望 Entry 到各状态的转换条件清晰明确，以便 Animator Controller 能正确路由到目标状态。

#### 验收标准

1. WHEN Entry 评估转换 AND Walk Bool == true THEN Animator SHALL 进入 Walk 状态
2. WHEN Entry 评估转换 AND Attack Bool == true THEN Animator SHALL 进入 Attack 状态
3. WHEN Entry 评估转换 AND Hit Bool == true THEN Animator SHALL 进入 Hit 状态
4. WHEN Entry 评估转换 AND SkillOne/SkillTwo/SkillThree/SkillFour Bool == true THEN Animator SHALL 进入对应的 Skill 状态
5. WHEN Entry 评估转换 AND Death Bool == true THEN Animator SHALL 进入 Death 状态
6. WHEN Entry 评估转换 AND 没有任何 Bool 为 true（或仅 Idle == true） THEN Animator SHALL 进入 Idle 状态（默认路径）

### 需求 5：各状态到 Exit 的退出条件

**用户故事：** 作为一名开发者，我希望每个状态在其对应 Bool 变为 false 时能正确退出到 Exit，以便 Animator 能重新从 Entry 评估下一个状态。

#### 验收标准

1. WHEN Idle 状态中 Idle Bool 变为 false THEN Animator SHALL 从 Idle 转换到 Exit
2. WHEN Walk 状态中 Walk Bool 变为 false THEN Animator SHALL 从 Walk 转换到 Exit
3. WHEN Attack 状态中 Attack Bool 变为 false THEN Animator SHALL 从 Attack 转换到 Exit
4. WHEN Hit 状态中 Hit Bool 变为 false THEN Animator SHALL 从 Hit 转换到 Exit
5. WHEN Skill 状态中对应 Skill Bool 变为 false THEN Animator SHALL 从 Skill 转换到 Exit
6. WHEN Death 状态中 Death Bool 变为 false THEN Animator SHALL 从 Death 转换到 Exit（仅在 Reset 时）

### 需求 6：代码适配 - 去除对 AnyState 的依赖

**用户故事：** 作为一名开发者，我希望代码中不再有任何依赖 AnyState 行为的逻辑（如防止 AnyState 自我转换的检查），以便代码与新的 Entry/Exit 连线方式完全匹配。

#### 验收标准

1. WHEN PlayIdle() 被调用且已在 Idle 状态 THEN 系统 SHALL 跳过切换（保留此优化，但注释更新为 Entry/Exit 模式的原因）
2. WHEN PlayWalk() 被调用且已在 Walk 状态 THEN 系统 SHALL 跳过切换（同上）
3. WHEN 代码注释中提到 "AnyState" THEN 系统 SHALL 更新注释为 Entry/Exit 模式的描述

### 需求 7：HitState 重入支持

**用户故事：** 作为一名开发者，我希望在 Entry/Exit 模式下，Hit 状态仍然支持重入（被打断时重新播放受击动画），以便角色连续受击时动画表现正确。

#### 验收标准

1. WHEN 角色已在 Hit 状态 AND 再次受到攻击 THEN 系统 SHALL 先将 Hit Bool 设为 false（退出到 Exit），再设为 true（重新从 Entry 进入 Hit），实现动画重播
2. IF Hit 状态的 canBeInterrupted 为 false（保护期内） THEN 系统 SHALL 仍然允许 Hit 重入（Hit 可以打断 Hit）
