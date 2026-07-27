# 需求文档

## 引言

本文档描述了 BlackHole UI 元素的两个 Bug 修复需求。BlackHole 是桌面宠物应用中的核心交互入口按钮，用户可以点击它来展开/收起环形径向菜单（RingRadialMenu），也可以拖拽它来改变位置。当前存在两个问题：

1. **拖拽时误触发点击**：用户拖拽 BlackHole 后松开鼠标，会错误地触发点击事件（展开/收起菜单），影响用户体验。
2. **拖拽范围受限**：BlackHole 只能在屏幕右半边拖拽，无法移动到屏幕左半边。

### 技术背景

- BlackHole 的点击事件通过 `EventTriggerListener`（继承自 `EventTrigger`）的 `OnPointerClick` 回调实现，定义在 `MainView.cs` 中。
- BlackHole 的拖拽功能通过 `BlackHoleDragHandler.cs`（实现 `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler`）实现。
- `EventTriggerListener` 同时覆盖了 `OnBeginDrag`/`OnDrag`/`OnEndDrag`（空实现），这会与 `BlackHoleDragHandler` 的拖拽接口产生冲突——`EventTrigger` 的空 `OnDrag` 实现会"吞掉"拖拽事件，导致 Unity EventSystem 无法正确识别拖拽状态，从而在拖拽结束后仍然触发 `OnPointerClick`。
- BlackHole 的父节点 MainView 的 RectTransform 锚定在屏幕右下角（AnchorMin/Max = (1,0)），且 SizeDelta 仅为 100x100。`BlackHoleDragHandler` 使用 `_rectTransform.parent`（即 MainView）作为坐标转换的参考矩形，导致坐标计算偏移，拖拽范围被限制在屏幕右半部分。

---

## 需求

### 需求 1：拖拽时不触发点击效果

**用户故事：** 作为一名桌面宠物用户，我希望拖拽 BlackHole 后松开鼠标时不会触发点击效果（展开/收起菜单），以便我可以自由调整 BlackHole 的位置而不会意外打开或关闭菜单。

#### 验收标准

1. WHEN 用户按下鼠标并拖拽 BlackHole 超过系统拖拽阈值后松开 THEN 系统 SHALL 不触发 BlackHole 的点击回调（不展开/收起 RingRadialMenu）。
2. WHEN 用户按下鼠标并在未超过拖拽阈值的情况下松开 THEN 系统 SHALL 正常触发 BlackHole 的点击回调（展开/收起 RingRadialMenu）。
3. WHEN 拖拽操作正在进行中 THEN 系统 SHALL 不播放点击动画（OnClick sprite animation）。
4. IF `EventTriggerListener` 的 `OnBeginDrag`/`OnDrag`/`OnEndDrag` 覆盖方法与 `BlackHoleDragHandler` 的拖拽接口产生冲突 THEN 系统 SHALL 通过修改 `EventTriggerListener` 或 `BlackHoleDragHandler` 来正确传递拖拽事件，确保 Unity EventSystem 能正确区分点击和拖拽。

### 需求 2：BlackHole 可在全屏范围内拖拽

**用户故事：** 作为一名桌面宠物用户，我希望能将 BlackHole 拖拽到屏幕的任意位置（包括左半边），以便我可以根据自己的使用习惯自由放置 BlackHole。

#### 验收标准

1. WHEN 用户拖拽 BlackHole THEN 系统 SHALL 允许 BlackHole 在整个屏幕/Canvas 范围内自由移动，不受 MainView RectTransform 大小和锚点位置的限制。
2. WHEN BlackHole 被拖拽到屏幕边缘 THEN 系统 SHALL 将 BlackHole 的位置限制在 Canvas 可见区域内（不允许拖出屏幕）。
3. WHEN `BlackHoleDragHandler` 进行坐标转换时 THEN 系统 SHALL 使用 Canvas 的 RectTransform（而非 MainView 的 RectTransform）作为参考坐标系，以确保坐标计算在全屏范围内正确。
4. IF `clampToCanvas` 选项为 true THEN 系统 SHALL 基于 Canvas 的完整尺寸进行边界限制，确保 BlackHole 不会超出可见区域。
