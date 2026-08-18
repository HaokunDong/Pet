# 实施计划

- [ ] 1. 创建 SummonSkillEffectData ScriptableObject 类
   - 在 `Scripts/Data/` 目录下新建 `SummonSkillEffectData.cs`
   - 继承 `SkillEffectData` 抽象基类
   - 添加 `[CreateAssetMenu(fileName = "NewSummonSkillEffect", menuName = "Game/SkillEffect/Summon")]` 特性
   - 定义字段：`summonPrefab`（GameObject）、`summonCount`（int, [Min(1)]）、`summonDuration`（float, [Min(0.1f)]）、`allowMultipleWaves`（bool）
   - 添加生成半径配置字段 `spawnRadius`（float），用于控制召唤物随机生成的范围
   - _需求：1.1, 1.2, 1.3, 1.4, 1.5_

- [ ] 2. 创建 SummonedEntityTracker 组件用于召唤物生命周期管理
   - 在 `Scripts/Combat/` 或 `Scripts/Character/` 目录下新建 `SummonedEntityTracker.cs`
   - 该 MonoBehaviour 挂载在每个召唤物实例上，负责计时并在 `summonDuration` 到期后自动销毁
   - 记录召唤者引用（用于 `allowMultipleWaves = false` 时的刷新机制）
   - 监听 `CharacterEntity.OnDeath` 事件，召唤物被击杀时提前销毁并清理引用
   - 提供公共方法 `ForceDestroy()` 供刷新机制调用
   - _需求：6.1, 6.2, 6.3, 6.4_

- [ ] 3. 创建 SummonOwnerRegistry 静态/单例管理器追踪施法者与召唤物的关系
   - 在 `Scripts/Combat/` 或 `Scripts/Manager/` 目录下新建 `SummonOwnerRegistry.cs`
   - 维护一个 `Dictionary<CharacterEntity, List<GameObject>>` 映射施法者到其存活的召唤物列表
   - 提供 `Register(owner, summon)`、`Unregister(owner, summon)`、`DestroyAllForOwner(owner)` 方法
   - 当 `allowMultipleWaves = false` 时，Execute 前调用 `DestroyAllForOwner` 清除旧召唤物
   - 场景切换时自动清理所有记录
   - _需求：7.1, 7.2, 7.4_

- [ ] 4. 实现 SummonSkillEffectData.Execute() 核心生成逻辑
   - 重写 `Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)` 方法
   - 校验 `summonPrefab` 是否为 null，为 null 时输出错误日志并 return
   - 若 `allowMultipleWaves = false`，调用 `SummonOwnerRegistry.DestroyAllForOwner(casterEntity)` 清除旧召唤物
   - 循环 `summonCount` 次，每次在施法者周围随机计算一个不重叠的生成位置
   - 使用 `Object.Instantiate()` 实例化预制体到计算出的位置
   - _需求：1.3, 2.1, 2.2, 7.2_

- [ ] 5. 实现召唤物实例化后的初始化流程
   - 在 Execute 循环中，对每个实例化的召唤物执行以下初始化：
   - 设置召唤物 GameObject 的 Tag 与施法者一致（`gameObject.tag = caster.gameObject.tag`）
   - 获取 `CharacterEntity` 组件，确认其 `characterData` 已配置，调用 `Initialize()` 确保独立的 `RuntimeCharacterStats`
   - 获取 `AIController` 组件，调用 `InitializeAI()` 启动行为树
   - 挂载 `SummonedEntityTracker` 组件，传入持续时间和施法者引用
   - 向 `SummonOwnerRegistry` 注册该召唤物
   - 校验必要组件是否存在，缺失时输出错误日志
   - _需求：2.3, 2.4, 2.5, 3.1, 4.1, 4.2, 4.3, 5.1, 5.4, 7.3_

- [ ] 6. 实现随机生成位置计算逻辑（避免重叠与障碍物检测）
   - 在 `SummonSkillEffectData` 中编写私有方法 `CalculateSpawnPositions(Vector3 center, int count, float radius)`
   - 使用均匀角度分布 + 随机偏移的方式在圆形区域内生成不重叠的位置
   - 对每个候选位置进行简单的 Physics2D 障碍物检测（OverlapCircle），若被阻挡则尝试偏移寻找可用位置
   - 返回 `Vector3[]` 数组供 Execute 循环使用
   - _需求：2.2, 7.6_

- [ ] 7. 处理场景切换时的召唤物清理
   - 在 `SummonOwnerRegistry` 中订阅 `SceneManager.sceneLoaded` 或 `sceneUnloaded` 事件
   - 场景切换时调用 `ClearAll()` 销毁所有存活的召唤物并清空注册表
   - 确保 `SummonedEntityTracker` 的 `OnDestroy` 中正确从注册表移除自身
   - _需求：7.4_

- [ ] 8. 集成测试与边界情况验证
   - 创建一个测试用的 SummonSkillEffectData 资产，配置召唤物预制体（需有 CharacterEntity + AIController）
   - 验证单次释放生成正确数量的召唤物
   - 验证 `allowMultipleWaves = true` 时多次释放召唤物共存
   - 验证 `allowMultipleWaves = false` 时多次释放会刷新（旧的被销毁）
   - 验证召唤物超时后自动销毁
   - 验证召唤物被击杀后正确清理
   - 验证召唤物 AI 能自动寻敌并攻击
   - _需求：1.1~7.6 全部_
