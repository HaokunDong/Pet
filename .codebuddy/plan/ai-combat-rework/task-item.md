# 实施计划

- [ ] 1. 扩展数据层：新增主动攻击距离字段
  - 在 `Assets/Scripts/Data/CharacterData.cs` 增加 `engageDistance`（`[Min(0f)]`，默认 `1.0`）与说明性 `Tooltip`
  - 在 `OnValidate` 中当 `engageDistance > GetMaxAttackDistance()` 时输出警告
  - 在 `Assets/Scripts/Character/RuntimeCharacterStats.cs` 的 `InitFromData` 中同步拷贝 `engageDistance`，并按 `min(engageDistance, GetMaxAttackDistance * 0.9)` 夹取
  - _需求：3.1、3.2_

- [ ] 2. 重构 `BTContext` 为状态机黑板
  - 在 `Assets/Scripts/AI/BTContext.cs` 移除 `IsMovingRight`、`MinSafeGap`、`AttackRangeTolerance`、`WallDetectedAhead`
  - 新增 `AIState`（枚举：`Wander`/`WanderPause`/`Engage`/`Strike`/`PostCombat`）、`CurrentState`、`WanderTargetX`、`WanderPhaseEndTime`、`PostCombatEndTime`、`LastAttackTime`、`HasFiredFirstStrike`、`TargetWasBehindOnEngage`、`EngageExitHysteresis`、`DisengageHysteresis`、`PostCombatDuration`、`WanderWalkDuration`、`WanderPauseDuration` 字段
  - 保留 `PatrolOrigin`、`PatrolRange`、`WallDetectDistance`、`TerrainLayerMask`、`Owner`、`CurrentTarget`
  - _需求：7.3、7.4_

- [ ] 3. 实现 `BTWander` 节点（散步 + 随机停顿 + 撞墙反向）
  - 新建 `Assets/Scripts/AI/Nodes/BTWander.cs`
  - 首次进入随机采样 `WanderTargetX ∈ [PatrolOrigin.x ± patrolRange]`、随机 `WanderPhaseEndTime`，调用 `PlayWalk` 并水平移动
  - 到点或超时切换到 `WanderPause` 子阶段播放 Idle，停顿结束后重新采样
  - 前方 `wallDetectDistance` 内命中 `Wall` 层时作废目标并进入 `WanderPause`，下一次采样偏向反方向
  - _需求：1.1、1.2、1.3、1.4、1.5、1.6_

- [ ] 4. 实现 `BTCombat` 复合节点（Engage / Strike / 首击转身）
  - 新建 `Assets/Scripts/AI/Nodes/BTCombat.cs`，内部按 `CurrentState` 分发 `Engage` 与 `Strike`
  - `Engage`：`dist > engageDistance` 时以 `moveSpeed` 水平追击，每帧 `SetFacingDirection(目标方向)`；撞墙时原地 `PlayIdle` 但保留 `Combat`；朝向与目标方向一致时清除 `TargetWasBehindOnEngage`；`dist ≤ engageDistance` 切到 `Strike`
  - `Strike`：水平速度为 0，`FaceTowards(target)`；按 `1 / attackSpeed` 节奏出招：若 `TargetWasBehindOnEngage && !HasFiredFirstStrike` 则本帧只转身不出手并清标志；`CombatSystem.IsAttacking` 或 `CharacterAnimator.IsInHitState` 时跳过本帧攻击；`Player`/`Boss` 优先 `TryUseSkill`，否则 `TryNormalAttack`；成功出手后更新 `LastAttackTime`、置 `HasFiredFirstStrike`
  - `dist > engageDistance + engageExitHysteresis` 时切回 `Engage`；`TryNormalAttack` 因 shape 失配失败时不回 `Engage`
  - _需求：3.3、3.4、3.5、4.1、4.2、4.3、4.4、4.5、4.6、4.1.3、4.1.4、4.1.5、8.1、8.2、8.3_

