# 实施计划

- [ ] 1. 创建伤害触发方式枚举和伤害范围组件 `ProjectileDamageArea`
   - 在 `Assets/Scripts/Skill/` 下创建新文件 `ProjectileDamageArea.cs`
   - 定义 `DamageTriggerMode` 枚举：`AnimationEvent`、`OnCollision`
   - 创建 `ProjectileDamageArea` MonoBehaviour 组件，包含：
     - `DamageTriggerMode triggerMode` 字段（选择伤害触发方式）
     - `AttackRangeShape[] damageShapes` 字段（复用现有形状体系）
     - `bool defaultFacesRight` 字段（用于形状偏移镜像）
   - 提供 `DealDamage(float damage, CharacterType casterType)` 公共方法，执行范围内伤害检测
   - 添加 `hasDamaged` 标志位防止重复伤害
   - 提供 `ResetDamageState()` 方法供对象池重置时调用
   - _需求：2.1、2.2、2.4、5.1、5.4_

- [ ] 2. 为 `ProjectileDamageArea` 实现动画帧事件触发伤害
   - 添加 `OnDamageEvent()` 公共方法作为动画帧事件回调
   - 该方法内部调用 `DealDamage()` 执行伤害检测
   - 当 `triggerMode` 为 `AnimationEvent` 时，仅通过此方法触发伤害
   - 如果 `damageShapes` 未配置，输出错误日志并跳过
   - _需求：3.1、3.2、3.3、3.4、5.2_

- [ ] 3. 为 `ProjectileDamageArea` 实现碰撞触发伤害
   - 添加 Collider2D 碰撞检测逻辑（`OnTriggerEnter2D`）
   - 当 `triggerMode` 为 `OnCollision` 时，碰撞到敌方目标立即调用 `DealDamage()`
   - 碰撞后通知 `ProjectileController` 进入爆炸状态
   - _需求：4.1、4.2、4.3、5.3_

- [ ] 4. 为 `ProjectileDamageArea` 添加 Gizmo 可视化
   - 实现 `OnDrawGizmosSelected()` 方法
   - 遍历 `damageShapes` 数组，根据形状类型（Box/Circle）绘制对应的 Gizmo 线框
   - 考虑 `defaultFacesRight` 对偏移的影响
   - _需求：2.3_

- [ ] 5. 移除 `ProjectileSkillEffectData` 中的 Explosion Settings
   - 删除 `explosionRadius` 字段及其 `[Header("Explosion Settings")]` 标注
   - 更新 `Execute()` 方法中调用 `controller.Launch()` 的代码，移除 `explosionRadius` 参数传递
   - _需求：1.1、1.3_

- [ ] 6. 重构 `ProjectileController.Launch()` 方法签名和爆炸逻辑
   - 从 `Launch()` 参数列表中移除 `explosionRadius` 参数
   - 移除 `ProjectileController` 内部的 `explosionRadius`、`damage`、`casterType` 私有字段（伤害逻辑转移到 `ProjectileDamageArea`）
   - 删除 `DealAOEDamage()` 方法
   - 修改 `EnterExplosionState()`：不再直接调用 `DealAOEDamage()`，改为通过 `ProjectileDamageArea` 组件触发伤害
     - 如果 `triggerMode` 为 `OnCollision`，在到达终点时调用 `ProjectileDamageArea.DealDamage()` 作为兜底
     - 如果 `triggerMode` 为 `AnimationEvent`，不主动触发伤害（等待动画帧事件）
   - 保留 `Launch()` 中的 `damage` 和 `casterType` 参数，在 Launch 时传递给 `ProjectileDamageArea` 组件
   - 添加公共方法 `EnterExplosionFromCollision()` 供碰撞触发时从外部调用进入爆炸状态
   - _需求：1.2、4.4、5.2、5.3_

- [ ] 7. 更新 `ProjectileController.ResetState()` 以兼容新组件
   - 在 `ResetState()` 中调用 `ProjectileDamageArea.ResetDamageState()` 重置伤害状态
   - 确保对象池回收和重用时所有状态正确重置
   - _需求：6.1_

- [ ] 8. 更新 `ProjectileSkillEffectDataEditor` 编辑器面板
   - 移除 `explosionRadius` 相关的 SerializedProperty 和 Inspector 绘制代码
   - 移除 Explosion Settings 区域的标题和帮助信息
   - _需求：1.4_

- [ ] 9. 更新 `CreateProjectilePrefab` 编辑器工具
   - 在创建投射物预制体模板时自动添加 `ProjectileDamageArea` 组件
   - 更新提示文本，说明新的伤害配置方式
   - _需求：2.1、6.2_

- [ ] 10. 验证近战技能系统不受影响
   - 确认 `MeleeSkillEffectData` 及其相关逻辑无任何修改
   - 确认 `CombatSystem` 中对 `MeleeSkillEffectData` 的调用路径不受影响
   - 确认 `SkillEffectData` 基类的 Displacement 功能保持不变
   - _需求：6.3、6.4_
