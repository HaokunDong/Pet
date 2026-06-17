# 实施计划

- [ ] 1. 创建环形轮盘核心目录与设置脚本
   - 在 `Assets/Scripts/UI/RingRadialMenu/` 下建立目录结构
   - 新建 `RingRadialMenuSettings.cs`，定义可序列化结构体/类，包含 `innerRadius`、`outerRadius`、`buttonCount`(默认 8)、`startAngleDegrees`(默认 90)、`clockwise`(默认 true)
   - 提供 `Validate()` 方法，对非法半径自动修正并打印警告
   - _需求：1.1、1.3、2.5、边界约束-扩展性_

- [ ] 2. 实现环形命中测试图形组件 `RingSectorGraphic`
   - 新建 `Assets/Scripts/UI/RingRadialMenu/RingSectorGraphic.cs`，继承自 `UnityEngine.UI.Image`（或 `MaskableGraphic`）
   - 重写 `IsRaycastLocationValid(Vector2 sp, Camera eventCamera)`，将屏幕坐标转换为相对于轮盘圆心的本地坐标
   - 使用平方距离 `sqrMagnitude` 与 `innerRadius²` / `outerRadius²` 比较，避免开方分配
   - 增加角度判定：根据按钮的 `sectorIndex` 与 `sectorAngleSize` 计算所属扇区，落在区间外返回 false
   - 暴露字段供 `RingRadialMenu` 在运行时注入半径、角度、扇区参数
   - _需求：3.1、3.2、3.3、3.4、3.5、边界约束-性能_

- [ ] 3. 实现按钮单元 `RingRadialMenuButton`
   - 新建 `Assets/Scripts/UI/RingRadialMenu/RingRadialMenuButton.cs`，挂在每个按钮节点上
   - 持有对 `RingSectorGraphic` 与 `Image`(图标) 的引用，提供 `SetIcon(Sprite)` 与 `SetCallback(UnityAction)` 方法
   - 内部包装一个 `UnityEngine.UI.Button`（或自行处理 `IPointerClickHandler`），点击时回调外部并抛出索引
   - _需求：6.1、6.2、6.3_

- [ ] 4. 实现核心组件 `RingRadialMenu` 的布局逻辑
   - 新建 `Assets/Scripts/UI/RingRadialMenu/RingRadialMenu.cs`，挂在轮盘根节点 `RectTransform` 上
   - 引入 `[ExecuteAlways]`，在 `OnValidate` / `Awake` / `OnEnable` 中调用 `RebuildLayout()`
   - `RebuildLayout()`：按 `360/buttonCount` 等分计算每个按钮的角度，按 `(innerRadius+outerRadius)/2` 半径定位 `anchoredPosition`，并把内/外半径与扇区参数同步写入每个 `RingSectorGraphic`
   - 子按钮数量与配置不一致时打印警告，按实际数量布局
   - _需求：1.2、2.1、2.2、2.3、2.4、2.5、2.6_

- [ ] 5. 实现圆心目标跟随逻辑
   - 在 `RingRadialMenu` 中新增 `centerTarget`(Transform)、`worldCamera`(Camera) 字段与 `CanvasGroup` 引用
   - 在 `LateUpdate` 中通过 `Camera.WorldToScreenPoint` 将世界坐标转换为屏幕坐标（兼容 Overlay 与 ScreenSpaceCamera 两种模式）
   - 当投影 z<0（目标在相机背后）时，将 `CanvasGroup.alpha=0`、`blocksRaycasts=false` 隐藏轮盘；恢复时重置为可见可交互
   - `centerTarget` 为空时使用当前 `anchoredPosition` 作为圆心，跳过跟随
   - _需求：1.4、1.5、4.1、4.2、4.3、4.4、4.5_

