# 需求文档：角色按钮面板（CharacterButtonPanel）

## 引言

当前点击角色时，`ControlModeManager` 会在角色旁边生成一个单按钮（操控/退出操控）。本次需求将该单按钮替换为 `Resources/Prefabs/UI/CharacterButtonPanel` 预制体，该预制体包含三个按钮：**ChangeCharacter**（更换）、**Train**（培养）、**Control**（操控）。每个按钮有各自的点击逻辑，面板生成在角色旁边，且支持通过 X/Y 偏移量调节位置。

### 现有结构概览

- **CharacterButtonPanel 预制体**：`Resources/Prefabs/UI/CharacterButtonPanel`，包含 GridLayoutGroup，子物体为 `ChangeCharacter`、`Train`、`Control` 三个 Button
- **ControlModeManager.cs**：挂载在角色上，负责点击角色后的交互流程（闪白 → 暂停 → 显示按钮 → 点击按钮/空白区域后恢复）
- **MainView.cs**：主界面脚本，其中 `button1` 的点击逻辑通过 `CardSplineDistributor.ToggleVisibility()` 打开/关闭卡组 Cards
- **CardSplineDistributor**：卡牌系统，提供 `Show()`、`Hide()`、`ToggleVisibility()` 方法

---

## 需求

### 需求 1：替换按钮为 CharacterButtonPanel 预制体

**用户故事：** 作为一名玩家，我希望点击角色后看到一个包含三个功能按钮的面板，以便我可以对角色执行不同的操作（更换、培养、操控）。

#### 验收标准

1. WHEN 玩家点击角色 THEN 系统 SHALL 实例化 `Resources/Prefabs/UI/CharacterButtonPanel` 预制体，替代当前的单按钮创建逻辑
2. WHEN CharacterButtonPanel 被实例化 THEN 系统 SHALL 在世界空间 Canvas 中显示该面板，面板位于角色旁边
3. WHEN CharacterButtonPanel 已存在 THEN 系统 SHALL 复用已有实例（显示/隐藏），而非重复实例化
4. IF 旧的单按钮创建逻辑（`CreateButtonUI` 中的程序化创建代码）仍存在 THEN 系统 SHALL 移除该逻辑，仅保留预制体实例化方式

### 需求 2：ChangeCharacter 按钮点击逻辑

**用户故事：** 作为一名玩家，我希望点击"更换"按钮后打开卡组选择界面，以便我可以更换当前角色。

#### 验收标准

1. WHEN 玩家点击 ChangeCharacter 按钮 THEN 系统 SHALL 调用 `CardSplineDistributor.ToggleVisibility()` 打开卡组 Cards（与 MainView 中 button1 的逻辑一致）
2. WHEN ChangeCharacter 按钮被点击 THEN 系统 SHALL 隐藏 CharacterButtonPanel 并恢复角色之前的行为模式

### 需求 3：Train 按钮点击逻辑

**用户故事：** 作为一名玩家，我希望点击"培养"按钮后触发培养功能（当前为占位逻辑），以便后续扩展培养系统。

#### 验收标准

1. WHEN 玩家点击 Train 按钮 THEN 系统 SHALL 打印日志 `Debug.Log("Train")`
2. WHEN Train 按钮被点击 THEN 系统 SHALL 隐藏 CharacterButtonPanel 并恢复角色之前的行为模式

### 需求 4：Control 按钮点击逻辑

**用户故事：** 作为一名玩家，我希望点击"操控"按钮后触发操控功能（当前为占位逻辑），以便后续扩展操控系统。

#### 验收标准

1. WHEN 玩家点击 Control 按钮 THEN 系统 SHALL 打印日志 `Debug.Log("Control")`
2. WHEN Control 按钮被点击 THEN 系统 SHALL 隐藏 CharacterButtonPanel 并恢复角色之前的行为模式

### 需求 5：面板位置偏移可调

**用户故事：** 作为一名开发者，我希望可以在 Inspector 中调节 CharacterButtonPanel 相对于角色的 X/Y 偏移量，以便在不同角色或场景下灵活调整面板位置。

#### 验收标准

1. WHEN ControlModeManager 组件在 Inspector 中显示 THEN 系统 SHALL 提供 `buttonOffset`（Vector2）字段，允许调节面板相对角色的 X 和 Y 偏移
2. WHEN CharacterButtonPanel 被显示 THEN 系统 SHALL 将面板的世界空间 Canvas 定位在角色位置 + buttonOffset 的位置
3. IF buttonOffset 值被修改 THEN 系统 SHALL 在下次显示面板时应用新的偏移值

### 需求 6：面板交互与关闭行为

**用户故事：** 作为一名玩家，我希望点击面板外的空白区域时面板能自动关闭，以便我可以取消操作。

#### 验收标准

1. WHEN 面板正在显示且玩家点击非按钮、非角色区域 THEN 系统 SHALL 隐藏面板并恢复角色之前的行为模式（保留现有 DismissButton 逻辑）
2. WHEN 面板正在显示且玩家再次点击角色 THEN 系统 SHALL 视为点击空白区域，隐藏面板

---

## 边界情况与技术考虑

- **预制体加载失败**：如果 `Resources.Load` 无法加载 CharacterButtonPanel 预制体，应在控制台输出警告并跳过面板创建
- **CardSplineDistributor 未找到**：ChangeCharacter 按钮点击时，如果场景中没有 CardSplineDistributor，应安全跳过（不崩溃）
- **现有操控模式切换**：当前 ControlModeManager 中的操控/退出操控模式切换逻辑（`OnActionButtonClicked`）需要被重构，Control 按钮当前仅打印日志，不再执行模式切换
- **按钮事件清理**：在 `OnDestroy` 时需要正确移除所有三个按钮的点击事件监听器
