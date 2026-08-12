# 实施计划

- [ ] 1. 删除现有 Unity Netcode 联机架构文件
   - 删除 `Scripts/Network/` 目录下所有文件：`NetworkBootstrap.cs`、`RelayManager.cs`、`LobbyManager.cs`、`NetworkPlayerController.cs`、`NetworkPlayerSetup.cs`、`NetworkEnemyController.cs`、`NetworkStatusUI.cs` 及其 `.meta` 文件
   - 删除 `Scripts/Editor/CreateNetworkPlayerPrefab.cs` 及其 `.meta` 文件（旧网络玩家预制体创建工具）
   - 删除现有的 `Scripts/UI/LobbyPanel.cs`（依赖旧 Unity.Netcode 架构）
   - 清理项目中所有对 `Unity.Netcode`、`Unity.Services.Relay`、`Unity.Services.Authentication`、`Unity.Netcode.Transports.UTP` 命名空间的引用
   - _需求：1.1、1.2、1.3_

- [ ] 2. 引入 FizzySteamworks 传输层
   - 将 FizzySteamworks 包（GitHub: Chykary/FizzySteamworks）导入到项目 `Assets/` 目录下
   - 确保 FizzySteamworks 能正确引用项目中已有的 Mirror 库和 Steamworks.NET
   - 验证编译无错误
   - _需求：2.1、2.3_

- [ ] 3. 创建 SteamLobbyManager.cs — Steam Lobby 核心管理器
   - 在 `Scripts/Network/` 目录下创建 `SteamLobbyManager.cs`，使用单例模式（继承 `SingletonMono<T>`）
   - 实现 Steam Lobby 创建功能：调用 `SteamMatchmaking.CreateLobby()`，设置 Lobby 类型为 `ELobbyType.k_ELobbyTypePublic`，最大玩家数为 2
   - 实现房间号生成逻辑：将 Steam Lobby ID（CSteamID）转换为可分享的字符串房间号
   - 实现通过房间号加入 Lobby：将房间号解析回 CSteamID，调用 `SteamMatchmaking.JoinLobby()`
   - 实现好友邀请功能：调用 `SteamFriends.ActivateGameOverlayInviteDialog()` 打开 Steam Overlay 邀请界面
   - 实现离开 Lobby 功能：调用 `SteamMatchmaking.LeaveLobby()`
   - 定义事件回调：`OnLobbyCreated`、`OnLobbyEntered`、`OnLobbyJoinFailed`、`OnPlayerCountChanged`、`OnHostDisconnected`
   - _需求：3.1、3.2、3.4、3.5、4.1、5.1_

- [ ] 4. 实现 Steam 回调注册与处理
   - 在 `SteamLobbyManager.cs` 中注册 Steamworks Callback：`Callback<LobbyCreated_t>`、`Callback<LobbyEnter_t>`、`Callback<GameLobbyJoinRequested_t>`、`Callback<LobbyChatUpdate_t>`、`Callback<LobbyDataUpdate_t>`
   - 处理 `GameLobbyJoinRequested_t`：当好友通过 Steam Overlay 接受邀请时，自动加入对应 Lobby 并启动 Mirror Client
   - 处理 `LobbyEnter_t`：更新 UI 状态，触发 `OnLobbyEntered` 事件
   - 处理 `LobbyChatUpdate_t`：检测玩家加入/离开，更新玩家数量，触发 `OnPlayerCountChanged` 事件
   - 处理 Host 断开：检测 Lobby Owner 变化或 Lobby 关闭，通知客户端并清理连接
   - 添加异常处理和错误日志记录
   - _需求：9.1、9.2、9.3、9.4、9.5_

- [ ] 5. 创建 MirrorNetworkManager.cs — Mirror 网络管理器封装
   - 在 `Scripts/Network/` 目录下创建 `MirrorNetworkManager.cs`，继承 Mirror 的 `NetworkManager`
   - 配置 FizzySteamworks 作为默认 Transport（在 Awake 中动态添加或在预制体中预配置）
   - 实现 `StartHostWithSteam()` 方法：创建 Lobby 成功后调用 `StartHost()`
   - 实现 `StartClientWithSteam(CSteamID hostSteamId)` 方法：设置 Transport 的目标地址为 Host 的 SteamID，调用 `StartClient()`
   - 重写 `OnServerDisconnect()`、`OnClientDisconnect()` 等回调，处理断开连接时的清理逻辑
   - 添加 Steam 未初始化时的检查和错误提示
   - _需求：2.1、2.2、8.1、8.3、8.4、8.5_

