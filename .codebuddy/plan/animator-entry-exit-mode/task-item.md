# 实施计划：Animator Controller 连线改为 Entry/Exit 模式

- [ ] 1. 修改 CharacterAnimator.cs - 移除 Play 方法中多余的 ResetAllBools() 调用
   - 移除 `PlayIdle()`、`PlayWalk()`、`PlayAttack()`、`PlaySkill()`、`PlayHit()` 中的 `ResetAllBools()` 调用
   - 仅在 `PlayDeath()` 和 `ResetStateMachine()` 中保留 `ResetAllBools()` 调用（这两个是强制场景）
   - 状态切换的 Bool 清除工作完全由各状态的 `OnExit()` 方法负责
   - 更新 `PlayIdle()` 和 `PlayWalk()` 中的注释，将 "prevent AnyState self-transition restart" 改为 Entry/Exit 模式的描述
   - _需求：2.1、2.3、6.1、6.2、6.3_

- [ ] 2. 修改 CharacterAnimator.cs - 更新类级别注释和文档
   - 将类顶部注释中关于 "Bool condition-driven logic" 的描述更新为明确的 Entry/Exit 模式说明
   - 更新 `ResetAllBools()` 方法的注释，说明该方法仅用于强制重置场景（Death、Reset）
   - _需求：6.3_

- [ ] 3. 修改 HitState.cs - 适配 Entry/Exit 模式下的重入逻辑
   - 修改 `OnEnter()` 中的重入逻辑：在 Entry/Exit 模式下，重入是通过 `OnExit()`（Bool=false → Exit）再 `OnEnter()`（Bool=true → Entry → Hit）自然实现的
   - 移除 `machine.Animator.Play("Hit", 0, 0f)` 的手动重播逻辑（因为 Entry/Exit 模式下重新进入状态会自动从头播放）
   - 更新 `CanExit()` 逻辑：当目标状态是 HitState 时（即重入场景），应允许退出（Hit 可以打断 Hit）
   - _需求：7.1、7.2_

- [ ] 4. 修改 EntityStateMachine.cs - 适配 HitState 重入的 ChangeState 逻辑
   - 修改 `ChangeState<T>()` 中对 HitState 重入的处理：当前状态是 HitState 且目标也是 HitState 时，跳过 `CanExit()` 检查（允许 Hit 打断 Hit）
   - 确保 `ExecuteTransition()` 的 OnExit → OnEnter 时序在重入场景下也能正确工作
   - _需求：1.1、7.1、7.2_

- [ ] 5. 修改 IdleState.cs - 确保 Idle 作为默认回退状态正确工作
   - 确认 `OnEnter()` 中设置 `Idle Bool = true` 的逻辑正确（当从 Entry 默认路径进入时）
   - 确认 `OnExit()` 中设置 `Idle Bool = false` 的逻辑正确（当切换到其他状态时触发退出到 Exit）
   - 验证当 Idle 是唯一 Bool 为 true 的状态时，Entry 能正确路由到 Idle
   - _需求：3.1、3.2、5.1_

- [ ] 6. 修改 CharacterAnimator.cs - 添加受保护状态结束后自动回 Idle 的逻辑
   - 在 `ClearSkillState()` / `ClearProtection()` 被调用后，如果当前状态是 Attack/Skill/Hit 且 canBeInterrupted 变为 true，自动切换到 IdleState
   - 确保 `ClearProtection()` 后状态机能正确从受保护状态退出并回到 Idle
   - _需求：3.3_

- [ ] 7. 验证所有状态的 OnExit() 正确清除 Bool 参数
   - 确认 `WalkState.OnExit()` 设置 `Walk Bool = false` ✓（已实现）
   - 确认 `AttackState.OnExit()` 设置 `Attack Bool = false` ✓（已实现）
   - 确认 `SkillState.OnExit()` 设置对应 `Skill Bool = false` ✓（已实现）
   - 确认 `DeathState.OnExit()` 设置 `Death Bool = false` ✓（已实现）
   - 如有遗漏则补充修复
   - _需求：5.1、5.2、5.3、5.4、5.5、5.6_

- [ ] 8. 编写 Animator Controller 配置指南注释
   - 在 `CharacterAnimator.cs` 类顶部注释中添加 Animator Controller 的 Entry/Exit 连线配置说明
   - 说明每个状态的 Entry 转换条件（Bool == true）和 Exit 转换条件（Bool == false）
   - 说明 Idle 作为默认路径（无条件从 Entry 进入）的配置方式
   - 说明所有转换都应取消勾选 Has Exit Time
   - _需求：4.1、4.2、4.3、4.4、4.5、4.6、5.1-5.6_
