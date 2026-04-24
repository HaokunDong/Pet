# 需求文档：可扩展技能效果框架 + 投掷型技能

## 引言

本功能为项目引入一套**可扩展的技能效果框架**，使得不同角色可以配置不同类型的技能效果。

### 普通攻击与技能的边界

项目中角色的战斗行为分为两个独立的概念：

- **普通攻击**：保持现有逻辑不变，由 `CombatSystem.TryNormalAttack()` → 帧事件 `OnAttackHit()` → `ApplyNormalAttackDamage()` 流程驱动，使用 `CharacterData` 中的 `attackPower` 和 `attackRangeShapes` 进行伤害计算。**本次不做任何修改。**
- **技能**：由 `CombatSystem.TryUseSkill()` → 帧事件 `OnSkillHit()` → `ApplySkillDamage()` 流程驱动。本次引入 `SkillEffectData` 框架，使技能效果可扩展、可配置。

### 技能效果框架设计

框架采用 **ScriptableObject 多态 + 策略模式**，每种技能效果类型（投掷抛物线、未来的其他类型）作为独立的 ScriptableObject 子类。`SkillData` 通过引用一个 `SkillEffectData` 基类来决定技能的具体表现。`CombatSystem` 在技能帧事件触发时，将执行逻辑委托给对应的效果数据对象，实现**开闭原则**——新增技能类型只需新增一个 ScriptableObject 子类，无需修改已有代码。

作为框架的第一个实现实例，本次实现**投掷型技能（Projectile）**：角色释放技能时播放动画，帧事件触发时生成投射物小球，小球沿抛物线飞向敌人（预判位移），落地后播放爆炸动画并造成范围伤害。

### 现有系统概述

- **SkillData**：已有 `damage`、`cooldown`、`skillRange`、`effectPrefab`、`icon` 等字段。其中 `cooldown` 字段用于配置每个技能的冷却时间（秒），已支持在 Inspector 中为每个技能独立设置不同的 CD 值
- **CharacterData**：已有 `skills: SkillData[]` 数组，品质等级限制技能数量（A=1, S=2, SS=3, SSS=4）
- **CombatSystem**：已有 `TryNormalAttack()` / `TryUseSkill()` 两套独立流程；`TryUseSkill()` 在释放技能时立即调用 `StartSkillCooldown(skillIndex, skillData.cooldown)` 开始冷却计时；`ApplySkillDamage()` 当前直接对缓存目标造成伤害并生成 effectPrefab
- **RuntimeCharacterStats**：已有 `skillCooldowns` 字典、`IsSkillReady()`、`StartSkillCooldown()`、`UpdateCooldowns()` 等完整的冷却管理机制，每帧由 `CharacterEntity.Update()` 驱动递减
- **AnimEventReceiver**：已有 `OnAttackHit()`（普通攻击）和 `OnSkillHit()`（技能）两个帧事件回调
- **对象池 PoolMgr**：已有通用对象池管理器
- **AI 行为树**：`BTCombat.TickStrike()` 已支持优先使用技能——Player/Boss 类型在 Strike 阶段每帧调用 `GetFirstReadySkillIndex()` 查找冷却完毕的技能，找到则自动调用 `TryUseSkill()` 释放，未找到则回退到普通攻击。**这意味着挂机时角色会在技能 CD 结束后自动释放技能，无需额外开发**

---

## 需求

### 需求 1：SkillEffectData 基类 — 可扩展的技能效果数据框架

**用户故事：** 作为一名开发者，我希望有一个可扩展的技能效果数据框架，以便未来为不同角色添加不同类型的技能效果时，只需新增 ScriptableObject 子类，无需修改已有的战斗系统代码。

#### 验收标准

1. WHEN 系统初始化时 THEN 系统 SHALL 提供一个抽象基类 `SkillEffectData`（继承自 ScriptableObject），定义以下抽象接口：
   - `Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)`：在技能帧事件触发时被调用，负责执行技能的具体效果逻辑
2. WHEN 开发者需要新增一种技能效果类型时 THEN 开发者 SHALL 只需创建一个继承自 `SkillEffectData` 的新子类，实现 `Execute` 方法即可，无需修改 `CombatSystem`、`AnimEventReceiver` 或其他已有代码
3. WHEN 在 Unity Inspector 中编辑 `SkillData` 时 THEN 系统 SHALL 提供一个 `SkillEffectData` 类型的引用字段（`skillEffect`），用户可以拖拽任意 `SkillEffectData` 子类的 ScriptableObject 实例到该字段上
4. WHEN `SkillData` 的 `skillEffect` 字段不为空时 THEN 系统 SHALL 在帧事件触发时调用 `skillEffect.Execute()` 来执行技能效果
5. IF `SkillData` 的 `skillEffect` 字段为空（null） THEN 系统 SHALL 在 Editor 中发出警告日志，提示开发者需要为该技能配置效果数据

