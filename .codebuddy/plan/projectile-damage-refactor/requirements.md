# 需求文档

## 引言

本功能对远程技能（Projectile）的伤害系统进行重构。当前远程技能的伤害范围（`explosionRadius`）由 `ProjectileSkillEffectData` ScriptableObject 控制，投射物落地后以固定圆形范围造成 AOE 伤害。

重构目标是将伤害范围的控制权从 `ProjectileSkillEffectData` 转移到技能预制体（Projectile Prefab）自身上，使每个投射物预制体可以独立配置伤害范围（支持多种形状），并提供两种伤害触发方式：
1. **动画帧事件触发**：通过在投射物动画中配置帧事件，当动画播放到指定帧时按照设定的伤害范围触发伤害
2. **碰撞触发**：投射物碰撞到目标后直接按照设定的伤害范围触发伤害

注意：近战技能（`MeleeSkillEffectData`）的现有功能保持不变。

## 需求

### 需求 1：移除远程 SkillEffectData 中的 Explosion Settings

**用户故事：** 作为一名开发者，我希望移除 `ProjectileSkillEffectData` 中的爆炸设置（`explosionRadius`），以便伤害范围不再由 ScriptableObject 数据资产统一控制，而是由预制体自身决定。

#### 验收标准

1. WHEN `ProjectileSkillEffectData` 被加载 THEN 系统 SHALL 不再包含 `explosionRadius` 字段
2. WHEN `ProjectileController.Launch()` 被调用 THEN 系统 SHALL 不再接收 `explosionRadius` 参数
3. WHEN 远程技能执行 THEN `ProjectileSkillEffectData.Execute()` SHALL 不再向 `ProjectileController` 传递爆炸半径数据
4. WHEN 编辑器面板显示 `ProjectileSkillEffectData` THEN 系统 SHALL 不再显示 Explosion Settings 区域（`ProjectileSkillEffectDataEditor` 需同步更新）

### 需求 2：为技能预制体添加伤害范围组件

**用户故事：** 作为一名开发者，我希望能在技能预制体上配置伤害范围，以便每个投射物可以拥有独立的、可视化的伤害区域设置。

#### 验收标准

1. WHEN 开发者为投射物预制体添加伤害范围组件 THEN 系统 SHALL 允许配置伤害范围形状（复用现有的 `AttackRangeShape` 体系，支持 Box / Circle 等形状）
2. WHEN 伤害范围组件被配置 THEN 系统 SHALL 支持配置多个形状的组合（Union），与近战技能的 `skillRangeShapes` 机制一致
3. WHEN 投射物预制体在 Scene 视图中被选中 THEN 系统 SHALL 以 Gizmo 可视化显示配置的伤害范围
4. IF 伤害范围组件未配置任何形状 THEN 系统 SHALL 在控制台输出警告日志

### 需求 3：支持动画帧事件触发伤害

**用户故事：** 作为一名开发者，我希望能通过在投射物动画中配置帧事件来触发伤害，以便精确控制伤害发生的时机（例如爆炸动画播放到特定帧时才造成伤害）。

#### 验收标准

1. WHEN 开发者选择"动画帧事件"作为伤害触发方式 THEN 系统 SHALL 允许在投射物的动画中添加帧事件来触发伤害检测
2. WHEN 动画播放到配置的事件帧 THEN 系统 SHALL 以预制体上配置的伤害范围为基准，检测范围内的所有敌方目标并造成伤害
3. WHEN 帧事件触发伤害 THEN 系统 SHALL 以投射物当前位置为中心进行伤害范围检测
4. IF 动画帧事件触发时伤害范围组件未配置 THEN 系统 SHALL 输出错误日志并跳过伤害处理

### 需求 4：支持碰撞触发伤害

**用户故事：** 作为一名开发者，我希望投射物在碰撞到目标后能直接触发伤害，以便实现直接命中型的远程技能效果。

#### 验收标准

1. WHEN 开发者选择"碰撞触发"作为伤害触发方式 THEN 系统 SHALL 在投射物与目标碰撞时立即触发伤害检测
2. WHEN 碰撞触发伤害 THEN 系统 SHALL 以投射物碰撞时的位置为中心，按照预制体上配置的伤害范围检测所有范围内的敌方目标并造成伤害
3. WHEN 碰撞触发伤害后 THEN 系统 SHALL 继续执行后续流程（如播放爆炸动画、回收到对象池等）
4. IF 投射物配置为碰撞触发 AND 飞行到达终点未碰撞到任何目标 THEN 系统 SHALL 在到达终点时仍然触发伤害检测（作为兜底逻辑）

### 需求 5：伤害触发方式的配置与互斥

**用户故事：** 作为一名开发者，我希望能在预制体上清晰地选择伤害触发方式，以便灵活配置不同类型的远程技能。

#### 验收标准

1. WHEN 开发者配置伤害触发方式 THEN 系统 SHALL 提供枚举选项：AnimationEvent（动画帧事件）、OnCollision（碰撞触发）
2. WHEN 选择 AnimationEvent 模式 THEN 系统 SHALL 不再在飞行结束时自动触发伤害（由动画帧事件控制）
3. WHEN 选择 OnCollision 模式 THEN 系统 SHALL 在碰撞时触发伤害，且飞行到达终点时作为兜底也触发伤害
4. WHEN 伤害已经被触发过一次 THEN 系统 SHALL 不再重复触发伤害（防止重复伤害）

### 需求 6：保持与现有系统的兼容性

**用户故事：** 作为一名开发者，我希望重构后的系统能与现有的对象池、动画系统和 CombatSystem 正确协作，以便不破坏其他功能。

#### 验收标准

1. WHEN 投射物被对象池回收并重新使用 THEN 系统 SHALL 正确重置伤害触发状态（`hasDamaged` 等标志位）
2. WHEN `ProjectileSkillEffectData.Execute()` 被调用 THEN 系统 SHALL 仍然正确传递伤害值（`skillData.damage`）和施法者类型（`casterType`）给投射物
3. WHEN 近战技能（`MeleeSkillEffectData`）执行 THEN 系统 SHALL 保持现有行为完全不变
4. WHEN 投射物的爆炸动画播放完毕 THEN 系统 SHALL 仍然通过 `onFinish` 回调触发对象池回收
