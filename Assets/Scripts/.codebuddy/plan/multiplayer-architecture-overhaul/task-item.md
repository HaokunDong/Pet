# 实施计划

- [ ] 1. 修复 `maxConnections` 确保运行时支持 4 人连接
  - 在 `MirrorNetworkManager.Awake()` 中显式设置 `maxConnections = 4`
  - 修改 `StartHostWithSteam()` 方法：当复用单人 Host 时，也要确保 `maxConnections` 被更新为 4
  - 添加日志确认 `maxConnections` 的值
  - _需求：1.1、1.5_

- [ ] 2. 修复 `StartHostWithSteam()` 复用单人 Host 的连接问题
  - 当前 `StartHostWithSteam()` 在检测到已有 Host 时直接 `return`，但单人 Host 可能未正确配置为接受远程连接
  - 修改逻辑：复用时确保 FizzySteamworks 传输层处于可接受远程连接的状态（检查 `Transport.active` 是否正确指向 FizzySteamworks）
  - 如果单人 Host 的传输层状态不正确，则先 `StopHost()` 再重新 `StartHost()`
  - _需求：1.1、1.2、1.5_

- [ ] 3. 添加场景同步机制 — 客户端加入时自动加载房主场景
  - 在 `MirrorNetworkManager` 中重写 `OnServerConnect()`，当新客户端连接时，如果当前场景不是 Mirror 的 `networkSceneName`，则调用 `ServerChangeScene()` 或通过 RPC 通知客户端加载正确场景
  - 由于当前项目只有 `Loading` → `Game` 两个场景，且 `Loading` 是资源加载场景，实际游戏场景只有 `Game`，因此：在 `OnServerAddPlayer()` 中确保客户端已在 `Game` 场景（如果不在则通知加载）
  - 在 `MirrorNetworkManager` 中添加 `[SyncVar]` 或公共属性 `currentGameScene` 跟踪当前场景名
  - _需求：2.1、2.3、2.5_

- [ ] 4. 添加场景切换网络同步 — 房主切换场景时同步所有客户端
  - 在 `MirrorNetworkManager` 中添加 `public void ChangeSceneForAll(string sceneName)` 方法，内部调用 Mirror 的 `ServerChangeScene(sceneName)`
  - 重写 `OnServerSceneChanged()` 和 `OnClientSceneChanged()` 回调，在场景切换完成后重新初始化网络对象（`NetworkEnemySpawner`、`BossFightManager`、`SpecialLevelListManager` 等）
  - 在 `OnClientSceneChanged()` 中重新注册本地角色（调用 `NetworkPlayer.RegisterLocalCharacterDelayed()`）
  - 修改 `Loading.cs`：如果是联机模式，不使用 `SceneManager.LoadScene("Game")`，而是由 `MirrorNetworkManager` 统一管理场景切换
  - _需求：4.1、4.3、4.5_

- [ ] 5. 完善 `OnServerAddPlayer` 的 late joiner 支持
  - 当前已有 `NetworkEnemySpawner.SyncAllEnemiesToNewClient()` 同步敌人
  - 增加同步当前 Boss 战斗状态：如果 `BossFightManager.IsBossFightActive` 为 true，通过 `TargetRpc` 通知新客户端启动 Boss 战斗
  - 增加同步所有已连接玩家的 MirrorCharacter：新客户端加入后，已有的 `NetworkPlayer` 的 `SyncVar` 会自动同步（Mirror 内置机制），但需要确认 `OnHasCharacterChanged` hook 能正确触发 `CreateMirrorCharacterDelayed`
  - _需求：1.6、5.6_

- [ ] 6. 确保玩家状态在场景切换后正确恢复
  - 在 `NetworkPlayer.OnStartClient()` 中增加场景切换后的重新初始化逻辑：检测 `MirrorCharacter` 是否需要重新创建
  - 在 `MirrorNetworkManager.OnClientSceneChanged()` 中触发所有 `NetworkPlayer` 重新注册本地角色
  - 确保 `GameCharacterManager` 在新场景中正确初始化后，`NetworkPlayer` 能找到并注册角色
  - _需求：3.1、3.2、3.3、3.5、4.3_

- [ ] 7. 确保敌人/Boss 生成和销毁在所有客户端同步
  - 验证 `NetworkEnemySpawner.ScanAndSyncEnemies()` 在场景切换后能正确重新扫描新场景中的敌人
  - 在 `NetworkEnemySpawner.OnEnable()` 中清理旧的 `serverEnemies` 和 `clientMirrorEnemies` 字典，避免场景切换后残留旧数据
  - 验证 `BossFightManager.RpcStartBossFightOnClients()` 在所有客户端上正确执行（包括 late joiner）
  - _需求：5.1、5.2、5.3、5.7_

- [ ] 8. 完善投射物和召唤物的网络同步
  - 验证 `NetworkPlayer.RpcSpawnProjectile()` 在所有客户端上正确显示投射物视觉效果
  - 验证 `NetworkPlayer.RpcSpawnSummon()` 在所有客户端上正确显示召唤物
  - 确保投射物和召唤物在场景切换后被正确清理
  - _需求：5.4、5.5_

- [ ] 9. 添加 Host 断线处理和客户端清理
  - 在 `MirrorNetworkManager.OnClientDisconnect()` 中：如果是非主动断线，清理所有 MirrorCharacter 和 MirrorEnemy，重置游戏状态
  - 在 `LobbyPanel.HandleHostDisconnected()` 中：确保 UI 正确回到初始状态
  - 在 `MirrorNetworkManager.OnStopServer()` 中：清理所有 `ConnectedPlayers`，重置 `NetworkEnemySpawner` 状态
  - _需求：2.4、4.4_

- [ ] 10. 集成测试验证 — 确保所有同步机制协同工作
  - 验证 4 人同时加入房间，LobbyPanel 人数实时更新（需求 1.2、1.3）
  - 验证客户端加入后自动加载 Game 场景并看到房主的角色和敌人（需求 2.1、2.3）
  - 验证所有玩家的位置、动画、血量、技能实时同步（需求 3.1-3.8）
  - 验证房主切换场景时所有客户端跟随（需求 4.1-4.3）
  - 验证敌人/Boss 生成和销毁在所有客户端一致（需求 5.1-5.3）
  - 验证玩家断线后其他玩家不受影响（需求 4.4）
  - _需求：全部_
