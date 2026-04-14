# 实施计划

- [ ] 1. 创建 `ButtonSpriteAnimation` 核心组件脚本
   - 在 `Assets/Scripts/Utils/` 目录下创建 `ButtonSpriteAnimation.cs`
   - 定义 Inspector 可配置字段：`idleSprites`（Sprite[]）、`onClickSprites`（Sprite[]）、`idleFPS`（float，默认10）、`onClickFPS`（float，默认10）、`autoPlayIdle`（bool，默认true）
   - 定义动画状态枚举 `AnimState { Idle, OnClick }`，用于跟踪当前播放状态
   - 在 `Awake` 中自动获取同 GameObject 上的 `Image` 组件，若缺失则输出警告日志
   - _需求：1.1、1.2、5.1、5.2、5.3_

- [ ] 2. 实现序列帧协程播放逻辑
   - 编写私有协程方法 `PlayAnimation(Sprite[] frames, float fps, bool loop)`，按帧率逐帧切换 Image 的 Sprite
   - 使用 `WaitForSeconds(1f / fps)` 控制帧间隔
   - `loop` 为 true 时循环播放（播完最后一帧回到第一帧），为 false 时单次播放完毕后结束协程
   - 单次播放结束后通过回调或状态判断触发后续逻辑（如回到 Idle）
   - _需求：1.3、1.4_

- [ ] 3. 实现 Idle 待机动画循环播放
   - 编写 `PlayIdle()` 公开方法，停止当前所有动画协程，启动 Idle 循环播放协程
   - 在 `Start` 或 `OnEnable` 中根据 `autoPlayIdle` 配置自动调用 `PlayIdle()`
   - 确保 Idle 动画无缝循环：最后一帧播完后立即回到第一帧
   - 当 `idleSprites` 数组为空时跳过播放，不报错
   - _需求：2.1、2.2、2.3、5.3_

- [ ] 4. 实现 OnClick 点击动画播放与自动回退
   - 编写 `PlayOnClick()` 公开方法，停止当前动画协程，将状态设为 `AnimState.OnClick`，启动 OnClick 单次播放协程
   - OnClick 协程播放完最后一帧后，自动调用 `PlayIdle()` 切回待机动画
   - 当 `onClickSprites` 数组为空时跳过播放，不报错
   - _需求：3.1、3.2、3.3、5.3_

- [ ] 5. 实现 OnClick 动画可打断（连续点击支持）
   - 在 `PlayOnClick()` 方法中，每次调用时先通过 `StopCoroutine` 停止当前正在运行的动画协程引用，再重新启动 OnClick 协程
   - 使用单一协程引用变量（`Coroutine _currentAnim`）确保同一时刻只有一个动画协程在运行，避免协程累积
   - 验证连续快速调用 `PlayOnClick()` 时，每次都能正确从第 0 帧重新播放，无状态混乱
   - _需求：4.1、4.2、4.3_

- [ ] 6. 实现组件生命周期管理
   - 在 `OnDisable` 中停止所有动画协程，重置状态
   - 在 `OnDestroy` 中清理协程引用，避免空引用异常
   - 在 `OnEnable` 中根据配置重新启动 Idle 动画（如果 `autoPlayIdle` 为 true）
   - _需求：5.4_

- [ ] 7. 在 `MainView` 中集成 `ButtonSpriteAnimation` 组件
   - 在 `MainView.OnAwake()` 中，为需要序列帧动画的按钮获取或添加 `ButtonSpriteAnimation` 组件
   - 在按钮的 `EventTriggerListener.onClick` 回调中调用对应按钮的 `PlayOnClick()` 方法
   - 确保与现有的 `ButtonClickAnim` 缩放动画共存，两者互不干扰
   - 如果序列帧资源需要通过代码加载（而非 Inspector 拖拽），则使用 `Resources.LoadAll<Sprite>()` 加载 SpriteSheet 中的所有子 Sprite
   - _需求：6.1、6.2、6.3_
