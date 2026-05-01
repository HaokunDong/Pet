# 需求文档

## 引言

本功能旨在为卡牌选择系统添加显示/隐藏切换机制。当前卡牌在场景加载时立即实例化并显示，用户希望卡牌初始状态为隐藏，通过点击场景中 `MenuView/Menus/Button1` 按钮来切换卡组的显示与隐藏。同时支持在卡组显示状态下按 Esc 键隐藏卡组。

### 涉及的关键文件
- `Assets/Scripts/CardSelection/CardSplineDistributor.cs` — 卡牌实例化与分布逻辑，当前在 `Start()` 中调用 `Initialize()` 自动实例化并显示卡牌
- `Assets/Scripts/View/MainView.cs` — 菜单视图脚本，包含 Button1 的点击事件处理（当前仅打印日志）
- `Assets/Scripts/CardSelection/CardFocusDisplay.cs` — 焦点卡牌显示逻辑
- `Assets/Scripts/CardSelection/CardDragHandler.cs` — 卡牌拖拽处理
- `Assets/Scripts/CardSelection/CardInertiaAndSnap.cs` — 卡牌惯性与吸附

## 需求

### 需求 1：卡牌初始隐藏

**用户故事：** 作为一名玩家，我希望卡牌在场景加载时默认隐藏，以便界面保持整洁，只在需要时才展示卡组。

#### 验收标准

1. WHEN 场景加载完成 THEN 卡牌系统 SHALL 完成卡牌的实例化和初始化，但所有卡牌保持隐藏状态（卡牌容器 GameObject 设为不激活）
2. WHEN 卡牌处于隐藏状态 THEN 卡牌系统 SHALL 不响应任何拖拽或焦点交互

### 需求 2：Button1 切换卡组显示/隐藏

**用户故事：** 作为一名玩家，我希望点击 `MenuView/Menus/Button1` 按钮时能切换卡组的显示与隐藏，以便我可以随时查看或收起卡牌。

#### 验收标准

1. WHEN 卡组处于隐藏状态 AND 玩家点击 Button1 THEN 系统 SHALL 显示卡组（激活卡牌容器 GameObject）
2. WHEN 卡组处于显示状态 AND 玩家点击 Button1 THEN 系统 SHALL 隐藏卡组（停用卡牌容器 GameObject）
3. WHEN Button1 被点击触发显示/隐藏切换 THEN 系统 SHALL 确保切换过程中不会出现卡牌位置或状态异常

### 需求 3：Esc 键隐藏卡组

**用户故事：** 作为一名玩家，我希望在卡组显示时按下 Esc 键能隐藏卡组，以便我可以通过键盘快捷键快速收起卡牌。

#### 验收标准

1. WHEN 卡组处于显示状态 AND 玩家按下 Esc 键 THEN 系统 SHALL 隐藏卡组
2. WHEN 卡组处于隐藏状态 AND 玩家按下 Esc 键 THEN 系统 SHALL 不执行任何操作（无副作用）

### 需求 4：状态一致性

**用户故事：** 作为一名玩家，我希望卡组在显示和隐藏切换时状态保持一致，以便每次打开卡组时都能正常使用。

#### 验收标准

1. WHEN 卡组从隐藏切换为显示 THEN 系统 SHALL 确保卡牌位置沿 Spline 正确分布、焦点卡牌正确高亮
2. WHEN 卡组从显示切换为隐藏 THEN 系统 SHALL 停止所有正在进行的 DOTween 动画和拖拽操作，避免残留状态
3. IF 玩家正在拖拽卡牌 AND 按下 Esc 键或点击 Button1 THEN 系统 SHALL 先取消拖拽操作，再隐藏卡组
