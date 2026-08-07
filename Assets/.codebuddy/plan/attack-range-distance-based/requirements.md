# 需求文档

## 引言

当前项目中，普通攻击和近战技能的攻击范围使用 `AttackRangeShape[]`（Box/Circle 形状组合）来定义攻击区域。这种方式虽然灵活，但对于横版动作游戏来说过于复杂——实际上大多数近战攻击只需要一个"从自身向前方延伸的距离"即可满足需求。

本需求旨在将普通攻击和近战技能的攻击范围判定方式简化为**基于距离的判定**：从角色自身的 Transform 位置向面朝方向发出一段距离，敌人在该范围内即视为命中。同时，AI模式下的攻击距离识别改为使用**自身 Transform 到目标碰撞体边缘的相对水平距离**来判断，这样可以正确处理不同体型敌人的碰撞体大小差异。

### 核心变更概要

| 现有方式 | 新方式 |
|---------|--------|
| `CharacterData.attackRangeShapes` (AttackRangeShape[]) | `CharacterData.attackDistance` (float) |
| `MeleeSkillEffectData.skillRangeShapes` (AttackRangeShape[]) | `MeleeSkillEffectData.skillAttackDistance` (float) |
| 基于形状（Box/Circle）的 `Contains()` 判定 | 基于自身 Transform 向前方的水平距离判定 |
| 使用 `ColliderCenter` 计算距离 | AI距离识别：使用自身 Transform 到目标碰撞体边缘的水平距离 |
| 场景中绘制 Box/Circle Gizmos | 场景中绘制前方距离线段/扇形 |

**注意**：此改动影响普通攻击伤害判定、近战技能伤害判定、AI攻击触发判定、场景Gizmos绘制、以及Editor预览。投射物技能（Projectile）不受影响。

## 需求

### 需求 1：普通攻击改为基于距离判定

**用户故事：** 作为一名开发者，我希望普通攻击的攻击范围改为从自身 Transform 向前方发出的一段距离来判定，以便简化配置并使攻击判定更加直观。

#### 验收标准

1. WHEN 角色发起普通攻击时 THEN 系统 SHALL 以角色自身 `Transform.position` 为起点，沿面朝方向（水平X轴）计算攻击距离，目标 `Transform.position` 的水平距离在该范围内即判定为命中。
2. WHEN 配置角色数据时 THEN 系统 SHALL 使用一个 `attackDistance`（float）参数替代现有的 `attackRangeShapes`（AttackRangeShape[]）数组。
3. WHEN 计算攻击命中时 THEN 系统 SHALL 忽略Y轴差异（横版游戏特性），仅比较双方 Transform 在X轴上的水平距离。
4. IF `attackDistance` 为0或未配置 THEN 系统 SHALL 使用合理的默认值（如0.5），确保向后兼容。
5. WHEN 多个敌人在攻击距离内时 THEN 系统 SHALL 对所有在距离内的敌人造成伤害（保持现有AOE行为）。

### 需求 2：近战技能改为基于距离判定

**用户故事：** 作为一名开发者，我希望近战技能的伤害范围也改为基于距离判定，以便与普通攻击保持一致的判定逻辑。

#### 验收标准

1. WHEN 近战技能触发伤害时 THEN 系统 SHALL 以施法者 `Transform.position` 为起点，沿面朝方向计算技能攻击距离，目标在该距离内即判定为命中。
2. WHEN 配置近战技能效果数据时 THEN 系统 SHALL 使用一个 `skillAttackDistance`（float）参数替代现有的 `skillRangeShapes`（AttackRangeShape[]）数组。
3. WHEN 近战技能配合位移（Displacement）使用时 THEN 系统 SHALL 在位移过程中使用 `skillAttackDistance` 进行持续伤害检测（替代原有的形状检测）。
4. IF `skillAttackDistance` 为0或未配置 THEN 系统 SHALL 使用合理的默认值，确保向后兼容。

### 需求 3：AI攻击距离识别改为与碰撞体边缘的相对距离

**用户故事：** 作为一名开发者，我希望AI模式下的攻击距离识别改为使用自身 Transform 到目标碰撞体边缘的相对水平距离来判断，以便正确处理不同体型敌人的碰撞体大小差异，避免大体型敌人需要走到碰撞体中心才能攻击的问题。

#### 验收标准

