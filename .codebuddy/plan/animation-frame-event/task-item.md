# 实施计划

- [ ] 1. 创建 AnimEventReceiver 动画帧事件接收器组件
   - 新建 `Assets/Scripts/Character/AnimEventReceiver.cs`
   - 挂载在角色 GameObject 上（与 Animator 同级），负责接收 Unity Animation Event 回调
   - 实现 `OnAttackHit()` 方法：从关联的 CombatSystem 获取缓存目标，调用普通攻击伤害逻辑
   - 实现 `OnSkillHit()` 方法：从关联的 CombatSystem 获取缓存目标和技能索引，调用技能伤害逻辑
   - 在 Awake 中通过 GetComponent 获取 CombatSystem 引用
   - 添加目标为 null 或已死亡时的安全检查和日志输出
   - _需求：1.1、1.2、1.3、1.4、1.5_

- [ ] 2. 改造 CombatSystem 支持延迟伤害（缓存目标与帧事件回调）
   - [ ] 2.1 添加攻击目标缓存字段
     - 添加 `private CharacterEntity _cachedTarget` 字段用于缓存当前攻击目标
     - 添加 `private int _cachedSkillIndex = -1` 字段用于缓存当前使用的技能索引
     - 添加 `private bool _isAttacking` 标志位表示是否处于攻击状态
     - 提供公共属性 `CachedTarget`、`CachedSkillIndex`、`IsAttacking` 供 AnimEventReceiver 读取
     - _需求：2.1、2.2_
   - [ ] 2.2 修改 `TryNormalAttack()` 方法
     - 移除已注释的 `target.TakeDamage()` 行
     - 在播放攻击动画前缓存目标：`_cachedTarget = target`，设置 `_isAttacking = true`
     - 保留原有的范围检查、攻击速度间隔检查、朝向和动画播放逻辑不变
     - _需求：2.1_
   - [ ] 2.3 修改 `TryUseSkill()` 方法
     - 移除 `target.TakeDamage(skillData.damage)` 直接伤害调用
     - 移除技能特效实例化代码（移至帧事件回调中）
     - 在播放技能动画前缓存目标和技能索引：`_cachedTarget = target`，`_cachedSkillIndex = skillIndex`，`_isAttacking = true`
     - 保留原有的冷却检查、范围检查、朝向、动画播放和冷却启动逻辑不变
     - _需求：2.2_
   - [ ] 2.4 添加帧事件回调方法供 AnimEventReceiver 调用
     - 实现 `ApplyNormalAttackDamage()` 方法：检查 `_cachedTarget` 有效性，调用 `_cachedTarget.TakeDamage(entity.RuntimeStats.attackPower)`，清理缓存状态
     - 实现 `ApplySkillDamage()` 方法：检查 `_cachedTarget` 和 `_cachedSkillIndex` 有效性，调用 `_cachedTarget.TakeDamage(skillData.damage)`，生成技能特效（若有 effectPrefab），2秒后销毁特效，清理缓存状态
     - 实现 `ClearAttackState()` 方法：重置 `_cachedTarget = null`、`_cachedSkillIndex = -1`、`_isAttacking = false`，用于攻击中断时的状态清理
     - _需求：2.3、2.4、2.5、5.1、5.2、5.3_

- [ ] 3. 改造 BTAttack 行为树节点，移除直接伤害调用
   - 修改 `Assets/Scripts/AI/Nodes/BTAttack.cs` 的 `Execute()` 方法
   - 移除 `target.TakeDamage(owner.RuntimeStats.attackPower)` 直接伤害调用
   - 改为调用 `owner.GetComponent<CombatSystem>().TryNormalAttack(target)`（或在构造时缓存 CombatSystem 引用）
   - 移除节点内部的攻击速度冷却检查和动画播放逻辑（这些已由 CombatSystem 统一处理）
   - 根据 `TryNormalAttack()` 返回值决定节点状态
   - _需求：3.1_

- [ ] 4. 改造 BTUseSkill 行为树节点，移除直接伤害和特效调用
   - 修改 `Assets/Scripts/AI/Nodes/BTUseSkill.cs` 的 `Execute()` 方法
   - 移除 `target.TakeDamage(skillData.damage)` 直接伤害调用
   - 移除技能特效实例化代码块（`Object.Instantiate` / `Object.Destroy`）
   - 改为调用 `owner.GetComponent<CombatSystem>().TryUseSkill(skillIndex, target)`（或在构造时缓存 CombatSystem 引用）
   - 移除节点内部的范围检查、朝向、动画播放和冷却启动逻辑（这些已由 CombatSystem 统一处理）
   - 根据 `TryUseSkill()` 返回值决定节点状态
   - _需求：3.2_

- [ ] 5. 增强 CharacterAnimator 支持攻击中断状态清理
   - 修改 `Assets/Scripts/Character/CharacterAnimator.cs`
   - 在 `PlayIdle()`、`PlayWalk()` 等非攻击动画切换时，通知 CombatSystem 清理攻击状态（调用 `ClearAttackState()`）
   - 确保攻击动画被其他动画打断时，缓存的伤害事件不会残留
   - _需求：1.5、4.2_

- [ ] 6. 为动画片段配置 Animation Event（Unity Editor 操作指南）
   - 在代码注释或 README 中记录 Unity Editor 配置步骤：
     - 普通攻击动画片段：在命中帧添加 Animation Event，回调方法名 `OnAttackHit`，无参数
     - 技能动画片段：在命中帧添加 Animation Event，回调方法名 `OnSkillHit`，无参数（技能索引从 CombatSystem 缓存读取）
   - 在 AnimEventReceiver 组件的类注释中添加配置说明
   - _需求：4.1、4.2_