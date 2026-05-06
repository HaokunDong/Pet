# 实施计划

- [ ] 1. 创建 IState 接口和 StateBase 抽象基类
   - 在 `Assets/Scripts/Character/StateMachine/` 目录下创建 `IState.cs`，定义 `OnEnter()`、`OnExit()`、`OnUpdate()`、`CanEnter()`、`CanExit()` 方法
   - 在同目录下创建 `StateBase.cs`，实现 `IState` 接口，持有 `protected EntityStateMachine machine` 反向引用，提供构造函数注入和虚方法默认实现
   - _需求：1.1、1.2、1.3、1.4_

- [ ] 2. 创建 EntityStateMachine 基类
   - 在 `Assets/Scripts/Character/StateMachine/` 目录下创建 `EntityStateMachine.cs`
   - 实现 Bool 条件变量组（`isIdle`、`isWalking`、`isAttacking`、`isUsingSkill`、`isHit`、`isDead`、`canBeInterrupted`）
   - 实现状态字典 `Dictionary<Type, IState>`、`RegisterState<T>()`、`ChangeState<T>()`（检查 CanExit + CanEnter）、`ForceChangeState<T>()`（无条件转换）
   - 实现 `Update()`（调用当前状态 OnUpdate）、`Reset()`（清零所有 Bool 并回到 Idle）、`ClearProtection()`（设置 canBeInterrupted = true）
   - 实现只读属性 `CurrentState`、`IsInHitState`、`IsInSkillState`、`IsAttacking`
   - 提供 `virtual OnStateChanged(IState from, IState to)` 钩子供子类重写
   - _需求：2.1、2.2、2.3、2.4、2.5、2.6、2.7_

- [ ] 3. 实现具体状态类（IdleState、WalkState、AttackState、SkillState、HitState、DeathState）
   - 在 `Assets/Scripts/Character/StateMachine/States/` 目录下分别创建 6 个状态类文件
   - `IdleState`：OnEnter 设置 `isIdle=true, canBeInterrupted=true`，触发 Idle 动画；CanExit 返回 `canBeInterrupted`
   - `WalkState`：OnEnter 设置 `isWalking=true, canBeInterrupted=true`，触发 Walk 动画；CanExit 返回 `canBeInterrupted`
   - `AttackState`：OnEnter 设置 `isAttacking=true, canBeInterrupted=false`，触发 Attack 动画；CanExit 返回 `canBeInterrupted`
   - `SkillState`：OnEnter 设置 `isUsingSkill=true, canBeInterrupted=false`，根据 skillIndex 触发对应 Skill Trigger；提供 `SetSkillIndex(int)` 方法；CanExit 返回 `canBeInterrupted`
   - `HitState`：OnEnter 设置 `isHit=true, canBeInterrupted=false`，触发 Hit 动画；CanEnter 允许从 Hit 重入（重置动画）；CanExit 返回 `canBeInterrupted`
   - `DeathState`：OnEnter 设置 `isDead=true`，触发 Death 动画；CanEnter 始终返回 true；CanExit 始终返回 false（不允许离开死亡状态）
   - _需求：5.1、5.2、5.3、5.4、5.5、5.6、5.7、6.1、6.2、6.3、6.4、6.5、6.6、6.7_

- [ ] 4. 创建 PlayerStateMachine 子类
   - 在 `Assets/Scripts/Character/StateMachine/` 目录下创建 `PlayerStateMachine.cs`，继承 `EntityStateMachine`
   - 重写 `Initialize()` 方法，注册所有玩家状态实例（Idle、Walk、Attack、Skill、Hit、Death）
   - 添加 `useMultiSkillTriggers` 字段，根据技能数量决定 SkillState 使用单 Trigger 还是多 Trigger
   - _需求：3.1、3.2、3.3、3.4_

- [ ] 5. 创建 EnemyStateMachine 子类
   - 在 `Assets/Scripts/Character/StateMachine/` 目录下创建 `EnemyStateMachine.cs`，继承 `EntityStateMachine`
   - 重写 `Initialize()` 方法，注册所有敌人状态实例
   - 确保 Boss 多技能使用 SkillOne/SkillTwo/SkillThree/SkillFour Trigger
   - _需求：4.1、4.2、4.3、4.4_

- [ ] 6. 重构 CharacterAnimator 集成状态机
   - 移除 `CharacterAnimator` 中的 `isInHitState`、`isInSkillState`、`hitStateTimer`、`skillStateTimer`、`currentAnimState` 枚举及相关计时器逻辑
   - 添加 `EntityStateMachine stateMachine` 字段，在 `Awake()` 或初始化时根据角色类型创建 `PlayerStateMachine` 或 `EnemyStateMachine` 实例
   - 重写 `PlayIdle()`、`PlayWalk()`、`PlayAttack()`、`PlaySkill()`、`PlayHit()`、`PlayDeath()` 方法，内部改为调用 `stateMachine.ChangeState<T>()` 或 `stateMachine.ForceChangeState<T>()`
   - 保持所有公共 API 签名不变（`IsInHitState`、`IsInSkillState`、`ClearSkillState()` 等），改为代理到状态机的 Bool 属性
   - 移除 `Update()` 中的计时器逻辑，改为调用 `stateMachine.Update()`
   - _需求：7.1、7.2、7.3、7.5_

- [ ] 7. 更新 CombatSystem 集成
   - 修改 `CombatSystem.ClearAttackState()` 方法，调用状态机的 `ClearProtection()` 方法代替直接操作 `isInSkillState`
   - 确保 `ApplyNormalAttackDamage()` 和 `ApplySkillDamage()` 完成后通过状态机正确清除保护状态
   - _需求：7.4_

- [ ] 8. 更新 BTCombat 行为树集成
   - 确保行为树中查询角色状态的代码（`IsInHitState`、`IsInSkillState`）通过 `CharacterAnimator` 的代理属性访问状态机
   - 确保行为树驱动的攻击/技能/移动请求通过 `CharacterAnimator` 的公共方法调用状态机的 `ChangeState<T>()`
   - 验证 Boss AI 的技能优先、CD 轮转、普通攻击补充逻辑在新状态机下正常工作
   - _需求：4.4、8.3、8.4_

- [ ] 9. 更新 ManualController 手动操控集成
   - 确保手动操控模式下的点击移动、追击、持续攻击通过状态机正确转换状态
   - 验证手动操控下攻击/技能的保护期（`canBeInterrupted == false`）正常工作，不会被移动打断
   - _需求：8.2_

- [ ] 10. 添加对象池重置支持和代码注释文档
   - 在角色被对象池回收时调用 `stateMachine.Reset()`，确保重新使用时状态干净
   - 在 `EntityStateMachine.cs` 头部添加详细的使用说明注释：如何新增状态、如何修改转换规则、如何为新角色类型创建状态机
   - 确保所有状态类文件都有清晰的 XML 文档注释
   - _需求：9.1、9.2、9.3、9.4、边界情况 5_
