# 实施计划

- [ ] 1. 修改 `CardSplineDistributor.Initialize()` 中的 `startT` 计算逻辑
   - 在 `CardSplineDistributor.cs` 的 `Initialize()` 方法中，将当前的 `startT = focusT - totalSpan * 0.5f` 替换为基于目标聚焦卡牌索引的计算方式
   - 新逻辑：确定目标聚焦索引 `focusCardIndex`：奇数时为 `count / 2`，偶数时为 `count / 2 - 1`
   - 计算 `startT = focusT - focusCardIndex * spacing`，确保目标聚焦卡牌的 baseT 恰好等于 `focusT`
   - 保持 `currentOffset = 0f` 不变，确保 `UpdateCardPositions()` 调用后该卡牌自然成为聚焦卡牌
   - _需求：1.1、1.2、1.3、1.5_

- [ ] 2. 验证 `UpdateCardPositions()` 中聚焦索引计算的兼容性
   - 检查 `UpdateCardPositions()` 中通过 `Mathf.Abs(t - focusT)` 寻找最近卡牌的逻辑，确认修改 `startT` 后该逻辑仍能正确识别目标聚焦卡牌
   - 确认聚焦变更事件 `OnFocusChanged` 和 `SetFocused()` 调用在初始化时能正确触发
   - _需求：1.3、1.4_

- [ ] 3. 验证 `ClampOffset` 和 snap 逻辑的兼容性
   - 检查 `ClampOffset()` 方法中 `maxOffset` 和 `minOffset` 的计算，确认新的 baseTs 分布下边界限制仍然正确（第一张和最后一张卡牌都能被滑动到聚焦位置）
   - 检查 `GetSnapOffset()` 和 `GetSnapOffsetForFocus()` 方法，确认 snap 计算在新分布下仍然正确
   - _需求：2.1、2.2、2.3_

- [ ] 4. 边界情况测试
   - 测试卡牌数量为 1 时，唯一卡牌对齐到 `focusT`
   - 测试卡牌数量为 2 时（偶数最小值），索引 0 的卡牌对齐到 `focusT`
   - 测试卡牌数量为 3 时（奇数），索引 1 的卡牌对齐到 `focusT`
   - 测试卡牌数量为 4 时（偶数），索引 1 的卡牌对齐到 `focusT`
   - _需求：1.1、1.2、1.5_
