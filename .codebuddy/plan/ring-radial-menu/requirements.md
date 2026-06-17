# 需求文档

## 引言

本功能旨在为 Unity 桌面宠物项目添加一个**环形轮盘菜单（Ring Radial Menu）**。该菜单是一个空心圆环（甜甜圈形状），围绕场景中的某个目标物体（如玩家角色、宠物）作为圆心进行布局。环形上等距环绕 8 个按钮，玩家可以通过点击按钮触发对应功能。

环形区域由内圈半径与外圈半径定义，二者均可在 Inspector 面板中调整；按钮的命中检测被严格限制在该环形区域内 —— 即使鼠标点击落在按钮的矩形 RectTransform 范围内，但若同时位于内圈以内或外圈以外，则视为无效点击，事件应穿透到下层 UI/场景。

该功能将作为项目通用 UI 模块，可复用于"角色技能轮盘"、"宠物互动菜单"、"快捷指令面板"等多种场景。

## 需求

### 需求 1：环形轮盘容器与几何参数配置

**用户故事：** 作为一名开发者，我希望能在 Inspector 中通过简单参数定义环形轮盘的几何形状，以便在不同场景下灵活调整菜单的视觉尺寸。

#### 验收标准

1. WHEN 开发者将 `RingRadialMenu` 组件挂载到 UI 节点上 THEN 系统 SHALL 在 Inspector 中暴露 `innerRadius`（内圈半径）、`outerRadius`（外圈半径）两个 `float` 字段
2. WHEN 开发者修改 `innerRadius` 或 `outerRadius` 的值 THEN 系统 SHALL 在 Editor 编辑模式下实时刷新按钮位置与可视化辅助线（Gizmos）
3. IF `innerRadius >= outerRadius` 或任一值小于 0 THEN 系统 SHALL 在 Console 输出警告日志，并按 `outerRadius = max(innerRadius+1, outerRadius)` 进行自动修正
4. WHEN 开发者在 Inspector 中调整 `centerTarget`（圆心目标 Transform）字段 THEN 系统 SHALL 让轮盘整体跟随该 Transform 的屏幕投影位置进行定位
5. IF `centerTarget` 为空 THEN 系统 SHALL 默认将轮盘的 `RectTransform.anchoredPosition` 作为圆心，不进行跟随

### 需求 2：8 个按钮的等角度环绕分布

**用户故事：** 作为一名玩家，我希望看到 8 个按钮均匀分布在圆环上，以便清晰区分各功能项。

#### 验收标准

1. WHEN 轮盘初始化或参数变更 THEN 系统 SHALL 将 8 个按钮按 360°/8 = 45° 的等间隔角度分布在圆环上
2. WHEN 计算单个按钮位置 THEN 系统 SHALL 使按钮中心点位于半径 `(innerRadius + outerRadius) / 2` 的圆周上
3. WHEN 开发者调整 `startAngleDegrees`（起始角度，默认 90°，即正上方） THEN 系统 SHALL 以该角度作为第 1 个按钮的角度，其余按钮顺时针或逆时针依次排列
4. WHEN 开发者勾选/取消 `clockwise`（是否顺时针） THEN 系统 SHALL 按照对应方向重新排列按钮
5. WHEN 按钮数量在代码中改变（虽默认 8，但需可配置） THEN 系统 SHALL 按新数量重新计算角度间隔与位置
6. IF 子按钮数量与配置数量不一致 THEN 系统 SHALL 输出警告，但仍按实际子节点数进行布局，不抛异常

### 需求 3：仅在环形区域内才响应点击（命中测试）

**用户故事：** 作为一名玩家，我希望只有在我点击到圆环本身的扇形区域时按钮才响应，落在内圈空白处或外圈之外的点击应该被忽略，以便菜单视觉与交互区域完全一致。

#### 验收标准

1. WHEN 玩家在屏幕上发起一次点击 AND 点击的屏幕坐标到圆心的距离 `d` 满足 `innerRadius <= d <= outerRadius` THEN 系统 SHALL 允许命中并触发对应按钮的 `onClick` 事件
2. WHEN 玩家点击的距离 `d < innerRadius` 或 `d > outerRadius` THEN 系统 SHALL 拒绝命中，且该点击事件 SHALL 穿透到下层 UI 或场景
3. WHEN 实现命中测试 THEN 系统 SHALL 在每个按钮的 `Image`（或自定义 `Graphic`）上重写 `IsRaycastLocationValid(Vector2 sp, Camera eventCamera)` 方法以执行环形区域判定
4. WHEN 玩家点击位置在环形区域内 AND 同时落在某个按钮所占的角度扇区内 THEN 系统 SHALL 仅触发该按钮的事件，不触发相邻按钮
5. IF 多个按钮的 RectTransform 在屏幕上重叠 THEN 系统 SHALL 通过角度判定与距离判定的双重过滤，确保最终只有正确的扇区按钮响应

### 需求 4：圆心目标跟随与位置更新

**用户故事：** 作为一名玩家，我希望轮盘能持续围绕指定的物体（如角色），即使该物体在场景中移动，轮盘也应跟随其屏幕位置。

#### 验收标准

1. WHEN `centerTarget` 被赋值为一个世界空间 Transform THEN 系统 SHALL 在 `LateUpdate` 中将该 Transform 的世界坐标通过 `Camera.WorldToScreenPoint` 转换为屏幕坐标，再赋给轮盘 `RectTransform`
2. WHEN 摄像机或目标移动导致屏幕投影位置变化 THEN 系统 SHALL 在下一帧自动更新轮盘位置，无需手动刷新
3. IF `centerTarget` 位于摄像机背后（投影 z<0） THEN 系统 SHALL 隐藏整个轮盘（CanvasGroup.alpha = 0 且 blocksRaycasts = false）
4. WHEN `centerTarget` 重新进入摄像机视野 THEN 系统 SHALL 恢复轮盘的可见与可交互状态
5. WHEN Canvas 的 `renderMode` 为 `ScreenSpaceOverlay` 或 `ScreenSpaceCamera` THEN 系统 SHALL 都能正确计算位置（区分是否传入 Camera 参数）

