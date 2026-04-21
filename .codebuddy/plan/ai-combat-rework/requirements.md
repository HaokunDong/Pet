# 需求文档

## 引言

当前项目的 AI 战斗逻辑（行为树）在实战中存在多个体验问题：敌人贴身后仍来回追着玩家跑、攻击范围几何判定与直线距离混用导致抖动、`BTPatrol` 机械式地左右来回走缺乏自然感、战斗状态被节点之间的 Success/Failure 频繁打断、旧节点（如 `BTCheckSkillReady`/`BTUseSkill` 兼容逻辑、`BTMoveToTarget` 中的 `MinSafeGap`/`AttackRangeTolerance` 防抖分支）相互重叠且冗余。

本次重构将 AI 战斗逻辑重写为一个**显式的 AI 状态机 + 精简后的行为树**：以 "Wander → Combat(Engage/Strike) → PostCombat Cooldown" 三段式状态划分，使用 `CharacterData` 中现有的 `moveSpeed`、`attackSpeed`、`attackRange`、`attackRangeShapes` 等数据驱动，同时新增"主动攻击距离"（engage distance，小于最大攻击范围）作为进入攻击状态的判定条件。玩家、小怪、Boss 共用同一套战斗主干（Boss 额外允许使用技能），并清理现有代码中与本方案冲突或重复的部分。

> 范围边界：本次不改动 `CombatSystem`、`CharacterAnimator`、`AnimEventReceiver`、`ManualController`（手动控制模式）、`HealthBar`、`AttackRangeHelper`、`AttackRangeShape` 的核心实现，仅在必要处新增最小量的接口或参数读取。

## 需求

### 需求 1

**用户故事：** 作为一名玩家，我希望当场上没有敌人或敌人未在视野范围内时，角色能像散步一样在场景中自由走动，以便游戏观感自然、不机械。

#### 验收标准
本节应使用 EARS 需求格式

1. WHEN `AIController` tick 时且 `BTContext.CurrentTarget` 为空（无敌人）或最近敌人的距离大于 `detectionRange` THEN AI 状态机 SHALL 进入 `Wander` 状态。
2. WHEN AI 处于 `Wander` 状态 THEN 角色 SHALL 以 `moveSpeed` 向当前随机目标点水平移动，并由 `CharacterAnimator.PlayWalk()` 播放行走动画，同时调用 `SetFacingDirection` 对齐移动方向。
3. WHEN 角色到达当前随机目标点（水平距离小于 0.1）或累计移动时间超过"单段最大行走时长" THEN AI SHALL 进入一个 `WanderPause` 子阶段并播放 Idle 动画。
4. WHEN `WanderPause` 子阶段累计已等待时间超过"随机停顿时长" THEN AI SHALL 重新在 `[PatrolOrigin.x - patrolRange, PatrolOrigin.x + patrolRange]` 区间内**随机**采样一个新的目标点，并回到步骤 2。
5. WHEN AI 在 `Wander` 状态下前方 `wallDetectDistance` 检测到 `Wall` 层障碍物 THEN 当前随机目标点 SHALL 被立即作废，AI SHALL 进入一次 `WanderPause` 子阶段，且下一次采样 SHALL 倾向于反向区间（防止反复撞墙）。
6. IF "单段最大行走时长"与"随机停顿时长"未在 Inspector 配置 THEN 系统 SHALL 分别使用默认范围 `[1.5s, 3.5s]` 与 `[0.5s, 1.5s]` 并在每段开始时独立随机取值。

### 需求 2

**用户故事：** 作为一名玩家，我希望当敌人进入视野范围时角色能自动进入战斗状态，以便不需要手动指挥也能与敌人交战。

#### 验收标准

1. WHEN AI tick 时通过 `FindNearestEnemy` 在 `detectionRange` 内找到任意存活敌人 THEN AI SHALL 将该敌人设为 `CurrentTarget` 并从 `Wander` 迁移到 `Combat` 状态。
2. WHEN AI 从 `Wander` 迁移到 `Combat` THEN 当前随机游走目标 SHALL 被清空，Walk/Idle 动画 SHALL 被新的战斗子状态的动画覆盖。
3. IF 当前 `CurrentTarget` 死亡或消失 THEN AI SHALL 立刻清空目标并进入 `PostCombat` 状态（见需求 5）。
4. IF 当前 `CurrentTarget` 与自己的直线距离已经超过 `detectionRange + disengageHysteresis`（滞回缓冲，默认 1.0） THEN AI SHALL 视为脱战并进入 `PostCombat` 状态。
5. WHEN AI 处于 `Combat` 状态且每帧重新查找敌人 THEN 系统 SHALL 仅在当前目标已死亡或已脱战时才允许切换到另一个更近的敌人（避免目标频繁跳变）。

### 需求 3

**用户故事：** 作为一名玩家，我希望角色在战斗中看到敌人未进入主动攻击距离时会自动靠近，以便玩家不需要手动走位。

#### 验收标准

