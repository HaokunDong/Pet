# 需求文档

## 引言

本需求描述将项目现有的联机架构（基于 Unity Netcode for GameObjects + Unity Relay + Unity Transport）**完全删除**，并替换为基于 **Mirror + Steamworks.NET** 的联机系统架构。

### 现有架构（待删除）
- `NetworkBootstrap.cs` — 基于 Unity.Netcode 的网络引导
- `RelayManager.cs` — 基于 Unity Relay Service 的 NAT 穿透
- `LobbyManager.cs` — 基于 Unity.Netcode 的大厅管理
- `NetworkPlayerController.cs` — 网络玩家控制器
- `NetworkPlayerSetup.cs` — 网络玩家初始化
- `NetworkEnemyController.cs` — 网络敌人控制器
- `NetworkStatusUI.cs` — 网络状态UI
- `LobbyPanel.cs`（现有实现）— 依赖 Unity.Netcode 的大厅面板

### 新架构（Mirror + Steamworks）
- 使用 Mirror 作为网络框架
- 使用 Steamworks.NET 进行好友系统集成和 Steam Lobby 功能
- 使用 FizzySteamworks 作为 Mirror 的 Steam 传输层（需引入）
- 通过 Steam Lobby API 实现房间创建、邀请好友、房间号加入等功能

### 交互入口
- 用户点击 `RingRadialMenu` 下的 `FriendsButton` 时，在 `GamePanel`（Canvas 下的面板容器）下动态生成 `LobbyPanel` 预制体

---

## 需求

### 需求 1：删除现有联机架构

**用户故事：** 作为一名开发者，我希望完全移除现有的 Unity Netcode + Unity Relay 联机架构代码，以便为新的 Mirror + Steamworks 架构腾出空间，避免代码冲突。

#### 验收标准

1. WHEN 重构完成 THEN 系统 SHALL 不再包含以下文件：`NetworkBootstrap.cs`、`RelayManager.cs`、`LobbyManager.cs`（旧版）、`NetworkPlayerController.cs`、`NetworkPlayerSetup.cs`、`NetworkEnemyController.cs`、`NetworkStatusUI.cs`
2. WHEN 重构完成 THEN 系统 SHALL 不再引用 `Unity.Netcode`、`Unity.Services.Relay`、`Unity.Services.Authentication`、`Unity.Netcode.Transports.UTP` 等命名空间
3. WHEN 重构完成 THEN 系统 SHALL 移除旧的 `LobbyPanel.cs` 实现并替换为新的基于 Mirror+Steamworks 的实现
4. WHEN 重构完成 THEN 系统 SHALL 确保单人模式功能不受影响，游戏在无网络时仍可正常运行

---

### 需求 2：集成 FizzySteamworks 传输层

**用户故事：** 作为一名开发者，我希望引入 FizzySteamworks 作为 Mirror 的 Steam 传输层，以便通过 Steam 网络进行 P2P 连接，无需额外的中继服务器。

#### 验收标准

1. WHEN Mirror NetworkManager 初始化 THEN 系统 SHALL 使用 FizzySteamworks 作为默认传输层
2. IF Steamworks 未初始化或 Steam 客户端未运行 THEN 系统 SHALL 显示友好的错误提示并阻止联机操作
3. WHEN 传输层配置完成 THEN 系统 SHALL 支持通过 Steam P2P 网络进行玩家间通信

---

### 需求 3：实现 Steam Lobby 房间管理

**用户故事：** 作为一名玩家，我希望能够创建多人游戏房间，以便邀请好友一起游戏。

#### 验收标准

1. WHEN 玩家点击"创建房间"按钮 THEN 系统 SHALL 通过 Steam Lobby API 创建一个新的 Steam Lobby
2. WHEN 房间创建成功 THEN 系统 SHALL 生成一个唯一的房间号（基于 Steam Lobby ID）并显示给玩家
3. WHEN 房间创建成功 THEN 系统 SHALL 自动启动 Mirror Host 模式
4. WHEN 房间创建失败 THEN 系统 SHALL 显示错误信息并允许玩家重试
5. IF 玩家已在房间中 THEN 系统 SHALL 阻止重复创建房间

---

### 需求 4：好友邀请功能

**用户故事：** 作为一名玩家，我希望能够邀请 Steam 好友加入我的房间，以便快速组队游戏。

#### 验收标准

1. WHEN 玩家在房间中点击"邀请好友"按钮 THEN 系统 SHALL 调用 Steam Overlay 的好友邀请界面
2. WHEN 被邀请的好友接受邀请 THEN 系统 SHALL 自动将该好友连接到对应的 Steam Lobby 并加入 Mirror 游戏会话
3. IF 房间已满 THEN 系统 SHALL 拒绝新的连接并通知被邀请者房间已满
4. WHEN 邀请发送成功 THEN 系统 SHALL 显示"邀请已发送"的反馈

