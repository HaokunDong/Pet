# 实施计划：动画跳帧/取消功能（Animation Cancel / Frame Skip）

- [ ] 1. CombatSystem 新增可取消状态管理
   - 新增 `_canBeCancelled` 私有字段和 `CanBeCancelled` 公开属性
   - 新增 `SetCancellable()` 方法，供 AnimEventReceiver 调用
   - 在 `TryComboNextStep()` 中重置 `_canBeCancelled = false`
   - 在 `TryNormalAttack()` 中重置 `_canBeCancelled = false`
   - 在 `ResetComboState()` 中重置 `_canBeCancelled = false`
   - 新增 `TryCancelAnimation()` 方法：执行跳帧取消逻辑（重置 combo、清理攻击状态、解锁朝向、调用 `ClearCurrentState` 等效清理）
   - _需求：1.1, 1.2, 1.3, 3.5, 3.6_

- [ ] 2. CombatSystem 新增连击跳帧加速逻辑
   - 新增 `HandleCancellablePoint()` 方法，封装 `OnCancellablePoint` 触发时的完整判断逻辑：
     - 优先级①：检查 `_comboInputBuffered && _comboStep + 1 < MaxComboCount` → 调用 `TryComboNextStep()` 立即衔接下一段
     - 优先级②：仅标记 `_canBeCancelled = true`，等待后续非攻击输入
   - 该方法由 AnimEventReceiver 的 `OnCancellablePoint()` 调用
   - _需求：2.1, 2.2, 2.3, 2.4, 2.5_

- [ ] 3. AnimEventReceiver 新增 `OnCancellablePoint` 帧事件处理
   - 新增 `OnCancellablePoint()` 公开方法
   - 方法内调用 `combatSystem.HandleCancellablePoint()`
   - 更新类顶部的文档注释，说明新帧事件的配置方式和推荐顺序
   - _需求：1.1, 1.4, 1.5_

- [ ] 4. ManualController 实现非攻击操作跳帧取消
   - 修改 `HandleRightClickInput()` 中点击空地的逻辑：当 `combatSystem.CanBeCancelled` 为 true 时，调用 `combatSystem.TryCancelAnimation()` 立即取消攻击并开始移动
   - 修改 `HandleSkillInput()` 中技能释放逻辑：当 `combatSystem.CanBeCancelled` 为 true 时，调用 `combatSystem.TryCancelAnimation()` 立即取消攻击并释放技能
   - 当 `CanBeCancelled` 为 false 时保持现有行为（忽略操作或记录意图）
   - _需求：3.1, 3.2, 3.4, 4.1, 4.2, 4.3, 4.4_

- [ ] 5. CharacterAnimator 新增 `CancelCurrentAnimation()` 方法
   - 新增公开方法 `CancelCurrentAnimation()`，执行与 `ClearCurrentState()` 等效的清理逻辑但不检查 combo 衔接
   - 该方法直接解锁朝向、清除保护、切换到 Idle（或由调用方决定下一状态）
   - 供 `CombatSystem.TryCancelAnimation()` 调用
   - _需求：3.5, 3.6_

- [ ] 6. BTCombat AI 模式自动享受连击跳帧加速
   - 验证现有 `HandleAIComboBuffer()` 逻辑：AI 在 combo 窗口期自动缓冲下一段输入
   - 由于连击加速逻辑在 `HandleCancellablePoint()` 中统一处理（检测到缓冲即跳帧），AI 无需额外修改即可自动享受加速
   - 如有需要，在 BTCombat 中添加对 `CanBeCancelled` 的检查以支持 AI 主动取消攻击（追击/技能场景）
   - _需求：5.1, 5.2, 5.3, 5.4_

- [ ] 7. 技能动画跳帧取消支持（扩展）
   - 确保 `HandleCancellablePoint()` 和 `TryCancelAnimation()` 不仅适用于普攻，也适用于技能状态
   - 在 `HandleCancellablePoint()` 中，当处于技能状态时跳过 combo 缓冲检查，直接标记为可取消
   - 验证技能动画中配置 `OnCancellablePoint` 后可以被后续操作取消
   - _需求：6.1, 6.2, 6.3_

- [ ] 8. 边界情况处理与防抖
   - 在 `TryCancelAnimation()` 中加入防抖：设置 `_canBeCancelled = false` 防止同一动画中重复触发
   - 确保 `PlayHit()` 中 `ResetComboState()` 也会重置 `_canBeCancelled`
   - 确保 `PlayDeath()` 路径中 `ClearAttackState()` 也会重置 `_canBeCancelled`
   - 验证未配置 `OnCancellablePoint` 的动画不会进入可取消状态（向后兼容）
   - _需求：边界情况 1, 6, 7_

- [ ] 9. CharacterAnimator 文档注释更新
   - 更新 `CharacterAnimator.cs` 顶部的 Animator Controller 配置指南
   - 新增 `OnCancellablePoint` 帧事件的配置说明和推荐顺序
   - 说明 `OnCancellablePoint` 与 `OnComboWindowOpen` 的关系和灵活配置方式
   - _需求：1.5, 边界情况 2_
