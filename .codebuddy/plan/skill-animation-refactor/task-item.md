# 实施计划

- [ ] 1. 重构 `CharacterAnimator.cs` 中的 `PlaySkill()` 方法，统一使用 Trigger 触发
   - 移除当前 `useSkillsInt` 分支中直接通过 `SetInteger(HashSkills, skillIndex+1)` 触发 AnyState 转换的逻辑
   - 重写 `PlaySkill(int skillIndex)` 方法：
     - 无技能（skillCount == 0）：不触发技能动画，回退到普通攻击
     - 单技能（skillCount == 1）：仅调用 `SetTrigger(HashSkill)`
     - 多技能（skillCount > 1）：先调用 `SetInteger(HashSkills, skillIndex + 1)` 设置 BlendTree 参数，然后调用 `SetTrigger(HashSkill)` 进入技能状态
   - 确保所有路径最终都通过 `Skill` Trigger 进入技能动画状态
   - _需求：1.1、1.2、2.1、2.2、2.3、2.4、3.1_

- [ ] 2. 确保 `OnSkillEnd` 帧事件正确归零 `Skills` Int 参数
   - 验证 `AnimEventReceiver.cs` 中 `OnSkillEnd()` 方法已存在
   - 验证 `CharacterAnimator.ResetSkillsParam()` 方法：将 `Skills` Int 设为 0 并清除 `isInSkillState` 保护状态
   - 确保 `OnSkillEnd` 仅在多技能角色时归零 `Skills` Int（单技能角色无需此操作）
   - _需求：3.3、5.2、5.3_

- [ ] 3. 修改 `ClearSkillState()` 和超时机制适配新方案
   - `ClearSkillState()` 中：如果是多技能角色（`useSkillsInt == true`），将 `Skills` Int 参数设为 0
   - `Update()` 中 `skillStateTimer` 超时时：同样将 `Skills` 归零作为安全保障
   - 确保超时归零逻辑与 `OnSkillEnd` 帧事件归零逻辑一致
   - _需求：5.4_

- [ ] 4. 修改 `ResetAllTriggers()` 方法适配统一 Trigger 方案
   - 对于 `Skill` Trigger 参数：始终在 `hasSkillParam` 时 ResetTrigger（因为所有角色都使用 Trigger）
   - 对于 `Skills` Int 参数：在 `hasSkillsParam` 且 `useSkillsInt` 时将其设为 0
   - _需求：2.1、5.5_

- [ ] 5. 验证 `CharacterEntity.cs` 初始化流程与新方案兼容
   - 确认 `CharacterEntity` 初始化时调用 `SetSkillCount()` 传入 `characterData.skills` 的长度
   - 确认 `characterData` 或 `skills` 为 null 时传入 0（无技能）
   - _需求：2.2、2.3_

- [ ] 6. 验证 `CombatSystem` 技能 CD 遍历逻辑与新方案兼容
   - 确认 `GetFirstReadySkillIndex()` 正确遍历 skills 数组并检查 CD
   - 确认 `TryUseSkill()` 传递的 `skillIndex` 与新的 `PlaySkill(skillIndex)` 逻辑兼容
   - 确认技能使用后立即开始冷却计时
   - _需求：4.1、4.2、4.3、4.4_

- [ ] 7. 边界情况处理与最终验证
   - 处理动画控制器缺少 `Skill` Trigger 参数的情况（回退到普通攻击）
   - 处理动画控制器缺少 `Skills` Int 参数但角色有多个技能的情况（仅触发 Trigger）
   - 处理技能动画播放中角色被击杀的情况（清除所有状态）
   - 确认 Trigger 消费后自动重置不会导致 AnyState 重复转换
   - _需求：5.5、边界情况 1-5_
