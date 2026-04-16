# 需求文档：可组合多形状攻击范围系统

## 引言

当前项目中的攻击范围系统使用单一的 `float attackRange` 值，以角色中心为圆心做圆形距离检测。这种方式无法满足不同角色多样化的攻击形态需求（如近战横砍的矩形范围、法术的圆形范围等）。

本功能将现有的单一浮点数攻击范围替换为**可组合的多形状攻击范围系统**，支持：
- **圆形（Circle）** 和 **矩形（Box）** 两种基础形状
- 每个形状可独立调整**位置偏移**和**大小**
- 每个角色可配置**多个攻击范围框**，最终攻击范围为所有框的**并集**
- 攻击范围配置存储在 **CharacterData（ScriptableObject）** 中，每个角色拥有独立的 Data 资产
- 编辑 CharacterData 时可同时预览角色外观与攻击范围，方便直观调整
- 在 Scene 视图中实时预览攻击范围 Gizmo
- 兼容现有的 AI 行为树和手动控制系统

## 需求

### 需求 1：攻击范围形状数据定义

**用户故事：** 作为一名游戏设计师，我希望能够为每个角色定义不同形状的攻击范围，以便不同角色拥有符合其攻击动作的判定区域。

#### 验收标准

1. WHEN 定义攻击范围形状 THEN 系统 SHALL 支持两种形状类型：Circle（圆形）和 Box（矩形）
2. WHEN 配置 Circle 形状 THEN 系统 SHALL 允许设置半径（radius）和相对于角色的位置偏移（offset）
3. WHEN 配置 Box 形状 THEN 系统 SHALL 允许设置宽度（width）、高度（height）和相对于角色的位置偏移（offset）
4. WHEN 角色面朝方向改变 THEN 系统 SHALL 自动镜像翻转攻击范围的水平偏移（offset.x 取反），使攻击范围始终在角色面朝的方向上
5. WHEN 定义攻击范围形状数据 THEN 系统 SHALL 使用可序列化的数据结构（如 `[System.Serializable]` 类），以便 Unity 能够在 Inspector 中显示和序列化

### 需求 2：多攻击范围框组合

**用户故事：** 作为一名游戏设计师，我希望能够为单个角色添加多个攻击范围框并取并集，以便组合出复杂的攻击判定区域。

#### 验收标准

1. WHEN 角色配置了多个攻击范围框 THEN 系统 SHALL 将所有框的覆盖区域取并集作为最终攻击范围
2. WHEN 判断目标是否在攻击范围内 THEN 系统 SHALL 检查目标位置是否落在任意一个攻击范围框内（只要命中一个即视为在范围内）
3. WHEN 角色没有配置任何攻击范围框 THEN 系统 SHALL 回退到使用 CharacterData 中的默认 attackRange 值作为圆形范围（向后兼容）
4. WHEN 需要计算"最大攻击距离"用于 AI 追击判断 THEN 系统 SHALL 计算所有攻击范围框的最大外接距离（从角色中心到范围框最远点的距离）

### 需求 3：CharacterData 中配置攻击范围

**用户故事：** 作为一名游戏设计师，我希望攻击范围配置存储在每个角色的 CharacterData（ScriptableObject）中，以便不同角色拥有各自独立的攻击范围定义，且方便统一管理。

#### 验收标准

1. WHEN 编辑 CharacterData 资产 THEN 系统 SHALL 在 Inspector 中显示一个"Attack Range Shapes"数组字段，允许添加、删除和编辑攻击范围框
2. WHEN CharacterData 中配置了攻击范围框数组 THEN 系统 SHALL 在角色实例化时将这些配置直接应用到 RuntimeCharacterStats 中
3. WHEN CharacterData 中的攻击范围框数组为空 THEN 系统 SHALL 回退使用 CharacterData 中已有的 `attackRange` 浮点值作为默认圆形范围
4. WHEN 在 CharacterData Inspector 中修改攻击范围配置 THEN 系统 SHALL 将修改序列化保存到 ScriptableObject 资产文件中

### 需求 4：CharacterData Inspector 带角色预览的可视化编辑

**用户故事：** 作为一名游戏设计师，我希望在编辑 CharacterData 的攻击范围时，能够同时看到角色的外观和攻击范围的叠加预览，以便直观地确定攻击范围相对于角色身体的位置和大小。

#### 验收标准

1. WHEN 在 Inspector 中选中 CharacterData 资产 THEN 系统 SHALL 显示一个可编辑的攻击范围框列表，支持添加和删除条目
2. WHEN 添加新的攻击范围框 THEN 系统 SHALL 允许选择形状类型（Circle 或 Box），并提供对应的参数编辑字段
3. WHEN 在 CharacterData Inspector 中编辑攻击范围 THEN 系统 SHALL 在 Inspector 底部显示一个预览区域，同时渲染角色的 Sprite 和所有攻击范围框的叠加效果
4. WHEN 预览区域显示时 THEN 系统 SHALL 以角色 Sprite 为中心，用半透明彩色区域绘制每个攻击范围框（圆形或矩形），使设计师能直观看到攻击范围相对于角色身体的覆盖位置
5. WHEN 修改攻击范围框的参数（偏移、大小、形状类型） THEN 系统 SHALL 实时更新预览区域的显示
6. WHEN CharacterData 未配置 Sprite THEN 系统 SHALL 在预览区域用一个默认占位图标代替角色外观，仍然显示攻击范围框

### 需求 5：Scene 视图 Gizmo 可视化

**用户故事：** 作为一名游戏设计师，我希望在 Scene 视图中能够看到每个角色实例的攻击范围框可视化显示，以便在运行时直观地确认攻击范围的覆盖区域。

#### 验收标准

1. WHEN 在 Scene 视图中选中角色实例 THEN 系统 SHALL 以半透明填充 + 线框的方式显示所有攻击范围框
2. WHEN 攻击范围框为 Circle 类型 THEN 系统 SHALL 绘制圆形 Gizmo
3. WHEN 攻击范围框为 Box 类型 THEN 系统 SHALL 绘制矩形 Gizmo
4. WHEN 角色实例未被选中 THEN 系统 SHALL 以较淡的线框显示攻击范围轮廓
5. WHEN 存在多个攻击范围框 THEN 系统 SHALL 用相同颜色绘制所有框，以表示它们属于同一角色
6. WHEN 在运行时通过 CharacterEntity 的 Inspector 修改攻击范围参数 THEN 系统 SHALL 立即更新 Gizmo 显示和实际战斗判定

### 需求 6：战斗系统集成

**用户故事：** 作为一名开发者，我希望新的攻击范围系统能够无缝替换现有的距离检测逻辑，以便 AI 行为树和手动控制都能正确使用新的范围判定。

#### 验收标准

1. WHEN CombatSystem 检查攻击范围 THEN 系统 SHALL 使用新的多形状范围判定替代原有的 `Vector2.Distance` 检测
2. WHEN BTCheckEnemyInRange 节点检查范围 THEN 系统 SHALL 使用新的范围判定逻辑
3. WHEN BTAttack 节点检查范围 THEN 系统 SHALL 使用新的范围判定逻辑
4. WHEN BTMoveToTarget 节点判断是否到达攻击距离 THEN 系统 SHALL 使用新系统计算的最大攻击距离
5. WHEN ManualController 判断是否在攻击范围内 THEN 系统 SHALL 使用新的范围判定逻辑
6. WHEN 角色没有配置任何自定义攻击范围框 THEN 系统 SHALL 保持与旧系统完全一致的行为（向后兼容）
