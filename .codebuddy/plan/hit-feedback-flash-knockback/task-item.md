# 实施计划

- [ ] 1. 修改 `FlashEffect.cs` — 支持中断重播闪白效果
   - 修改 `TriggerFlash()` 方法，移除 `if (IsFlashing) return false` 的限制，改为：若正在闪白则停止当前协程并重新开始
   - 添加 `ResetFlash()` 公共方法，用于立即停止闪白并重置为正常颜色（供死亡时调用）
   - _需求：1.5、1.6_

- [ ] 2. 在 `CharacterEntity.TakeDamage()` 中接入闪白效果
   - 在 `TakeDamage()` 方法中，Hit 动画播放之后调用 `FlashEffect.TriggerFlash()`
   - 通过 `GetComponent<FlashEffect>()` 获取组件引用（初始化时缓存）
   - 在 `Die()` 方法中调用 `FlashEffect.ResetFlash()` 停止闪白
   - _需求：1.1、1.6、3.1_

- [ ] 3. 确保角色初始化时自动挂载 `FlashEffect` 组件并使用正确材质
   - 在 `CharacterEntity.Initialize()` 中检查是否已有 `FlashEffect` 组件，若无则自动添加
   - 检查 `SpriteRenderer` 的材质 shader 是否为 `Game/CharacterFlash`，若不是则通过 `Resources.Load` 加载 `CharacterFlash_Mat` 并赋值
   - _需求：1.3、1.4_

- [ ] 4. 在 `CharacterData` 中添加击退参数配置字段
   - 添加 `knockbackHorizontalSpeed`（默认 2.0）、`knockbackVerticalSpeed`（默认 1.5）、`knockbackGravity`（默认 8.0）三个可配置字段
   - 在 `RuntimeCharacterStats` 中同步添加对应的运行时字段并在 `InitFromData()` 中初始化
   - _需求：2.6、2.8_

- [ ] 5. 创建 `KnockbackController.cs` 击退控制器组件
   - 新建脚本，挂载在角色 GameObject 上，负责管理击退状态和位移
   - 实现 `ApplyKnockback(Vector2 attackerPosition)` 公共方法：根据攻击者位置计算击退方向（远离攻击者 + 向上），设置初始速度
   - 在 `Update()` 中：若处于击退状态，每帧施加重力衰减垂直速度、更新 `transform.position`；当 Y 坐标回到地面高度时结束击退并钳制 Y 坐标
   - 暴露 `IsInKnockback` 属性供外部查询
   - 支持击退过程中再次受击时重置速度（需求 2.7）
   - _需求：2.1、2.2、2.4、2.7_

- [ ] 6. 在 `CharacterEntity.TakeDamage()` 中接入击退效果
   - 修改 `TakeDamage()` 方法签名，增加攻击者信息参数（`CharacterEntity attacker`），以便计算击退方向
   - 在 `TakeDamage()` 中调用 `KnockbackController.ApplyKnockback(attacker.transform.position)`
   - 同步修改所有 `TakeDamage()` 的调用方（`CombatSystem.ApplyNormalAttackDamage()`、`CombatSystem.ApplySkillDamage()`），传入攻击者引用
   - _需求：2.1、3.1、3.3_

- [ ] 7. 在 AI 行为树中协调击退状态 — 防止 AI 移动覆盖击退位移
   - 在 `BTCombat.TickEngage()` 和 `BTCombat.TickStrike()` 开头检查 `KnockbackController.IsInKnockback`，若为 true 则跳过本帧的移动和攻击逻辑
   - 确保击退结束后 AI 能正常恢复 Engage/Strike 状态判定
   - _需求：2.3、2.4、3.2_

- [ ] 8. 击退边界保护 — 场景边界钳制
   - 在 `KnockbackController.Update()` 的位移计算后，将角色 X 坐标钳制在场景可活动范围内（可通过常量或配置定义左右边界）
   - 确保角色不会被击退出屏幕
   - _需求：2.5_

- [ ] 9. 编译验证与集成检查
   - 确认修改后的所有脚本无编译错误
   - 确认 `TakeDamage()` 签名变更后所有调用方已同步更新
   - 确认闪白和击退效果在玩家角色和敌人角色上均能独立正常工作
   - _需求：3.1、3.2、3.3_
