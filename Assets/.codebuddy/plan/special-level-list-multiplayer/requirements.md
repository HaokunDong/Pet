# 需求文档

## 引言

本功能为联机模式下新增一个"特殊关卡申请列表"界面（SpecialLevelListForMultiplayer）。在联机状态下，当任意玩家点击桌面上的传送门（Portal）时，不再直接生成Boss关卡，而是向该列表界面中添加一条申请记录（Option）。房主可以通过该界面决定接受或拒绝某个关卡申请。该界面的数据在房间内所有玩家之间同步。

### 核心概念

- **SpecialLevelListForMultiplayer**：联机专用的关卡申请列表UI界面，默认不可见，最多显示4个Option
- **SpecialLevelData**：ScriptableObject数据资产，包含关卡图片（LevelImage）和奖励描述（RewardDescription）
- **Option**：列表中的单条申请项，展示对应Portal的SpecialLevelData信息，包含YesButton和NoButton
- **房主权限**：仅房主可以点击YesButton/NoButton来决定是否开启关卡

### 现有系统关联

- `PortalController.OnPortalClicked()` — 当前直接调用 `BossFightManager.StartBossFight()`，联机模式下需改为添加申请
- `SteamLobbyManager.InLobby` / `SteamLobbyManager.IsHost` — 判断联机状态和房主身份
- `RingRadialMenu` 中已有 `SpecialLevelListButton` 按钮节点
- `MainView.BindRingButton()` — 绑定环形菜单按钮回调

---

## 需求

### 需求 1：SpecialLevelData 数据定义

**用户故事：** 作为一名开发者，我希望有一个 SpecialLevelData 数据结构来存储关卡信息，以便每个Portal可以关联对应的关卡展示数据。

#### 验收标准

1. WHEN 创建 SpecialLevelData 资产 THEN 系统 SHALL 提供 LevelImage（Sprite类型）字段用于存储关卡图片
2. WHEN 创建 SpecialLevelData 资产 THEN 系统 SHALL 提供 RewardDescription（string类型）字段用于存储奖励描述文本
3. IF SpecialLevelData 为 ScriptableObject THEN 系统 SHALL 支持在 Unity Editor 中通过 Create 菜单创建该资产

---

### 需求 2：SpecialLevelListForMultiplayer 界面显示控制

**用户故事：** 作为一名玩家，我希望在联机状态下能通过RingRadialMenu中的SpecialLevelListButton打开关卡申请列表界面，以便查看当前待审批的关卡申请。

#### 验收标准

1. WHEN 游戏启动 THEN SpecialLevelListForMultiplayer界面 SHALL 默认处于不可见状态
2. IF 玩家处于联机状态（SteamLobbyManager.InLobby == true）AND 点击 SpecialLevelListButton THEN 系统 SHALL 显示/切换 SpecialLevelListForMultiplayer 界面的可见性
3. IF 玩家未处于联机状态（SteamLobbyManager.InLobby == false）AND 点击 SpecialLevelListButton THEN 系统 SHALL 不响应该点击（界面不打开）
4. WHEN SpecialLevelListForMultiplayer界面可见 THEN 系统 SHALL 最多显示4个Option项

---

### 需求 3：联机模式下Portal点击行为变更

**用户故事：** 作为一名联机玩家，我希望点击传送门后不直接开始Boss战，而是向关卡列表中提交一条申请，以便房主统一决策选择哪个关卡。

#### 验收标准

