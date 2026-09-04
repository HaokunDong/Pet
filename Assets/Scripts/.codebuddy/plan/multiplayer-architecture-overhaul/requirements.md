# 需求文档：联机架构整体改造

## 引言

本需求文档针对当前联机系统存在的根本性架构问题进行整体改造规划。当前系统存在以下核心问题：

1. **第二个客户端无法加入房间**：`MirrorNetworkManager.StartHostWithSteam()` 在检测到已有单人 Host 运行时直接复用（`return`），但单人 Host 的 `maxConnections` 可能未正确设置为 4，或者 FizzySteamworks 传输层在单人模式下未正确监听远程连接。
2. **房间人数不显示变化**：`LobbyPanel` 的事件订阅在 `OnDisable()` 中被取消（已在前一轮修复）。
3. **缺少场景同步机制**：当前没有使用 Mirror 的 `ServerChangeScene()` 进行场景切换同步，房主切换场景/关卡时客户端不会跟随。
4. **物体生成/销毁同步不完整**：敌人通过自定义消息系统同步（`NetworkEnemySpawner`），但其他动态物体（如 Boss、召唤物、投射物）的同步存在不一致。
5. **玩家状态同步存在延迟和遗漏**：部分状态（如技能释放、特效、UI 交互）未完全同步。

### 当前架构概览

| 组件 | 职责 | 当前状态 |
|------|------|----------|
| `SteamLobbyManager` | Steam 大厅管理（创建/加入/离开） | ✅ 已支持 4 人（`MAX_PLAYERS = 4`） |
| `MirrorNetworkManager` | Mirror 网络生命周期管理 | ⚠️ `maxConnections` 仅在 Editor 脚本中设置为 4，运行时未显式设置 |
| `NetworkPlayer` | 玩家代理，同步角色信息/位置/动画/血量 | ⚠️ 基本功能可用，但缺少部分状态同步 |
| `NetworkEnemySpawner` | 敌人同步（自定义消息系统） | ⚠️ 功能可用但不使用 Mirror 标准 Spawn |
| `NetworkCharacterSync` | 角色位置/血量/动画同步 | ✅ 基本可用 |
| `NetworkDamageHelper` | 伤害路由（MirrorCharacter → 远程客户端） | ✅ 已支持 |
| `BossFightManager` | Boss 战斗流程管理 | ⚠️ 使用 `[ClientRpc]` 通知客户端，但场景状态不完全同步 |
| `SpecialLevelListManager` | 挑战申请列表（SyncList） | ✅ 基本可用 |
| 场景同步 | 房主切换场景时同步所有客户端 | ❌ 完全缺失 |

---

## 需求

### 需求 1：房间支持最多四人同时加入

**用户故事：** 作为一名玩家，我希望创建的房间最多能容纳四名玩家，所有人都能成功加入并正常游玩，以便与朋友一起联机。

#### 验收标准

1. WHEN 房主创建房间 THEN 系统 SHALL 确保 `MirrorNetworkManager.maxConnections` 在运行时被设置为 4（或 `SteamLobbyManager.MAX_PLAYERS`），无论是从单人模式复用 Host 还是新启动 Host。
2. WHEN 第 2、3、4 个客户端尝试加入房间 THEN 系统 SHALL 允许连接成功，并为每个客户端创建对应的 `NetworkPlayer` 实例。
3. WHEN 任意客户端加入或离开房间 THEN `LobbyPanel` SHALL 实时更新人数显示（格式为 "Players: X/4"）。
4. WHEN 房间已满（4人）且第 5 个客户端尝试加入 THEN 系统 SHALL 拒绝连接并返回 "Room is full" 错误。
5. IF `MirrorNetworkManager.StartHostWithSteam()` 复用了单人模式的 Host THEN 系统 SHALL 确保 `maxConnections` 被更新为 4，且传输层（FizzySteamworks）能接受远程连接。
6. WHEN 新客户端加入时 THEN 系统 SHALL 将当前场景中所有已存在的敌人、Boss、玩家角色等状态同步给新加入的客户端（late joiner support）。

---

### 需求 2：所有客户端加入房主的场景

**用户故事：** 作为一名加入房间的客户端玩家，我希望自动加载房主当前所在的场景，以便与房主在同一个游戏世界中互动。

#### 验收标准

1. WHEN 客户端成功连接到房主 THEN 客户端 SHALL 自动加载房主当前所在的场景（使用 Mirror 的 `ServerChangeScene` 或等效机制）。
2. WHEN 客户端正在加载场景时 THEN 系统 SHALL 显示加载界面，防止玩家在场景未就绪时操作。
3. WHEN 客户端场景加载完成 THEN 系统 SHALL 在正确的出生点生成客户端的角色，并同步当前场景中所有已存在的实体状态。
4. IF 客户端在加载场景过程中断线 THEN 系统 SHALL 正确清理资源并允许重新连接。
5. WHEN 房主当前在 "Game" 场景 THEN 新加入的客户端 SHALL 直接加载 "Game" 场景（而非从 "Loading" 场景开始）。

---

### 需求 3：房主和所有玩家的状态、操作、信息实时同步

**用户故事：** 作为一名联机玩家，我希望能实时看到所有其他玩家的位置、动画、血量、技能释放等状态，以便获得流畅的联机体验。

#### 验收标准

