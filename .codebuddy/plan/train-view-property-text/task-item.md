# 实施计划

- [ ] 1. 扩展 CultivationData，添加天赋点可用数量计算逻辑
   - 在 `CultivationData` 中添加 `GetAvailableTalentPoints()` 方法，计算公式为：`(level - 1) - (attackLevel + defenseLevel + healthLevel + attackSpeedLevel + moveSpeedLevel + skillCDLevel)`
   - 确保 `talentPoints` 字段正确反映可用天赋点数量（已有升级时 +1 逻辑，需验证与已分配点数的关系）
   - _需求：6.1、6.2、6.4_

- [ ] 2. 创建属性计算工具类 `TrainPropertyCalculator`
   - 在 `Assets/Scripts/Cultivation/` 下创建 `TrainPropertyCalculator.cs`
   - 实现通用属性公式方法：`CalculateProperty(float baseValue, int level, int talentLevel)` 返回 (X, A, B, C)
   - 实现 CD 属性特殊公式方法：`CalculateCDProperty(int level, int skillCDLevel)` 返回 (X%, B%, C%)
   - 实现文本格式化方法：通用属性返回 `"属性名：X（A+B+C）"`，CD 属性返回 `"冷却缩减：X%（B%+C%）"`
   - 数值保留一位小数显示
   - _需求：1.2~1.7、1.9_

- [ ] 3. 创建 `TalentSystemController` 脚本处理单个 TalentSystem 面板的交互逻辑
   - 在 `Assets/Scripts/CardSelection/` 下创建 `TalentSystemController.cs`
   - 定义字段：属性类型（AttributeType）、临时天赋等级、已确认天赋等级
   - 在 `Initialize()` 方法中查找并绑定 PlusButton、MinusButton、SquareFrame 的 Button 组件和 Text 组件
   - SquareFrame 文本默认显示为当前天赋等级数值（默认 0），数值始终 ≥ 0
   - _需求：4.1、4.2、4.3_

- [ ] 4. 实现 PlusButton 点击逻辑
   - 在 `TalentSystemController` 中实现 `OnPlusButtonClicked()` 方法
   - 检查可用天赋点是否 > 0，若无则不响应
   - 临时天赋等级 +1，可用天赋点 -1
   - 更新 SquareFrame 文本为临时天赋等级
   - 触发回调通知 TrainViewController 更新对应 Property 的 Text
   - _需求：2.1、2.2、2.3_

- [ ] 5. 实现 MinusButton 点击逻辑
   - 在 `TalentSystemController` 中实现 `OnMinusButtonClicked()` 方法
   - 检查临时天赋等级是否 > 已确认天赋等级（或 > 0），若等于则不响应
   - 临时天赋等级 -1，可用天赋点 +1（返还天赋点）
   - 更新 SquareFrame 文本为临时天赋等级
   - 触发回调通知 TrainViewController 更新对应 Property 的 Text
   - _需求：3.1、3.2、3.3、3.4_

- [ ] 6. 实现 SquareFrame 确认按钮逻辑
   - 在 `TalentSystemController` 中实现 `OnConfirmButtonClicked()` 方法
   - 若临时天赋等级与已确认天赋等级相同则不执行任何操作
   - 计算消耗的天赋点数 = 临时天赋等级 - 已确认天赋等级
   - 将变更写入 CultivationData（调用 `UpgradeAttribute` 或直接设置对应字段）
   - 更新已确认天赋等级为临时天赋等级
   - 触发 Property Text 最终更新
   - _需求：4.4、4.5、4.6、4.7_

- [ ] 7. 重构 `TrainViewController`，集成属性文本显示和数据获取
   - 在 `TrainViewController` 的 `Start()` 中获取当前聚焦 CharacterCard 的 CharacterData 引用
   - 获取或创建该角色对应的 CultivationData 实例
   - 遍历 6 个 Property 节点，查找其下的 Text 组件并调用 `TrainPropertyCalculator` 计算并填充初始文本
   - 为每个 TalentSystem 面板创建/初始化 `TalentSystemController`，传入属性类型、CultivationData 引用和文本更新回调
   - 若 CharacterData 为 null 则输出警告日志并显示默认值
   - _需求：1.1、5.1、5.2、5.3_

- [ ] 8. 实现属性文本动态更新机制
   - 在 `TrainViewController` 中实现 `UpdatePropertyText(AttributeType type)` 方法
   - 根据属性类型调用 `TrainPropertyCalculator` 重新计算并更新对应 Property 的 Text
   - 将此方法作为回调传递给各 `TalentSystemController`，在 Plus/Minus/Confirm 操作后调用
   - _需求：2.2、3.2、4.6_

- [ ] 9. 实现 CD 冷却缩减的实际结算效果
   - 在确认天赋点后，计算最终冷却缩减百分比 X%
   - 将 X% 应用到角色所有技能的 CD 时间上（修改 SkillData 的实际冷却时间或在技能释放时动态计算）
   - 确保冷却缩减在角色升级时也能正确更新
   - _需求：1.8_

- [ ] 10. 实现边界情况处理与面板关闭重置
   - 在 `TrainViewController` 的 `OnPropertyClicked()` 中，当关闭 TalentSystem 面板时重置未确认的临时变更
   - 当 TrainView 面板被销毁/隐藏时（`OnDisable` 或 `OnDestroy`），丢弃所有未确认的临时变更
   - 确保等级为 1 时成长属性 B 显示为 0，天赋点为 0
   - 确保浮点数精度问题不影响显示（使用 `Mathf.Round` 或格式化字符串）
   - _需求：7.1、7.2、7.3、7.4、7.5_