### 需求 5：可视化调试与编辑器辅助

**用户故事：** 作为一名开发者，我希望在 Scene 视图能直观地看到环形轮盘的内外圈范围，以便快速调试半径值。

#### 验收标准

1. WHEN 在 Scene 视图选中带有 `RingRadialMenu` 组件的物体 THEN 系统 SHALL 通过 `OnDrawGizmosSelected` 绘制内圈（绿色）与外圈（红色）两条圆形辅助线
2. WHEN 在 Scene 视图绘制 Gizmos THEN 系统 SHALL 同时绘制每个按钮的中心位置点
3. WHEN 开发者在编辑模式（非运行时）调整参数 THEN 系统 SHALL 通过 `[ExecuteAlways]` 或 `OnValidate` 实时刷新按钮布局，便于所见即所得
4. IF 处于运行模式 THEN 系统 SHALL 不执行额外的 Editor-only 代码，避免性能影响

### 需求 6：按钮事件解耦与点击回调

**用户故事：** 作为一名开发者，我希望以统一的方式注册每个按钮的点击回调，以便快速接入业务逻辑。

#### 验收标准

1. WHEN 轮盘初始化 THEN 系统 SHALL 通过 `RingRadialMenu.SetButtonCallback(int index, UnityAction callback)` 接口为每个索引按钮注册回调
2. WHEN 开发者调用 `SetButtonIcon(int index, Sprite icon)` THEN 系统 SHALL 设置对应索引按钮的图标
3. WHEN 玩家在环形有效区域点击某按钮 THEN 系统 SHALL 触发对应索引的 `UnityAction`，并把按钮索引通过事件 `OnButtonClicked(int index)` 抛出
4. IF 索引越界 THEN 系统 SHALL 仅打印警告，不抛异常，不影响其他按钮

### 需求 7：鼠标滚轮在环形区域内旋转轮盘

**用户故事：** 作为一名玩家，我希望当鼠标停留在环形区域内时滚动滚轮，整个轮盘可以围绕圆心旋转，以便我能快速把想要的按钮转到方便点击的位置。

#### 验收标准

1. WHEN 鼠标光标位于外圈以内的整个圆形区域内（即与圆心的距离 `d <= outerRadius`） AND 玩家滚动鼠标滚轮 THEN 系统 SHALL 将滚轮事件视为有效旋转输入，进行轮盘旋转
2. WHEN 滚轮向上滚动（`scrollDelta.y > 0`） THEN 系统 SHALL 让轮盘**顺时针**旋转一定角度
3. WHEN 滚轮向下滚动（`scrollDelta.y < 0`） THEN 系统 SHALL 让轮盘**逆时针**旋转一定角度
4. WHEN 鼠标光标位于外圈以外（`d > outerRadius`） AND 玩家滚动滚轮 THEN 系统 SHALL **不**响应该滚轮事件，事件 SHALL 穿透到下层 UI 或场景
5. WHEN 实现滚轮捕获 THEN 系统 SHALL 通过 `IScrollHandler` 接口或自定义命中测试在轮盘根节点上接收滚轮事件，并仅在距离判定通过时消费事件
6. WHEN 旋转生效 THEN 系统 SHALL 通过累加 `RingRadialMenuSettings.startAngleDegrees`（或等价的旋转偏移角）并调用 `RebuildLayout()` 更新所有按钮的角度位置与扇区命中参数，以保持点击命中区与可视位置同步
7. WHEN 开发者在 Inspector 中调整 `scrollRotateStepDegrees`（每一格滚轮对应的旋转角度，默认 15°） THEN 系统 SHALL 按该值作为单次滚动的旋转步长
8. IF 开发者将 `enableScrollRotate` 设置为 `false` THEN 系统 SHALL 完全关闭滚轮旋转响应，滚轮事件 SHALL 直接穿透
9. WHEN 滚轮旋转过程中按钮在角度环上移动 THEN 系统 SHALL 同步更新每个按钮 `RingSectorGraphic` 的 `sectorCenterDegrees`，确保旋转后点击命中区域与按钮视觉位置一致
10. WHEN 滚轮触发轮盘旋转 THEN 系统 SHALL 通过事件 `event Action<float> OnRotated`（参数为当前累计旋转角度）对外抛出，便于业务侧监听

### 边界与约束

- **性能约束**：命中测试为高频调用，环形判定 SHALL 仅使用平方距离比较（避免开方），且不分配堆内存
- **兼容性**：SHALL 在 `Canvas.renderMode` 的三种模式下均可工作（Overlay、ScreenSpaceCamera、WorldSpace 至少前两种）
- **扩展性**：按钮数量虽默认 8，但 SHALL 支持 4、6、8、12 等任意正整数
- **UI 风格**：本期仅实现交互逻辑与基础布局，按钮的视觉样式（背景图、动画）由美术后续在 Prefab 中配置
- **滚轮命中范围**：滚轮旋转的命中区域为"整个外圈以内的圆"（包含内圈空白），与点击命中"环形区域"的范围**不同**，需分别处理
- **方向语义**：在屏幕坐标系（Y 向上为正）下，"顺时针"对应角度数值减小，"逆时针"对应角度数值增大；实现时 SHALL 显式注释方向约定，避免与 `clockwise` 字段产生混淆
