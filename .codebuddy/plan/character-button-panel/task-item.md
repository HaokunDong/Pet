# 实施计划

- [ ] 1. 重构 `ControlModeManager` 的字段声明与初始化
   - 移除旧的单按钮相关字段（`buttonPrefab`、`actionButton`、`buttonText`）
   - 新增三个按钮引用字段：`changeCharacterBtn`、`trainBtn`、`controlBtn`
   - 新增 `CardSplineDistributor` 的缓存引用字段
   - 保留 `buttonOffset`（Vector2）字段用于面板位置偏移
   - _需求：1.1、1.4、5.1_

- [ ] 2. 重写 `CreateButtonUI` 方法，改为加载 CharacterButtonPanel 预制体
   - 使用 `Resources.Load<GameObject>("Prefabs/UI/CharacterButtonPanel")` 加载预制体
   - 如果加载失败，输出 `Debug.LogWarning` 并提前返回
   - 保留世界空间 Canvas 的创建逻辑（Canvas、CanvasScaler、GraphicRaycaster）
   - 将预制体实例化到 Canvas 下，替代旧的程序化按钮创建代码
   - 移除旧的 `else` 分支中程序化创建按钮的全部代码
   - _需求：1.1、1.2、1.4_

- [ ] 3. 绑定三个按钮的引用与点击事件
   - 在 `CreateButtonUI` 中通过 `transform.Find` 获取 `ChangeCharacter`、`Train`、`Control` 三个子物体的 Button 组件
   - 为三个按钮分别绑定点击事件：`OnChangeCharacterClicked`、`OnTrainClicked`、`OnControlClicked`
   - _需求：2.1、3.1、4.1_

- [ ] 4. 实现 `OnChangeCharacterClicked` 方法
   - 隐藏面板并恢复角色之前的行为模式（调用 `DismissButton`）
   - 查找场景中的 `CardSplineDistributor`（使用缓存引用），调用 `ToggleVisibility()` 打开卡组
   - 如果 `CardSplineDistributor` 未找到，安全跳过不崩溃
   - _需求：2.1、2.2_

- [ ] 5. 实现 `OnTrainClicked` 和 `OnControlClicked` 方法
   - `OnTrainClicked`：打印 `Debug.Log("Train")`，然后调用 `DismissButton` 隐藏面板并恢复模式
   - `OnControlClicked`：打印 `Debug.Log("Control")`，然后调用 `DismissButton` 隐藏面板并恢复模式
   - _需求：3.1、3.2、4.1、4.2_

- [ ] 6. 重构 `ShowButton` 方法，移除旧的文本切换逻辑
   - 移除根据 `previousMode` 设置 `buttonText.text` 的逻辑（不再需要单按钮的文本切换）
   - 保留面板的显示/隐藏和状态切换逻辑（`buttonInstance.SetActive(true)`、`isButtonShowing = true`、`CurrentMode = Paused_ShowingButton`）
   - _需求：1.3、6.1_

- [ ] 7. 更新 `IsClickOnButton` 方法以适配三按钮面板
   - 保留现有的 `EventSystem.IsPointerOverGameObject()` 检测逻辑（该方法对多按钮面板同样有效）
   - 确保点击面板上任意按钮时不会触发 Dismiss 逻辑
   - _需求：6.1、6.2_

- [ ] 8. 清理 `OnDestroy` 中的事件监听器
   - 移除旧的 `actionButton.onClick.RemoveAllListeners()` 
   - 改为移除三个按钮（`changeCharacterBtn`、`trainBtn`、`controlBtn`）的所有点击事件监听器
   - _需求：边界情况 - 按钮事件清理_

- [ ] 9. 移除旧的 `OnActionButtonClicked` 方法和不再使用的 `ControlMode.Manual` 相关逻辑
   - 删除 `OnActionButtonClicked` 方法（已被三个新方法替代）
   - 由于 Control 按钮当前仅打印日志，评估是否需要保留 `ControlMode` 枚举中的 `Manual` 状态和 `ManualController` 相关代码（暂时保留以备后续扩展）
   - _需求：4.1、边界情况 - 现有操控模式切换_
