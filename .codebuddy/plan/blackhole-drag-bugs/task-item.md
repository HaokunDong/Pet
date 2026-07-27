# 实施计划

- [ ] 1. 修改 `EventTriggerListener` 解决拖拽与点击冲突
   - 修改文件：`Assets/Scripts/Utils/EventTriggerListener.cs`
   - 在 `OnPointerClick` 中添加拖拽状态判断：新增 `_isDragging` 标志位，在 `OnBeginDrag` 中设为 true，在 `OnEndDrag` 中重置为 false
   - 当 `_isDragging` 为 true 时，`OnPointerClick` 不调用 `onClick` 委托
   - 确保 `OnDrag` 方法调用 `ExecuteEvents.ExecuteHierarchy` 向上传递拖拽事件，避免吞掉 `BlackHoleDragHandler` 的拖拽事件
   - _需求：1.1、1.2、1.3、1.4_

- [ ] 2. 修改 `BlackHoleDragHandler` 使用 Canvas RectTransform 作为坐标参考
   - 修改文件：`Assets/Scripts/UI/BlackHoleDragHandler.cs`
   - 在 `Awake` 中将 `_parentRect` 的赋值从 `_rectTransform.parent as RectTransform` 改为 `_parentCanvas.GetComponent<RectTransform>()`（即 Canvas 的 RectTransform）
   - 确保 `OnBeginDrag` 和 `OnDrag` 中的 `RectTransformUtility.ScreenPointToLocalPointInRectangle` 使用 Canvas 的 RectTransform 进行坐标转换
   - 同步更新 `ClampToCanvasBounds` 中的 localPosition 计算，使其相对于 Canvas 坐标系正确工作
   - _需求：2.1、2.3_

- [ ] 3. 修正 `ClampToCanvasBounds` 边界限制逻辑
   - 修改文件：`Assets/Scripts/UI/BlackHoleDragHandler.cs`
   - 由于 BlackHole 现在相对于 Canvas 坐标系移动，需要确保 clamp 逻辑使用 Canvas RectTransform 的尺寸和 BlackHole 自身尺寸（考虑 localScale = 10）来正确计算边界
   - 确保 BlackHole 不会被拖出 Canvas 可见区域
   - _需求：2.2、2.4_

- [ ] 4. 验证拖拽事件传递链路完整性
   - 修改文件：`Assets/Scripts/Utils/EventTriggerListener.cs`
   - 确保 `EventTriggerListener` 的 `OnBeginDrag`/`OnDrag`/`OnEndDrag` 不会阻断事件向 `BlackHoleDragHandler` 的传递；如果两个组件在同一 GameObject 上，Unity 会依次调用所有实现了接口的组件，需确认 `EventTrigger` 基类的 drag 事件处理不会设置 `eventData.used = true`
   - 如果存在事件阻断问题，考虑在 `EventTriggerListener` 的 drag 方法中调用 `eventData.Use()` 前进行条件判断，或移除 `EventTriggerListener` 中不必要的 drag 覆盖方法（仅保留 `onBeginDrag` 委托不为空时才覆盖）
   - _需求：1.4_

- [ ] 5. 功能集成测试
   - 在 Unity Editor 中运行 TestScene，验证以下场景：
     - 点击 BlackHole（不拖拽）→ RingRadialMenu 正常展开/收起，点击动画正常播放
     - 拖拽 BlackHole → RingRadialMenu 不展开/收起，无点击动画
     - 将 BlackHole 拖拽到屏幕左半边 → 能正常到达
     - 将 BlackHole 拖拽到屏幕边缘 → 被正确限制在 Canvas 内
     - 拖拽后 RingRadialMenu 跟随 BlackHole 位置更新
   - _需求：1.1、1.2、1.3、2.1、2.2_
