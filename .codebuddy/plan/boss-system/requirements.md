# 需求文档

## 引言

本功能实现一个完整的 Boss 战斗系统。当玩家点击按钮在桌面生成传送门后，场景中的小怪生成器将停止生成小怪，同时在指定的 Boss 出生点生成一个 Boss 角色。Boss 的预制体与玩家角色预制体（PlayerPrefab）拥有相同的组件结构。首次击败 Boss 后，玩家将获得该 Boss 对应的 CharacterData，并将其作为新卡牌加入卡组，使玩家可以切换为该 Boss 角色进行游戏。

### 现有系统概述
- **角色预制体**：PlayerPrefab 包含 Transform、SpriteRenderer、Animator、BoxCollider2D、Rigidbody2D、CharacterEntity、CharacterAnimator、CombatSystem、AIController、ManualController、FlashEffect、ControlModeManager 等组件
- **敌人生成器**：EnemySpawner 通过定时器和对象池生成小怪
- **传送门系统**：PortalManager 管理传送门按钮和传送门生成，PortalController 处理传送门的点击/拖拽交互
- **卡牌系统**：CardSplineDistributor 管理卡牌列表，通过 characterDataList 生成卡牌，NotifyFocusCardClicked 触发角色切换

---

## 需求

### 需求 1：Boss 预制体创建

**用户故事：** 作为一名开发者，我希望有一个与 PlayerPrefab 组件结构一致的 BossPrefab 预制体，以便 Boss 角色能够复用现有的角色系统（动画、战斗、AI 等）。

#### 验收标准

1. WHEN BossPrefab 被创建 THEN 系统 SHALL 包含与 PlayerPrefab 相同的组件列表：Transform、SpriteRenderer、Animator、BoxCollider2D、Rigidbody2D、CharacterEntity、CharacterAnimator、CombatSystem、AIController、FlashEffect、AnimEventReceiver
2. WHEN BossPrefab 被实例化 THEN 系统 SHALL 将其 Tag 设置为 "Enemy"，Layer 设置为 "Enemy"
3. WHEN BossPrefab 被初始化 THEN 系统 SHALL 通过 CharacterEntity.Initialize() 使用 Boss 类型的 CharacterData 进行配置
4. IF Boss 的 CharacterData 的 characterType 字段为 CharacterType.Boss THEN 系统 SHALL 正确识别其为 Boss 类型角色

---

### 需求 2：传送门触发 Boss 战斗

**用户故事：** 作为一名玩家，我希望点击传送门按钮生成传送门后能触发 Boss 战斗流程，以便体验 Boss 挑战。

#### 验收标准

1. WHEN 玩家点击"生成传送门"按钮 THEN 系统 SHALL 生成传送门并同时触发 Boss 战斗流程
2. WHEN Boss 战斗流程被触发 THEN 系统 SHALL 立即停止 EnemySpawner 的小怪生成（设置暂停状态）
3. WHEN Boss 战斗流程被触发 THEN 系统 SHALL 在配置的 Boss 出生点位置生成 Boss
4. IF Boss 出生点未配置 THEN 系统 SHALL 使用默认位置（如传送门位置或场景中心）并输出警告日志
5. WHEN Boss 被生成 THEN 系统 SHALL 为其配置 AI 控制器，使 Boss 能够自动寻敌并战斗

---

### 需求 3：EnemySpawner 暂停与恢复

**用户故事：** 作为一名玩家，我希望 Boss 战斗期间不再生成小怪，以便专注于 Boss 战斗。

#### 验收标准

1. WHEN Boss 战斗开始 THEN EnemySpawner SHALL 停止生成新的小怪
2. WHEN Boss 战斗开始 THEN 系统 SHALL 清除场景中已存在的所有小怪
3. WHEN Boss 被击败 THEN EnemySpawner SHALL 恢复正常的小怪生成

---

### 需求 4：Boss 击败奖励 — 收入卡组

**用户故事：** 作为一名玩家，我希望首次击败 Boss 后能获得该 Boss 角色并加入我的卡组，以便我可以切换为该 Boss 角色进行游戏。

#### 验收标准

1. WHEN Boss 首次被击败 THEN 系统 SHALL 将该 Boss 的 CharacterData 添加到 CardSplineDistributor 的 characterDataList 中
2. WHEN 新的 CharacterData 被添加到卡组 THEN 系统 SHALL 刷新卡牌 UI，生成对应的新卡牌
3. WHEN Boss 被重复击败（非首次） THEN 系统 SHALL 不重复添加相同的 CharacterData 到卡组
4. WHEN Boss 的 CharacterData 被加入卡组后 THEN 玩家 SHALL 能够通过点击焦点卡牌切换为该 Boss 角色
5. IF 系统需要判断是否为首次击败 THEN 系统 SHALL 通过检查 characterDataList 中是否已存在该 CharacterData 来判断

---

### 需求 5：Boss 战斗管理器

**用户故事：** 作为一名开发者，我希望有一个集中的 Boss 战斗管理器来协调整个 Boss 战斗流程，以便各系统之间的交互清晰可控。

#### 验收标准

1. WHEN BossFightManager 被创建 THEN 系统 SHALL 提供以下可配置字段：Boss 的 CharacterData、Boss 出生点 Transform、Boss 预制体名称（用于对象池）
2. WHEN StartBossFight() 被调用 THEN 系统 SHALL 按顺序执行：暂停 EnemySpawner → 生成 Boss → 监听 Boss 死亡事件
3. WHEN Boss 死亡事件触发 THEN 系统 SHALL 按顺序执行：判断是否首次击败 → 发放奖励 → 恢复 EnemySpawner
4. WHEN Boss 战斗结束 THEN 系统 SHALL 发出事件通知（如 OnBossDefeated），以便其他系统响应
