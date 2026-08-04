# 实施计划

- [ ] 1. 在 TrainViewController 中添加 LevelSystem 相关 UI 引用字段
   - 在 `TrainViewController.cs` 中新增 `Text levelText`、`Text talentPointText`、`Text experienceText` 三个私有字段
   - 用于缓存 LevelSystem 节点下三个 Text 组件的引用
   - _需求：5.3_

- [ ] 2. 在 Start() 中查找并绑定 LevelSystem 子节点的 Text 组件
   - 在 `TrainViewController.cs` 的 `Start()` 方法中，通过 `transform.Find("LevelSystem/Level")` 查找 Level 节点并获取其 Text 组件
   - 同理查找 `"LevelSystem/TalentPoint"` 和 `"LevelSystem/Experience"` 节点的 Text 组件
   - 如果节点或组件为 null，输出 `Debug.LogWarning` 日志
   - _需求：5.3、5.1_

- [ ] 3. 实现 UpdateLevelText() 方法填充等级文本
   - 在 `TrainViewController.cs` 中新增 `UpdateLevelText()` 私有方法
   - 从 `cultivationData.level` 读取当前等级值
   - 将 `levelText.text` 设置为格式 `"等级：X"`（X 为等级值）
   - 如果 `cultivationData` 为 null，显示默认值 `"等级：1"`
   - _需求：1.1、1.2、5.1_

- [ ] 4. 实现 UpdateTalentPointText() 方法填充天赋点文本
   - 在 `TrainViewController.cs` 中新增 `UpdateTalentPointText()` 私有方法
   - 从 `availableTalentPoints` 字段读取当前可用天赋点数
   - 将 `talentPointText.text` 设置为格式 `"天赋点：X"`（X 为可用天赋点数）
   - 如果 `cultivationData` 为 null，显示默认值 `"天赋点：0"`
   - 确保显示数值始终 ≥ 0
   - _需求：2.1、2.5、5.1、5.4_

- [ ] 5. 实现 UpdateExperienceText() 方法填充经验值文本
   - 在 `TrainViewController.cs` 中新增 `UpdateExperienceText()` 私有方法
   - 从 `cultivationData.currentExp` 读取当前经验值 A
   - 从 `cultivationData.GetRequiredExp()` 读取升级所需最大经验值 B
   - 将 `experienceText.text` 设置为格式 `"A / B"`
   - 如果 `cultivationData` 为 null，显示默认值 `"0 / 100"`
   - _需求：3.1、3.2、5.1_

- [ ] 6. 在 Start() 初始化流程中调用三个 LevelSystem 文本更新方法
   - 在 `TrainViewController.cs` 的 `Start()` 方法中，在 `InitializePropertyTexts()` 和 `InitializeTalentSystems()` 之后，依次调用 `UpdateLevelText()`、`UpdateTalentPointText()`、`UpdateExperienceText()`
   - 确保面板实例化时三个文本立即显示正确数据
   - _需求：1.1、2.1、3.1_

- [ ] 7. 在天赋点变化时联动更新 TalentPoint 文本
   - 修改 `OnTalentLevelChanged` 回调方法，在属性文本更新后额外调用 `UpdateTalentPointText()`
   - 修改 `OnTalentConfirmed` 回调方法，在确认后额外调用 `UpdateTalentPointText()`
   - 修改 `ResetTempChanges` 相关逻辑（`OnPropertyClicked` 和 `OnDisable`），在重置后也调用 `UpdateTalentPointText()`
   - 确保 PlusButton（天赋点-1）、MinusButton（天赋点+1）、SquareFrame（确认）操作后 TalentPoint 文本实时更新
   - _需求：2.2、2.3、2.4、4.1、4.2、4.3、4.4_