1. WHEN 任意玩家移动 THEN 所有其他客户端 SHALL 在 50ms 内看到该玩家的位置更新（通过插值平滑显示）。
2. WHEN 任意玩家释放技能或普通攻击 THEN 所有其他客户端 SHALL 看到对应的攻击动画和特效。
3. WHEN 任意玩家受到伤害 THEN 所有其他客户端 SHALL 看到该玩家的血量变化和受击动画。
4. WHEN 任意玩家死亡 THEN 所有其他客户端 SHALL 看到死亡动画。
5. WHEN 任意玩家切换角色 THEN 所有其他客户端 SHALL 看到角色切换（旧角色消失，新角色出现）。
6. WHEN 任意玩家被击退（Knockback）THEN 所有其他客户端 SHALL 看到平滑的击退效果（而非抖动）。
7. WHEN 敌人攻击任意玩家（包括 MirrorCharacter）THEN 伤害 SHALL 通过 `NetworkDamageHelper` 正确路由到对应客户端的 `LocalCharacter`。
8. WHEN 任意玩家对敌人造成伤害 THEN 所有其他客户端 SHALL 看到敌人的血量变化和受击动画。
9. IF 网络延迟超过 200ms THEN 系统 SHALL 使用插值和预测机制保证视觉流畅性，避免明显的卡顿或瞬移。

---

### 需求 4：房主切换场景/关卡时同步所有玩家

**用户故事：** 作为房主，我希望在切换场景或进入新关卡时，房间内所有玩家都能自动跟随切换到同一场景，以便大家始终在同一个游戏世界中。

#### 验收标准

1. WHEN 房主触发场景切换（如进入新关卡、返回主场景等）THEN 系统 SHALL 使用 Mirror 的 `ServerChangeScene()` 将所有已连接的客户端同步切换到目标场景。
2. WHEN 场景切换开始 THEN 所有客户端 SHALL 显示加载界面。
3. WHEN 场景切换完成 THEN 所有客户端 SHALL 在新场景中重新生成角色，并恢复网络同步状态。
4. WHEN 场景切换过程中有客户端断线 THEN 系统 SHALL 不阻塞其他客户端的场景切换流程。
5. IF 目标场景中有需要网络同步的对象（如 `BossFightManager`、`SpecialLevelListManager`、`NetworkEnemySpawner`）THEN 这些对象 SHALL 在场景加载后自动重新初始化并建立网络连接。

---

### 需求 5：房主生成/销毁物体时实时同步所有玩家

**用户故事：** 作为一名联机玩家，我希望房主生成或销毁的所有游戏物体（敌人、Boss、投射物、召唤物等）都能实时同步到我的画面中，以便所有人看到一致的游戏世界。

#### 验收标准

1. WHEN 房主生成新敌人 THEN 所有客户端 SHALL 在自己的场景中看到对应的 MirrorEnemy 出现。
2. WHEN 房主生成 Boss THEN 所有客户端 SHALL 看到 Boss 出现并开始战斗。
3. WHEN 敌人或 Boss 被击杀 THEN 所有客户端 SHALL 看到死亡动画并在延迟后移除该实体。
4. WHEN 任意玩家释放投射物技能 THEN 所有其他客户端 SHALL 看到投射物的飞行轨迹（视觉效果）。
5. WHEN 任意玩家释放召唤技能 THEN 所有其他客户端 SHALL 看到召唤物出现。
6. WHEN 新客户端中途加入（late joiner）THEN 系统 SHALL 将当前场景中所有已存在的动态物体状态同步给新客户端。
7. IF 物体在同步过程中被销毁 THEN 系统 SHALL 正确处理竞态条件，不产生空引用异常。

---

## 技术约束与边界情况

### 技术约束

- 项目使用 **Mirror** 作为网络框架，**FizzySteamworks** 作为传输层（Steam P2P）。
- 当前架构采用 **Host-Client 模式**（房主同时是服务器和客户端）。
- 敌人 AI 仅在 Host 端运行，客户端的敌人是 MirrorEnemy（视觉代理）。
- 玩家角色不使用 `NetworkIdentity`，而是通过 `NetworkPlayer`（有 `NetworkIdentity`）代理同步。
- Portal 是本地 UI 元素，不通过 Mirror 的 `NetworkServer.Spawn` 生成。
- 当前场景结构：`Loading` → `Game`，没有多场景/多关卡的网络切换。
- `MirrorNetworkManager` 在 `Start()` 中自动启动单人 Host，联机时复用或重启。
- `maxConnections` 仅在 Editor 脚本（`CreateMirrorNetworkManagerPrefab.cs`）中设置为 4，运行时的 `MirrorNetworkManager.Awake()` 中未显式设置。

### 边界情况

- **单人模式 Host 复用**：`StartHostWithSteam()` 检测到已有 Host 时直接 return，需确保此时 `maxConnections` 已正确设置。
- **Late Joiner**：新客户端加入时需要同步所有已存在的敌人、Boss、玩家角色、战斗状态。
- **玩家断线重连**：断线后重新加入应能恢复到当前游戏状态。
- **Host 断线**：Host 断线后所有客户端应收到通知并返回大厅。
- **场景切换期间的消息**：场景切换过程中的网络消息需要正确缓冲或丢弃。
- **多人同时操作**：多个玩家同时攻击同一敌人、同时释放技能等并发场景。
- **性能**：4 人联机时的网络带宽和同步频率需要合理控制。

### 成功标准

- 4 个客户端都能成功加入同一房间，LobbyPanel 实时显示人数。
- 所有客户端加载相同的场景，看到一致的游戏世界。
- 所有玩家的位置、动画、血量、技能等状态实时同步。
- 房主切换场景时所有客户端自动跟随。
- 敌人、Boss、投射物、召唤物等动态物体在所有客户端上一致显示。
- 玩家断线/重连不会导致游戏崩溃或状态不一致。
