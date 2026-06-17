# Ring Radial Menu

环形（甜甜圈）轮盘菜单，围绕指定物体作为圆心进行布局，环上等距环绕若干按钮（默认 8 个）。
**只有在环形区域内（内圈与外圈之间）的点击才会被按钮响应**，落在内圈空洞或外圈之外的点击会穿透到下层 UI / 场景。

---

## 文件结构

| 文件 | 作用 |
|------|------|
| `RingRadialMenuSettings.cs` | 几何参数（内/外半径、按钮数、起始角、方向）的可序列化配置 |
| `RingSectorGraphic.cs` | 自定义 `Image`，重写 `IsRaycastLocationValid` 实现"圆环 + 扇区"命中测试 |
| `RingRadialMenuButton.cs` | 单个按钮单元，包装 `RingSectorGraphic` + 图标 `Image`，并派发点击事件 |
| `RingRadialMenu.cs` | 核心组件：等角度布局 + 圆心目标跟随 + 对外回调 API + 滚轮旋转 + Gizmos |
| `RingScrollRotateZone.cs` | 透明 Graphic，捕获外圈以内的鼠标滚轮事件并驱动轮盘旋转 |
| `RingRadialMenuSample.cs` | 用法示例（设置目标、注册回调、切换图标、订阅旋转事件） |
| `Editor/CreateRingRadialMenuPrefab.cs` | 一键生成 Prefab：`Tools > PetGame > Create Ring Radial Menu Prefab` |

---

## 快速开始

### 1. 自动生成 Prefab

Unity 菜单：`Tools > PetGame > Create Ring Radial Menu Prefab`

会在 `Assets/Resources/Prefabs/UI/RingRadialMenu.prefab` 下生成包含：

- 根节点：`RectTransform` + `CanvasGroup` + `RingRadialMenu`
- `ScrollRotateZone` 子节点：透明的 `RingScrollRotateZone`，负责接收外圈以内的滚轮事件
- 8 个子按钮：每个含 `RingRadialMenuButton`，子节点：
  - `Sector`（铺满父节点的 `RingSectorGraphic` 背景，负责命中测试）
  - `Icon`（居中的图标 `Image`）

### 2. 放入场景

将该 Prefab 放到一个 `Canvas` 下（任意 `renderMode` 都支持）。

### 3. 代码用法

```csharp
RingRadialMenu menu = GetComponent<RingRadialMenu>();

// 让轮盘围绕某个世界物体（角色 / 宠物）
menu.SetCenterTarget(playerTransform);

// 单独配置某个按钮
menu.SetButtonIcon(0, attackSprite);
menu.SetButtonCallback(0, () => Debug.Log("Attack!"));

// 监听所有按钮点击（带 index）
menu.OnButtonClicked += idx => Debug.Log($"clicked {idx}");

// 监听滚轮驱动的旋转
menu.OnRotated += deg => Debug.Log($"rotated to {deg:F1}°");

// 运行时关闭滚轮旋转（事件将直接穿透）
menu.SetScrollRotateEnabled(false);
```

或参考 [`RingRadialMenuSample.cs`](RingRadialMenuSample.cs) 的完整示例。

---

## Inspector 参数

### `RingRadialMenu` 组件

- **Geometry / Settings**
  - `innerRadius` — 内圈半径（rect 单位）。默认 80。
  - `outerRadius` — 外圈半径（rect 单位）。默认 160。
  - `buttonCount` — 按钮数量（默认 8，可设 4/6/8/12 等）。
  - `startAngleDegrees` — 第一个按钮的角度，0 = +X，90 = +Y。默认 90（正上方）。
  - `clockwise` — 是否顺时针排列。默认 true。
- **Scroll Rotation**
  - `enableScrollRotate` — 是否允许鼠标滚轮在外圈以内驱动轮盘旋转。默认 true。
  - `scrollRotateStepDegrees` — 单次滚轮对应的旋转步长（度）。默认 15。
- **Center Following**
  - `centerTarget` — 世界空间圆心目标。空 = 不跟随，使用自身 `anchoredPosition`。
  - `worldCamera` — 投影所用相机；空时回退到 `Camera.main`。
  - `parentCanvas` — 父 Canvas；自动解析。
  - `canvasGroup` — 用于隐藏/禁交互；缺省时按需自动添加。
- **Buttons**
  - `buttons` — 子按钮列表；空时自动从子节点收集。
- **Debug**
  - `drawGizmos` — Scene 视图绘制内/外圈与按钮中心点。

### `RingSectorGraphic` 组件

