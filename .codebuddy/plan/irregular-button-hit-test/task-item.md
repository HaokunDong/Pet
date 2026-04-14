# 实施计划

- [ ] 1. 创建 AlphaHitTestImage 组件
   - 在 `Assets/Scripts/Utils/` 下新建 `AlphaHitTestImage.cs`
   - 继承 `MonoBehaviour`，在 `Awake` / `OnEnable` 中获取同 GameObject 上的 `Image` 组件
   - 暴露一个 `[Range(0, 1)]` 的 `alphaThreshold` 字段，默认值 0.5
   - 在初始化时将 `Image.alphaHitTestMinimumThreshold` 设置为该阈值
   - _需求：1.1、1.2、1.3、3.1、3.2、3.3、3.4_

- [ ] 2. 实现纹理可读性检测与优雅降级
   - 在 `AlphaHitTestImage` 初始化时检查当前 `Image.sprite.texture.isReadable`
   - 若纹理不可读，输出 `Debug.LogWarning` 提示开发者开启 `Read/Write Enabled`，并将 `alphaHitTestMinimumThreshold` 回退为 0（矩形区域）
   - 确保不会抛出异常或导致崩溃
   - _需求：2.1、2.2、2.3、5.3_

- [ ] 3. 实现阈值动态同步（Inspector 实时更新）
   - 在 `AlphaHitTestImage` 中通过 `OnValidate()` 方法监听 Inspector 中 `alphaThreshold` 的变化
   - 当值变化时实时同步到 `Image.alphaHitTestMinimumThreshold`
   - _需求：3.1_

- [ ] 4. 与 ButtonSpriteAnimation 集成 — 序列帧切换时保持阈值
   - 在 `ButtonSpriteAnimation.cs` 的 `PlayAnimation` 协程中，每次切换 `_image.sprite` 后，重新设置 `_image.alphaHitTestMinimumThreshold`（因为 Unity 在更换 sprite 时可能重置该值）
   - 为 `ButtonSpriteAnimation` 添加对同 GameObject 上 `AlphaHitTestImage` 组件的引用缓存
   - 若 `AlphaHitTestImage` 存在，则在每帧切换后调用其公开方法刷新阈值；若不存在则跳过，不影响原有逻辑
   - _需求：1.4、4.1、4.2、4.3_

- [ ] 5. 在 MainView 中集成 AlphaHitTestImage 组件
   - 在 `MainView.cs` 的 `InitButtonSpriteAnim` 方法中，为按钮 GameObject 添加或获取 `AlphaHitTestImage` 组件
   - 确保组件在 `ButtonSpriteAnimation` 初始化之后挂载，以便正确关联
   - _需求：4.1、4.2_

- [ ] 6. 配置 Sprite 纹理导入设置为 Read/Write Enabled
   - 确认 `Resources/UI/MainMenu/idle.png` 和 `Resources/UI/MainMenu/onclicked.png` 的纹理导入设置中 `Read/Write Enabled` 已开启
   - 如果未开启，需要在 Unity Editor 中手动开启或通过编辑 `.meta` 文件将 `isReadable: 1` 设置为启用
   - _需求：2.1、2.3_

- [ ] 7. 功能验证与边界情况测试
   - 在 Unity Editor 中运行项目，验证以下场景：
     - 点击黑洞不透明区域 → 触发 OnClick 动画 ✓
     - 点击黑洞透明区域 → 无响应，事件穿透 ✓
     - 连续快速点击不透明区域 → OnClick 动画可被打断并重播 ✓
     - 在 Inspector 中调整 alphaThreshold → 点击区域实时变化 ✓
     - 移除 AlphaHitTestImage 组件 → 回退为矩形点击区域 ✓
   - _需求：1.1、1.2、1.4、3.1、4.3、5.1、5.2_
