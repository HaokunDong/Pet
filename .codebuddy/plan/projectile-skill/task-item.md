# 实施计划

- [ ] 1. 创建 SkillEffectData 抽象基类
   - 在 `Assets/Scripts/Data/` 下新建 `SkillEffectData.cs`
   - 继承 `ScriptableObject`，定义抽象方法 `Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)`
   - 命名空间 `PetGame`，与现有 Data 脚本保持一致
   - _需求：1.1、1.2_

- [ ] 2. 修改 SkillData，添加 skillEffect 引用字段
   - 在 `Assets/Scripts/Data/SkillData.cs` 中新增 `[Header("Skill Effect")] public SkillEffectData skillEffect` 字段
   - 添加 `#if UNITY_EDITOR` 的 `OnValidate()` 方法，当 `skillEffect` 为空时输出警告日志
   - _需求：1.3、1.5_

- [ ] 3. 创建 ProjectileSkillEffectData 子类
   - 在 `Assets/Scripts/Data/` 下新建 `ProjectileSkillEffectData.cs`，继承 `SkillEffectData`
   - 添加 `[CreateAssetMenu(menuName = "Game/SkillEffect/Projectile")]` 特性
   - 实现以下可配置字段：`projectileSprite`（Sprite）、`explosionAnimatorController`（RuntimeAnimatorController）、`projectileFlightDuration`（float, 默认 0.6）、`projectileArcHeight`（float, 默认 1.5）、`explosionRadius`（float）、`projectileScale`（float, 默认 1.0）
   - 实现 `Execute()` 方法：通过 `caster` 上的 MonoBehaviour 启动投射物协程
   - _需求：2.1、2.2、2.3_

- [ ] 4. 实现投射物飞行组件 ProjectileController
   - 在 `Assets/Scripts/Skill/` 下新建 `ProjectileController.cs`（MonoBehaviour）
   - 实现 `Launch(Vector3 start, Vector3 predictedEnd, float flightDuration, float arcHeight, System.Action onArrive)` 方法
   - 抛物线飞行逻辑：水平线性插值 + 垂直弧度 `arcHeight × 4 × t × (1-t)`
   - 每帧计算运动切线方向并更新 `transform.rotation`，使投射物朝向运动方向
   - 敌人位移预判：在 `ProjectileSkillEffectData.Execute()` 中计算 `预测位置 = 敌人当前位置 + 敌人速度向量 × 飞行时间`，传入 `predictedEnd`
   - 飞行结束后调用 `onArrive` 回调，由外部处理爆炸和回收
   - _需求：3.1、3.2、3.3、3.4、3.5_

- [ ] 5. 实现爆炸效果组件 ExplosionController
   - 在 `Assets/Scripts/Skill/` 下新建 `ExplosionController.cs`（MonoBehaviour）
   - 实现 `Play(RuntimeAnimatorController animController, float explosionRadius, float damage, CharacterType casterType, System.Action onFinish)` 方法
   - 在落点位置播放爆炸动画（通过 Animator + 传入的 RuntimeAnimatorController）
   - 爆炸开始时，使用 `Physics2D.OverlapCircleAll` 或遍历 `GameCharacterManager` 中的角色列表，对 `explosionRadius` 范围内的敌方角色调用 `TakeDamage()`
   - 根据 `casterType` 判断敌方：Player 攻击 Enemy/Boss，Enemy/Boss 攻击 Player
   - 动画播放完毕后调用 `onFinish` 回调，由外部回收到对象池
   - _需求：4.1、4.2、4.3、4.4、4.5_

- [ ] 6. 投射物与爆炸效果的对象池集成
   - 在 `ProjectileSkillEffectData.Execute()` 中，通过动态创建投射物预制体（SpriteRenderer + ProjectileController）并注册到 `PoolMgr`，使用 `PoolMgr.Instance.GetNode()` 获取投射物
   - 投射物到达落点后，通过 `PoolMgr.Instance.PutNode()` 回收
   - 爆炸效果同理：动态创建爆炸预制体（Animator + ExplosionController）并注册到 `PoolMgr`，使用 `GetNode()` 获取，动画播放完毕后 `PutNode()` 回收
   - 确保对象池复用时正确重置投射物和爆炸效果的状态（位置、旋转、激活状态等）
   - _需求：6.1、6.2、6.3、6.4_

- [ ] 7. 修改 CombatSystem.ApplySkillDamage()，集成 SkillEffectData 委托
   - 修改 `Assets/Scripts/Character/CombatSystem.cs` 中的 `ApplySkillDamage()` 方法
   - 当 `skillData.skillEffect != null` 时，调用 `skillData.skillEffect.Execute(this, _cachedTarget, skillData)` 替代原有的直接伤害逻辑
   - 当 `skillData.skillEffect == null` 时，输出警告日志并跳过
   - Execute 调用后立即调用 `ClearAttackState()`，不等待投射物飞行完毕
   - 保持普通攻击流程 `ApplyNormalAttackDamage()` 完全不变
   - _需求：5.1、5.2、5.3、5.4_

- [ ] 8. 获取敌人速度向量以支持位移预判
   - 检查 `CharacterEntity` 或 `RuntimeCharacterStats` 是否已暴露速度向量接口
   - 如果没有，在 `CharacterEntity` 中添加一个公共属性 `Velocity`（基于 Rigidbody2D.velocity 或每帧位置差计算）
   - 在 `ProjectileSkillEffectData.Execute()` 中使用该速度向量计算预测落点
   - _需求：3.1_

- [ ] 9. 创建 BonneSkillData 资产的 SkillEffectData 配置
   - 为已有的 `Assets/DATA/Characters/Bonne/BonneSkillData.asset` 创建对应的 `ProjectileSkillEffectData` 资产
   - 在 `BonneSkillData` 中将 `skillEffect` 字段指向新创建的效果资产
   - 验证 Inspector 中的配置工作流是否顺畅（SkillEffectData → SkillData → CharacterData）
   - _需求：7.1、7.2、7.3_
