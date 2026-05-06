# 需求文档：桌面透明窗口（Desktop Transparent Window）

## 引言

本功能旨在实现类似《摸鱼小镇》的桌面放置类游戏效果：游戏窗口背景完全透明，GamePlay 内容（角色、UI、特效等）直接显示在用户桌面上方，用户可以一边工作一边与桌面宠物/角色互动。

核心体验目标：
- 窗口背景透明，只显示游戏中有像素的内容
- 窗口无边框，没有标题栏
- 窗口始终置顶，显示在其他应用程序上方
- 透明区域支持鼠标点击穿透（点击桌面/其他应用）
- 有像素的区域可以正常接收鼠标交互（点击角色、UI按钮等）
- 支持窗口拖动（通过特定方式移动窗口位置）

目标平台：Windows Standalone（x86_64）

## 需求

### 需求 1：窗口透明化

**用户故事：** 作为一名桌面宠物游戏玩家，我希望游戏窗口的背景完全透明，以便游戏角色看起来像是直接站在我的桌面上。

#### 验收标准

1. WHEN 游戏启动 THEN 系统 SHALL 将窗口设置为无边框模式（移除标题栏和窗口边框）
2. WHEN 游戏启动 THEN 系统 SHALL 通过 Windows DWM API 启用逐像素透明（per-pixel alpha）
3. WHEN 游戏运行时 THEN Camera SHALL 使用 Solid Color 清除模式且背景色 alpha 为 0
4. WHEN 游戏运行时 THEN 没有任何游戏对象渲染的区域 SHALL 显示为完全透明（可以看到桌面）
5. IF 当前运行环境为 Unity Editor THEN 系统 SHALL 跳过透明窗口设置并输出提示日志（透明效果仅在 Build 后生效）

### 需求 2：窗口置顶

**用户故事：** 作为一名桌面宠物游戏玩家，我希望游戏窗口始终显示在其他应用程序上方，以便我在使用其他软件时也能看到我的桌面宠物。

#### 验收标准

1. WHEN 游戏启动 THEN 系统 SHALL 将窗口设置为 TOPMOST 模式（始终置顶）
2. WHEN 用户切换到其他应用程序 THEN 游戏窗口 SHALL 仍然保持在最上层显示
3. WHEN 系统提供置顶开关选项时 AND 用户关闭置顶 THEN 窗口 SHALL 恢复为普通窗口层级

### 需求 3：智能点击穿透

**用户故事：** 作为一名桌面宠物游戏玩家，我希望透明区域的鼠标点击能穿透到桌面/其他应用，而有内容的区域能正常接收点击，以便我不会被游戏窗口阻挡正常的桌面操作。

#### 验收标准

1. WHEN 鼠标点击在完全透明的区域（alpha = 0）THEN 系统 SHALL 将该点击事件穿透到下方的桌面或其他应用程序
2. WHEN 鼠标点击在有像素渲染的区域（alpha > 0）THEN 系统 SHALL 正常接收该点击事件并传递给 Unity 的 EventSystem
3. WHEN 每帧更新时 THEN 系统 SHALL 读取鼠标位置对应的屏幕像素 alpha 值来动态决定是否启用点击穿透（WS_EX_TRANSPARENT）
4. IF 像素 alpha 值大于设定阈值（默认 10/255）THEN 系统 SHALL 移除 WS_EX_TRANSPARENT 标志（允许点击）
5. IF 像素 alpha 值小于等于设定阈值 THEN 系统 SHALL 添加 WS_EX_TRANSPARENT 标志（点击穿透）

### 需求 4：窗口拖动

**用户故事：** 作为一名桌面宠物游戏玩家，我希望能够拖动游戏窗口到桌面的任意位置，以便我可以把宠物放在我喜欢的位置。

#### 验收标准

1. WHEN 用户按住鼠标左键在角色/UI区域拖动 AND 当前处于拖动模式 THEN 系统 SHALL 通过 SetWindowPos API 移动窗口位置
2. WHEN 用户提供一个专用的拖动热区（如系统托盘图标或特定UI按钮）THEN 系统 SHALL 允许通过该热区拖动整个窗口
3. WHEN 窗口被拖动到屏幕边缘外 THEN 系统 SHALL 将窗口位置限制在屏幕可见范围内

### 需求 5：窗口尺寸与分辨率

**用户故事：** 作为一名桌面宠物游戏玩家，我希望游戏窗口有合适的默认大小，并且可以调整，以便游戏内容在我的桌面上显示得恰到好处。

#### 验收标准

1. WHEN 游戏启动 THEN 系统 SHALL 以配置的默认分辨率创建窗口（如 800x600 或全屏大小）
2. IF 配置为全屏透明模式 THEN 窗口 SHALL 覆盖整个屏幕但背景透明，角色可在整个桌面范围内活动
3. IF 配置为固定区域模式 THEN 窗口 SHALL 以指定尺寸显示，角色活动范围限制在窗口内

### 需求 6：Player Settings 兼容性配置

**用户故事：** 作为开发者，我希望有明确的 Player Settings 配置指引，以便透明窗口功能能正确工作。

#### 验收标准

1. WHEN 项目构建时 THEN Fullscreen Mode SHALL 设置为 Windowed 模式
2. WHEN 使用 Unity 2021.2+ 版本时 THEN "Use DXGI Flip Model Swapchain" SHALL 被关闭
3. WHEN 项目构建时 THEN Color Space 建议设置为 Gamma（避免 Linear 模式下的透明度问题）
4. IF 以上配置不正确 THEN 系统 SHALL 在启动时通过 Debug.LogWarning 输出配置建议

### 需求 7：系统托盘支持（可选）

**用户故事：** 作为一名桌面宠物游戏玩家，我希望游戏有系统托盘图标，以便我可以通过右键菜单退出游戏或进行设置，而不需要在任务栏中找到窗口。

#### 验收标准

1. WHEN 游戏启动 THEN 系统 SHALL 在 Windows 系统托盘区域显示一个图标
2. WHEN 用户右键点击托盘图标 THEN 系统 SHALL 显示上下文菜单（包含：置顶开关、退出等选项）
3. WHEN 用户选择"退出" THEN 系统 SHALL 正常关闭游戏应用程序

## 技术约束

- 目标平台：Windows Standalone (x86_64)
- 使用 Windows API：user32.dll（SetWindowLong、SetWindowPos、GetWindowLong）、Dwmapi.dll（DwmExtendFrameIntoClientArea）
- 智能点击穿透需要每帧读取屏幕像素，可能有性能开销，需要优化（如降低检测频率或使用 RenderTexture）
- 在 Unity Editor 中无法预览透明效果，仅在 Build 后生效
- 需要确保现有的 GamePlay（角色战斗、UI交互等）在透明窗口模式下正常工作

## 边界情况

- 多显示器环境下窗口拖动的处理
- 系统 DPI 缩放对窗口位置和像素检测的影响
- 全屏应用程序（如游戏）运行时置顶窗口的行为
- Windows 版本兼容性（Win10/Win11）
- 用户关闭 DWM（桌面窗口管理器）的极端情况
