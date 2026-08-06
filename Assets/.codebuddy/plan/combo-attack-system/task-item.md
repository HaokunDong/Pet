# 实施计划

## 背景

本任务清单基于更新后的需求文档，重点修复需求 4（ManualController 输入缓冲与攻击保护）的变更：
- 攻击动画播放期间右键点击敌人不中断当前攻击
- 右键点击空地只取消攻击模式（`isAttacking = false`），不立即重置 combo 状态
- 角色实际改变状态（从 AttackState 退出进入 IdleState/WalkState）时才清除 combo 缓冲并重置 combo 状态

同时修复之前实现中发现的已知问题（循环动画重复触发 OnStateEnd、Animator 卡在上一段攻击状态、范围检查中断 combo 等）。

---

- [ ] 1. ManualController：攻击动画期间右键点击敌人不中断当前攻击
   - 修改 `HandleRightClickInput()` 方法：当角色正在播放攻击动画时（`IsFacingLocked = true` 或 `stateMachine.isAttacking = true`），右键点击敌人不执行 `isAttacking = false` 和 `combatSystem.ResetComboState()`
   - 如果 combo 窗口已打开，将该点击视为 combo 缓冲输入（调用 `BufferComboInput()`）
   - 如果 combo 窗口未打开，忽略该点击（不中断、不重置），当前攻击正常播放完毕
   - 允许更新 `attackTarget`（切换目标），但不中断动画
   - 非攻击状态下（Idle/Walk）右键点击敌人正常发起攻击
   - _需求：4.3、4.4、4.5、4.6_

- [ ] 2. ManualController：右键点击空地时不立即重置 combo 状态
   - 修改 `HandleRightClickInput()` 中点击空地的逻辑：只设置 `isAttacking = false`，不调用 `combatSystem.ResetComboState()`
   - combo 状态的重置延迟到角色实际改变状态时处理（由任务 3 实现）
   - 如果角色正在播放攻击动画，点击空地后角色应在当前攻击段播放完毕后自然退出（因为 `isAttacking = false` 后 `HandleSustainedAttack` 不再 buffer 输入）
   - _需求：4.7_

- [ ] 3. CombatSystem/CharacterAnimator：状态切换时重置 combo 状态
   - 在 `CharacterAnimator.ClearCurrentState()` 中，当 `HandleComboOnStateEnd()` 返回 `false` 且角色即将回到 Idle 时，确保 `ResetComboState()` 被调用（当前已实现）
   - 在 `AttackState.OnExit()` 中添加 `combatSystem.ResetComboState()` 调用，确保任何从 AttackState 退出的路径都会重置 combo
   - 移除 `HandleSustainedAttack()` 中范围检查失败时的 `combatSystem.ResetComboState()` 调用（由状态切换时统一处理）
   - _需求：4.8_

- [ ] 4. ManualController：攻击动画期间跳过范围检查
   - 确认 `HandleSustainedAttack()` 中当 `IsFacingLocked = true` 时跳过 `IsTargetInAttackRange` 检查（之前已修复）
   - 验证修复逻辑：攻击动画期间不因范围检查失败而退出攻击模式
   - 确保攻击动画结束后（`IsFacingLocked = false`）恢复正常的范围检查
   - _需求：4.2、4.5_

- [ ] 5. CharacterAnimator：防止循环动画 OnStateEnd 重复触发 combo 逻辑
   - 确认 `ClearCurrentState()` 开头的 `isIdle || isWalking` 早期返回逻辑（之前已修复）
   - 验证：当状态机已在 Idle/Walk 状态时，循环动画的 `OnStateEnd` 事件不会再次触发 combo 判断
   - _需求：1.5、1.6_

- [ ] 6. CharacterAnimator：PlayAttack 确保 Animator 正确重置
   - 确认 `PlayAttack()` 中在 `ChangeState<AttackState>()` 后强制设置 `AttackIndex` 并播放 EmptyState（之前已修复）
   - 验证：combo 结束后发起新攻击时，Animator 不会卡在上一段攻击状态
   - 确保 `animator.SetInteger("AttackIndex", attackIndex)` 在 `animator.Play()` 之前执行
   - _需求：3.1_

- [ ] 7. CharacterAnimator：PlayComboNextAttack 不退出 AttackState
   - 确认 `PlayComboNextAttack()` 不使用 `ForceChangeState`，而是直接更新 `AttackIndex` 并 `Animator.Play("Base Layer.Attack.EmptyState")` + `Animator.Update(0f)`（之前已修复）
   - 验证：combo 衔接时 Animator 正确从 EmptyState 跳转到下一段攻击动画
   - 确保 `stateMachine.isAttacking` 和 `stateMachine.canBeInterrupted` 被正确维护
   - _需求：1.4_

- [ ] 8. AI 模式（BTCombat）：验证 combo 自动衔接
   - 确认 `HandleAIComboBuffer()` 在攻击动画期间被正确调用（通过 `stateMachine.isAttacking` 检查）
   - 确认 `combatSystem.IsAttacking` 在 hit frame 后为 `false` 时，第二个检查 `stateMachine.isAttacking` 能正确通过
   - 验证：AI 模式下 combo 窗口打开后，如果敌人在范围内，`BufferComboInput()` 被正确调用
   - 如果 AI 模式仍不工作，在 `HandleAIComboBuffer` 中添加调试日志定位问题
   - _需求：6.1、6.2、6.3、6.4_

- [ ] 9. 清理调试日志
   - 在所有功能验证通过后，移除 `CombatSystem.cs` 中的 `Debug.Log` 调试日志
   - 移除 `CharacterAnimator.cs` 中的 `Debug.Log` 调试日志
   - 移除 `ManualController.cs` 中的 `Debug.Log` 调试日志
   - 保留必要的注释说明
   - _需求：所有_

- [ ] 10. 集成验证与边界情况测试
   - 验证手动模式：右键点击一次敌人后自动连击（Attack1 → Attack2 → Attack1 → ...循环）
   - 验证手动模式：攻击动画期间右键点击敌人不中断当前攻击
   - 验证手动模式：右键点击空地后当前攻击段播完再回到 Idle
   - 验证手动模式：连续右键点击敌人时，如果在 combo 窗口内则衔接下一段
   - 验证 AI 模式：敌人在范围内时自动打完全套连击
   - 验证 AI 模式：敌人离开范围后当前段播完不衔接
   - 验证受击打断：combo 过程中被打断时正确重置
   - 验证单段攻击角色（`comboCount = 1`）行为不受影响
   - _需求：1-6 全部_
