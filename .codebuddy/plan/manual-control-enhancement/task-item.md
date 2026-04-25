# 实施计划：手动操控模式增强

- [ ] 1. 创建 SpriteOutline Shader
   - 在 `Assets/Shaders/` 下创建 `SpriteOutline.shader`
   - 基于现有 `CharacterFlash.shader` 的 Sprite 渲染结构，扩展支持描边功能
   - Shader 属性包括：`_OutlineEnabled`（开关）、`_OutlineColor`（描边颜色）、`_OutlineThickness`（描边粗细，像素单位）
   - 描边原理：在 fragment shader 中采样周围像素的 alpha 值，如果当前像素 alpha 为 0 但周围存在非零 alpha 像素，则绘制描边颜色
   - 保留原有的 Flash 效果属性（`_FlashColor`、`_FlashAmount`），使两个效果可以共存
   - _需求：3.6、4.6_

- [ ] 2. 创建 OutlineEffect 组件
   - 在 `Assets/Scripts/Character/` 下创建 `OutlineEffect.cs`
   - 参考 `FlashEffect.cs` 的模式，使用 `MaterialPropertyBlock` 控制描边属性，避免创建材质实例
   - 提供公共方法：`SetOutline(bool enabled, float thickness)` 用于设置描边开关和粗细
   - 提供公共方法：`TriggerBoldPulse()` 用于触发一次加粗脉冲效果（通过协程实现：立即加粗 → 等待可配置时长 → 恢复为指定粗细或关闭）
   - Inspector 可配置参数：`outlineColor`（默认红色）、`hoverThickness`（悬停粗细）、`boldThickness`（加粗粗细）、`boldDuration`（加粗持续时长）
   - 提供 `ResetOutline()` 方法用于立即清除所有描边状态
   - _需求：3.1、3.2、3.6、4.1、4.2、4.6_

- [ ] 3. 在 CharacterEntity 中集成 OutlineEffect
   - 在 `CharacterEntity.cs` 中添加 `OutlineEffect` 的公共属性引用（类似 `FlashFx`）
   - 在 `Initialize()` 方法中确保 `OutlineEffect` 组件存在（GetComponent 或 AddComponent）
   - 在 `InitializeFlashEffect()` 中将 SpriteRenderer 的材质替换为使用新的 `SpriteOutline` shader（该 shader 同时支持 Flash 和 Outline）
   - 在 `Die()` 方法中调用 `OutlineEffect.ResetOutline()` 清除描边
   - 在 `CleanUp()` 方法中重置描边状态
   - _需求：3.4、3.5、4.4、4.5_

- [ ] 4. 修改 ManualController 实现定点水平移动
   - 将现有的 `moveDirection`（方向）+ 无限移动逻辑替换为 `targetX`（目标 X 坐标）+ 定点移动逻辑
   - 新增 `float targetX` 和 `bool hasTargetX` 字段
   - 在 `HandleRightClickInput()` 中：点击空地时记录 `targetX = mouseWorldPos.x`，设置 `hasTargetX = true`
   - 在 `HandleMovement()` 中：每帧向 `targetX` 移动，当 `Mathf.Abs(transform.position.x - targetX) < 0.05f` 时停止移动并播放 Idle
   - 移动过程中使用 `Mathf.MoveTowards` 防止越过目标点
   - _需求：1.1、1.2、1.3、1.4、1.5、1.6_

- [ ] 5. 创建 MoveArrowIndicator 组件
   - 在 `Assets/Scripts/Character/` 下创建 `MoveArrowIndicator.cs`
   - 使用代码动态创建箭头 GameObject：通过 `Mesh` 或 `LineRenderer` 绘制向下的箭头形状
   - 箭头动画通过协程实现：缩放从 0 到 1 的弹性动画 + 淡出消失，播放完毕后自动销毁 GameObject
   - Inspector 可配置参数：`arrowColor`（箭头颜色）、`arrowScale`（箭头大小）、`animationDuration`（动画时长）
   - 提供静态方法 `MoveArrowIndicator.Spawn(Vector3 worldPos)` 用于在指定位置生成箭头
   - 提供 `DestroyImmediate()` 方法用于立即销毁当前箭头
   - _需求：2.1、2.2、2.6_

- [ ] 6. 在 ManualController 中集成箭头指示器
   - 新增 `MoveArrowIndicator currentArrow` 字段跟踪当前箭头实例
   - 在 `HandleRightClickInput()` 中：点击空地时销毁旧箭头、在点击位置生成新箭头
   - 在 `HandleRightClickInput()` 中：点击敌人时不生成箭头
   - 在 `SetActive(false)` 中：销毁当前存在的箭头
   - _需求：2.1、2.3、2.4、2.5_

- [ ] 7. 在 ManualController 中实现鼠标悬停敌人描边检测
   - 新增 `CharacterEntity hoveredEnemy` 字段跟踪当前悬停的敌人
   - 在 `Update()` 中新增 `HandleMouseHover()` 方法：每帧通过 `Physics2D.OverlapPoint(mouseWorldPos)` 检测鼠标下方的敌人
   - 当检测到新的敌人时：移除旧敌人的描边，为新敌人调用 `OutlineEffect.SetOutline(true, hoverThickness)`
   - 当鼠标移开敌人时：调用 `OutlineEffect.SetOutline(false, 0)` 移除描边
   - 当悬停的敌人死亡时（检查 `IsAlive`）：清除描边和 `hoveredEnemy` 引用
   - 在 `SetActive(false)` 中：清除所有描边状态
   - _需求：3.1、3.2、3.3、3.4、3.5_

- [ ] 8. 在 ManualController 中实现右键点击敌人描边加粗反馈
   - 在 `HandleRightClickInput()` 中：当右键点击敌人时，调用该敌人的 `OutlineEffect.TriggerBoldPulse()` 触发加粗脉冲
   - 加粗脉冲结束后，`OutlineEffect` 内部根据当前是否仍被悬停来决定恢复为悬停粗细或完全关闭
   - 为此需要在 `OutlineEffect` 中增加一个回调或状态查询机制，让它知道脉冲结束后应恢复到什么状态
   - _需求：4.1、4.2、4.3、4.4、4.5_
