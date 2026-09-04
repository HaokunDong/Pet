# 需求文档

## 引言

在联机模式下，当房主选择 Portal 关卡生成 Boss 后，客户端上出现了一个 Bug：Boss 一出场（尚未发起攻击），客户端上所有玩家角色的 Sprite 就出现异常。

**关键事实（用户确认）：**
- 角色**没有死亡**，HP 没有降为 0
- **主机（房主端）画面显示正常**，角色 Sprite 站立正常
- **只有客户端画面异常**
- Boss 还没有进行攻击，问题在 Boss 出场瞬间就发生了
- **所有角色都受影响** — 包括客户端自己的 LocalCharacter 和房主的 MirrorCharacter
- **具体表现**：Sprite 顺时针旋转 90°，偶尔伴随上下抽搐（像是卡了）
- **Death 动画中没有包含 Transform 相关的修改**，与死亡无关

### 问题根因分析

#### 核心发现

**PlayerPrefab 的 `Rigidbody2D` 缺少 `FreezeRotation` 约束（`m_Constraints: 0`）**，导致物理碰撞时角色会旋转。

对比两个 Prefab 的 Rigidbody2D 配置：

| Prefab | BodyType | GravityScale | Constraints |
|--------|----------|-------------|-------------|
| **PlayerPrefab** | Dynamic (0) | 1 | **0（无约束！）** |
| **Boss LihuoTest** | Dynamic (0) | 1 | **4（FreezeRotation ✅）** |

#### 触发链路

**在客户端上：**
1. 房主端 `BossFightManager.SpawnBoss()` 生成 Boss（tag="Enemy"）
2. `NetworkEnemySpawner.ScanAndSyncEnemies()` 检测到新 Boss，发送 `EnemySpawnMessage` 到客户端
3. 客户端收到消息后，`CreateMirrorEnemy()` 从 Boss 的 prefab 实例化一个 MirrorEnemy
4. MirrorEnemy 的 `Rigidbody2D` 是 **Dynamic + Simulated = true + GravityScale = 1**
5. 虽然 MirrorEnemy 的 AI 和 CombatSystem 被禁用了，但 **`Rigidbody2D` 仍然活跃**
6. MirrorEnemy 受重力影响，可能与 Player 角色发生物理碰撞
7. **Player 角色的 `Rigidbody2D` 没有 `FreezeRotation` 约束**（`m_Constraints: 0`）
8. 物理碰撞产生的力矩导致 Player 角色的 `Rigidbody2D` 旋转 → **Sprite 顺时针旋转 90°**
9. 同时，`NetworkEnemySpawner` 每帧通过 `EnemyPositionMessage` 同步 MirrorEnemy 的位置，与物理系统争夺 position 控制权 → **"上下抽搐"**

**为什么主机上正常？**
- 主机上没有 MirrorEnemy，只有真实的 Boss 实体
- 真实的 Boss 由 AI 控制移动，不会因为重力掉落到 Player 身上
- 即使 Boss 和 Player 碰撞，Boss 的 `Rigidbody2D` 有 `FreezeRotation`，碰撞力矩被 Boss 吸收
- 主机上的 Player 角色虽然也没有 `FreezeRotation`，但正常游戏中碰撞力矩不足以导致明显旋转

**为什么客户端上所有角色都受影响？**
- 客户端自己的 LocalCharacter：与 MirrorEnemy 的物理碰撞导致旋转
- 房主的 MirrorCharacter：也有 `Rigidbody2D`（`m_Constraints: 0`），同样受物理碰撞影响

### 核心设计目标

- Boss 可以正常攻击场上的所有角色（包括房主和客户端的角色）
- 所有角色也可以正常攻击 Boss
- Boss 和所有角色的状态（位置、HP、动画）要在主机和所有客户端之间实时同步
- **主机和客户端的画面必须一致** — 如果主机上角色 Sprite 是正常站立的，客户端上也必须是正常站立的
- 角色 Sprite 不应该出现旋转 90° 或抽搐的异常表现

---

## 需求

### 需求 1：PlayerPrefab 的 Rigidbody2D 必须冻结旋转

**用户故事：** 作为一名联机玩家，我希望我的角色在任何物理碰撞下都不会旋转，以便角色 Sprite 始终保持正确的朝向。

#### 验收标准