### 需求 2：ProjectileSkillEffectData — 投掷型技能效果

**用户故事：** 作为一名游戏设计师，我希望能配置一种投掷型技能效果，使角色在技能帧事件触发时投掷一个小球，小球沿抛物线飞向敌人并在落地后爆炸造成范围伤害，以便为角色创造丰富的技能表现。

#### 验收标准

1. WHEN 在 Unity Inspector 中创建 `ProjectileSkillEffectData` 资产时 THEN 系统 SHALL 通过 `CreateAssetMenu` 提供便捷的创建入口（菜单路径：`Game/SkillEffect/Projectile`）
2. WHEN 编辑 `ProjectileSkillEffectData` 时 THEN 系统 SHALL 提供以下可配置字段：
   - 投射物精灵（`projectileSprite`）：小球的 Sprite 图片，由用户装配
   - 爆炸动画控制器（`explosionAnimatorController`）：爆炸效果的 RuntimeAnimatorController，由用户装配精灵图序列帧动画
   - 投射物飞行时间（`projectileFlightDuration`）：小球从发射到落地的总时间（秒），默认 0.6s
   - 抛物线高度（`projectileArcHeight`）：抛物线最高点相对于起点和终点连线的高度，默认 1.5
   - 爆炸伤害范围半径（`explosionRadius`）：落地爆炸后造成伤害的圆形范围半径
   - 投射物大小（`projectileScale`）：投射物的缩放比例，默认 1.0
3. WHEN `ProjectileSkillEffectData.Execute()` 被调用时 THEN 系统 SHALL 在施法者位置生成投射物 GameObject，使用配置的 `projectileSprite` 作为显示图片，并启动抛物线飞行协程

### 需求 3：投射物抛物线飞行与敌人位移预判

**用户故事：** 作为一名玩家，我希望投掷的小球能沿抛物线飞向敌人，并且能准确命中正在移动的敌人，以便获得流畅且精准的技能释放体验。

#### 验收标准

1. WHEN 投射物生成时 THEN 系统 SHALL 获取目标敌人当前的移动速度和方向，使用公式 `预测位置 = 敌人当前位置 + 敌人速度向量 × 飞行时间` 计算预测落点
2. WHEN 投射物飞行中 THEN 系统 SHALL 使投射物沿抛物线轨迹从起点飞向预测落点：
   - 水平方向：从起点线性插值到终点
   - 垂直方向：在水平插值基础上叠加抛物线弧度（由 `projectileArcHeight` 控制），公式为 `height = arcHeight × 4 × t × (1 - t)`，其中 t 为归一化飞行进度
3. WHEN 投射物飞行中 THEN 系统 SHALL 使投射物的旋转朝向其运动方向（切线方向），使视觉效果自然
4. WHEN 投射物到达落点 THEN 系统 SHALL 回收投射物 GameObject 到对象池
5. IF 敌人在投射物飞行期间已死亡或被回收 THEN 系统 SHALL 让投射物继续飞向原预测落点（不中途消失），到达后仍然播放爆炸动画并造成范围伤害

### 需求 4：爆炸动画与范围伤害

**用户故事：** 作为一名玩家，我希望小球落地后播放爆炸动画并对范围内的敌人造成伤害，以便感受到技能的冲击力。

#### 验收标准

1. WHEN 投射物到达落点时 THEN 系统 SHALL 在落点位置生成爆炸效果 GameObject，使用 `ProjectileSkillEffectData` 中配置的 `explosionAnimatorController` 播放爆炸动画
2. WHEN 爆炸动画开始播放时 THEN 系统 SHALL 对落点周围 `explosionRadius` 范围内的所有敌方角色造成 `SkillData.damage` 的伤害
3. WHEN 范围伤害判定时 THEN 系统 SHALL 根据施法者的 `characterType` 确定敌方标签（Player 攻击 Enemy，Enemy/Boss 攻击 Player），只对敌方造成伤害
4. WHEN 爆炸动画播放完毕后 THEN 系统 SHALL 将爆炸效果 GameObject 归还到对象池
5. IF 爆炸范围内没有任何敌方角色 THEN 系统 SHALL 仍然播放爆炸动画（视觉反馈），但不造成伤害

### 需求 5：CombatSystem 集成 — 技能效果委托与冷却自动释放

