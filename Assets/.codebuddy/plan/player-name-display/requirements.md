# 需求文档

## 引言

本功能旨在实现多人联机模式下，当玩家创建房间或加入房间后，每个玩家角色头顶（血条上方）显示该玩家的 **Steam 名称**。名字标签需要跟随角色移动，并且对所有联网玩家可见。

### 项目背景
- 项目使用 Mirror 网络框架进行多人联机，通过 Steamworks 进行连接
- `NetworkPlayer` 类中已有 `playerName` 字段（通过 SyncVar 同步），其值来源于 `SteamFriends.GetPersonaName()`（即玩家的 Steam 显示名称）
- `CharacterEntity` 是角色实体组件，已有 `HealthBar`（基于 SpriteRenderer，浮动在角色头顶，yOffset=0.9f）
- 角色创建通过 `GameCharacterManager.CreatePlayerCharacter()` 完成
- 远程玩家角色通过 `NetworkPlayer.CreateMirrorCharacter()` 创建

---

## 需求

### 需求 1：玩家 Steam 名称标签的创建与显示

**用户故事：** 作为一名多人游戏玩家，我希望在创建或加入房间后能看到每个玩家角色头顶显示其 Steam 名称，以便我能区分不同的玩家角色。

#### 验收标准

1. WHEN 玩家创建房间并生成角色 THEN 系统 SHALL 在该玩家角色头顶（血条上方）创建并显示该玩家的 Steam 名称标签
2. WHEN 玩家加入已有房间并生成角色 THEN 系统 SHALL 在该玩家角色头顶（血条上方）创建并显示该玩家的 Steam 名称标签
3. WHEN 远程玩家的 Mirror 角色被创建 THEN 系统 SHALL 在该远程角色头顶显示对应玩家的 Steam 名称
4. IF Steam 名称为空或未初始化 THEN 系统 SHALL 显示默认名字（如 "Player"）

### 需求 2：名字标签跟随角色移动

**用户故事：** 作为一名多人游戏玩家，我希望名字标签始终跟随角色移动，以便我在游戏过程中随时能识别各个玩家。

#### 验收标准

1. WHEN 角色移动时 THEN 系统 SHALL 使名字标签实时跟随角色位置更新
2. WHEN 角色精灵翻转（flipX）时 THEN 系统 SHALL 保持名字标签始终正向显示（不随角色翻转）
3. WHEN 名字标签显示时 THEN 系统 SHALL 将名字标签定位在血条上方，不与血条重叠

### 需求 3：名字标签的视觉表现

**用户故事：** 作为一名多人游戏玩家，我希望名字标签清晰可读且不影响游戏体验，以便我能舒适地进行游戏。

#### 验收标准

1. WHEN 名字标签显示时 THEN 系统 SHALL 使用与游戏风格一致的字体和大小渲染名字文本
2. WHEN 名字标签显示时 THEN 系统 SHALL 确保文字在各种背景下清晰可读（可通过描边或阴影实现）
3. WHEN 名字标签显示时 THEN 系统 SHALL 使名字文本居中对齐于角色上方
4. IF 名字文本过长 THEN 系统 SHALL 适当截断或缩小字体以避免显示异常

### 需求 4：网络同步

**用户故事：** 作为一名多人游戏玩家，我希望所有玩家都能看到彼此的 Steam 名称，以便多人协作时能互相识别。

#### 验收标准

1. WHEN 玩家 Steam 名称通过网络同步（SyncVar `playerName`，来源为 `SteamFriends.GetPersonaName()`）更新时 THEN 系统 SHALL 在所有客户端上更新对应角色的名称标签显示
2. WHEN 新玩家加入房间时 THEN 系统 SHALL 确保已在房间中的玩家能看到新玩家的 Steam 名称标签
3. WHEN 新玩家加入房间时 THEN 系统 SHALL 确保新玩家能看到所有已在房间中的玩家的 Steam 名称标签

### 需求 5：生命周期管理

**用户故事：** 作为一名开发者，我希望名字标签的生命周期被正确管理，以便避免内存泄漏和显示异常。

#### 验收标准

1. WHEN 角色被销毁或回收到对象池时 THEN 系统 SHALL 正确清理名字标签资源
2. WHEN 玩家断开连接时 THEN 系统 SHALL 移除该玩家角色的名字标签
3. WHEN 玩家切换角色时 THEN 系统 SHALL 在新角色上重新创建名字标签并显示正确的玩家名字
4. IF 角色从对象池中重新获取 THEN 系统 SHALL 正确重新初始化名字标签

---

## 技术约束

- 名字标签应采用与现有 `HealthBar` 类似的实现方式（基于 SpriteRenderer 或 TextMesh），作为角色的子对象
- 需要兼容现有的 `HealthBar` 组件，定位在血条上方
- 名字标签的 sortingOrder 应高于血条，确保不被遮挡
- 实现应考虑性能，避免每帧进行不必要的更新
- 需要与现有的 Mirror 网络同步机制（SyncVar）配合工作