- [ ] 6. 重写 LobbyPanel.cs — 基于 Mirror+Steamworks 的大厅面板 UI
   - 在 `Scripts/UI/LobbyPanel.cs` 中重新实现 `LobbyPanel` 类（命名空间 `PetGame.UI`）
   - 保留现有的 UI 字段结构（`initialPanel`、`lobbyPanel`、`createLobbyButton`、`joinCodeInput`、`joinButton`、`closeButton`、`lobbyCodeText`、`copyCodeButton`、`playerCountText`、`leaveLobbyButton`、`statusText`、`errorText`）
   - 新增"邀请好友"按钮（`inviteFriendsButton`）的引用和事件绑定
   - 将所有网络操作改为调用 `SteamLobbyManager` 和 `MirrorNetworkManager`
   - 实现 `Show()`/`Hide()` 方法，Escape 键关闭面板
   - 实现初始状态视图和房间内状态视图的切换逻辑
   - 订阅 `SteamLobbyManager` 的事件来更新 UI（房间号显示、玩家数量、错误提示）
   - 实现输入验证：空房间号提示、房间不存在提示、房间已满提示
   - _需求：3.2、3.3、3.4、4.1、4.4、5.2、5.3、5.4、5.5、6.3、6.4、6.5、7.1、7.2、7.3、7.4、7.5_

- [ ] 7. 修改 MainView.cs — 更新 FriendsButton 交互逻辑
   - 修改 `OnFriendsButtonClick()` 方法：改为动态实例化 LobbyPanel 预制体到 GamePanel 下（使用 `Resources.Load` + `Instantiate`）
   - 添加 LobbyPanel 实例缓存逻辑：首次点击创建，后续点击切换显示/隐藏
   - 移除 `[SerializeField] private LobbyPanel lobbyPanel` 字段（改为动态引用）
   - 确保 `using PetGame.Network` 引用更新为新的命名空间
   - _需求：6.1、6.2、6.4_

- [ ] 8. 更新 LobbyPanel 预制体编辑器脚本
   - 修改 `Scripts/Editor/CreateLobbyPanelPrefab.cs`，更新预制体创建逻辑以匹配新的 LobbyPanel 组件结构
   - 添加"邀请好友"按钮到预制体布局中
   - 确保预制体保存到 `Resources/Prefabs/UI/LobbyPanel.prefab` 路径，以支持 `Resources.Load` 动态加载
   - _需求：6.1、6.3、7.1_

- [ ] 9. 创建 MirrorNetworkManager 预制体及场景配置
   - 创建编辑器脚本或手动配置：生成包含 `MirrorNetworkManager` + `FizzySteamworks` Transport 组件的预制体
   - 配置 Player Prefab 注册（可暂时使用空的 NetworkIdentity 预制体作为占位）
   - 确保 `SteamManager`（已存在于项目中）在游戏启动时正确初始化
   - 确保 `SteamLobbyManager` 在需要时自动创建（DontDestroyOnLoad）
   - _需求：8.1、8.2_

- [ ] 10. 集成测试与单人模式兼容性验证
   - 验证在 Steam 客户端未运行时，游戏单人模式功能正常（不崩溃、不报错）
   - 验证点击 FriendsButton 时，如果 Steam 未初始化，显示友好错误提示而非崩溃
   - 验证创建房间 → 显示房间号 → 邀请好友 → 好友加入的完整流程
   - 验证通过房间号加入的完整流程
   - 验证 Host 离开时客户端正确处理断开
   - 确保编译无错误、无遗留的旧 Unity.Netcode 引用
   - _需求：1.4、2.2、3.3、4.2、4.3、5.3、5.4、7.5_
