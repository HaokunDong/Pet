# 需求文档

## 引言

本功能旨在为近战技能创建一个新的 `MeleeSkillEffectData` ScriptableObject，作为现有 `SkillEffectData` 抽象基类的具体子类（与已有的 `ProjectileSkillEffectData` 远程投射物技能并列）。

近战技能的核心特点是：技能伤害范围由一个或多个可配置的 Box 形状组成，每个 Box 支持独立的偏移调整。当技能动画播放到事件帧（`OnSkillHit`）时，系统会根据这些 Box 范围检测并对范围内的敌人造成伤害。

为了方便策划在 Unity Editor 中调整技能范围，`MeleeSkillEffectData` 的 Inspector 面板需要显示角色的精灵图，并在其上叠加绘制技能攻击范围的可视化预览（类似于 `CharacterDataEditor` 中已有的攻击范围预览功能）。

### 现有系统概述

- **`SkillEffectData`**：抽象基类，定义 `Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)` 接口
- **`AttackRangeShape`**：已有的可序列化形状类，支持 Circle 和 Box 类型，带偏移和朝向镜像
- **`AttackRangeHelper`**：静态工具类，提供多形状联合范围检测
- **`AnimEventReceiver.OnSkillHit()`**：动画事件帧回调，触发 `CombatSystem.ApplySkillDamage()`
- **`CombatSystem.ApplySkillDamage()`**：已有逻辑会调用 `skillData.skillEffect.Execute()` 委托给具体的 SkillEffectData 子类

---

## 需求

### 需求 1：MeleeSkillEffectData ScriptableObject 创建

**用户故事：** 作为一名策划，我希望能够通过 Unity 的 Create Asset 菜单创建近战技能效果数据资产，以便为不同角色配置不同的近战技能攻击范围。

#### 验收标准

1. WHEN 用户在 Unity 编辑器中右键选择 Create > Game > SkillEffect > Melee THEN 系统 SHALL 创建一个新的 `MeleeSkillEffectData` ScriptableObject 资产
2. `MeleeSkillEffectData` SHALL 继承自 `SkillEffectData` 抽象基类
3. `MeleeSkillEffectData` SHALL 包含一个 `AttackRangeShape[]` 数组字段（命名为 `skillRangeShapes`），用于定义技能的攻击范围
4. 每个 `AttackRangeShape` 元素 SHALL 支持 Box 类型，并可独立配置偏移（offset）和尺寸（size）
5. `MeleeSkillEffectData` SHALL 包含一个 `Sprite` 字段（命名为 `previewSprite`），用于在 Editor 中显示角色精灵图以辅助范围调整
6. `MeleeSkillEffectData` SHALL 包含一个 `bool defaultFacesRight` 字段，用于指示精灵图的默认朝向，以便正确镜像偏移

### 需求 2：近战技能伤害执行逻辑

**用户故事：** 作为一名开发者，我希望近战技能在动画事件帧触发时能够根据配置的 Box 范围检测并对范围内的敌人造成伤害，以便近战技能能够正确地产生战斗效果。

#### 验收标准

1. WHEN 动画事件帧触发 `OnSkillHit` 且当前技能的 `SkillEffectData` 为 `MeleeSkillEffectData` 类型 THEN 系统 SHALL 调用 `MeleeSkillEffectData.Execute()` 方法
2. WHEN `Execute()` 被调用 THEN 系统 SHALL 根据施法者的位置和朝向，使用 `skillRangeShapes` 中定义的所有形状进行联合范围检测
3. WHEN 范围检测执行时 THEN 系统 SHALL 查找所有对立阵营的存活角色（玩家技能检测 Enemy 标签，敌人技能检测 Player 标签）
4. WHEN 目标角色位于任意一个 `skillRangeShapes` 形状范围内 THEN 系统 SHALL 对该目标造成 `SkillData.damage` 点伤害
5. IF `skillRangeShapes` 为空或 null THEN 系统 SHALL 不造成任何伤害并直接返回
6. WHEN 伤害应用完成后 THEN 系统 SHALL 正常清理攻击状态（与现有流程一致）

### 需求 3：Editor 可视化预览

**用户故事：** 作为一名策划，我希望在 `MeleeSkillEffectData` 的 Inspector 面板中能够看到角色精灵图和技能攻击范围的叠加预览，以便我能够直观地调整技能范围使其与角色动作匹配。

#### 验收标准

1. WHEN 在 Inspector 中选中一个 `MeleeSkillEffectData` 资产 THEN 系统 SHALL 在默认 Inspector 字段下方显示一个可视化预览区域
2. IF `previewSprite` 字段已赋值 THEN 预览区域 SHALL 显示该精灵图，居中于预览区域
3. WHEN 预览区域绘制时 THEN 系统 SHALL 在精灵图上叠加绘制所有 `skillRangeShapes` 中定义的形状
4. Box 形状 SHALL 以蓝色半透明矩形显示，Circle 形状 SHALL 以红色半透明圆形显示（与 `CharacterDataEditor` 风格一致）
5. WHEN `defaultFacesRight` 值改变时 THEN 预览中的形状偏移 SHALL 相应地镜像显示
6. WHEN `skillRangeShapes` 的任何参数发生变化 THEN 预览 SHALL 实时更新
7. 预览区域 SHALL 显示十字准线标记角色中心位置
8. 预览区域 SHALL 显示提示信息说明各颜色代表的含义

### 需求 4：与现有系统的集成

**用户故事：** 作为一名开发者，我希望 `MeleeSkillEffectData` 能够无缝集成到现有的技能系统中，以便不需要修改 `CombatSystem`、`AnimEventReceiver` 等核心代码。

#### 验收标准

1. `MeleeSkillEffectData` SHALL 通过实现 `SkillEffectData.Execute()` 抽象方法来集成，无需修改 `CombatSystem` 或 `AnimEventReceiver` 的代码
2. WHEN 一个 `SkillData` 资产的 `skillEffect` 字段被赋值为 `MeleeSkillEffectData` 实例 THEN 现有的技能触发流程 SHALL 自动调用其 `Execute()` 方法
3. `MeleeSkillEffectData` SHALL 复用现有的 `AttackRangeShape` 类和 `AttackRangeHelper` 工具类进行范围检测
4. `MeleeSkillEffectData` 的 Editor 脚本 SHALL 放置在 `Assets/Scripts/Editor/` 或 `Assets/Editor/` 目录下（与现有 Editor 脚本位置一致）