1. WHEN AI判断是否进入攻击范围时 THEN 系统 SHALL 计算 `owner.Transform.position.x` 到 `target` 碰撞体在X轴上最近边缘的水平距离，与 `attackDistance` 进行比较。
2. WHEN AI计算 Engage Distance 时 THEN 系统 SHALL 直接基于 `attackDistance` 计算（如 `attackDistance * 0.9f`），而非从形状中提取最大距离。
3. WHEN AI判断是否在攻击范围内（用于Combo连击判定）时 THEN 系统 SHALL 同样使用自身 Transform 到目标碰撞体边缘的水平距离与 `attackDistance` 比较。
4. WHEN 双方互相判断攻击距离时 THEN 系统 SHALL 各自使用自己的 `attackDistance` 与自身 Transform 到对方碰撞体边缘的水平距离进行比较（即A计算自身到B碰撞体边缘的距离判断B是否在A的攻击范围内，B计算自身到A碰撞体边缘的距离判断A是否在B的攻击范围内，各自独立）。
5. WHEN 计算到碰撞体边缘的距离时 THEN 系统 SHALL 使用目标 Collider 的 `ClosestPoint` 或基于 Collider bounds 在X轴上的最近边缘点来计算水平距离，确保对不同体型的敌人都能正确判定。

### 需求 4：攻击距离在场景中可视化绘制

**用户故事：** 作为一名开发者，我希望攻击距离能在Unity场景视图中绘制出来，以便在编辑和调试时直观地看到攻击范围。

#### 验收标准

1. WHEN 角色在场景中被选中时 THEN 系统 SHALL 在角色前方绘制一条水平线段（或带有一定高度的矩形区域），表示攻击距离的范围。
2. WHEN 角色未被选中时 THEN 系统 SHALL 以较淡的颜色绘制攻击距离线段，保持场景整洁。
3. WHEN 在Editor Inspector中编辑 `attackDistance` 时 THEN 系统 SHALL 实时更新场景中的Gizmos绘制，提供即时反馈。
4. WHEN 近战技能配置了 `skillAttackDistance` 时 THEN 系统 SHALL 同样在场景中绘制技能的攻击距离（使用不同颜色区分普通攻击和技能）。
5. WHEN 绘制攻击距离时 THEN 系统 SHALL 根据角色当前面朝方向正确绘制（面朝左则向左绘制，面朝右则向右绘制）。

### 需求 5：直接移除旧的形状系统

**用户故事：** 作为一名开发者，我希望直接移除普通攻击和近战技能中的 `AttackRangeShape[]` 相关字段和逻辑，以便彻底简化代码和数据配置，不保留任何废弃兼容。

#### 验收标准

1. WHEN 系统使用新的距离判定后 THEN 系统 SHALL 直接删除 `CharacterData.attackRangeShapes` 字段及其所有引用代码。
2. WHEN 系统使用新的距离判定后 THEN 系统 SHALL 直接删除 `MeleeSkillEffectData.skillRangeShapes` 字段及其所有引用代码。
3. WHEN 移除旧字段后 THEN 系统 SHALL 同步更新所有相关的 ScriptableObject 资产文件，确保序列化数据不会因字段缺失而报错。
4. IF 投射物技能（ProjectileSkillEffectData）使用了 `AttackRangeShape[]` THEN 系统 SHALL 保持投射物的形状判定不变（投射物不受此改动影响）。
5. WHEN `AttackRangeHelper` 工具类仅被投射物系统使用时 THEN 系统 SHALL 保留该类供投射物继续使用；IF `AttackRangeHelper` 已无任何引用 THEN 系统 SHALL 将其一并删除。

### 需求 6：`CombatSystem.TryNormalAttack` 和 `ApplyNormalAttackDamage` 适配

**用户故事：** 作为一名开发者，我希望 `CombatSystem` 中的攻击判定逻辑适配新的距离系统，以便攻击伤害正确应用。

#### 验收标准

1. WHEN `TryNormalAttack` 检查攻击范围时 THEN 系统 SHALL 使用 `attackDistance` 与双方 Transform 的水平距离进行比较，替代原有的 `IsTargetInAttackRange` 形状检测。
2. WHEN `ApplyNormalAttackDamage` 在hit frame应用伤害时 THEN 系统 SHALL 使用缓存的攻击位置与 `attackDistance` 进行距离判定，对范围内所有敌人造成伤害。
3. WHEN 角色面朝方向与目标方向不一致时 THEN 系统 SHALL 仅对面朝方向前方的敌人造成伤害（保持方向性攻击的特性）。