1. WHEN PlayerPrefab 被实例化 THEN 其 `Rigidbody2D` 的 `constraints` SHALL 包含 `FreezeRotation`（`RigidbodyConstraints2D.FreezeRotation`，即 `m_Constraints` 的 bit 2 被设置）。
2. WHEN Player 角色与任何物体（Boss、敌人、其他玩家）发生物理碰撞 THEN Player 角色的 `transform.rotation` SHALL 保持为 `Quaternion.identity`（不旋转）。
3. WHEN 从对象池中取出 Player 角色时 THEN 系统 SHALL 确保 `Rigidbody2D.constraints` 包含 `FreezeRotation`。

### 需求 2：MirrorEnemy 在客户端上不应参与物理模拟

**用户故事：** 作为一名联机玩家，我希望客户端上的 MirrorEnemy（从主机同步过来的敌人镜像）不会因为物理模拟而产生异常行为（如重力掉落、碰撞推挤），以便客户端上的画面与主机一致。

#### 验收标准

1. WHEN `CreateMirrorEnemy()` 在客户端上创建 MirrorEnemy THEN 系统 SHALL 将其 `Rigidbody2D` 设为 Kinematic（`bodyType = RigidbodyType2D.Kinematic`）或禁用 `Simulated`，以防止物理模拟干扰网络同步的位置。
2. WHEN MirrorEnemy 存在于客户端上 THEN 其位置 SHALL 完全由 `EnemyPositionMessage` 网络同步驱动，不受物理引擎影响。
3. WHEN MirrorEnemy 与 Player 角色重叠 THEN 系统 SHALL 不产生物理碰撞力，不导致 Player 角色旋转或位移。

### 需求 3：MirrorCharacter 在客户端上不应参与物理模拟

**用户故事：** 作为一名联机玩家，我希望客户端上的 MirrorCharacter（其他玩家的镜像角色）不会因为物理模拟而产生异常行为，以便所有端的画面一致。

#### 验收标准

1. WHEN `CreateMirrorCharacter()` 在客户端上创建 MirrorCharacter THEN 系统 SHALL 将其 `Rigidbody2D` 设为 Kinematic 或禁用 `Simulated`，以防止物理模拟干扰网络同步的位置。
2. WHEN MirrorCharacter 存在于客户端上 THEN 其位置 SHALL 完全由 `UpdateMirrorCharacter()` 的 `Vector3.Lerp` 插值驱动，不受物理引擎影响。
3. WHEN MirrorCharacter 与其他物体碰撞 THEN 系统 SHALL 不产生物理碰撞力。

### 需求 4：Boss 对 MirrorCharacter 的伤害必须通过网络正确路由

**用户故事：** 作为一名联机玩家，我希望 Boss 攻击我的角色时，伤害能正确地通过网络路由到我的客户端上的 LocalCharacter，而不是直接在房主端对我的 MirrorCharacter 调用 `TakeDamage()`，以便我的角色在所有端上显示一致的 HP 和动画状态。

#### 验收标准

1. WHEN Boss 在房主端攻击一个 MirrorCharacter（代表远程客户端玩家的镜像角色）THEN 系统 SHALL 不对 MirrorCharacter 直接调用 `TakeDamage()`，而是通过网络将伤害路由到该客户端的 LocalCharacter。
2. WHEN 远程客户端的 LocalCharacter 收到来自 Boss 的伤害 THEN 系统 SHALL 通过 `CmdUpdateHealth` 将最新的 HP 同步回服务器，再由服务器广播给所有客户端。
3. WHEN Boss 在房主端攻击房主自己的 LocalCharacter THEN 系统 SHALL 直接在本地调用 `TakeDamage()`（与当前行为一致，无需改变）。

### 需求 5：MirrorCharacter 不应在房主端被直接伤害和回收

**用户故事：** 作为一名联机玩家，我希望我的角色在房主端的镜像（MirrorCharacter）不会被 Boss 直接杀死和回收，以便我的角色在所有端上始终保持可见和状态一致。

#### 验收标准

1. WHEN Boss 的 AOE 攻击范围内包含 MirrorCharacter THEN 系统 SHALL 跳过 MirrorCharacter 或将伤害路由到对应客户端，不直接调用 `TakeDamage()`。
2. WHEN MirrorCharacter 在房主端存在 THEN 系统 SHALL 确保它不会因为本地伤害而触发 `Die()` → `PlayDeath()` → `Recycle()` 流程。
3. WHEN 远程客户端的 LocalCharacter HP 变化 THEN 系统 SHALL 通过 SyncVar 同步 HP 到所有端的 MirrorCharacter，使其 HealthBar 正确显示。

### 需求 6：主机和客户端的角色画面必须一致

