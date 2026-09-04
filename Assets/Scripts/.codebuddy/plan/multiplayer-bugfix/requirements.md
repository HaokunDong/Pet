# 需求文档

## 引言

本需求文档针对联机模式下发现的三个Bug进行修复规划。这些Bug涉及：

1. **房间人数显示不更新**：其他客户端加入房间后，LobbyPanel上的玩家人数文本没有实时更新，但客户端角色已经正常出现在画面中。
2. **客户端Portal点击未发送挑战申请**：联机模式下，客户端点击Portal时没有成功向SpecialLevelListForMultiplayer发送挑战申请。
3. **敌人不攻击其他玩家**：其他客户端加入房间后，敌人（Boss/MinorEnemy）只攻击Host的本地角色，不会攻击其他远程玩家（MirrorCharacter代理）。

### 现状分析

**Bug 1 - 房间人数不更新：**
- `LobbyPanel` 在 `OnEnable()` 中订阅 `SteamLobbyManager.OnPlayerCountChanged` 事件，在 `OnDisable()` 中取消订阅。
- `LobbyPanel` 在 `Awake()` 中调用 `gameObject.SetActive(false)` 隐藏自身，这会触发 `OnDisable()`，导致事件订阅被取消。
- 当其他玩家加入房间时，`SteamLobbyManager.OnLobbyChatUpdate` 会触发 `OnPlayerCountChanged` 事件，但由于 LobbyPanel 处于隐藏状态（已取消订阅），无法接收到人数变化通知。
- 即使之后重新打开 LobbyPanel（触发 `OnEnable()` 重新订阅），`OnEnable()` 中虽然有 `HandlePlayerCountChanged(lobbyMgr.PlayerCount)` 来恢复当前人数，但在面板隐藏期间的实时变化不会被感知。

**Bug 2 - Portal点击未发送挑战申请：**
- `HandleMultiplayerPortalClick()` 方法中，通过 `SpecialLevelListManager.Instance` 或 `FindObjectOfType<SpecialLevelListManager>()` 查找管理器。
- `SpecialLevelListManager` 继承自 `NetworkBehaviour`，需要挂载在带有 `NetworkIdentity` 的场景对象上。
- 客户端可能因为 `SpecialLevelListManager.Instance` 为 null（单例未初始化）、`specialLevelData` 未赋值、`portalId` 为 0、或 `LocalPlayer` 为 null 等原因导致请求失败。
- 需要排查客户端侧的具体失败点，并确保挑战申请能正确发送到服务器。

**Bug 3 - 敌人不攻击其他玩家：**
- `BTFindNearestEnemy.FindNearest()` 中，敌人AI通过 `GameObject.FindGameObjectsWithTag("Player")` 查找目标。
- 代码中明确跳过了 `MirrorCharacterTag` 组件的对象：`if (go.GetComponent<MirrorCharacterTag>() != null) continue;`
- 这意味着在Host端，敌人AI只会攻击Host的本地角色，完全忽略了远程玩家的MirrorCharacter代理。
- 在客户端，敌人是MirrorEnemy（视觉代理），不运行AI逻辑，所以敌人的攻击行为完全由Host端决定。
- 需要让敌人AI能够将MirrorCharacter也作为有效攻击目标，同时确保伤害通过网络正确路由到对应的远程客户端。

---

## 需求

### 需求 1：房间人数显示实时更新

**用户故事：** 作为一名联机玩家，我希望在LobbyPanel上能实时看到房间内的玩家人数变化，以便了解当前有多少人在房间中。

#### 验收标准

1. WHEN 其他玩家加入或离开房间 THEN LobbyPanel SHALL 实时更新玩家人数显示文本（格式为 "Players: X/4"），无论LobbyPanel当前是否处于显示状态。
2. WHEN LobbyPanel从隐藏状态变为显示状态 THEN LobbyPanel SHALL 立即显示当前最新的玩家人数。
3. WHEN LobbyPanel处于隐藏状态（SetActive(false)）THEN 系统 SHALL 确保人数变化事件不会因为OnDisable取消订阅而丢失。
4. IF LobbyPanel的GameObject被禁用 THEN 系统 SHALL 采用不依赖OnEnable/OnDisable的事件订阅机制（如在Awake/OnDestroy中订阅/取消订阅，或使用其他持久化监听方式）来保证事件不丢失。

