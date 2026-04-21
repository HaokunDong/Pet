# 实施计划

- [ ] 1. 重构 `BTCombat.TickEngage()` — 添加最小安全间距检查与过冲保护
   - 在移动前先计算当前与目标的水平距离，若已 ≤ `engageDistance` 则跳过移动直接切换到 Strike
   - 计算单帧位移后的预期位置，若预期距离 < `engageDistance`，则将位置钳制到目标 X ± `engageDistance` 处
   - 添加穿越检测：比较移动前后角色相对目标的左右侧关系，若发生穿越则钳制到 `engageDistance` 位置
   - 对 `engageDistance` 添加最小兜底值（0.1），防止配置为 0 或极小值时角色完全重叠
   - _需求：1.1、1.2、3.1、3.2、5.1_

- [ ] 2. 修改 `BTCombat.TickStrike()` — 添加朝向防抖逻辑
   - 在 `FaceTowards` 调用前检查与目标的水平距离绝对值
   - 若水平距离 < 死区阈值（0.05），跳过 `FaceTowards` 调用，保持当前朝向不变
   - 若水平距离 ≥ 死区阈值，正常调用 `FaceTowards` 面向目标
   - _需求：2.1、2.2_

- [ ] 3. 在 `BTCombat.Execute()` 中增强 Engage/Strike 状态判定
   - 在状态判定逻辑中，确保当距离 ≤ `engageDistance` 时无论从哪个状态进入都直接切换到 Strike
   - 确保 `engageDistance` 兜底值在状态判定中也生效
   - _需求：1.2、5.1_

- [ ] 4. 验证双向对称性 — 确认玩家和敌人 AI 共用同一套修复逻辑
   - 检查 `BTCombat` 节点在 `AIController` 中对玩家和敌人的构建方式，确认双方使用相同的节点实例化路径
   - 确认 `engageDistance` 对双方各自独立生效，不存在共享状态导致的干扰
   - _需求：4.1、4.2_

- [ ] 5. 边界情况处理与防御性编程
   - 确认墙壁检测逻辑（已有的 `Physics2D.Raycast` 墙检测）在新的间距钳制逻辑之前执行，墙壁阻挡时不会尝试穿墙
   - 确认目标被击退/位移后，下一帧距离重新计算能正确触发 Engage/Strike 状态切换
   - 在 `TickEngage` 中确保 `engageDistance` 兜底值（`Mathf.Max(engageDistance, 0.1f)`）统一应用
   - _需求：5.1、5.2、5.3_

- [ ] 6. 静态代码审查与编译验证
   - 检查修改后的 `BTCombat.cs` 无编译错误
   - 确认没有引入新的未使用变量或死代码
   - 确认修改不影响 `BTWander`、`BTPostCombat`、`BTFindNearestEnemy` 等其他节点的正常运行
   - _需求：5.2、5.3_