通常无需手动配置 —— `RingRadialMenu.RebuildLayout()` 会在每次布局时把 `innerRadius` / `outerRadius` / `sectorCenterDegrees` / `sectorAngleSize` 注入。

---

## 工作原理

### 命中测试

`RingSectorGraphic` 重写 `IsRaycastLocationValid(Vector2 sp, Camera cam)`：

1. 先调用基类的标准 rect/alpha 检查；
2. 通过 `RectTransformUtility.ScreenPointToLocalPointInRectangle` 把屏幕坐标转换为 **圆心 RectTransform 的本地坐标**；
3. **半径检查**：`sqrDist` 与 `innerRadius²` / `outerRadius²` 比较，**完全规避 `Mathf.Sqrt` 与堆内存分配**；
4. **角度检查**：`Mathf.Atan2` + `Mathf.DeltaAngle`，落在 `[sectorCenter - half, sectorCenter + half]` 之外则拒绝。

任一检查失败都会返回 `false`，UGUI 会继续向下层穿透检测。

### 布局

`RingRadialMenu.RebuildLayout()` 在 `Awake` / `OnEnable` / `OnValidate`（编辑器模式）时调用：

- 角度间隔 = `360 / buttonCount`
- 每个按钮中心位于半径 `(innerRadius + outerRadius) / 2` 的圆周
- 第 i 个按钮角度 = `startAngleDegrees ± step × i`（方向取决于 `clockwise`）
- 同时把扇区参数注入对应的 `RingSectorGraphic`

### 圆心跟随

`LateUpdate` 中：

- `Camera.WorldToScreenPoint(centerTarget.position)` → 屏幕坐标；
- 若 `screen.z < 0`（目标在相机后方），通过 `CanvasGroup` 隐藏并禁交互；
- 否则 `RectTransformUtility.ScreenPointToLocalPointInRectangle` 将屏幕坐标转回父 RectTransform 局部坐标，赋给 `anchoredPosition`；
- 自动兼容 `ScreenSpaceOverlay`（uiCam 传 null）与 `ScreenSpaceCamera`（uiCam = `canvas.worldCamera`）。

### 滚轮旋转

`RingScrollRotateZone` 是一个不渲染任何像素的 `Graphic`，只参与 UGUI 的 raycast 与滚轮事件分发：

- **命中范围**：仅当鼠标位置到圆心的 `sqrDistance <= outerRadius²` 时返回 `true`；这个范围是「整个外圈以内的圆」，与点击的「环形区域」不同（包含内圈空洞）。
- **方向约定**（屏幕坐标系，Y 向上为正）：
  - `scrollDelta.y > 0`（滚轮向上）→ 顺时针旋转 → 角度数值减小
  - `scrollDelta.y < 0`（滚轮向下）→ 逆时针旋转 → 角度数值增大
- **逻辑**：`OnScroll` 仅在命中后调用 `RingRadialMenu.HandleScrollRotate(deltaY)`，后者按 `scrollRotateStepDegrees` 累加到内部 `accumulatedRotation`，再调用 `RebuildLayout()` 同步按钮位置与扇区命中参数，保证「看得见」与「点得中」始终一致。
- **事件**：每次旋转生效后会招出 `OnRotated(float accumulatedDegrees)`，供业务侧监听。
- **运行时开关**：`SetScrollRotateEnabled(false)` 合上后，`RingScrollRotateZone` 的 `IsRaycastLocationValid` 直接返回 `false`，滚轮事件会穿透到下层。

---

## 常见问题

**Q：按钮的 RectTransform 范围是矩形，会不会"误命中"？**
不会。`RingSectorGraphic.IsRaycastLocationValid` 强制做了圆环 + 扇区双重判定，rect 的矩形范围只是渲染区域。

**Q：为什么默认按钮区域看起来是半透明黑色矩形？**
那是 Prefab 自动生成时给 `RingSectorGraphic` 的占位颜色（`alpha=0.5`），便于调试。换成实际的扇形 sprite 后矩形就不可见了。
注意：**Sprite 仅影响视觉，命中测试始终是圆环 + 扇区，不依赖 sprite 形状**。

**Q：可以改成 4 / 6 / 12 个按钮吗？**
可以。修改 `RingRadialMenu.settings.buttonCount` 并增删子节点（每个子节点必须含 `RingRadialMenuButton`），运行时调用 `RebuildLayout()` 即可。

**Q：编辑模式下调整半径，按钮位置不刷新？**
保证根节点 `RingRadialMenu` 处于激活状态。`OnValidate` 中通过 `EditorApplication.delayCall` 延迟刷新，避免 `SendMessage` 警告。