---

### 需求 2：客户端Portal点击正确发送挑战申请

**用户故事：** 作为一名联机客户端玩家，我希望点击Portal时能成功向SpecialLevelListForMultiplayer发送挑战申请，以便房主可以审批我的挑战请求。

#### 验收标准

1. WHEN 客户端玩家在联机模式下点击Portal THEN 系统 SHALL 成功调用 `SpecialLevelListManager.CmdRequestAddOption()` 向服务器发送挑战申请。
2. WHEN 客户端点击Portal且 `SpecialLevelListManager.Instance` 为 null THEN 系统 SHALL 通过备用方式（如 `FindObjectOfType`）找到管理器，或在找不到时给出明确的用户提示。
3. WHEN 挑战申请成功发送到服务器 THEN SpecialLevelListForMultiplayer面板 SHALL 在所有客户端上显示新增的挑战选项。
4. IF 客户端的 `LocalPlayer` 尚未初始化 THEN 系统 SHALL 等待初始化完成或给出明确的错误提示，而不是静默失败。
5. WHEN 挑战申请被服务器拒绝（如列表已满、已有待处理请求等）THEN 客户端 SHALL 收到拒绝原因的反馈提示。
6. IF Portal的 `specialLevelData` 未赋值或 `portalId` 为 0 THEN 系统 SHALL 在日志中输出明确的警告信息，帮助开发者定位问题。

---

### 需求 3：敌人攻击场上所有玩家

**用户故事：** 作为一名联机玩家，我希望敌人能够攻击场上的所有玩家（包括Host和远程客户端的角色），以便联机战斗体验更加真实和公平。

#### 验收标准

1. WHEN 敌人AI在Host端搜索攻击目标 THEN 系统 SHALL 将所有带 "Player" 标签的活跃角色（包括Host的LocalCharacter和远程玩家的MirrorCharacter）都纳入目标候选列表。
2. WHEN 敌人AI选中MirrorCharacter作为攻击目标 THEN 系统 SHALL 正常执行攻击动画和伤害计算。
3. WHEN 敌人对MirrorCharacter造成伤害 THEN 系统 SHALL 通过 `NetworkDamageHelper` 将伤害路由到对应远程客户端的LocalCharacter上。
4. WHEN 敌人的当前目标死亡或脱离范围 THEN 敌人AI SHALL 能够切换到其他任意存活的玩家（包括MirrorCharacter）作为新目标。
5. IF 场上有多个玩家 THEN 敌人AI SHALL 选择距离最近的玩家作为攻击目标（无论是LocalCharacter还是MirrorCharacter）。
6. WHEN 敌人攻击MirrorCharacter THEN 客户端 SHALL 能看到对应的受击动画和血量变化。
7. WHEN 敌人使用AOE技能 THEN 技能伤害 SHALL 同时作用于范围内的所有玩家（包括MirrorCharacter），并将伤害正确路由到各自的客户端。

---

## 技术约束与边界情况

### 技术约束
- 项目使用 **Mirror** 作为网络框架，**Steamworks** 作为传输层。
- 敌人AI仅在Host端运行，客户端的敌人是MirrorEnemy（视觉代理），不运行AI逻辑。
- Portal是本地UI元素（非网络对象），每个客户端在自己的Canvas上生成Portal实例，通过共享的 `portalId` 进行网络标识。
- `SpecialLevelListManager` 使用Mirror的 `SyncList` 进行数据同步，`Command` 进行客户端到服务器的请求。
- 伤害路由已有现成机制：`NetworkDamageHelper.ApplyDamage()` 能检测 `MirrorCharacterTag` 并将伤害路由到对应客户端。

### 边界情况
- 玩家在战斗中断线：敌人应能正确切换到其他存活玩家。
- 所有远程玩家断线后，敌人应回退到只攻击Host的本地角色。
- MirrorCharacter被销毁（玩家断线）时，敌人AI不应崩溃或卡住。
- 多个敌人同时攻击同一个MirrorCharacter时，伤害路由不应产生竞态条件。

### 成功标准
- Host创建房间后，其他玩家加入时LobbyPanel人数文本实时更新。
- 客户端点击Portal后，SpecialLevelListForMultiplayer面板上出现对应的挑战选项。
- 联机战斗中，敌人能在所有玩家之间切换目标并正常造成伤害。
