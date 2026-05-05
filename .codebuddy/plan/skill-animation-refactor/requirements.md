# 需求文档：技能动画触发机制重构（v2）

## 引言

当前技能动画触发机制存在问题：对于多技能角色（如 Boss），直接使用 `Skills` Int 参数通过 AnyState 转换触发技能动画，会导致当 Int 值等于 1 或 2 时角色反复进入同一状态（AnyState 的 `CanTransitionToSelf` 问题）。

本次重构采用**统一 Trigger + BlendTree** 方案：
- **所有角色**统一使用 `Skill` Trigger 参数进入技能状态
- **单技能角色**：`Skill` Trigger 直接触发唯一的技能动画状态
- **多技能角色**：`Skill` Trigger 进入一个技能 BlendTree，进入 BlendTree 后再由 `Skills` Int 参数的值决定具体播放哪个技能动画

这样避免了 Int 参数直接参与 AnyState 转换条件导致的循环触发问题。

## 需求

### 需求 1：撤销当前实现

**用户故事：** 作为开发者，我想要撤销当前 `CharacterAnimator.cs` 中基于 `useSkillsInt` 的分支逻辑（直接用 `SetInteger(HashSkills)` 触发 AnyState 转换），以便采用新的统一 Trigger + BlendTree 方案。

#### 验收标准

1. WHEN `CharacterAnimator.cs` 被修改 THEN 系统 SHALL 移除 `useSkillsInt` 分支中直接通过 `SetInteger(HashSkills, skillIndex+1)` 触发动画的逻辑
2. WHEN 撤销完成 THEN `PlaySkill()` SHALL 统一使用 `SetTrigger(HashSkill)` 作为进入技能状态的方式

### 需求 2：统一使用 Skill Trigger 进入技能状态

**用户故事：** 作为开发者，我想要所有角色（无论单技能还是多技能）都通过 `Skill` Trigger 参数进入技能动画状态，以便动画控制器的 AnyState 转换逻辑统一且不会循环触发。

#### 验收标准

1. WHEN 角色释放技能 THEN 系统 SHALL 始终使用 `SetTrigger(HashSkill)` 来触发技能动画转换
2. WHEN `CharacterData.skills` 数组长度为 0 THEN 系统 SHALL 不触发任何技能动画（回退到普通攻击）
3. WHEN `CharacterData.skills` 数组长度等于 1 THEN 系统 SHALL 仅使用 `Skill` Trigger 触发技能动画（无需设置 `Skills` Int）
4. WHEN `CharacterData.skills` 数组长度大于 1 THEN 系统 SHALL 先设置 `Skills` Int 参数为对应技能索引值，然后再触发 `Skill` Trigger

### 需求 3：多技能角色的 BlendTree 选择逻辑

**用户故事：** 作为开发者，我想要多技能角色在进入技能 BlendTree 后，通过 `Skills` Int 参数的值来选择具体播放哪个技能动画，以便每个技能都能正确播放对应的动画片段。

#### 验收标准

1. WHEN 多技能角色释放技能 THEN 系统 SHALL 在触发 `Skill` Trigger 之前先将 `Skills` Int 参数设置为对应的整数值（第1个技能=1，第2个技能=2，以此类推）
2. WHEN `Skill` Trigger 触发后进入 BlendTree THEN Animator SHALL 根据 `Skills` Int 的当前值播放对应的技能动画片段
3. WHEN 技能动画播放完成（通过 `OnSkillEnd` 帧事件） THEN 系统 SHALL 将 `Skills` Int 参数归零
4. WHEN `Skills` 归零后 THEN 系统 SHALL 继续检查下一个技能的 CD 状态

### 需求 4：技能 CD 遍历与优先级

**用户故事：** 作为开发者，我想要系统遍历 `CharacterData.skills` 数组中每个 `SkillData` 的 `cooldown` 字段来判断技能是否可用，以便按顺序使用已冷却完毕的技能。

#### 验收标准

1. WHEN 进入攻击判定阶段 THEN 系统 SHALL 遍历 `CharacterData.skills` 数组中的每一个 `SkillData`
2. IF 某个技能的冷却时间已到（CD 好了） THEN 系统 SHALL 优先使用该技能（按数组索引顺序优先）
3. IF 没有任何技能 CD 好了 THEN 系统 SHALL 执行普通攻击
4. WHEN 技能使用后 THEN 系统 SHALL 立即开始该技能的冷却计时

### 需求 5：动画只播放一次的保证

**用户故事：** 作为开发者，我想要技能动画只播放一次就回到待机状态，并通过专门的帧事件来控制状态归零，以便精确控制技能动画的结束时机，避免循环播放。

#### 验收标准

1. WHEN 技能动画开始播放 THEN 系统 SHALL 确保动画只播放一次（非循环）
2. WHEN 技能动画中插入的专用帧事件 `OnSkillEnd` 被触发 THEN 系统 SHALL 将 `Skills` Int 参数归零（对于多技能角色），该帧事件独立于命中帧事件 `OnSkillHit`
3. WHEN `OnSkillEnd` 触发 THEN 系统 SHALL 同时清除技能动画保护状态（isInSkillState），允许角色转换到其他动画状态
4. IF `OnSkillEnd` 帧事件未触发（异常情况） THEN 系统 SHALL 通过超时机制（skillStateTimer）将 `Skills` 归零并清除保护状态作为安全保障
5. WHEN 使用 Trigger 方式触发 THEN Trigger 在消费后自动重置 SHALL 不会导致 AnyState 重复转换

## 技术约束

- 动画控制器约定：
  - **所有角色**：AnyState → 技能状态的转换条件为 `Skill` Trigger
  - **单技能角色**：`Skill` Trigger 直接转换到单个技能动画状态
  - **多技能角色**：`Skill` Trigger 转换到一个 BlendTree 节点，BlendTree 内部根据 `Skills` Int 参数选择具体动画片段（1=SkillOne, 2=SkillTwo, ...）
- `Skills` Int 参数不参与 AnyState 转换条件，仅在 BlendTree 内部使用
- `CombatSystem.GetFirstReadySkillIndex()` 已有遍历技能 CD 的逻辑，可以复用
- `CharacterAnimator` 需要知道技能数量以决定是否设置 `Skills` Int

## 边界情况

1. 角色没有配置 `CharacterData`（skills 为 null）→ 视为无技能，回退到普通攻击
2. 动画控制器中缺少 `Skill` Trigger 参数 → 回退到普通攻击
3. 动画控制器中缺少 `Skills` Int 参数但角色有多个技能 → 仅触发 `Skill` Trigger（BlendTree 可能使用默认值）
4. 技能动画播放中角色被击杀 → 清除所有状态（Skills 归零、保护状态清除）
5. `OnSkillEnd` 帧事件未配置 → 超时机制兜底归零