1. IF 玩家处于联机状态 AND 点击桌面上的Portal THEN 系统 SHALL 不直接调用 BossFightManager.StartBossFight()，而是向 SpecialLevelListForMultiplayer 界面添加一条对应的 Option
2. IF 玩家未处于联机状态 AND 点击桌面上的Portal THEN 系统 SHALL 保持现有行为（直接触发Boss战）
3. WHEN 向列表添加Option THEN 系统 SHALL 从该Portal关联的 SpecialLevelData 中读取 LevelImage 和 RewardDescription 并展示在 Option 中
4. IF 列表中已有4个Option AND 玩家点击新的Portal THEN 系统 SHALL 拒绝添加并给出提示（列表已满）
5. IF 该玩家当前已有一个未处理的申请（Option）在列表中 AND 该玩家点击另一个Portal THEN 系统 SHALL 拒绝添加并给出提示（每人一次只能申请一个Portal关卡）
6. WHEN 该玩家的申请被房主通过（YesButton）或拒绝（NoButton）后 THEN 系统 SHALL 解除该玩家的申请限制，允许其再次申请新的Portal关卡

---

### 需求 4：房主审批机制（YesButton / NoButton）

**用户故事：** 作为房主，我希望能通过YesButton和NoButton来决定是否开启某个关卡，以便我可以为房间内所有玩家选择最合适的关卡。

#### 验收标准

1. IF 当前玩家是房主（SteamLobbyManager.IsHost == true）THEN 系统 SHALL 允许点击 Option 中的 YesButton 和 NoButton
2. IF 当前玩家不是房主 THEN 系统 SHALL 禁止点击 YesButton 和 NoButton（按钮不可交互或不响应）
3. WHEN 房主点击某个Option的 YesButton THEN 系统 SHALL 调用 BossFightManager.StartBossFight() 生成对应的Boss关卡，并传入对应Portal引用，同时解除该申请者的申请限制
4. WHEN 房主点击某个Option的 NoButton THEN 系统 SHALL 从 SpecialLevelListForMultiplayer 界面中移除该 Option，同时解除该申请者的申请限制
5. WHEN 房主点击 NoButton 移除 Option THEN 系统 SHALL 不销毁桌面上对应的传送门Portal（Portal保留，玩家可再次点击申请）

---

### 需求 5：网络同步

**用户故事：** 作为一名联机玩家，我希望SpecialLevelListForMultiplayer界面中的信息在房间内所有玩家之间实时同步，以便所有人都能看到当前的关卡申请状态。

#### 验收标准

1. WHEN 任意玩家点击Portal添加Option THEN 系统 SHALL 通过网络将该Option信息同步到房间内所有玩家的 SpecialLevelListForMultiplayer 界面
2. WHEN 房主点击 YesButton 接受某个Option THEN 系统 SHALL 通过网络通知所有玩家移除该Option并开始Boss战
3. WHEN 房主点击 NoButton 拒绝某个Option THEN 系统 SHALL 通过网络通知所有玩家从界面中移除该Option
4. WHEN 新玩家加入房间 THEN 系统 SHALL 将当前已有的Option列表同步给新加入的玩家
5. IF 网络通信使用 Mirror 框架 THEN 系统 SHALL 通过 Command（客户端→服务器）和 ClientRpc/SyncList（服务器→客户端）实现数据同步

---

### 需求 6：边界情况处理

**用户故事：** 作为一名玩家，我希望系统能正确处理各种异常情况，以便游戏体验不会因为网络或状态问题而中断。

#### 验收标准

1. IF Boss战正在进行中 AND 玩家点击Portal THEN 系统 SHALL 拒绝添加新的Option并给出提示（当前已有Boss战在进行）
2. IF Boss战正在进行中 THEN 系统 SHALL 保留列表中当前所有已有的Option（不清除），同时禁止开启新的Portal关卡
3. WHEN 房主点击 YesButton 开始Boss战 THEN 系统 SHALL 仅移除被选中的该Option，保留列表中其他Option不变
4. WHEN Portal关卡胜利（Boss被击败）后 THEN 系统 SHALL 仅清除对应的Option和对应的Portal实体（桌面上的传送门），并解除该申请者的申请限制
5. IF 提交申请的玩家断开连接 THEN 系统 SHALL 自动从列表中移除该玩家已提交的Option
6. IF 对应的Portal被其他原因销毁 THEN 系统 SHALL 自动从列表中移除关联的Option，并解除该申请者的申请限制
