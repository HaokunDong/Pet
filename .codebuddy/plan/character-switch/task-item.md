# 实施计划

- [ ] 1. 在 `CardInertiaAndSnap` 中暴露动画状态属性
   - 添加公开只读属性 `IsAnimating`，当惯性滚动或吸附动画正在进行时返回 `true`
   - 供点击检测逻辑判断是否应忽略点击事件
   - _需求：1.4_

- [ ] 2. 在 `CardDragHandler` 中添加点击检测逻辑（区分拖拽与点击）
   - 实现 `IPointerDownHandler` 和 `IPointerUpHandler` 接口，记录按下位置和时间
   - 在 `OnPointerUp` 中判断：如果拖拽距离小于阈值且未触发 `OnBeginDrag`，则视为点击
   - 点击时检查：卡组是否可见（`distributor.IsVisible`）、是否处于惯性/吸附动画中（`CardInertiaAndSnap.IsAnimating`）
   - 仅当该卡牌是焦点卡牌时（`distributor.FocusIndex` 匹配自身索引）才触发点击事件
   - 通过 `CardSplineDistributor` 上的新事件 `OnFocusCardClicked` 转发点击
   - _需求：1.1、1.2、1.3、1.4、3.2、3.3_

- [ ] 3. 在 `CardSplineDistributor` 中添加焦点卡牌点击事件和卡牌索引查询
   - 添加 `OnFocusCardClicked` 事件（`Action<CharacterData>`），当焦点卡牌被点击时触发
   - 添加 `GetCardIndex(CharacterCard card)` 方法，返回卡牌在列表中的索引
   - 在 `Initialize()` 中将 `CardInertiaAndSnap` 引用注入到 `CardDragHandler`（用于动画状态检查）
   - _需求：1.1、1.2_

- [ ] 4. 在 `GameCharacterManager` 中实现角色切换方法
   - 将 `CreatePlayerCharacter` 方法改为 `public`
   - 添加 `public void SwitchPlayerCharacter(CharacterData newData)` 方法：
     - 检查 `newData` 是否为 null，是则输出错误日志并返回
     - 检查当前角色的 `CharacterData` 是否与 `newData` 相同，相同则跳过（避免重复切换）
     - 记录当前角色位置
     - 销毁当前玩家角色（通过 `PoolMgr` 回收或 `Destroy`）
     - 调用 `CreatePlayerCharacter(newData, position)` 生成新角色
     - 更新 `PlayerCharacters` 列表
   - _需求：2.1、2.2、2.4、2.5、1.5_

- [ ] 5. 连接焦点卡牌点击事件到角色切换流程
   - 在 `CardSplineDistributor` 中订阅 `OnFocusCardClicked` 事件，调用 `GameCharacterManager.SwitchPlayerCharacter`
   - 角色切换成功后自动调用 `Hide()` 关闭卡组面板
   - 需要在 `CardSplineDistributor` 中添加对 `GameCharacterManager` 的引用（通过 `FindObjectOfType` 或序列化字段）
   - _需求：2.1、2.3_

- [ ] 6. 添加焦点卡牌点击视觉反馈
   - 在 `CharacterCard` 或 `CardDragHandler` 中，当焦点卡牌被点击时播放短暂的缩放动画（如先缩小到 0.9 再恢复，使用 DOTween）
   - 动画完成后再触发角色切换事件
   - _需求：3.1_

- [ ] 7. 更新 `ControlModeManager.OnChangeCharacterClicked` 的逻辑
   - 当前 `OnChangeCharacterClicked` 只是打开卡组面板，无需修改（角色切换逻辑已在卡牌点击事件中处理）
   - 确认整体流程：点击角色 → 点击 ChangeCharacter 按钮 → 打开卡组 → 点击焦点卡牌 → 切换角色 → 关闭卡组
   - 验证角色切换后 `ControlModeManager` 等组件在新角色上正确初始化
   - _需求：2.2、2.4_