1. THE 系统 SHALL 在 `CharacterData` 上新增字段 `engageDistance`（主动攻击距离，`Min(0f)`，默认 `1.0`），并同步到 `RuntimeCharacterStats`。
2. IF `engageDistance` 大于 `GetMaxAttackDistance()` THEN `OnValidate` SHALL 产生警告日志，且运行时 SHALL 将 `engageDistance` 夹取为 `min(engageDistance, GetMaxAttackDistance() * 0.9)` 以确保它严格小于最大攻击范围。
3. WHEN AI 处于 `Combat` 且目标与自己的直线距离大于 `engageDistance` THEN AI SHALL 进入 `Engage` 子状态，以 `moveSpeed` 水平向目标移动，并调用 `PlayWalk()` 与 `SetFacingDirection`。
4. WHEN AI 处于 `Engage` 子状态且前方 `wallDetectDistance` 内检测到 `Wall` 层障碍 THEN AI SHALL 原地停下播放 Idle，但 SHALL 保持 `Combat` 状态（不回 Wander），并继续每帧重试（等待目标自己走到可达处）。
5. WHEN AI 处于 `Engage` 子状态且目标与自己的直线距离小于或等于 `engageDistance` THEN AI SHALL 立即切换到 `Strike` 子状态。

### 需求 4

**用户故事：** 作为一名玩家，我希望敌人进入主动攻击距离后角色停下不动、按照攻速节奏播放攻击动画，以便攻击流畅、符合数值配置。

#### 验收标准

1. WHEN AI 处于 `Strike` 子状态 THEN 角色水平移动速度 SHALL 为 0（不调用 `transform.position` 的水平位移）。
2. WHEN AI 处于 `Strike` 子状态 THEN AI SHALL 调用 `CharacterAnimator.FaceTowards(target)` 使朝向对准目标。
3. WHEN AI 处于 `Strike` 子状态且距上一次成功攻击的时间间隔 ≥ `1 / attackSpeed` 秒 THEN AI SHALL 调用 `CombatSystem.TryNormalAttack(target)` 触发一次普通攻击；IF 角色是 `Boss` 或 `Player` 类型且存在可用技能 THEN AI SHALL 在普通攻击前优先尝试 `CombatSystem.TryUseSkill`（技能就绪且在技能范围内时）。
4. WHEN `CombatSystem.IsAttacking` 为 true（攻击动画进行中，等待帧事件） THEN AI SHALL 不发起新的攻击，也不切换子状态，仅保留 `Strike` 态。
5. WHEN AI 处于 `Strike` 子状态但目标移动到超出 `engageDistance + engageExitHysteresis`（滞回缓冲，默认 0.25） THEN AI SHALL 切回 `Engage` 子状态。
6. WHEN AI 处于 `Strike` 子状态且 `CharacterAnimator.IsInHitState` 为 true THEN AI SHALL 在本帧跳过攻击触发（不打断受击反馈），但保留 `Strike` 态。

### 需求 4.1

**用户故事：** 作为一名玩家，我希望当进入战斗状态时如果敌人正好处于我的身后，角色能在下一次攻击前先转身再挥击，以便不会背对敌人出手。

#### 验收标准

1. WHEN AI 从 `Wander` 迁移到 `Combat` 的那一帧 THEN 系统 SHALL 基于目标相对角色的水平位置与 `CharacterAnimator.FacingDirection` 计算出 `TargetWasBehindOnEngage` 标志（目标方向与当前朝向相反即为 true），并存入 `BTContext`。
2. WHEN AI 在 `Combat` 期间切换目标（旧目标死亡或脱战后获得新目标） THEN `TargetWasBehindOnEngage` SHALL 以同样规则针对新目标重新计算一次。
3. WHEN AI 即将发起本次战斗的**首次**攻击（`Strike` 子状态下第一次满足攻速冷却） AND `TargetWasBehindOnEngage == true` THEN AI SHALL 先调用 `CharacterAnimator.FaceTowards(target)` 完成转身，本帧 SHALL 不触发 `TryNormalAttack`/`TryUseSkill`，并将 `TargetWasBehindOnEngage` 置为 false。
4. WHEN `TargetWasBehindOnEngage == true` 且 AI 仍处于 `Engage` 子状态并开始向目标移动 THEN `SetFacingDirection` SHALL 每帧根据"目标相对自己的水平方向"更新朝向，并在朝向与目标方向一致的那一帧将 `TargetWasBehindOnEngage` 置为 false（视为已在移动途中自然转身，避免到达后再多插一次无意义转身帧）。
5. WHEN `TargetWasBehindOnEngage == false`（含进入战斗时就已面向目标的常规情况） THEN `Strike` 子状态 SHALL 按需求 4 第 3 条的正常节奏出招，不插入额外的"仅转身"帧。

### 需求 5

**用户故事：** 作为一名玩家，我希望击败敌人后角色不要立刻回到散步状态，而是有一小段"结束战斗"的过渡，以便战斗节奏更自然。

#### 验收标准