---

### 需求 5：房间号加入功能

**用户故事：** 作为一名玩家，我希望能够通过输入房间号加入好友的房间，以便在没有直接好友关系时也能一起游戏。

#### 验收标准

1. WHEN 玩家输入房间号并点击"加入"按钮 THEN 系统 SHALL 尝试通过房间号查找并加入对应的 Steam Lobby
2. WHEN 加入成功 THEN 系统 SHALL 启动 Mirror Client 模式并连接到 Host
3. IF 房间号无效或房间不存在 THEN 系统 SHALL 显示"房间不存在"的错误提示
4. IF 房间已满 THEN 系统 SHALL 显示"房间已满"的错误提示
5. WHEN 输入框为空时点击加入 THEN 系统 SHALL 显示"请输入房间号"的提示

---

### 需求 6：LobbyPanel UI 动态生成与管理

**用户故事：** 作为一名玩家，我希望点击 FriendsButton 时能看到联机大厅面板，以便进行联机操作。

#### 验收标准

1. WHEN 玩家点击 RingRadialMenu 下的 FriendsButton THEN 系统 SHALL 在 GamePanel（Canvas 下的面板容器）下动态实例化 LobbyPanel 预制体
2. IF LobbyPanel 已经存在 THEN 系统 SHALL 切换其显示/隐藏状态而非重复创建
3. WHEN LobbyPanel 显示时 THEN 系统 SHALL 展示初始状态界面（包含"创建房间"按钮、房间号输入框、"加入"按钮、"关闭"按钮）
4. WHEN 玩家点击关闭按钮或再次点击 FriendsButton THEN 系统 SHALL 隐藏 LobbyPanel
5. WHEN 玩家按下 Escape 键 THEN 系统 SHALL 隐藏 LobbyPanel

---

### 需求 7：房间内状态显示

**用户故事：** 作为一名玩家，我希望在房间内能看到当前房间状态，以便了解其他玩家的连接情况。

#### 验收标准

1. WHEN 玩家进入房间 THEN 系统 SHALL 切换到房间内视图，显示房间号、玩家数量、邀请好友按钮、离开按钮
2. WHEN 有新玩家加入或离开 THEN 系统 SHALL 实时更新玩家数量显示
3. WHEN 玩家点击"复制房间号"按钮 THEN 系统 SHALL 将房间号复制到系统剪贴板并显示反馈
4. WHEN 玩家点击"离开房间"按钮 THEN 系统 SHALL 断开 Mirror 连接、离开 Steam Lobby 并返回初始状态界面
5. IF Host 玩家离开 THEN 系统 SHALL 通知所有客户端并将其踢回初始状态

---

### 需求 8：Mirror NetworkManager 配置

**用户故事：** 作为一名开发者，我希望有一个统一的 Mirror NetworkManager 管理器，以便集中管理网络生命周期。

#### 验收标准

1. WHEN 联机流程触发 THEN 系统 SHALL 确保场景中存在配置好的 Mirror NetworkManager（使用 FizzySteamworks Transport）
2. WHEN NetworkManager 初始化 THEN 系统 SHALL 注册必要的网络预制体（Player Prefab 等）
3. WHEN Host 启动 THEN 系统 SHALL 通过 Mirror 的 NetworkManager.StartHost() 启动主机
4. WHEN Client 连接 THEN 系统 SHALL 通过 Mirror 的 NetworkManager.StartClient() 连接到主机
5. WHEN 断开连接 THEN 系统 SHALL 正确清理所有网络资源并重置状态

---

### 需求 9：Steam 回调处理

**用户故事：** 作为一名开发者，我希望正确处理 Steam 的各种回调事件，以便系统能响应好友邀请、Lobby 状态变化等事件。

#### 验收标准

1. WHEN 收到 Steam GameLobbyJoinRequested 回调（好友通过 Steam Overlay 接受邀请）THEN 系统 SHALL 自动加入对应 Lobby 并连接到游戏
2. WHEN 收到 LobbyEnter 回调 THEN 系统 SHALL 更新 UI 状态为"已加入房间"
3. WHEN 收到 LobbyChatUpdate 回调（玩家加入/离开）THEN 系统 SHALL 更新玩家列表和数量
4. WHEN 收到 LobbyDataUpdate 回调 THEN 系统 SHALL 同步最新的 Lobby 元数据
5. IF Steam 回调处理过程中发生异常 THEN 系统 SHALL 记录错误日志并显示用户友好的错误提示
