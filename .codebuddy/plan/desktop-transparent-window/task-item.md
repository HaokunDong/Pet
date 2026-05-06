# 实施计划：桌面透明窗口（Desktop Transparent Window）

- [ ] 1. 创建 TransparentWindowManager 核心脚本
   - 创建 `Assets/Scripts/DesktopWindow/TransparentWindowManager.cs` 单例 MonoBehaviour
   - 使用 `[DllImport]` 声明所需的 Windows API：`GetActiveWindow`、`SetWindowLong`、`GetWindowLong`、`SetWindowPos`、`SetLayeredWindowAttributes`（user32.dll）和 `DwmExtendFrameIntoClientArea`（Dwmapi.dll）
   - 在 `Awake()` 中获取窗口句柄，移除 `WS_BORDER`、`WS_DLGFRAME`、`WS_CAPTION` 样式实现无边框
   - 添加 `WS_EX_LAYERED` 扩展样式，调用 `DwmExtendFrameIntoClientArea` 设置 margins 为 -1 实现逐像素透明
   - 使用 `#if !UNITY_EDITOR` 条件编译，Editor 环境下跳过并输出 `Debug.Log` 提示
   - _需求：1.1、1.2、1.5_

- [ ] 2. 配置 Camera 透明背景
   - 在 `TransparentWindowManager` 初始化时，将 Main Camera 的 Clear Flags 设置为 `CameraClearFlags.SolidColor`，背景色设为 `new Color(0, 0, 0, 0)`
   - 确保场景中没有 Skybox 或其他不透明背景元素遮挡透明效果
   - _需求：1.3、1.4_

- [ ] 3. 实现窗口置顶功能
   - 在 `TransparentWindowManager` 中调用 `SetWindowPos` 将窗口设置为 `HWND_TOPMOST`（-1）
   - 添加公开方法 `SetTopMost(bool enabled)` 支持运行时切换置顶/取消置顶（`HWND_NOTOPMOST`）
   - _需求：2.1、2.2、2.3_

- [ ] 4. 实现智能点击穿透系统
   - 创建 `Assets/Scripts/DesktopWindow/ClickThroughManager.cs`
   - 在 `Update()` 中使用 RenderTexture + `ReadPixels()` 读取鼠标位置对应像素的 alpha 值
   - 当 alpha <= 阈值（可配置，默认 10）时，通过 `SetWindowLong` 添加 `WS_EX_TRANSPARENT` 标志实现点击穿透
   - 当 alpha > 阈值时，移除 `WS_EX_TRANSPARENT` 标志恢复点击接收
   - 添加性能优化：降低检测频率（如每 2-3 帧检测一次）或使用小尺寸 RenderTexture
   - _需求：3.1、3.2、3.3、3.4、3.5_

- [ ] 5. 实现窗口拖动功能
   - 创建 `Assets/Scripts/DesktopWindow/WindowDragHandler.cs`
   - 监听鼠标按下/拖动事件，计算鼠标位移差值，通过 `SetWindowPos` 移动窗口
   - 提供一个可配置的拖动触发方式（如按住特定键+鼠标拖动，或指定 UI 区域拖动）
   - 添加屏幕边界检测，防止窗口被拖出可见范围（使用 `Screen.currentResolution` 获取屏幕尺寸）
   - _需求：4.1、4.2、4.3_

- [ ] 6. 实现窗口尺寸配置
   - 创建 `Assets/Scripts/DesktopWindow/WindowConfig.cs` ScriptableObject 存储窗口配置（默认尺寸、是否全屏透明模式等）
   - 在 `TransparentWindowManager` 启动时根据配置调用 `SetWindowPos` 设置窗口大小和位置
   - 全屏透明模式下将窗口尺寸设为屏幕分辨率；固定区域模式下使用配置的宽高值
   - _需求：5.1、5.2、5.3_

- [ ] 7. 添加 Player Settings 兼容性检查
   - 在 `TransparentWindowManager` 的 `Awake()` 中检查运行时配置
   - 如果检测到 `Screen.fullScreenMode` 不是 `FullScreenMode.Windowed`，输出 `Debug.LogWarning` 提示
   - 在项目中创建 `Assets/Editor/TransparentWindowValidator.cs` 编辑器脚本，在 Build 前自动检查 Player Settings（Windowed 模式、DXGI Flip Model 关闭等）
   - _需求：6.1、6.2、6.3、6.4_

- [ ] 8. 实现系统托盘功能（可选）
   - 创建 `Assets/Scripts/DesktopWindow/SystemTrayManager.cs`
   - 使用 Windows Shell_NotifyIcon API 或第三方库（如 `System.Windows.Forms.NotifyIcon`）创建托盘图标
   - 实现右键菜单：置顶开关（调用 `SetTopMost`）、退出（调用 `Application.Quit()`）
   - _需求：7.1、7.2、7.3_

- [ ] 9. 集成测试与场景配置
   - 创建一个测试场景或在现有场景中添加 `TransparentWindowManager` 预制体
   - 确保现有 GamePlay（角色、战斗、UI）在透明窗口模式下正常工作
   - 进行 Windows Build 测试，验证透明效果、点击穿透、置顶、拖动功能
   - 处理 DPI 缩放问题（使用 `SetProcessDPIAware` API）
   - _需求：1.1-1.5、2.1-2.2、3.1-3.5、4.1-4.3_
