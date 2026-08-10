# 实施计划

- [ ] 1. 创建进化条件数据模型
   - 新建 `Scripts/Evolution/EvolutionRequirement.cs`，定义 `EvolutionRequirementType` 枚举（Level、Material、KillCount）和 `[System.Serializable]` 的 `EvolutionRequirement` 类（包含 type、targetValue、materialId、description 字段）
   - 确保该类可在 Unity Inspector 中序列化显示和编辑
   - _需求：2.1、2.6_

- [ ] 2. 扩展 CharacterData 添加进化配置字段
   - 在 `Scripts/Data/CharacterData.cs` 中新增 `[Header("Evolution")]` 区域
   - 添加 `CharacterData evolutionTarget` 引用字段，用于指定进化体
   - 添加 `EvolutionRequirement[] evolutionRequirements` 数组字段，用于配置进化条件列表
   - _需求：1.1、1.3、1.4、2.1_

- [ ] 3. 实现击杀统计管理器 KillStatsManager
   - 新建 `Scripts/Evolution/KillStatsManager.cs`，继承 `Singleton<KillStatsManager>`
   - 实现按 characterId 记录和查询累计击杀数的方法（`AddKill(string characterId)`、`GetKillCount(string characterId)`）
   - 在角色击杀敌人的逻辑处（CharacterEntity 或战斗系统中）调用 `KillStatsManager.Instance.AddKill()` 进行统计
   - _需求：6.1、6.2、6.3_

- [ ] 4. 实现材料管理器 MaterialManager
   - 新建 `Scripts/Evolution/MaterialManager.cs`，继承 `Singleton<MaterialManager>`
   - 实现材料查询方法 `GetMaterialCount(string materialId)` 和扣除方法 `ConsumeMaterial(string materialId, int amount)`
   - 提供 `AddMaterial(string materialId, int amount)` 方法用于测试和后续材料获取系统对接
   - _需求：7.1、7.2、7.3_

- [ ] 5. 实现进化条件检测服务 EvolutionChecker
   - 新建 `Scripts/Evolution/EvolutionChecker.cs`，提供静态方法 `CheckAllRequirements(CharacterData data)` 返回每个条件的完成状态列表
   - 实现各条件类型的检测逻辑：Level 类型从 CultivationManager 获取等级、Material 类型从 MaterialManager 查询数量、KillCount 类型从 KillStatsManager 查询击杀数
   - 提供 `AreAllRequirementsMet(CharacterData data)` 方法返回是否全部满足
   - 提供 `GetCurrentValue(EvolutionRequirement req, string characterId)` 方法获取条件当前进度值
   - _需求：3.1、3.2、3.3、3.4、3.5_

- [ ] 6. 在 TrainViewController 中集成进化条件显示（RequirmentFrame）
   - 在 `TrainViewController.cs` 的 `Start()` 中查找 RequirmentFrame 子节点及其子 Text 组件
   - 实现 `UpdateEvolutionRequirementDisplay()` 方法，遍历 characterData 的 evolutionRequirements，生成格式化文本（"[描述]: 当前值/目标值"），已满足条件用绿色/✓标记，未满足用红色/✗标记
   - 当角色无进化体时显示"无可用进化"
   - 在等级变化等事件回调中调用刷新方法实现实时更新
   - _需求：4.1、4.2、4.3、4.4、4.5、4.6_

- [ ] 7. 在 TrainViewController 中集成 EvolutionButton 逻辑
   - 在 `TrainViewController.cs` 的 `Start()` 中查找 EvolutionButton 并绑定点击事件
   - 根据 `EvolutionChecker.AreAllRequirementsMet()` 结果设置按钮的 interactable 状态
   - 当角色无进化体（evolutionTarget 为空）时，设置按钮不可交互
   - _需求：3.2、3.3、5.5_

- [ ] 8. 实现进化执行逻辑
   - 在 EvolutionButton 点击回调中，调用 `CardSplineDistributor.AddCharacterData(evolutionTarget)` 将进化体添加到卡牌列表
   - 添加重复检测逻辑：如果进化体已存在于卡牌列表中则阻止添加并提示
   - 进化成功后调用 `MaterialManager.ConsumeMaterial()` 消耗材料条件中的材料
   - 进化成功后输出日志提示并更新 RequirmentFrame 显示为"已进化"
   - _需求：5.1、5.2、5.3、5.4、5.6_

- [ ] 9. 在战斗系统中接入击杀统计
   - 找到角色击杀敌人的代码位置（CharacterEntity 的死亡处理或伤害系统中）
   - 当玩家角色击杀敌人时，调用 `KillStatsManager.Instance.AddKill(attackerCharacterId)` 记录击杀
   - 确保只统计玩家角色（CharacterType.Player）的击杀
   - _需求：6.1、6.2_

- [ ] 10. 整体联调与边界情况处理
   - 验证无进化体角色的 UI 状态（按钮不可交互、RequirmentFrame 显示"无可用进化"）
   - 验证空条件列表时默认满足进化条件的逻辑
   - 验证多级进化链（A→B→C）场景：A 进化后 B 的卡牌也能正确显示其进化条件
   - 验证进化成功后 RequirmentFrame 和 EvolutionButton 状态正确更新
   - _需求：1.2、2.5、5.4、5.6_
