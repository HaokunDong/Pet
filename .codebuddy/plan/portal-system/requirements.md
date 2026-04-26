# 需求文档：传送门系统（Portal System）

## 引言

本功能为游戏新增一个"传送门"系统。屏幕上方有一个可调位置的按钮，文字为"点击生成传送门"，玩家点击该按钮后会在指定的矩形区域内随机生成一个传送门。传送门是一个基于 UI 的可播放动画的预制体（Prefab），默认播放 Idle 动画，点击判定基于精灵形状（与裂隙主菜单逻辑一致，使用 AlphaHitTestImage）。传送门可被点击（打印日志）和拖拽移动。生成区域的大小/位置等参数均通过全局 ScriptableObject 配置，方便在 Inspector 中调控。

---

## 需求

### 需求 1：按钮触发传送门生成

**用户故事：** 作为一名玩家，我希望通过点击屏幕上方的按钮来生成传送门，以便在需要时主动创建传送门。

#### 验收标准

1. 系统 SHALL 在屏幕上方显示一个按钮，按钮文字为"点击生成传送门"
2. 按钮的位置 SHALL 可通过 `PortalSettings` 全局配置进行调整
3. WHEN 玩家点击该按钮 THEN 系统 SHALL 在配置的矩形区域内随机选取一个位置生成一个传送门预制体
4. IF 矩形区域内已有传送门 THEN 系统 SHALL 仍然允许在该区域内生成新的传送门（不做重叠限制，除非后续需求另行规定）

---

### 需求 2：全局配置（PortalSettings ScriptableObject）

**用户故事：** 作为一名开发者，我希望通过一个全局的 ScriptableObject 配置来调控传送门系统的所有参数，以便在 Inspector 中方便地调整而无需修改代码。

#### 验收标准

1. 系统 SHALL 提供一个名为 `PortalSettings` 的 ScriptableObject，可通过 `Assets > Create > Game > PortalSettings` 菜单创建
2. `PortalSettings` SHALL 包含以下可配置参数：
   - **生成区域中心**（spawnAreaCenter）：矩形区域在世界空间中的中心位置（Vector2）
   - **生成区域大小**（spawnAreaSize）：矩形区域的宽高（Vector2）
   - **传送门预制体**（portalPrefab）：传送门的 Prefab 引用
   - **生成按钮位置**（spawnButtonPosition）：屏幕上方按钮的锚点位置，可调整（Vector2）
3. `PortalSettings` SHALL 采用与项目中 `OutlineSettings` 相同的 Singleton 模式，通过 `Resources.Load` 自动加载
4. IF 未在 Resources 文件夹中找到 `PortalSettings` 资产 THEN 系统 SHALL 输出警告日志并使用默认值创建内存实例

---

### 需求 3：生成区域可视化（Editor Gizmo）

**用户故事：** 作为一名开发者，我希望在编辑器中能直观地看到传送门的生成矩形区域，以便方便地调整区域的大小和位置。

#### 验收标准

1. WHEN 在 Scene 视图中查看时 THEN 系统 SHALL 使用 Gizmos 绘制一个半透明的矩形框来表示传送门的生成区域
2. 矩形框 SHALL 根据 `PortalSettings` 中的 `spawnAreaCenter` 和 `spawnAreaSize` 参数实时更新
3. WHEN 在 Inspector 中修改生成区域参数 THEN Scene 视图中的矩形框 SHALL 立即更新以反映变化
4. 矩形框的颜色 SHALL 使用半透明的可辨识颜色（如半透明青色），不干扰正常的场景编辑

---

### 需求 4：传送门预制体（Portal Prefab）

**用户故事：** 作为一名开发者，我希望传送门是一个基于 UI 的可播放动画的预制体，点击判定基于精灵形状（与裂隙主菜单逻辑一致），以便我可以为其配置不同的动画效果。

#### 验收标准

1. 传送门预制体 SHALL 是一个 UI 元素（基于 RectTransform），包含以下组件：
   - **Image**：用于显示传送门的精灵图
   - **Animator**：用于播放传送门动画
   - **AlphaHitTestImage**：用于基于精灵 Alpha 通道的形状点击判定（与裂隙主菜单逻辑一致），只有精灵不透明的区域才响应点击
   - **Button（或 EventTriggerListener）**：用于接收点击事件
   - **自定义脚本 PortalController**：用于处理点击逻辑、拖拽逻辑和动画控制
