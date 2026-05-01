# 需求文档：卡牌 Spline 贴合与交互重构

## 引言

当前卡牌选择系统存在三个核心问题需要修复：

1. **卡牌未贴合 Spline 曲线**：`CardSplineDistributor.UpdateCardPositions()` 使用自定义的扇形弧线数学公式（`fanRadius`、`fanAnglePerCard`、`fanCenterYOffset`）来计算卡牌位置和旋转，完全没有使用 Unity Splines 包提供的 `SplineContainer` 实际路径数据。虽然场景中绑定了 `SplineContainer`，但它仅作为一个"名义引用"存在，卡牌的位置和朝向完全由手动参数控制，导致卡牌无法贴合用户在 Editor 中绘制的 Spline 曲线。

2. **拖拽交互依赖 DragArea**：当前 `CardDragHandler` 挂载在一个全屏透明 Image（`DragArea`）上，通过 `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler` 接口接收拖拽事件。用户希望移除 DragArea，改为鼠标在卡牌上时即可直接拖动。

3. **CardSelectionSettings 存在冗余字段**：之前已移除了缩放相关字段（`focusScale`、`nonFocusScale`、`scaleTransitionDuration`、`scaleTransitionEase`），但仍残留大量与自定义扇形布局相关的字段（`fanAnglePerCard`、`fanRadius`、`fanCenterYOffset`、`rotationTransitionDuration`、`rotationTransitionEase`），这些字段在改为真正的 Spline 贴合后将不再需要。

### 技术背景

- 项目使用 **Unity Splines 包**（`com.unity.splines: 2.8.4`），提供 `SplineContainer`、`SplineUtility.EvaluatePosition()`、`SplineUtility.EvaluateTangent()` 等 API
- 项目使用 **DOTween Pro** 处理动画
- 卡牌系统运行在 **UI Canvas**（`Screen Space - Camera` 模式）上
- 当前代码文件：`CardSplineDistributor.cs`、`CardDragHandler.cs`、`CardFocusDisplay.cs`、`CardInertiaAndSnap.cs`、`CardSelectionSettings.cs`、`CharacterCard.cs`

---

## 需求

### 需求 1：卡牌完全贴合 Spline 曲线

**用户故事：** 作为一名开发者，我希望卡牌的位置和旋转完全由 Spline 曲线决定，以便卡牌能精确贴合我在 Editor 中绘制的任意形状的 Spline 路径。

#### 验收标准

1. WHEN 卡牌系统初始化时 THEN `CardSplineDistributor` SHALL 使用 `SplineUtility.EvaluatePosition()` 根据每张卡牌的 t 值从 `SplineContainer` 获取世界坐标位置，并将其转换为 `cardParent` 的本地坐标系后设置为卡牌的 `anchoredPosition`。
2. WHEN 卡牌被放置到 Spline 上时 THEN 每张卡牌 SHALL 使用 `SplineUtility.EvaluateTangent()` 获取该点的切线方向，并将卡牌旋转设置为垂直于该切线（即卡牌的"上方向"沿切线方向，卡牌面朝观察者）。
3. WHEN 用户拖动卡牌使全局偏移量变化时 THEN 所有卡牌 SHALL 根据新的 t 值重新从 Spline 采样位置和切线，实时更新位置和旋转。
4. WHEN 用户在 Editor 中修改 Spline 的形状（移动控制点、调整切线等）时 THEN 运行时卡牌的分布 SHALL 自动跟随 Spline 的新形状。
5. IF Spline 为闭合曲线 THEN 系统 SHALL 仍然正确处理 t 值的边界（0~1 范围内采样）。
6. WHEN 焦点卡牌需要弹出效果时 THEN 弹出偏移 SHALL 沿该卡牌在 Spline 上的法线方向（垂直于切线的方向）施加，而非固定方向。
7. `CardSelectionSettings` 中与自定义扇形布局相关的字段（`fanAnglePerCard`、`fanRadius`、`fanCenterYOffset`、`rotationTransitionDuration`、`rotationTransitionEase`）SHALL 被移除，因为位置和旋转完全由 Spline 决定。

---

### 需求 2：鼠标直接在卡牌上拖拽

**用户故事：** 作为一名玩家，我希望只需要在卡牌上按下鼠标并拖动即可滑动卡牌，而不需要一个额外的全屏透明拖拽区域，以便交互更加直观自然。

#### 验收标准

