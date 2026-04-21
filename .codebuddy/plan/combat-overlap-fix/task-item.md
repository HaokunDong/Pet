# 实施计划 — 修复敌我重叠来回跑抖动问题

- [ ] 1. 在 `BTContext` 中暴露抖动修复相关的可调阈值
   - 新增常量/字段：`MinSafeGap`（默认 0.4）、`AttackRangeTolerance`（默认 0.2）
   - 保持与现有 `WallDetectDistance`、`TerrainLayerMask` 字段相同的书写风格，便于后续统一管理
   - _需求：1.3、2.1、5.1_

- [ ] 2. 重构 `BTMoveToTarget` 停止判定，改用"直线距离 vs `GetMaxAttackDistance`"
   - 用 `Vector2.Distance` 到目标中心的距离与 `owner.RuntimeStats.GetMaxAttackDistance()` 比较；当 `attackRangeShapes` 为空时退化为 `attackRange`
   - 去除每帧调用 `IsTargetInAttackRange`（带朝向翻转）作为停止条件，仅保留直线距离语义
   - 停止时执行一次 `FaceTowards(target)` + `PlayIdle()` 并返回 Success
   - _需求：1.1、1.2、1.4、5.2_

- [ ] 3. 在 `BTMoveToTarget` 中加入最小安全间距检查，防止两实体穿越
   - 当 `Mathf.Abs(target.x - owner.x) <= BTContext.MinSafeGap` 时，当前帧不再推进 x 坐标
   - 命中安全间距时：`FaceTowards(target)` + `PlayIdle()`，返回 Success
   - 将原有的 `MIN_STOPPING_DISTANCE` 常量统一为 `BTContext.MinSafeGap`
   - _需求：1.3、4.1、4.2、4.3_

- [ ] 4. 修正 `BTMoveToTarget` 朝向覆盖策略，战斗期间不翻转朝向
   - 当 `context.IsInCombat == true` 时，直接返回 Success，不再调用 `SetFacingDirection(direction)`
   - 仅在"真正移动"分支才调用 `SetFacingDirection(direction)` + `PlayWalk()`
   - 进入"停止/到达范围"分支时调用 `FaceTowards(target)` 覆盖一次朝向（面向目标稳定站定）
   - _需求：3.1、3.2_

- [ ] 5. 给 `BTAttack` 增加"暂时飞出范围"的容差保护
   - 先调用 `FaceTowards(target)` 翻正朝向（若目标与当前朝向异侧）后再做范围判定
   - 当 `IsTargetInAttackRange` 返回 false 时，退化用 `Vector2.Distance <= GetMaxAttackDistance + BTContext.AttackRangeTolerance` 做二次判定
   - 二次判定通过：保留 `IsInCombat = true`，返回 Running（不打断战斗）
   - 二次判定失败：执行原有的退出流程（`IsInCombat = false`, Failure）
   - _需求：2.1、2.3、2.4_

- [ ] 6. 保留 `BTAttack` 对"目标死亡/消失"的立即退出逻辑
   - 确认 `target == null || !target.RuntimeStats.IsAlive` 分支立即返回 Failure 且清 `IsInCombat`
   - 确认 `IsInHitState` 保护期分支不变
   - _需求：2.2、3.3、5.3_

- [ ] 7. 验证与 `ManualController` 手动控制的兼容性
   - 手动控制不经过 `BTMoveToTarget/BTAttack`，确认新阈值仅生效于 AI 行为树路径
   - 若 `ManualController` 内部也存在攻击距离判定，保证未被本次改动污染（只读检查，无需修改）
   - _需求：5.4_

- [ ] 8. 运行期自测 AI 对 AI / 玩家 AI 对 敌人 AI 两种战斗场景
   - 进入挂机战斗，确认两个角色在攻击范围外缘停下、保持安全间距、稳定面向对方
   - 确认不再出现 Idle ↔ Walk 高频切换、来回横跳、相互穿越
   - 确认攻击动画/Hit 动画/死亡流程仍正常触发
   - _需求：1.1、1.2、1.3、2.1、3.1、4.1_
