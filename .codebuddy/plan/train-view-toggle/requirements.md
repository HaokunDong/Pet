# 需求文档

## 引言

本功能为 CharacterCard 预制体中的 Train 按钮添加点击交互逻辑。当用户点击 CharacterCard 上的 Train 按钮时，在该卡片右侧（X 方向偏移 280）的位置生成 TrainView 预制件实例；再次点击 Train 按钮时，关闭（销毁或隐藏）已生成的 TrainView。该逻辑与项目中其他按钮的点击模式保持一致（如 ControlModeManager 中按钮点击加载/实例化预制体的模式）。

### 相关资源
- **TrainView 预制体路径**：`Resources/Prefabs/UI/View/TrainView`
- **CharacterCard 预制体路径**：`Resources/Prefabs/UI/CharacterCard`
- **CharacterCard 脚本**：`Assets/Scripts/CardSelection/CharacterCard.cs`
- **现有按钮逻辑参考**：`Assets/Scripts/Character/ControlModeManager.cs`（OnTrainClicked 方法）

## 需求

### 需求 1：Train 按钮点击生成 TrainView

**用户故事：** 作为一名玩家，我希望点击 CharacterCard 上的 Train 按钮时能在卡片右侧弹出 TrainView 面板，以便我可以查看和操作训练相关内容。

#### 验收标准

1. WHEN 用户点击 CharacterCard 预制体中的 Train 按钮 AND TrainView 当前未显示 THEN 系统 SHALL 在 CharacterCard 位置右侧 X 偏移 280 的位置实例化 TrainView 预制体
2. WHEN TrainView 被实例化 THEN 系统 SHALL 将 TrainView 放置在与 CharacterCard 相同的 Canvas 层级下
3. WHEN TrainView 被实例化 THEN 系统 SHALL 确保 TrainView 的 Y 坐标与 CharacterCard 保持一致（仅 X 方向偏移 280）

### 需求 2：Train 按钮再次点击关闭 TrainView

**用户故事：** 作为一名玩家，我希望再次点击 Train 按钮时能关闭已打开的 TrainView 面板，以便我可以方便地切换训练面板的显示状态。

#### 验收标准

1. WHEN 用户点击 CharacterCard 预制体中的 Train 按钮 AND TrainView 当前已显示 THEN 系统 SHALL 关闭（销毁）当前的 TrainView 实例
2. WHEN TrainView 被关闭 THEN 系统 SHALL 释放相关资源引用，确保不会产生内存泄漏

### 需求 3：按钮点击逻辑一致性

**用户故事：** 作为一名开发者，我希望 Train 按钮的点击逻辑与项目中其他按钮的模式保持一致，以便代码风格统一、易于维护。

#### 验收标准

1. WHEN 实现 Train 按钮点击逻辑 THEN 系统 SHALL 遵循与 ControlModeManager 中 OnTrainClicked/OnChangeCharacterClicked 相同的模式（通过 Resources.Load 加载预制体、Instantiate 实例化、Toggle 切换显示/隐藏）
2. WHEN CharacterCard 脚本中添加按钮引用 THEN 系统 SHALL 通过 SerializeField 或 transform.Find 方式获取 Train 按钮引用
3. WHEN 绑定按钮事件 THEN 系统 SHALL 使用 Button.onClick.AddListener 方式注册点击回调

### 需求 4：边界情况处理

**用户故事：** 作为一名玩家，我希望在各种边界情况下系统都能正确处理 TrainView 的显示/隐藏，以便不会出现异常行为。

#### 验收标准

1. IF TrainView 预制体加载失败 THEN 系统 SHALL 输出警告日志并不执行后续操作
2. IF CharacterCard 被销毁时 TrainView 仍然存在 THEN 系统 SHALL 自动销毁关联的 TrainView 实例
3. WHEN 多个 CharacterCard 同时存在 THEN 系统 SHALL 确保每个卡片独立管理自己的 TrainView 实例（互不干扰）
