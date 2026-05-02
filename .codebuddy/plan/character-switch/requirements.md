# 需求文档

## 引言

本功能实现一个角色更换系统，允许玩家通过点击卡组中的焦点卡牌来切换场景中的角色。当前卡牌系统已具备拖拽滑动、焦点卡牌视觉效果（放大、弹出）等功能，但焦点卡牌不可点击。本功能需要将焦点卡牌变为可点击的按钮，点击后将场景中的玩家角色切换为该卡牌对应的 `CharacterData`。

### 现有系统概述
- **CardSplineDistributor**：管理卡牌沿 Spline 的分布，持有 `characterDataList` 数组和 `FocusIndex`
- **CharacterCard**：卡牌 UI 组件，通过 `SetData(CharacterData)` 绑定数据，持有 `Data` 属性
- **CardDragHandler**：处理卡牌拖拽交互（`IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`）
- **GameCharacterManager**：场景级管理器，负责生成玩家角色（`CreatePlayerCharacter`）
- **CharacterEntity**：角色核心组件，通过 `Initialize(CharacterData)` 初始化

## 需求

### 需求 1：焦点卡牌可点击

**用户故事：** 作为一名玩家，我希望能够点击卡组中的焦点卡牌，以便触发角色更换流程

#### 验收标准

1.1 WHEN 焦点卡牌被点击（非拖拽） THEN 系统 SHALL 触发角色更换逻辑，将场景中的玩家角色切换为该卡牌对应的 `CharacterData`

1.2 WHEN 非焦点卡牌被点击 THEN 系统 SHALL 不触发角色更换（仅焦点卡牌可触发）

1.3 WHEN 用户在卡牌上执行拖拽操作 THEN 系统 SHALL 不触发点击事件（拖拽与点击互斥）

1.4 WHEN 卡组处于惯性滑动或吸附动画中 THEN 系统 SHALL 不响应点击事件，避免误触

1.5 IF 焦点卡牌对应的角色与当前场景中的角色相同 THEN 系统 SHALL 不执行切换操作（避免重复切换）

### 需求 2：角色切换逻辑

**用户故事：** 作为一名玩家，我希望点击焦点卡牌后场景中的角色能够无缝切换为新角色，以便我可以体验不同的角色

#### 验收标准

2.1 WHEN 角色更换被触发 THEN `GameCharacterManager` SHALL 销毁/回收当前玩家角色，并使用焦点卡牌对应的 `CharacterData` 在相同位置生成新角色

2.2 WHEN 新角色生成后 THEN 系统 SHALL 确保新角色具备完整的组件配置（`CharacterEntity`、`CharacterAnimator`、`CombatSystem`、`AIController`、`ManualController`、`ControlModeManager` 等），与 `GameCharacterManager.CreatePlayerCharacter` 的逻辑一致

2.3 WHEN 角色切换完成后 THEN 系统 SHALL 自动关闭卡组面板（调用 `CardSplineDistributor.Hide()`）

2.4 WHEN 角色切换完成后 THEN 新角色 SHALL 默认处于 AI 挂机模式

2.5 IF 角色切换过程中发生错误（如 CharacterData 为 null） THEN 系统 SHALL 输出错误日志并保持当前角色不变

### 需求 3：交互体验

**用户故事：** 作为一名玩家，我希望角色更换的交互流程清晰直观，不会与现有的卡牌拖拽操作冲突

#### 验收标准

3.1 WHEN 焦点卡牌被点击时 THEN 系统 SHALL 提供视觉反馈（如卡牌短暂缩放动画），让玩家知道点击已被识别

3.2 WHEN 卡组面板未显示时 THEN 系统 SHALL 不响应任何卡牌点击事件

3.3 系统 SHALL 通过区分拖拽距离阈值来判断用户意图是点击还是拖拽（拖拽距离小于阈值视为点击）
