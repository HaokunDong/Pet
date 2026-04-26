# 实施计划：传送门系统（Portal System）

- [ ] 1. 创建 PortalSettings 全局配置 ScriptableObject
  - 在 `Assets/Scripts/Data/` 下创建 `PortalSettings.cs`
  - 采用与 `OutlineSettings` 相同的 Singleton 模式（`Resources.Load<PortalSettings>("PortalSettings")`）
  - 包含可配置参数：`spawnAreaCenter`（Vector2）、`spawnAreaSize`（Vector2）、`portalPrefab`（GameObject）、`spawnButtonPosition`（Vector2）
  - 添加 `[CreateAssetMenu(fileName = "PortalSettings", menuName = "Game/PortalSettings")]` 菜单
  - 未找到资产时输出警告日志并创建内存默认实例
  - _需求：2.1、2.2、2.3、2.4_

- [ ] 2. 创建 PortalController 脚本（传送门点击、拖拽、动画控制）
  - 在 `Assets/Scripts/UI/` 下创建 `PortalController.cs`
  - 实现 `IBeginDragHandler`、`IDragHandler`、`IEndDragHandler` 接口处理 UI 拖拽
  - 拖拽时将传送门位置限制在摄像机可视范围内（通过 `Camera.main` 的 ViewportToWorldPoint 计算边界，再转换为 Canvas 坐标）
  - 通过拖拽距离阈值区分点击和拖拽：拖拽开始时记录起始位置，拖拽距离超过阈值则标记为拖拽，否则视为点击
  - 点击时打印日志 `Debug.Log("传送门被点击")`
  - Animator 默认播放 "Idle" 动画状态
  - 在脚本注释中说明预制体需要挂载的组件：Image、Animator、AlphaHitTestImage、Button、PortalController
  - _需求：4.1、4.2、4.3、5.1、5.2、7.1、7.2、7.3、7.4、7.5_

- [ ] 3. 创建 PortalManager 场景管理器
  - 在 `Assets/Scripts/Manager/` 下创建 `PortalManager.cs`
  - 持有对场景中 Canvas 的引用（`[SerializeField] private Canvas portalCanvas`）
  - 在 `Start` 中动态创建"点击生成传送门"按钮（UI Button），按钮文字为"点击生成传送门"，位置由 `PortalSettings.Instance.spawnButtonPosition` 控制
  - 按钮点击回调：在 `PortalSettings` 配置的矩形区域内随机选取世界坐标位置，将世界坐标转换为 Canvas 局部坐标，实例化传送门预制体作为 Canvas 子物体
  - 实现 `OnDrawGizmos` 方法：读取 `PortalSettings.Instance` 的 `spawnAreaCenter` 和 `spawnAreaSize`，绘制半透明青色矩形框可视化生成区域
  - _需求：1.1、1.2、1.3、1.4、3.1、3.2、3.3、3.4、6.1、6.2、6.3、6.4、6.5_

- [ ] 4. 在 Resources 文件夹中创建 PortalSettings 资产
  - 通过代码或手动方式在 `Assets/Resources/` 下创建 `PortalSettings.asset`
  - 设置合理的默认值：生成区域中心、大小、按钮位置等
  - 将传送门预制体引用留空（由用户在 Unity 编辑器中手动指定）
  - _需求：2.3_

- [ ] 5. 验证与集成测试
  - 确保 `PortalSettings` 的 Singleton 能正确加载
  - 确保点击按钮后传送门在矩形区域内正确生成并显示为 Canvas 子物体
  - 确保传送门点击打印日志 "传送门被点击"
  - 确保传送门拖拽正常工作且不与点击冲突
  - 确保 Scene 视图中 Gizmos 矩形框正确显示
  - 确保 `AlphaHitTestImage` 的形状点击判定正常工作（需要传送门精灵纹理启用 Read/Write Enabled）
  - _需求：1.3、3.1、4.4、5.1、5.3、7.4_
