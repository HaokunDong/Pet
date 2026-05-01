# 实施计划：卡牌 Spline 贴合与交互重构

- [ ] 1. 清理 CardSelectionSettings，移除冗余字段
   - 移除 `fanAnglePerCard`、`fanRadius`、`fanCenterYOffset`、`rotationTransitionDuration`、`rotationTransitionEase` 五个字段及其 Header/Tooltip 特性
   - 重新整理剩余字段的 `[Header]` 分组，确保 Inspector 中结构清晰
   - 保留的字段：`snapDuration`、`snapEase`、`inertiaDamping`、`inertiaMinVelocity`、`cardSpacing`、`focusT`、`dragSensitivity`、`focusPopUpOffset`、`focusPopUpDuration`、`focusPopUpEase`
   - _需求：3.1、3.3、3.4_

- [ ] 2. 重写 CardSplineDistributor.UpdateCardPositions()，使用 Spline 真实路径定位卡牌
   - 将位置计算从自定义扇形弧线公式改为调用 `SplineUtility.EvaluatePosition(spline, t, ...)` 从 SplineContainer 采样世界坐标
   - 将世界坐标通过 `cardParent.InverseTransformPoint()` 转换为 UI 本地坐标，设置为卡牌的 `anchoredPosition`
   - 使用 `SplineUtility.EvaluateTangent(spline, t, ...)` 获取切线方向，投影到 XY 平面后计算旋转角度，使卡牌垂直于切线
   - 焦点卡牌的弹出偏移改为沿 Spline 法线方向（垂直于切线的 2D 方向）施加
   - 处理 t 值超出 0~1 范围时的 Clamp 逻辑
   - _需求：1.1、1.2、1.3、1.5、1.6_

- [ ] 3. 更新 CardSplineDistributor.GetCardBaseAnchoredPosition()，与新的 Spline 定位逻辑保持一致
   - 该方法当前使用旧的扇形弧线公式计算基础位置，需要改为从 Spline 采样
   - 确保返回的位置不包含焦点弹出偏移（仅基础位置）
   - _需求：1.1、1.6_

- [ ] 4. 移除 CardSplineDistributor 中对已删除 Settings 字段的所有引用
   - 搜索并移除代码中对 `Settings.fanAnglePerCard`、`Settings.fanRadius`、`Settings.fanCenterYOffset`、`Settings.rotationTransitionDuration`、`Settings.rotationTransitionEase` 的引用
   - 确保编译无错误
   - _需求：1.7、3.5_

- [ ] 5. 重构 CardDragHandler，改为挂载在卡牌上并驱动整组卡牌移动
   - 移除 `CardDragHandler` 中通过 `[SerializeField]` 引用 `CardSplineDistributor` 和 `CardFocusDisplay` 的字段，改为公共属性由外部注入
   - 添加 `Initialize(CardSplineDistributor distributor, CardFocusDisplay focusDisplay)` 方法，供 `CardSplineDistributor` 在实例化卡牌后调用注入引用
   - 保持 `IBeginDragHandler`/`IDragHandler`/`IEndDragHandler` 接口实现不变，拖拽逻辑驱动整组卡牌沿 Spline 滑动（通过修改 `distributor.CurrentOffset`）
   - 确保卡牌预制体根对象有 `Image` 组件且 `Raycast Target = true`，以接收 UI 拖拽事件
   - _需求：2.1、2.4、2.5、2.7_

- [ ] 6. 修改 CardSplineDistributor.Initialize()，在实例化卡牌时动态添加并初始化 CardDragHandler
   - 在每张卡牌实例化后，通过 `AddComponent<CardDragHandler>()` 或 `GetComponent<CardDragHandler>()`（如果预制体已挂载）获取 `CardDragHandler`
   - 调用 `CardDragHandler.Initialize()` 注入 `CardSplineDistributor` 和 `CardFocusDisplay` 的引用
   - _需求：2.4、边界情况 5_

- [ ] 7. 重构 CardInertiaAndSnap 的事件订阅机制，适配多个 CardDragHandler
   - 移除 `CardInertiaAndSnap` 中通过 `[SerializeField]` 引用单个 `CardDragHandler` 的字段
   - 改为在 `CardSplineDistributor` 上添加统一的 `OnDragEnded` 事件转发机制：每张卡牌的 `CardDragHandler.OnDragEnded` 事件触发时，由 `CardSplineDistributor` 统一转发给 `CardInertiaAndSnap`
   - 或者让 `CardInertiaAndSnap` 订阅 `CardSplineDistributor` 上的统一拖拽事件，而非直接订阅单个 `CardDragHandler`
   - 同步更新 `CardInertiaAndSnap` 中对 `dragHandler.IsDragging` 的检查逻辑
   - _需求：2.6、边界情况 6_

- [ ] 8. 更新 CardFocusDisplay 中的拖拽通知机制
   - 当前 `CardFocusDisplay` 的 `NotifyDragStart()` / `NotifyDragEnd()` 由 `CardDragHandler` 直接调用
   - 重构后需要确保任意卡牌上的 `CardDragHandler` 开始/结束拖拽时，都能正确通知 `CardFocusDisplay`
   - 可通过 `CardSplineDistributor` 统一转发拖拽状态变化事件
   - _需求：2.5、2.6_

- [ ] 9. 全局编译验证与引用清理
   - 确保所有脚本中对已移除字段和已变更接口的引用都已更新
   - 验证 `CardSelectionSettings`、`CardSplineDistributor`、`CardDragHandler`、`CardFocusDisplay`、`CardInertiaAndSnap` 五个文件编译无错误
   - 确保 DOTween 动画在对象销毁时正确清理（`OnDestroy` 中调用 `DOTween.Kill`）
   - _需求：3.5、边界情况 7_