**用户故事：** 作为一名开发者，我希望 CombatSystem 的技能伤害逻辑能自动委托给 SkillEffectData 的具体实现，同时保持普通攻击逻辑完全不变，并且挂机时角色能在技能冷却结束后自动释放技能，以便新增技能类型时无需修改战斗系统核心代码。

#### 验收标准

1. WHEN `CombatSystem.ApplySkillDamage()` 被帧事件触发时 AND 当前 `SkillData.skillEffect` 不为空 THEN 系统 SHALL 调用 `skillData.skillEffect.Execute(combatSystem, cachedTarget, skillData)` 来执行技能效果
2. IF `SkillData.skillEffect` 为空 THEN 系统 SHALL 输出警告日志并跳过技能效果执行，然后清除攻击状态
3. WHEN 普通攻击流程（`TryNormalAttack` → `OnAttackHit` → `ApplyNormalAttackDamage`）执行时 THEN 系统 SHALL 保持现有逻辑完全不变，不受技能效果框架的任何影响
4. WHEN 技能效果的 `Execute` 被调用后 THEN 系统 SHALL 立即清除攻击状态（`ClearAttackState`），不等待投射物飞行完毕，以便角色可以继续行动
5. WHEN AI 行为树（BTCombat）选择使用技能时 THEN 系统 SHALL 无需任何修改即可正常触发各种类型的技能（通过现有的 `TryUseSkill` 流程）
6. WHEN 技能的冷却计时 THEN 系统 SHALL 保持现有的冷却机制不变——`TryUseSkill()` 在释放技能时立即调用 `StartSkillCooldown(skillIndex, skillData.cooldown)` 开始冷却，`CharacterEntity.Update()` 每帧驱动 `RuntimeCharacterStats.UpdateCooldowns()` 递减冷却时间
7. WHEN 角色处于挂机状态（AI 行为树驱动）且技能冷却时间归零时 THEN 系统 SHALL 通过现有的 `BTCombat.TickStrike()` → `GetFirstReadySkillIndex()` → `TryUseSkill()` 流程自动释放该技能，无需额外开发
8. WHEN 在 `SkillData` 的 Inspector 中编辑 `cooldown` 字段时 THEN 系统 SHALL 允许为每个技能独立配置不同的冷却时间（单位：秒），该值直接决定挂机时技能的自动释放频率

### 需求 6：对象池复用

**用户故事：** 作为一名开发者，我希望投射物和爆炸效果使用对象池管理，以便减少频繁的 Instantiate/Destroy 带来的性能开销。

#### 验收标准

1. WHEN 需要生成投射物时 THEN 系统 SHALL 优先从 PoolMgr 对象池中获取，池中无可用对象时再实例化新对象
2. WHEN 投射物到达落点后 THEN 系统 SHALL 将投射物归还到对象池（而非 Destroy）
3. WHEN 需要生成爆炸效果时 THEN 系统 SHALL 优先从 PoolMgr 对象池中获取
4. WHEN 爆炸动画播放完毕后 THEN 系统 SHALL 将爆炸效果归还到对象池

### 需求 7：配置工作流 — 为角色装配不同技能

**用户故事：** 作为一名游戏设计师，我希望能通过 Unity Inspector 为每个角色自由装配不同类型的技能，并为每个技能独立配置冷却时间，以便快速迭代角色设计并控制挂机时的技能释放节奏。

#### 验收标准

1. WHEN 为角色配置技能时 THEN 用户 SHALL 按以下工作流操作：
   - 步骤 1：创建一个 `SkillEffectData` 子类资产（如 `ProjectileSkillEffectData`），配置效果参数（投射物图片、爆炸动画、飞行时间等）
   - 步骤 2：创建一个 `SkillData` 资产，配置基础参数（伤害、**冷却时间 CD**、范围等），并将步骤 1 的效果资产拖拽到 `skillEffect` 字段
   - 步骤 3：在角色的 `CharacterData` 中，将步骤 2 的 `SkillData` 资产添加到 `skills` 数组中
2. WHEN 同一个 `SkillEffectData` 资产被多个 `SkillData` 引用时 THEN 系统 SHALL 正常工作（效果数据是无状态的，可共享）
3. WHEN 同一个角色的 `skills` 数组中包含不同类型的技能效果时 THEN 系统 SHALL 正确地为每个技能调用对应的效果逻辑（例如：技能 0 是投掷型，技能 1 是未来新增的其他类型）
4. WHEN 角色的品质等级限制了技能数量时 THEN 系统 SHALL 保持现有的品质等级校验逻辑不变（A=1, S=2, SS=3, SSS=4）
5. WHEN 用户为不同技能配置不同的 `cooldown` 值时 THEN 系统 SHALL 在挂机时按照各自的冷却时间独立计时，冷却结束后自动释放对应技能