**用户故事：** 作为一名联机玩家，我希望我在客户端上看到的角色状态（Sprite 方向、动画、HP）与主机上完全一致，以便所有玩家看到相同的游戏画面。

#### 验收标准

1. WHEN 主机上角色 Sprite 是正常站立的 THEN 客户端上该角色的 MirrorCharacter Sprite 也 SHALL 是正常站立的，不出现旋转或抽搐。
2. WHEN 主机上角色播放某个动画（Idle、Walk、Attack、Hit 等）THEN 客户端上该角色的 MirrorCharacter SHALL 播放相同的动画。
3. IF `syncedIsAlive` 为 true THEN 客户端上的 MirrorCharacter SHALL 不播放 Death 动画，不出现 Sprite 旋转 90° 的现象。

### 需求 7：战斗系统需要区分 MirrorCharacter 和本地角色

**用户故事：** 作为开发者，我希望战斗系统能正确区分 MirrorCharacter（远程玩家镜像）和本地角色，以便伤害能正确路由而不是直接在房主端应用。

#### 验收标准

1. WHEN 战斗系统（`CombatSystem`、`MeleeSkillEffectData`、`SkillDisplacementController`）的攻击目标搜索找到一个 MirrorCharacter THEN 系统 SHALL 跳过该对象或将伤害路由到对应客户端。
2. WHEN `BTFindNearestEnemy.FindNearest()` 搜索攻击目标时 THEN 系统 SHALL 跳过带有 MirrorCharacter 标记的对象，Boss AI 不将其作为攻击目标。
3. WHEN `NetworkDamageHelper.ApplyDamage()` 的目标是一个 MirrorCharacter THEN 系统 SHALL 将伤害通过网络发送到拥有该角色的客户端，而不是直接调用 `TakeDamage()`。
4. IF 系统需要识别 MirrorCharacter THEN 系统 SHALL 使用一种可靠的标记机制（如 `MirrorCharacterTag` 组件）来区分。

### 需求 8：`ClearAllEnemies()` 在客户端上不应破坏 NetworkEnemySpawner 的状态

**用户故事：** 作为开发者，我希望 `ClearAllEnemies()` 在客户端上执行时不会破坏 `NetworkEnemySpawner` 的 `clientMirrorEnemies` 字典状态，以便后续的敌人同步能正常工作。

#### 验收标准

1. WHEN `ClearAllEnemies()` 在客户端上销毁 MirrorEnemy THEN 系统 SHALL 同时清理 `NetworkEnemySpawner` 的 `clientMirrorEnemies` 字典中对应的条目。
2. WHEN `ClearAllEnemies()` 执行后 THEN `NetworkEnemySpawner` SHALL 能正常接收和处理后续的 `EnemySpawnMessage`（如 Boss 的 MirrorEnemy 创建）。

---

## 边界情况与技术考虑

1. **对象池状态残留**：从对象池中取出的角色可能保留了之前的 `Rigidbody2D` 状态（如 velocity、angularVelocity）。`Initialize()` 时需要重置这些状态。
2. **MirrorEnemy 的 Rigidbody2D 设为 Kinematic 后的影响**：Kinematic 的 Rigidbody2D 不会响应物理力，但仍然可以被 `OverlapCircle` 等物理查询检测到。这对于客户端上的攻击检测（如玩家攻击 MirrorEnemy）是必要的。
3. **MirrorCharacter 的 Rigidbody2D 设为 Kinematic 后的影响**：同上，Kinematic 不影响碰撞检测，只是不响应物理力。
4. **Boss 登场瞬间的时序问题**：Boss 生成后 AI 立即开始搜索目标。需要确保 MirrorCharacter 的标记在 Boss AI 首次 tick 之前完成。
5. **多个客户端同时被攻击**：Boss 的 AOE 攻击可能同时命中多个 MirrorCharacter，每个伤害都需要正确路由到对应的客户端。
6. **`ClearAllEnemies()` 在客户端上的影响**：客户端上调用 `ClearAllEnemies()` 会销毁所有 MirrorEnemy（tag="Enemy"），但 `NetworkEnemySpawner` 的 `clientMirrorEnemies` 字典不会被清理。需要确保字典被正确清理。
7. **EnemyPrefab 和 SlimeDeadMan Prefab**：这些 Prefab 也有 `Rigidbody2D`，需要检查它们的 `FreezeRotation` 设置。如果它们也缺少 `FreezeRotation`，在客户端上作为 MirrorEnemy 时也可能导致类似问题。
