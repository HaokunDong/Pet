# 实施计划

- [ ] 1. 集成网络框架与基础设施搭建
   - 通过 Unity Package Manager 安装 Netcode for GameObjects 和 Unity Relay 包
   - 创建 `Assets/Scripts/Network/NetworkBootstrap.cs`，在场景中添加 NetworkManager 组件并配置 Unity Transport + Relay
   - 创建 `Assets/Scripts/Network/RelayManager.cs`，封装 Unity Relay 的创建/加入逻辑（Allocation、JoinCode 生成与解析）
   - 确保现有单人模式不受影响：NetworkManager 仅在联机流程触发时初始化
   - _需求：技术约束 1、3、4、5_

- [ ] 2. 实现联机大厅 UI 面板
   - 创建 `Assets/Prefabs/UI/LobbyPanel.prefab`，包含 Create Lobby 按钮、房间号输入框、Join 按钮、关闭按钮
   - 创建 `Assets/Scripts/UI/LobbyPanel.cs`，管理面板的显示/隐藏逻辑和 UI 交互事件
   - 修改 `Assets/Scripts/View/MainView.cs`，在 Button3 的点击事件中打开 LobbyPanel
   - 实现房间创建成功后的 UI 状态切换：显示房间号、复制按钮、人数显示、Leave Lobby 按钮
   - _需求：1.1、1.2、1.3、2.2、2.3、2.4、2.5_

- [ ] 3. 实现创建房间逻辑
   - 在 `Assets/Scripts/Network/LobbyManager.cs` 中实现 CreateLobby 方法：调用 RelayManager 创建 Allocation，启动 Host，生成 JoinCode
   - 实现复制房间号到剪贴板功能（GUIUtility.systemCopyBuffer）
   - 实现房间创建失败时的错误回调与 UI 提示
   - _需求：2.1、2.2、2.3、2.6_

- [ ] 4. 实现加入房间逻辑
   - 在 `LobbyManager.cs` 中实现 JoinLobby 方法：根据输入的 JoinCode 调用 RelayManager 加入房间，启动 Client
   - 实现输入验证：空输入提示、无效房间号/房间已满的错误处理
   - 加入成功后更新 UI 状态（显示房间信息、Leave 按钮）
   - _需求：3.1、3.2、3.3、3.4、3.5_

- [ ] 5. 实现房间状态管理与游戏启动
   - 在 `LobbyManager.cs` 中监听客户端连接/断开事件，同步房间人数
   - 实现第二位玩家加入后自动加载联机战斗场景（NetworkManager.SceneManager.LoadScene）
   - 实现 Leave Lobby 功能：Host 离开时解散房间通知 Client，Client 离开时通知 Host 更新人数
   - _需求：4.1、4.2、4.3、4.4_

- [ ] 6. 实现联机角色生成与所有权管理
   - 创建 `Assets/Scripts/Network/NetworkPlayerController.cs`（继承 NetworkBehaviour），作为联机角色的网络同步组件
   - 修改 `GameCharacterManager.cs`，在联机模式下通过 NetworkManager 的 PlayerPrefab 机制生成玩家角色
   - 实现 IsOwner 判断：仅本地拥有者启用 ManualController/ControlModeManager 输入控制，远程角色禁用输入
   - _需求：5.1、6.1、6.3_

- [ ] 7. 实现角色状态网络同步
   - 在 `NetworkPlayerController.cs` 中使用 NetworkVariable 同步位置、朝向（flipX）
   - 实现动画状态同步：通过 NetworkVariable 或 RPC 同步当前动画触发器/状态参数
   - 实现技能/攻击动作的 RPC 广播，确保远程客户端播放对应动画和特效
   - _需求：5.2、5.3、5.4_

- [ ] 8. 修改战斗系统实现攻击隔离
   - 修改 `CombatSystem.cs` 中的攻击判定逻辑：在检测攻击目标时排除所有 Tag 为 "Player" 的对象
   - 确保联机模式下所有玩家角色均使用 "Player" Tag，攻击范围检测仅命中 "Enemy" Tag 目标
   - 验证现有的 Physics2D.IgnoreLayerCollision 设置在联机模式下仍然有效
   - _需求：6.2、6.4_

- [ ] 9. 实现敌人网络同步
   - 创建 `Assets/Scripts/Network/NetworkEnemyController.cs`（继承 NetworkBehaviour），用于同步敌人位置、动画、血量
   - 修改 `EnemySpawner.cs`，在联机模式下仅由 Host 执行生成逻辑，通过 NetworkObject.Spawn 同步到 Client
   - 伤害计算统一在 Host 端执行，通过 ClientRpc 同步受击动画和血量变化；死亡时由 Host 调用 NetworkObject.Despawn
   - _需求：7.1、7.2、7.3、7.4_

- [ ] 10. 实现断线处理与网络状态提示
   - 在 `LobbyManager.cs` 中监听 OnClientDisconnectCallback 事件
   - Host 端：检测到 Client 断线时移除对应角色，允许继续单人或返回大厅
   - Client 端：检测到与 Host 断开时显示提示并返回主界面
   - 创建网络状态 UI 指示器，在延迟过高时显示警告图标
   - _需求：8.1、8.2、8.3_