- [ ] 6. 实现对外回调 API 与事件
   - 在 `RingRadialMenu` 中提供 `SetButtonCallback(int index, UnityAction)` 与 `SetButtonIcon(int index, Sprite)` 公开方法
   - 暴露 `event Action<int> OnButtonClicked` 事件，按钮点击时同时触发该事件与对应的 `UnityAction`
   - 对索引越界做边界保护，仅打印警告，不抛异常
   - _需求：6.1、6.2、6.3、6.4_

- [ ] 7. 实现 Gizmos 可视化与编辑器辅助
   - 在 `RingRadialMenu` 中实现 `OnDrawGizmosSelected`：用绿色绘制内圈、红色绘制外圈
   - 通过本地坐标转世界坐标（结合 `RectTransform.TransformPoint`）确保 Gizmos 在 Scene 视图正确显示
   - 仅在编辑器下编译（`#if UNITY_EDITOR`），运行时不执行额外开销代码
   - _需求：5.1、5.3、5.4_

- [ ] 8. 实现滚轮旋转命中拦截组件 `RingScrollRotateZone`
   - 新建 `Assets/Scripts/UI/RingRadialMenu/RingScrollRotateZone.cs`，作为轮盘根节点（或与轮盘同级的覆盖层）上的图形组件，继承自 `Graphic`（不渲染像素，仅参与 Raycast）
   - 重写 `IsRaycastLocationValid`：仅当鼠标到圆心的 `sqrDistance <= outerRadius²` 时返回 true，外圈以外直接穿透
   - 实现 `IScrollHandler.OnScroll(PointerEventData)`：将 `eventData.scrollDelta.y` 转交给 `RingRadialMenu.HandleScrollRotate(float deltaY)` 处理
   - 当 `RingRadialMenu.enableScrollRotate == false` 时，让 `IsRaycastLocationValid` 返回 false，使滚轮事件穿透
   - _需求：7.1、7.4、7.5、7.8、边界约束-滚轮命中范围_

- [ ] 9. 在 `RingRadialMenu` 中加入滚轮旋转参数与逻辑
   - 在 `RingRadialMenuSettings`（或 `RingRadialMenu` 主体）新增字段：`enableScrollRotate`(默认 true)、`scrollRotateStepDegrees`(默认 15f)
   - 新增运行时字段 `accumulatedRotation`(float) 记录当前累计旋转角度
   - 实现 `HandleScrollRotate(float scrollDeltaY)`：根据方向规则（`scrollDeltaY > 0` 顺时针 = 角度数值减小；`< 0` 逆时针 = 角度数值增大）累加偏移到 `startAngleDegrees`，并调用 `RebuildLayout()` 同步按钮位置与扇区命中参数
   - 暴露 `event Action<float> OnRotated` 事件，在每次旋转生效后抛出当前累计角度
   - 在源代码中显式注释屏幕坐标系下的方向约定（Y 向上为正、顺时针对应角度递减），避免与 `clockwise` 字段混淆
   - _需求：7.2、7.3、7.6、7.7、7.9、7.10、边界约束-方向语义_

- [ ] 10. 创建/更新 Prefab 与示例脚本
   - 新建或更新 `Assets/Scripts/Editor/CreateRingRadialMenuPrefab.cs`，菜单项 `Tools/Create Ring Radial Menu` 自动生成：根节点(`RingRadialMenu` + `CanvasGroup` + `RingScrollRotateZone`) + 8 个子按钮（每个含 `RingSectorGraphic` 与图标 `Image`）
   - 默认参数：内半径 80、外半径 160、起始角 90°、顺时针、`enableScrollRotate=true`、`scrollRotateStepDegrees=15`
   - 在 `Assets/Scripts/UI/RingRadialMenu/RingRadialMenuSample.cs` 中演示注册回调、订阅 `OnRotated` 监听旋转、运行时切换 `enableScrollRotate`
   - 同目录 `README.md` 增补"滚轮旋转"使用说明（命中区域差异、参数含义、事件订阅）
   - _需求：1.1、2.1、6.2、7.7、7.8、7.10_
