# 需求文档：角色 Variant Prefab 方案

## 引言

当前项目使用单一的 `PlayerPrefab` 预制体来承载所有玩家角色，通过在运行时动态切换 `AnimatorController` 和 `CharacterData` 来实现角色切换。但由于不同角色的碰撞体（Collider）形状、大小、偏移可能不同，单一预制体无法满足差异化碰撞体的需求。

本方案将采用 **Prefab Variant（预制体变体）** 的方式，为每个角色创建基于 `PlayerPrefab` 的变体预制体。每个 Variant 可以独立覆盖碰撞体参数（大小、偏移、形状），同时继承基础预制体的所有脚本组件和通用配置。角色切换时，不再复用同一个 GameObject，而是销毁旧实例、实例化对应角色的 Variant 预制体。

### 现有架构概述

| 组件 | 当前实现 |
|------|----------|
| `PlayerPrefab` | 单一预制体，含 BoxCollider2D、Rigidbody2D、SpriteRenderer、Animator 及多个脚本 |
| `GameManager` | 在 Awake 中将 `PlayerPrefab` 注册到 `PoolMgr` 对象池 |
| `GameCharacterManager` | 通过 `PoolMgr.GetNode("PlayerPrefab")` 获取实例，动态配置角色数据 |
| `CharacterData` | ScriptableObject，包含角色属性、AnimatorController、技能等，不含碰撞体配置 |
| 角色切换 | `SwitchPlayerCharacter()` 销毁旧角色 → 创建新角色（同一 Prefab） |

### 改造目标

将"一个 Prefab + 动态配置"模式改为"每角色一个 Variant Prefab + CharacterData 引用对应 Prefab"模式，使每个角色可以在编辑器中可视化地配置独立的碰撞体。

---

## 需求

### 需求 1：CharacterData 增加角色专属 Prefab 引用

**用户故事：** 作为一名开发者，我希望在 `CharacterData` 中配置该角色对应的 Prefab 引用，以便系统在创建角色时能自动使用正确的 Variant Prefab。

#### 验收标准

1. WHEN `CharacterData` 被创建或编辑 THEN 系统 SHALL 提供一个 `GameObject prefab` 字段（Header: "Prefab"），用于指定该角色的专属预制体。
2. IF `CharacterData.prefab` 字段为空 THEN 系统 SHALL 回退使用默认的 `PlayerPrefab`（向后兼容）。
3. WHEN 在 Editor 中配置 `CharacterData` THEN 开发者 SHALL 能够将角色的 Variant Prefab 拖拽赋值到该字段。

---

### 需求 2：创建角色 Variant Prefab 体系

**用户故事：** 作为一名开发者，我希望为每个角色创建基于 `PlayerPrefab` 的 Prefab Variant，以便在编辑器中可视化地为每个角色配置独立的碰撞体参数。

#### 验收标准

1. WHEN 创建新角色 THEN 开发者 SHALL 基于 `PlayerPrefab` 创建一个 Prefab Variant（如 `PlayerPrefab_Bonne`、`PlayerPrefab_Tank`）。
2. WHEN 编辑角色 Variant Prefab THEN 开发者 SHALL 能够独立修改该 Variant 的 BoxCollider2D 的 `size`、`offset` 参数，而不影响基础 Prefab 和其他 Variant。
3. WHEN 基础 `PlayerPrefab` 的脚本组件被修改 THEN 所有 Variant SHALL 自动继承这些修改（Unity Prefab Variant 原生行为）。
4. WHEN 角色 Variant Prefab 被创建 THEN 它 SHALL 存放在 `Resources/Prefabs/Entity/Characters/` 目录下，命名规则为 `PlayerPrefab_{角色名}`。

---

### 需求 3：改造 GameManager 的对象池注册逻辑

**用户故事：** 作为一名开发者，我希望对象池能支持多个角色 Prefab 的注册和获取，以便角色切换时能正确实例化对应的 Variant Prefab。

#### 验收标准

1. WHEN `GameManager` 初始化 THEN 系统 SHALL 扫描所有 `CharacterData` 中配置的 Prefab 并注册到 `PoolMgr`（或按需加载）。
2. IF 使用按需加载策略 THEN 系统 SHALL 在首次需要某角色 Prefab 时从 Resources 加载并注册到对象池。
3. WHEN 对象池中已有某角色的回收实例 THEN 系统 SHALL 优先复用该实例，而非重新 Instantiate。

