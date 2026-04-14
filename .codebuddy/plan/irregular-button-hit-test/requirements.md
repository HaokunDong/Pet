# 需求文档：按钮不规则形状点击区域

## 引言

当前项目中的按钮（如 BlackHole）使用 Unity UI 的 `Image` 组件进行渲染，并通过 `EventTriggerListener` 检测点击事件。默认情况下，Unity UI 的射线检测（Raycast）基于 `RectTransform` 的矩形边界进行判定，这意味着即使图片中存在大量透明区域，用户点击这些透明区域仍然会触发点击事件。

本需求旨在让按钮的可点击区域精确贴合其挂载图片（Sprite）的不透明像素区域，使得点击透明区域时不会触发任何交互响应，从而提升用户体验的精确性和自然感。

### 当前状态

- 按钮使用 `Image` 组件显示序列帧动画（idle / onClicked），图片为不规则形状（如黑洞）
- 点击检测通过 `EventTriggerListener`（继承自 `EventTrigger`）实现，依赖 Unity 的 `Graphic.Raycast`
- 当前点击区域为 `RectTransform` 定义的矩形区域，无法区分透明与不透明像素
- 序列帧动画在运行时会动态切换 `Image.sprite`，点击区域需要跟随当前帧的形状变化

## 需求

### 需求 1：基于 Alpha 通道的不规则点击区域检测

**用户故事：** 作为一名玩家，我希望只有点击到按钮图片的可见（不透明）部分时才触发点击响应，以便点击体验更加精确自然，不会误触透明区域。

#### 验收标准

1. WHEN 用户点击按钮图片的不透明像素区域 THEN 系统 SHALL 正常触发点击事件（与当前行为一致）
2. WHEN 用户点击按钮图片的透明像素区域（Alpha 值低于设定阈值） THEN 系统 SHALL 不触发任何点击事件，点击事件应穿透到下层 UI 或被忽略
3. IF Alpha 阈值未被显式配置 THEN 系统 SHALL 使用默认阈值 0.5（即 Alpha < 0.5 的像素视为透明）
4. WHEN 按钮的序列帧动画切换到不同帧时 THEN 系统 SHALL 始终基于当前显示帧的 Alpha 通道进行点击区域判定

### 需求 2：Sprite 纹理可读性保障

**用户故事：** 作为一名开发者，我希望系统能自动处理 Sprite 纹理的可读性要求，以便不需要手动修改每张图片的导入设置。

#### 验收标准

1. WHEN 使用 Alpha 点击检测功能时 THEN 系统 SHALL 要求对应 Sprite 的纹理导入设置中 `Read/Write Enabled` 为开启状态
2. IF 纹理的 `Read/Write Enabled` 未开启 THEN 系统 SHALL 在控制台输出明确的警告信息，提示开发者需要开启该选项
3. WHEN 开发者按照警告提示开启 `Read/Write Enabled` 后 THEN 系统 SHALL 正常工作，无需额外操作

### 需求 3：Inspector 可配置的 Alpha 阈值

**用户故事：** 作为一名开发者，我希望能在 Inspector 面板中调整 Alpha 判定阈值，以便针对不同按钮微调点击区域的灵敏度。

#### 验收标准

1. WHEN 开发者在 Inspector 中修改 Alpha 阈值 THEN 系统 SHALL 实时更新点击区域的判定标准
2. IF Alpha 阈值设置为 0 THEN 系统 SHALL 将整个矩形区域都视为可点击（等同于默认行为）
3. IF Alpha 阈值设置为 1 THEN 系统 SHALL 仅允许完全不透明的像素区域响应点击
4. WHEN Alpha 阈值在 0 到 1 之间 THEN 系统 SHALL 将 Alpha 值大于等于阈值的像素视为可点击区域

### 需求 4：与现有序列帧动画系统兼容

**用户故事：** 作为一名开发者，我希望不规则点击区域功能能与现有的 `ButtonSpriteAnimation` 组件无缝配合，以便不需要重构现有的动画系统。

#### 验收标准

1. WHEN `ButtonSpriteAnimation` 在 Idle 和 OnClick 动画之间切换时 THEN 系统 SHALL 自动适应当前帧的不规则形状进行点击检测
2. WHEN 不规则点击检测组件与 `ButtonSpriteAnimation` 同时挂载在同一 GameObject 上时 THEN 系统 SHALL 正常工作，无冲突
3. IF 不规则点击检测组件被移除 THEN 系统 SHALL 回退到默认的矩形点击区域行为，不影响其他功能

### 需求 5：性能考量

**用户故事：** 作为一名开发者，我希望不规则点击检测不会对游戏性能产生明显影响，以便在移动端也能流畅运行。

#### 验收标准

1. WHEN 使用 Alpha 点击检测时 THEN 系统 SHALL 仅在射线检测命中矩形区域后才进行 Alpha 采样，避免不必要的计算
2. WHEN 多个按钮同时使用不规则点击检测时 THEN 系统 SHALL 不产生明显的帧率下降
3. IF 纹理不可读（`Read/Write Enabled` 未开启） THEN 系统 SHALL 优雅降级为矩形点击区域，而非抛出异常或崩溃