2. 传送门 SHALL 默认播放名为 "Idle" 的动画状态
3. 预制体由用户自行在 Unity 编辑器中创建，代码中 SHALL 提供清晰的说明告知用户需要挂载哪些组件
4. 传送门生成后 SHALL 作为 Canvas 的子物体正确显示，确保 UI 层级正确

---

### 需求 5：传送门点击交互

**用户故事：** 作为一名玩家，我希望能够点击传送门并获得反馈，点击区域与传送门的实际形状一致，以便知道传送门是可交互的。

#### 验收标准

1. WHEN 玩家鼠标左键点击传送门 THEN 系统 SHALL 在控制台打印日志："传送门被点击"
2. 传送门的点击检测 SHALL 使用 `AlphaHitTestImage` 组件，基于精灵的 Alpha 通道判定点击区域，只有不透明像素区域才响应点击（与裂隙主菜单的逻辑一致）
3. 传送门精灵的纹理 SHALL 启用 Read/Write Enabled，以支持 Alpha 通道点击判定

---

### 需求 6：场景管理器（PortalManager）

**用户故事：** 作为一名开发者，我希望有一个管理器来统一管理传送门的生成和 UI 逻辑，以便系统运行有序且易于维护。

#### 验收标准

1. 系统 SHALL 提供一个 `PortalManager` MonoBehaviour 脚本，挂载到场景中的 GameObject 上
2. `PortalManager` SHALL 负责创建和管理"点击生成传送门"按钮
3. WHEN 按钮被点击 THEN `PortalManager` SHALL 在配置的矩形区域内随机位置实例化传送门预制体，作为 Canvas 的子物体
4. `PortalManager` SHALL 在 Scene 视图中通过 `OnDrawGizmos` 绘制生成区域的可视化矩形框
5. `PortalManager` SHALL 持有对 Canvas 的引用，以便将生成的传送门正确放置在 UI 层级中

---

### 需求 7：传送门拖拽移动

**用户故事：** 作为一名玩家，我希望能够按住鼠标拖拽传送门到摄像机范围内的任意位置，以便自由调整传送门的位置。

#### 验收标准

1. WHEN 玩家在传送门上按住鼠标左键并移动鼠标 THEN 传送门 SHALL 跟随鼠标位置实时移动
2. WHEN 玩家松开鼠标左键 THEN 传送门 SHALL 停留在当前位置
3. WHEN 拖拽传送门时 THEN 传送门的位置 SHALL 被限制在当前摄像机可视范围内，不允许拖出屏幕
4. WHEN 拖拽传送门时 THEN 拖拽操作 SHALL 不与传送门的点击事件冲突（需区分点击和拖拽：短按为点击，按住移动为拖拽）
5. 传送门作为 UI 元素，拖拽 SHALL 使用 `IDragHandler`、`IBeginDragHandler`、`IEndDragHandler` 等 UI 拖拽接口实现，确保与 UI 系统兼容

---

## 边界情况与注意事项

1. **性能考虑**：频繁点击按钮可能导致大量传送门生成，当前版本不做数量限制，但后续可考虑添加最大数量配置
2. **动画配置**：Animator Controller 和动画剪辑需要用户自行在 Unity 编辑器中创建和配置，代码仅负责播放 "Idle" 状态
3. **坐标系统**：生成区域使用世界坐标系，矩形区域的中心和大小均为世界空间值；传送门作为 UI 元素需要将世界坐标转换为 Canvas 坐标
4. **拖拽与点击区分**：需要通过拖拽距离阈值来区分点击和拖拽操作，避免拖拽结束时误触发点击事件
5. **拖拽边界限制**：拖拽时传送门位置应被限制在摄像机可视范围内
6. **Alpha 点击判定**：传送门精灵的纹理必须启用 Read/Write Enabled，否则 `AlphaHitTestImage` 将回退到矩形点击判定并输出警告
7. **Canvas 层级**：传送门和生成按钮都是 UI 元素，需要确保它们在同一个 Canvas 下且层级关系正确
