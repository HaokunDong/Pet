# 需求文档：按钮序列帧动画组件（ButtonSpriteAnimation）

## 引言

本功能旨在为 Unity UI 按钮提供一个通用的**序列帧动画组件**，支持挂载 Idle（待机）和 OnClick（点击）两组序列帧动画。按钮平时循环播放 Idle 动画，当被点击时立即切换到 OnClick 动画播放，播放完毕后自动回到 Idle 动画。OnClick 动画支持被打断——连续快速点击时，每次点击都会从头重新播放 OnClick 动画，以增强操作手感和视觉反馈。

### 项目现状

- 项目已有 `ButtonClickAnim.cs` 组件，仅实现了 DOTween 缩放动画（按下缩小/松开恢复），不涉及序列帧
- 已有序列帧资源：`idle.png`（SpriteSheet，11帧 idle_0~idle_10）和 `onclicked.png`（SpriteSheet，10帧 onclicked_0~onclicked_9），位于 `Resources/UI/MainMenu/`
- 项目使用 `EventTriggerListener` 进行 UI 事件监听，使用 DOTween 做动画
- UI 基于 `BaseView` 体系，按钮在 `MainView` 中使用

---

## 需求

### 需求 1：序列帧动画播放器核心组件

**用户故事：** 作为一名开发者，我希望有一个可复用的序列帧动画播放组件，以便可以在任意 UI Image 上播放序列帧动画。

#### 验收标准

1. WHEN 组件挂载到带有 `Image` 组件的 GameObject 上 THEN 系统 SHALL 通过在 Inspector 中配置 Sprite 数组来设置序列帧
2. WHEN 组件启动 THEN 系统 SHALL 支持配置帧率（FPS），默认值为 10 帧/秒
3. WHEN 播放序列帧动画时 THEN 系统 SHALL 按照设定的帧率依次切换 Image 的 Sprite，实现流畅的逐帧动画效果
4. WHEN 动画播放中 THEN 系统 SHALL 支持循环播放（Loop）和单次播放（Once）两种模式

### 需求 2：Idle 待机动画循环播放

**用户故事：** 作为一名玩家，我希望按钮在未操作时持续播放待机动画，以便界面看起来生动有活力。

#### 验收标准

1. WHEN 组件初始化完成 THEN 系统 SHALL 自动开始循环播放 Idle 序列帧动画
2. WHEN Idle 动画播放到最后一帧 THEN 系统 SHALL 自动从第一帧重新开始播放，形成无缝循环
3. WHEN 没有任何点击操作发生 THEN 系统 SHALL 持续循环播放 Idle 动画，不中断

### 需求 3：OnClick 点击动画播放与自动回退

**用户故事：** 作为一名玩家，我希望点击按钮时看到一个点击反馈动画，播放完后自动恢复待机状态，以便获得清晰的操作反馈。

#### 验收标准

1. WHEN 按钮被点击 THEN 系统 SHALL 立即停止当前 Idle 动画，并从第 0 帧开始播放 OnClick 序列帧动画
2. WHEN OnClick 动画播放完最后一帧 THEN 系统 SHALL 自动切换回 Idle 动画并继续循环播放
3. WHEN 从 Idle 切换到 OnClick 动画时 THEN 系统 SHALL 确保切换过程无闪烁、无跳帧，视觉上流畅自然

### 需求 4：OnClick 动画可打断（连续点击支持）

**用户故事：** 作为一名玩家，我希望连续快速点击按钮时，每次点击都能立即重新播放点击动画，以便获得即时的操作手感反馈。

#### 验收标准

1. WHEN OnClick 动画正在播放期间再次点击按钮 THEN 系统 SHALL 立即打断当前 OnClick 动画，并从第 0 帧重新开始播放 OnClick 动画
2. WHEN 连续快速点击多次 THEN 系统 SHALL 每次点击都立即重置并重新播放 OnClick 动画，无延迟、无卡顿
3. IF 点击频率极高（如每秒 10 次以上） THEN 系统 SHALL 仍然保持正常响应，不出现动画状态混乱或内存泄漏

### 需求 5：组件通用性与 Inspector 配置

**用户故事：** 作为一名开发者，我希望该组件是通用的、可配置的，以便可以方便地应用到项目中的任意按钮上。

#### 验收标准

1. WHEN 在 Inspector 中配置组件 THEN 系统 SHALL 提供以下可配置项：
   - Idle 序列帧 Sprite 数组
   - OnClick 序列帧 Sprite 数组
   - Idle 动画帧率（FPS）
   - OnClick 动画帧率（FPS）
   - 是否自动播放 Idle 动画（默认开启）
2. WHEN 组件挂载到按钮上 THEN 系统 SHALL 自动获取同 GameObject 上的 Image 组件，无需手动拖拽引用
3. WHEN Idle 或 OnClick 序列帧数组为空 THEN 系统 SHALL 跳过对应动画的播放，不报错、不崩溃
4. WHEN 组件被销毁或禁用 THEN 系统 SHALL 正确清理协程/计时器，避免空引用异常

### 需求 6：与现有按钮系统集成

**用户故事：** 作为一名开发者，我希望新组件能与现有的 `EventTriggerListener` 和 `ButtonClickAnim` 共存，以便不影响已有功能。

#### 验收标准

1. WHEN 新组件与 `ButtonClickAnim`（缩放动画）同时挂载 THEN 系统 SHALL 两者互不干扰，可以同时生效（序列帧 + 缩放）
2. WHEN 使用 `EventTriggerListener` 的 onClick 事件触发点击 THEN 系统 SHALL 能够正确响应并播放 OnClick 动画
3. IF 开发者选择不使用 `EventTriggerListener` THEN 系统 SHALL 也支持通过公开方法 `PlayOnClick()` 手动触发 OnClick 动画

---

## 技术方案建议

推荐使用 **Coroutine（协程）** 方式实现序列帧播放，原因如下：

1. **轻量高效**：协程是 Unity 原生支持的异步机制，无额外依赖
2. **易于打断**：通过 `StopCoroutine` + 重新启动即可实现动画打断和重播
3. **帧率控制精确**：使用 `WaitForSeconds(1f / fps)` 可精确控制每帧间隔
4. **状态管理简单**：通过一个枚举状态（Idle / OnClick）即可管理动画切换

### 核心状态机

```
[Idle 循环播放] --点击--> [OnClick 单次播放] --播放完毕--> [Idle 循环播放]
                                  |
                              再次点击
                                  |
                          [重置 OnClick 从头播放]
```

## 边界情况考虑

- **序列帧为空**：跳过播放，保持当前 Sprite 不变
- **组件禁用/销毁**：在 `OnDisable` 和 `OnDestroy` 中停止所有协程
- **极高频点击**：每次点击只是重置帧索引并重启协程，不会累积协程
- **Image 组件缺失**：在 `Awake` 中检查并输出警告日志