1. WHEN `CurrentTarget` 由存活变为死亡或为空 AND 之前 AI 处于 `Combat` 状态 THEN AI SHALL 进入 `PostCombat` 状态，并记录进入时刻。
2. WHEN AI 处于 `PostCombat` 状态 THEN 角色水平移动速度 SHALL 为 0 且 SHALL 播放 Idle 动画。
3. WHEN AI 处于 `PostCombat` 状态且 `detectionRange` 内重新出现存活敌人 AND `PostCombat` 尚未结束 THEN AI SHALL 允许立即切回 `Combat` 状态而不必等到倒计时结束。
4. WHEN AI 处于 `PostCombat` 状态且已停留时间超过 `postCombatDuration`（默认 `1.5s`，可在 `AIController` Inspector 配置） THEN AI SHALL 迁移回 `Wander` 状态。

### 需求 6

**用户故事：** 作为一名策划/开发者，我希望敌人的 AI 行为树与玩家共用一套新的状态机逻辑，以便维护成本最小。

#### 验收标准

1. THE `AIController` SHALL 为 `Player`、`MinorEnemy`、`Boss` 三种 `CharacterType` 构建**同一棵**新的行为树主干（Wander → Combat(Engage / Strike) → PostCombat）。
2. IF 角色类型为 `MinorEnemy` THEN 技能分支 SHALL 被跳过（只使用普通攻击）。
3. IF 角色类型为 `Player` 或 `Boss` THEN 在 `Strike` 子状态中 SHALL 允许技能优先于普通攻击（按需求 4 第 3 条执行）。
4. WHEN `FindNearestEnemy` 根据 `owner.RuntimeStats.characterType` 决定搜索 Tag THEN `Player`→`Enemy`、其它→`Player` 的映射关系 SHALL 保持不变。
5. WHEN `ManualController.enabled == true` 时 AI SHALL 被 `AIController.PauseAI()` 暂停，且切回自动模式时 `ResumeAI()` SHALL 重置状态机为 `Wander` 初始态（不保留旧的目标或战斗状态）。

### 需求 7

**用户故事：** 作为一名开发者，我希望清理掉不再需要的旧 AI 节点与防抖代码，以便代码库保持精简、无冗余逻辑。

#### 验收标准

1. THE 系统 SHALL 删除 `BTPatrol.cs`（被 Wander 替代）、`BTMoveToTarget.cs`（被 Engage 替代）、`BTCheckEnemyInRange.cs`（被 Strike 的内置距离判定替代）及各自 `.meta` 文件。
2. THE 系统 SHALL 删除 `BTAttack.cs` 中围绕 `AttackRangeTolerance` 的距离兜底分支并将其整合进新的 `Strike` 实现（或删除整个 `BTAttack.cs` 并由新节点替代）。
3. THE `BTContext` SHALL 移除 `IsMovingRight`、`MinSafeGap`、`AttackRangeTolerance`、`WallDetectedAhead` 这些仅服务于旧逻辑的字段，新增 Wander/Engage/Strike/PostCombat 状态机所需的最小字段集（例如 `CurrentState`、`WanderTargetX`、`WanderPhaseEndTime`、`PostCombatEndTime`、`LastAttackTime`）。
4. THE `AIController` Inspector SHALL 移除 `minSafeGap`、`attackRangeTolerance`，新增 `engageExitHysteresis`、`disengageHysteresis`、`postCombatDuration`、`wanderWalkDuration`（Vector2 范围）、`wanderPauseDuration`（Vector2 范围）。
5. WHEN 重构完成后 THE 行为树脚本目录 `Assets/Scripts/AI/Nodes/` SHALL 只保留本次新设计实际使用的节点文件，且所有文件通过 C# 编译与 Unity 编辑器引用检查（无悬挂 `.meta`、无编译错误）。
6. IF `BTCheckSkillReady`/`BTUseSkill` 在新状态机中不再作为独立 BT 节点被调用 THEN 它们 SHALL 被删除；技能就绪判断与触发 SHALL 被并入 `Strike` 子状态逻辑中。

### 需求 8

**用户故事：** 作为一名开发者，我希望新的 AI 状态能够稳定、不抖动地在各子状态间迁移，以便避免旧版中出现的"敌人贴着玩家来回跑"问题。

#### 验收标准

1. WHEN 目标穿过角色身后导致朝向翻转 THEN `Engage`/`Strike` 的判定 SHALL 基于 `Vector2.Distance(owner, target)` 纯直线距离，而非依赖朝向的 shape 判定（shape 判定只在 `CombatSystem.TryNormalAttack` 内部真正造成伤害前使用）。
2. WHEN 目标位于攻击距离边界来回漂移 THEN `Engage↔Strike` 的切换 SHALL 使用滞回带（进入用 `engageDistance`，退出用 `engageDistance + engageExitHysteresis`），避免每帧抖动。
3. WHEN AI 发起 `TryNormalAttack` 但因朝向/范围 shape 判定在本帧未命中 THEN AI SHALL 保持 `Strike` 态（不回到 `Engage`），并允许下一帧由 `FaceTowards` 对齐后再次尝试。
4. WHEN 新状态机任一状态调用动画 THEN 每帧对同一动画（例如 Idle 或 Walk）的重复 `Play` 调用 SHALL 是幂等的（依赖 `CharacterAnimator` 现有逻辑），不得对动画状态机产生重置副作用。

