# 实施计划：宠物培养系统

- [ ] 1. 创建培养系统配置 ScriptableObject
   - 创建 `Assets/Scripts/Data/CultivationConfig.cs`，定义 ScriptableObject 配置类
   - 包含字段：基础经验值、经验增长系数、最大等级、每个属性每级加成百分比（攻击/防御/生命/攻速/移速/技能CD）
   - 在 `Assets/Resources/Data/` 下创建默认配置资产实例
   - _需求：1.6、3.5_

- [ ] 2. 创建培养数据模型类
   - 创建 `Assets/Scripts/Cultivation/CultivationData.cs`，定义可序列化的培养数据类
   - 包含字段：等级(level)、当前经验值(currentExp)、可用天赋点(talentPoints)、6个属性加成等级(attackLevel, defenseLevel, healthLevel, attackSpeedLevel, moveSpeedLevel, skillCDLevel)
   - 实现 `GetRequiredExp(int level)` 方法，根据配置公式计算升级所需经验
   - 实现 `AddExp(int amount)` 方法，处理经验值增加、升级（含连续升级溢出）和天赋点分配
   - 实现 `UpgradeAttribute(AttributeType type)` 方法，消耗天赋点提升指定属性
   - _需求：1.1、1.2、1.6、2.1、2.2、3.1_

- [ ] 3. 实现培养属性加成与 RuntimeCharacterStats 集成
   - 创建 `Assets/Scripts/Cultivation/CultivationManager.cs`，作为培养系统的核心管理器
   - 实现 `ApplyCultivationBonuses(CharacterEntity entity, CultivationData data)` 方法，将百分比加成应用到角色的 RuntimeStats
   - 在属性升级时立即调用重新计算，确保属性实时生效
   - 处理角色切换时的培养数据加载/卸载逻辑
   - _需求：3.2、3.4、3.5_

- [ ] 4. 在 CharacterData 中添加经验值奖励字段
   - 在 `CharacterData.cs` 中添加 `expReward` 字段（默认值10）
   - 在敌人死亡逻辑中（`CharacterEntity.Die()` 或战斗系统中），向击杀者的培养数据添加经验值
   - 确保只有玩家角色击败敌人时才获得经验
   - _需求：6.1、6.2、6.3_

- [ ] 5. 创建培养界面 UI 控制脚本
   - 创建 `Assets/Scripts/UI/CultivationPanelUI.cs`，管理培养界面的显示/隐藏和数据绑定
   - 绑定 UI 元素引用：等级文本、天赋点文本、经验条(Fill Image + 文本)、6个属性行(文本+按钮)、关闭按钮
   - 实现 `Open(CharacterEntity entity)` 方法，打开面板并加载对应角色的培养数据
   - 实现 `Close()` 方法，关闭面板并通知 ControlModeManager 恢复控制模式
   - 实现 `RefreshUI()` 方法，刷新所有 UI 元素显示
   - 实现升级按钮点击回调，调用 CultivationData 的升级方法并刷新 UI
   - 实现天赋点为0时禁用所有升级按钮的逻辑
   - _需求：4.1、4.3、4.4、4.5、4.6、4.7_

- [ ] 6. 修改 ControlModeManager 连接培养界面
   - 修改 `OnTrainClicked()` 方法，调用 CultivationPanelUI 的 Open 方法打开培养界面
   - 确保打开培养界面时关闭按钮面板，角色保持暂停状态
   - 培养界面关闭时恢复角色之前的控制模式（AI 或手动）
   - _需求：4.2、4.4、4.8_

- [ ] 7. 搭建 CultivationPanel 预制体
   - 在 `Assets/Resources/Prefabs/UI/` 下创建 `CultivationPanel.prefab`
   - 按照需求文档的布局搭建 UI 层级结构（等级、天赋点、经验条、6个属性行+升级按钮、关闭按钮）
   - 将 CultivationPanelUI 脚本挂载到预制体上并绑定所有 UI 引用
   - _需求：4.1、4.7_

- [ ] 8. 实现数据持久化（存档/读档）
   - 在 `SaveData.cs` 中添加 `CultivationSaveData` 序列化类，包含所有培养字段
   - 在 `GameSaveData` 中添加培养数据列表字段
   - 修改 `SaveManager.cs` 的 `SaveGame()` 方法，保存每个玩家角色的培养数据
   - 修改 `SaveManager.cs` 的 `ApplySaveData()` 方法，加载时恢复培养数据并重新计算属性加成
   - _需求：5.1、5.2、5.3_

- [ ] 9. 实现经验值变化时的 UI 实时更新
   - 在 CultivationData 中添加事件回调（OnExpChanged、OnLevelUp、OnTalentPointChanged、OnAttributeUpgraded）
   - CultivationPanelUI 订阅这些事件，在培养界面打开时实时刷新显示
   - 确保经验进度条填充比例、等级数字、天赋点数字在变化时立即更新
   - _需求：1.4、1.5、2.4、3.3_

- [ ] 10. 集成测试与边界情况处理
   - 验证一次获得大量经验时连续升级的正确性（经验溢出循环处理）
   - 验证角色切换时培养数据的独立性（每个角色有自己的培养进度）
   - 验证天赋点为0时所有升级按钮正确禁用
   - 验证属性加成在升级后立即生效（攻击力、防御力、生命值、攻速、移速、技能CD）
   - 验证存档/读档后培养数据完整恢复
   - _需求：1.1、2.3、3.2、5.2_