---

### 需求 4：改造 GameCharacterManager 的角色创建逻辑

**用户故事：** 作为一名开发者，我希望 `CreatePlayerCharacter` 方法能根据 `CharacterData` 中的 Prefab 引用来实例化正确的 Variant Prefab，以便每个角色自动拥有正确的碰撞体配置。

#### 验收标准

1. WHEN `CreatePlayerCharacter(CharacterData, Vector3)` 被调用 THEN 系统 SHALL 优先使用 `CharacterData.prefab` 指定的预制体进行实例化。
2. IF `CharacterData.prefab` 为 null THEN 系统 SHALL 回退使用 `playerPrefabName`（即默认的 `PlayerPrefab`）从对象池获取实例。
3. WHEN 使用 Variant Prefab 实例化角色 THEN 系统 SHALL 不再动态添加/修改 Collider2D 组件（因为 Variant 已预配置好碰撞体）。
4. WHEN 角色实例化完成 THEN 系统 SHALL 仍然正常执行 AnimatorController 设置、CharacterEntity 初始化、CombatSystem 添加等后续流程。

---

### 需求 5：改造角色切换逻辑

**用户故事：** 作为一名开发者，我希望角色切换时能正确销毁旧角色实例并实例化新角色的 Variant Prefab，以便切换后的角色拥有正确的碰撞体。

#### 验收标准

1. WHEN `SwitchPlayerCharacter(CharacterData)` 被调用 THEN 系统 SHALL 销毁（或回收到对象池）当前角色的 GameObject。
2. WHEN 旧角色被回收 THEN 系统 SHALL 将其回收到与其 Prefab 名称对应的对象池中（而非统一回收到 "PlayerPrefab" 池）。
3. WHEN 新角色被创建 THEN 系统 SHALL 使用新 `CharacterData` 中指定的 Variant Prefab 进行实例化。
4. WHEN 角色切换完成 THEN 新角色 SHALL 出现在正确的出生点位置，并拥有完整的运行时状态（满血、培养加成等）。

---

### 需求 6：向后兼容性保障

**用户故事：** 作为一名开发者，我希望改造后的系统对未配置专属 Prefab 的旧 CharacterData 保持向后兼容，以便不需要一次性修改所有角色配置。

#### 验收标准

1. IF `CharacterData.prefab` 字段为 null THEN 系统 SHALL 使用原有的 `PlayerPrefab` 逻辑（从默认对象池获取）。
2. WHEN 旧的 `CharacterData` 资产未修改 THEN 系统 SHALL 保持与改造前完全相同的行为。
3. WHEN 新角色被添加到系统中 THEN 开发者 SHALL 能够选择是否为其创建 Variant Prefab（不强制）。

---

### 需求 7：网络多人模式兼容

**用户故事：** 作为一名开发者，我希望 Variant Prefab 方案能兼容现有的网络多人模式，以便联机时也能正确加载不同角色的碰撞体。

#### 验收标准

1. WHEN 在多人模式下创建网络玩家角色 THEN 系统 SHALL 支持根据 `CharacterData` 加载对应的 Variant Prefab。
2. IF 网络模式使用 `NetworkPlayerPrefab` THEN 系统 SHALL 保持现有网络预制体逻辑不变（网络预制体有独立的注册机制）。
3. WHEN 远程玩家角色需要同步 THEN 系统 SHALL 能够通过 CharacterData ID 确定应使用哪个 Variant Prefab。

---

## 技术约束与边界情况

### 技术约束
- Unity Prefab Variant 不支持删除基础 Prefab 上的组件，只能覆盖参数或添加新组件
- 对象池需要按 Prefab 名称区分不同角色的池，避免将 A 角色的实例错误地当作 B 角色使用
- `Resources.Load` 路径需要与实际文件路径一致

### 边界情况
- 角色 Variant Prefab 被误删时，系统应能回退到默认 Prefab 并输出警告日志
- 对象池中残留的旧角色实例（名称不匹配）应被正确处理
- 编辑器中修改基础 Prefab 后，所有 Variant 的碰撞体覆盖值不应被重置
