# 实施计划

- [ ] 1. 在 `CardSplineDistributor` 中添加显示/隐藏控制方法
   - 添加 `bool IsVisible` 属性，用于跟踪卡组当前的显示状态
   - 添加 `Show()` 方法：激活 `cardParent` GameObject，调用 `UpdateCardPositions()` 确保卡牌位置正确，并通知 `CardFocusDisplay` 刷新焦点状态
   - 添加 `Hide()` 方法：停止所有 DOTween 动画（遍历所有卡牌调用 `DOTween.Kill`），重置 `IsDragging` 状态，停用 `cardParent` GameObject
   - 添加 `ToggleVisibility()` 方法：根据 `IsVisible` 调用 `Show()` 或 `Hide()`
   - 修改 `Start()` 方法：在 `Initialize()` 之后调用 `Hide()` 使卡牌初始隐藏
   - _需求：1.1, 1.2, 2.1, 2.2, 2.3, 4.1, 4.2_

- [ ] 2. 在 `CardInertiaAndSnap` 中添加停止惯性和吸附的公共方法
   - 添加 `public void StopAll()` 方法：调用 `CancelInertia()` 停止惯性滚动，调用 `KillSnapTween()` 停止吸附动画
   - 该方法将在 `CardSplineDistributor.Hide()` 中被调用，确保隐藏时所有运动停止
   - _需求：4.2_

- [ ] 3. 在 `CardDragHandler` 中添加强制取消拖拽的公共方法
   - 添加 `public void CancelDrag()` 方法：如果 `IsDragging` 为 true，重置 `IsDragging` 为 false，重置 `distributor.IsDragging`，通知 `focusDisplay.NotifyDragEnd()`，但不触发惯性（不调用 `NotifyDragEnded`）
   - 该方法将在 `CardSplineDistributor.Hide()` 中被调用，确保隐藏时拖拽被取消
   - _需求：4.3_

- [ ] 4. 在 `CardSplineDistributor` 中添加 Esc 键监听逻辑
   - 添加 `Update()` 方法，在其中检测 `Input.GetKeyDown(KeyCode.Escape)`
   - 当检测到 Esc 键按下且 `IsVisible` 为 true 时，调用 `Hide()`
   - 当 `IsVisible` 为 false 时，不执行任何操作
   - _需求：3.1, 3.2_

- [ ] 5. 修改 `MainView.cs` 中 Button1 的点击事件，连接卡组切换逻辑
   - 在 `OnButtonClick` 方法中，当 `go == button1` 时，通过 `FindObjectOfType<PetGame.CardSplineDistributor>()` 获取卡牌系统引用（或在 `OnAwake` 中缓存引用），调用其 `ToggleVisibility()` 方法
   - 移除原有的 `Debug.Log("button1")` 调试日志
   - _需求：2.1, 2.2_

- [ ] 6. 集成测试与边界情况验证
   - 验证场景加载后卡牌初始隐藏（`cardParent` 不激活）
   - 验证点击 Button1 可以正确切换卡组显示/隐藏
   - 验证卡组显示时按 Esc 键可以隐藏，隐藏时按 Esc 无效果
   - 验证拖拽过程中点击 Button1 或按 Esc 能正确取消拖拽并隐藏
   - 验证重新显示卡组后卡牌位置和焦点状态正确
   - _需求：1.1, 1.2, 2.1, 2.2, 2.3, 3.1, 3.2, 4.1, 4.2, 4.3_