- [ ] 5. 实现 `BTPostCombat` 节点（战斗冷却过渡）
  - 新建 `Assets/Scripts/AI/Nodes/BTPostCombat.cs`
  - 进入时记录 `PostCombatEndTime = Time.time + postCombatDuration`，水平速度为 0 并 `PlayIdle`
  - 每帧调用一次 `FindNearestEnemy`：若检测到存活敌人且在 `detectionRange` 内，则立即置目标并切到 `Combat`
  - 超时后切到 `Wander`
  - _需求：5.1、5.2、5.3、5.4_

- [ ] 6. 改造 `BTFindNearestEnemy` 为每帧可调用的"目标维护"节点
  - 在 `Assets/Scripts/AI/Nodes/BTFindNearestEnemy.cs` 中保留基于 Tag（`Player→Enemy`，其它→`Player`）的最近敌人搜索逻辑
  - 行为调整：若 `CurrentTarget` 存活且 `dist ≤ detectionRange + disengageHysteresis`，则保持当前目标不跳变；仅当目标死亡或脱战时允许选择新目标
  - 暴露供 `BTCombat`/`BTPostCombat` 复用的公开方法（如 `TryAcquireTarget(detectionRange)`）
  - _需求：2.1、2.3、2.4、2.5、6.4_

- [ ] 7. 重写 `AIController` 状态机主干与 Inspector 字段
  - 在 `Assets/Scripts/AI/AIController.cs` 移除 `minSafeGap`、`attackRangeTolerance`
  - 新增 `engageExitHysteresis`（0.25）、`disengageHysteresis`（1.0）、`postCombatDuration`（1.5）、`wanderWalkDuration`（Vector2，默认 `(1.5, 3.5)`）、`wanderPauseDuration`（Vector2，默认 `(0.5, 1.5)`）
  - `BuildTree` 为 `Player`/`MinorEnemy`/`Boss` 构建统一行为树：`Selector(Sequence(FindEnemy, Combat), PostCombat, Wander)`，并将各参数注入 `BTContext`
  - 在 `Combat` 进入/切换目标那一帧，基于目标水平位置 vs `CharacterAnimator.FacingDirection` 写入 `TargetWasBehindOnEngage` 并重置 `HasFiredFirstStrike`
  - `PauseAI`/`ResumeAI` 重置 `CurrentState = Wander`、`CurrentTarget = null`、`HasFiredFirstStrike = false`
  - _需求：2.1、2.2、4.1.1、4.1.2、5.1、6.1、6.2、6.3、6.5、7.4、8.4_

- [ ] 8. 清理遗留节点与编译残留
  - 删除 `Assets/Scripts/AI/Nodes/BTPatrol.cs`、`BTMoveToTarget.cs`、`BTCheckEnemyInRange.cs`、`BTAttack.cs`、`BTCheckSkillReady.cs`、`BTUseSkill.cs` 以及对应 `.meta`
  - 检查 `AIController`、`BTContext`、`BTSelector`/`BTSequence`/`BTInverter` 对上述类型的引用，全部清除
  - 确认 `Assets/Scripts/AI/Nodes/` 只保留 `BTWander.cs`、`BTCombat.cs`、`BTPostCombat.cs`、`BTFindNearestEnemy.cs`
  - _需求：7.1、7.2、7.5、7.6_

- [ ] 9. 联调与边界验证
  - 在场景中对 `Player`、`MinorEnemy`、`Boss` 三种角色跑通 Wander → Combat(Engage/Strike) → PostCombat → Wander 的完整循环
  - 构造"敌人位于身后进入战斗"的场景，验证首击前插入一帧转身且后续按攻速连续出手
  - 构造"目标在 `engageDistance` 边界漂移"场景，验证 `Engage↔Strike` 无抖动（滞回带生效）
  - 构造"目标穿过身后导致朝向翻转"场景，验证不会把 AI 拉回到 Engage/Wander
  - 切换 `ControlModeManager` 手动/自动模式，验证 `PauseAI`/`ResumeAI` 不残留战斗状态
  - _需求：2.2、2.5、4.1.3、5.3、5.4、6.5、8.1、8.2、8.3、8.4_