1. WHEN 玩家在任意一张卡牌上按下鼠标并拖动时 THEN 所有卡牌 SHALL 沿 Spline 曲线方向跟随鼠标移动。
2. WHEN 玩家的鼠标不在任何卡牌上时 THEN 拖拽操作 SHALL 不会被触发（即空白区域不响应拖拽）。
3. WHEN 拖拽交互改为卡牌上直接拖拽后 THEN 场景中的 `DragArea`（全屏透明 Image）SHALL 不再需要，可以被移除。
4. `CardDragHandler` 脚本 SHALL 改为挂载在每张卡牌预制体上（或通过代码在运行时动态添加），而非挂载在一个单独的 DragArea 上。
5. WHEN 任意一张卡牌接收到拖拽事件时 THEN 该事件 SHALL 驱动整个卡牌组的移动（所有卡牌一起沿 Spline 滑动），而非仅移动被拖拽的那张卡牌。
6. WHEN 拖拽结束后 THEN 惯性滑动和吸附动画 SHALL 与之前的行为保持一致（`CardInertiaAndSnap` 的逻辑不变）。
7. IF 多张卡牌重叠且玩家点击重叠区域 THEN 系统 SHALL 响应层级最高（sibling index 最大）的卡牌的拖拽事件。

---

### 需求 3：清理 CardSelectionSettings

**用户故事：** 作为一名开发者，我希望 `CardSelectionSettings` 中只保留当前系统实际使用的配置字段，以便配置界面清晰整洁、不产生混淆。

#### 验收标准

1. WHEN 需求 1 完成后 THEN `CardSelectionSettings` SHALL 移除以下不再使用的字段：
   - `fanAnglePerCard`（扇形角度 — 被 Spline 切线替代）
   - `fanRadius`（扇形半径 — 被 Spline 位置替代）
   - `fanCenterYOffset`（扇形中心偏移 — 被 Spline 位置替代）
   - `rotationTransitionDuration`（旋转过渡时长 — 旋转由 Spline 切线直接决定）
   - `rotationTransitionEase`（旋转过渡缓动 — 同上）
2. WHEN 需求 2 完成后 THEN `CardSelectionSettings` SHALL 检查 `dragSensitivity` 字段是否仍然需要，如果拖拽逻辑有变化则相应调整。
3. WHEN 清理完成后 THEN `CardSelectionSettings` SHALL 仅保留以下有效字段：
   - `snapDuration`（吸附动画时长）
   - `snapEase`（吸附缓动曲线）
   - `inertiaDamping`（惯性衰减系数）
   - `inertiaMinVelocity`（惯性最小速度阈值）
   - `cardSpacing`（卡牌间距 t 值）
   - `focusT`（焦点位置 t 值）
   - `dragSensitivity`（拖拽灵敏度）
   - `focusPopUpOffset`（焦点卡牌弹出偏移量）
   - `focusPopUpDuration`（弹出动画时长）
   - `focusPopUpEase`（弹出缓动曲线）
4. WHEN 开发者在 Inspector 中查看 `CardSelectionSettings` 时 THEN 所有字段 SHALL 按功能分组（Header），结构清晰。
5. IF 清理后有其他脚本引用了被移除的字段 THEN 编译 SHALL 无错误（所有引用都已同步更新）。

---

## 边界情况与注意事项

1. **Spline 坐标系转换**：Spline 的 `EvaluatePosition()` 返回的是世界坐标，需要正确转换为 `cardParent`（UI RectTransform）的本地坐标。Canvas 使用 `Screen Space - Camera` 模式，需要通过 `WorldToScreenPoint` + `ScreenPointToLocalPointInRectangle` 或 `InverseTransformPoint` 进行转换。
2. **Spline 切线方向**：`EvaluateTangent()` 返回的切线是 3D 向量，需要投影到 2D 平面（XY 平面）后计算旋转角度，确保卡牌在 UI 层面正确旋转。
3. **拖拽事件穿透**：将 `CardDragHandler` 挂在卡牌上后，需要确保卡牌预制体的根对象有 `Graphic`（如 `Image`）组件且 `Raycast Target = true`，否则无法接收 UI 事件。
4. **拖拽冲突**：多张卡牌重叠时，Unity EventSystem 会将事件发送给层级最高的 UI 元素，这与 `CardFocusDisplay` 的 sibling order 管理一致，无需额外处理。
5. **CardDragHandler 引用获取**：如果 `CardDragHandler` 改为挂在每张卡牌上，需要考虑如何让每张卡牌上的 `CardDragHandler` 获取到 `CardSplineDistributor` 和 `CardFocusDisplay` 的引用。建议在卡牌实例化后由 `CardSplineDistributor` 统一注入引用。
6. **CardInertiaAndSnap 的 DragHandler 引用**：当前 `CardInertiaAndSnap` 通过 Inspector 引用单个 `CardDragHandler`，改为多个卡牌各自持有 `CardDragHandler` 后，需要调整事件订阅机制（例如由 `CardSplineDistributor` 统一转发拖拽结束事件）。
7. **已有的 CardSelectionSettings.asset 文件**：清理字段后，已有的 `.asset` 文件中被移除字段的序列化数据会被 Unity 自动忽略，不会导致错误，但建议在 Editor 中重新检查一次配置值。
