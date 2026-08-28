# 实施计划

- [ ] 1. 创建 SpecialLevelData ScriptableObject 数据定义
   - 在 `Scripts/Data/` 目录下新建 `SpecialLevelData.cs`
   - 定义字段：`LevelImage`（Sprite）、`RewardDescription`（string）
   - 添加 `[CreateAssetMenu]` 特性，支持在 Unity Editor 中通过 Create 菜单创建资产
   - 在 `PortalController` 中添加 `SpecialLevelData` 的序列化引用字段，使每个 Portal 可关联对应数据
   - _需求：1.1、1.2、1.3_

- [ ] 2. 创建 SpecialLevelListForMultiplayer 网络管理器（核心逻辑）
   - 在 `Scripts/Network/` 目录下新建 `SpecialLevelListManager.cs`，继承 Mirror 的 `NetworkBehaviour`
   - 定义网络同步数据结构 `SpecialLevelOptionData`（包含：Portal引用NetId、申请者NetId、SpecialLevelData索引）
   - 使用 `SyncList<SpecialLevelOptionData>` 管理当前Option列表，实现自动同步
   - 实现每人申请限制逻辑：维护一个已申请玩家的集合，添加/移除时更新
   - 实现 `[Command] CmdRequestAddOption()`：客户端请求添加Option，服务器验证（列表未满、该玩家无未处理申请、Boss战未进行中）后添加到SyncList
   - 实现 `[Command] CmdApproveOption(int index)`：房主批准Option，服务器验证房主身份后调用 `BossFightManager.StartBossFight()`，从SyncList移除该Option，解除申请者限制
   - 实现 `[Command] CmdRejectOption(int index)`：房主拒绝Option，服务器验证后从SyncList移除该Option，解除申请者限制，保留Portal
   - 实现玩家断线时自动移除其Option的逻辑（监听 Mirror 的 `OnServerDisconnect` 或 `OnStopClient`）
   - 实现Portal销毁时自动移除关联Option的逻辑
   - _需求：3.1、3.4、3.5、3.6、4.3、4.4、4.5、5.1、5.2、5.3、5.4、5.5、6.1、6.2、6.3、6.4、6.5、6.6_

- [ ] 3. 创建 SpecialLevelListForMultiplayer UI 界面控制器
   - 在 `Scripts/UI/` 目录下新建 `SpecialLevelListView.cs`
   - 引用界面预制体中的4个Option槽位（已创建占位）
   - 每个Option槽位包含：LevelImage（Image组件）、RewardDescription（Text组件）、YesButton、NoButton
   - 监听 `SpecialLevelListManager` 的 SyncList 回调（`Callback` 事件），当列表变化时刷新UI显示
   - 根据 `SteamLobbyManager.IsHost` 控制 YesButton/NoButton 的可交互状态（非房主禁用）
   - YesButton 点击时调用 `SpecialLevelListManager.CmdApproveOption()`
   - NoButton 点击时调用 `SpecialLevelListManager.CmdRejectOption()`
   - 界面默认不可见（`gameObject.SetActive(false)`）
   - _需求：2.1、2.4、3.3、4.1、4.2_

- [ ] 4. 在 MainView 中绑定 SpecialLevelListButton 按钮
   - 在 `MainView.OnAwake()` 中使用 `BindRingButton("SpecialLevelListButton", OnSpecialLevelListButtonClick)` 绑定按钮
   - 实现 `OnSpecialLevelListButtonClick()` 方法：检查 `SteamLobbyManager.Instance.InLobby`，仅联机状态下切换 SpecialLevelListForMultiplayer 界面的可见性
   - 非联机状态下点击不响应（可选：给出提示）
   - _需求：2.2、2.3_

- [ ] 5. 修改 PortalController 的点击逻辑
   - 修改 `PortalController.OnPortalClicked()` 方法
   - 添加联机状态判断：`SteamLobbyManager.Instance.InLobby`
   - 联机模式下：调用 `SpecialLevelListManager` 的 `CmdRequestAddOption()`，传入当前Portal信息
   - 非联机模式下：保持现有行为（直接调用 `BossFightManager.StartBossFight()`）
   - _需求：3.1、3.2_

- [ ] 6. 实现 Boss 战期间的边界逻辑
   - 在 `SpecialLevelListManager` 的 `CmdRequestAddOption()` 中检查 `BossFightManager.IsBossFightActive`，为 true 时拒绝添加
   - 在 `CmdApproveOption()` 中检查 `IsBossFightActive`，为 true 时拒绝开启新关卡
   - 房主点击 YesButton 开始Boss战时，仅移除被选中的Option，保留其他Option不变
   - _需求：6.1、6.2、6.3_

- [ ] 7. 实现 Boss 战胜利后的清理逻辑
   - 在 `BossFightManager.OnBossDeath()` 中（或通过 `OnBossDefeated` 事件），通知 `SpecialLevelListManager` 清除对应Option和Portal
   - 仅清除与当前Boss战对应的Option和Portal实体，不影响其他Option
   - 解除该申请者的申请限制
   - _需求：6.4_

- [ ] 8. 实现玩家断线和Portal销毁的自动清理
   - 在 `SpecialLevelListManager` 中监听玩家断线事件（Override `OnServerDisconnect` 或订阅 Mirror 的 `NetworkServer.OnDisconnectedEvent`）
   - 玩家断线时，遍历SyncList移除该玩家的所有Option
   - 在 Portal 的 `OnDestroy()` 中通知 `SpecialLevelListManager` 移除关联Option
   - _需求：6.5、6.6_

- [ ] 9. 新玩家加入时的状态同步
   - 利用 Mirror SyncList 的自动同步机制，新玩家连接时自动接收当前列表状态
   - 在 `SpecialLevelListView` 的 `OnStartClient()` 或 `OnEnable()` 中，根据当前 SyncList 内容初始化UI显示
   - 确保新加入玩家的 YesButton/NoButton 交互状态正确（根据是否为房主）
   - _需求：5.4_
