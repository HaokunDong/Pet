# 实施计划：角色阵亡与复活系统

- [ ] 1. 在 CharacterCard 中添加冷却状态支持
   - 在 `CharacterCard.cs` 中添加冷却相关字段：`isCooldown`、`cooldownRemaining`、`cooldownDuration`
   - 添加一个 `TMP_Text cooldownText` 引用用于显示冷却倒计时
   - 实现 `StartCooldown(float duration)` 方法：设置灰色、显示倒计时文本、开始计时
   - 在 `Update()` 中递减冷却时间，冷却结束时恢复正常颜色并隐藏倒计时文本
   - 添加 `IsCooldown` 公开属性供外部查询
   - _需求：1.1, 1.2, 1.3, 5.1, 5.2, 5.3_

- [ ] 2. 在 CharacterData 中添加可选的独立冷却时间配置
   - 在 `CharacterData` ScriptableObject 中添加 `respawnCooldown` 字段（默认值 0 表示使用全局值）
   - _需求：4.2_

- [ ] 3. 创建 CharacterDeathManager 管理角色阵亡与冷却逻辑
   - 创建 `CharacterDeathManager.cs` 单例脚本
   - 添加全局冷却时间配置字段 `[SerializeField] float globalCooldown = 30f`（可在 Inspector 调整）
   - 监听玩家角色的 `OnDeath` 事件
   - 角色阵亡时：找到对应的 `CharacterCard`，调用 `StartCooldown()` 并传入冷却时间（优先使用 CharacterData 的独立值，否则使用全局值）
   - 角色阵亡时：保留培养数据（CultivationManager 中的数据不做清除，无需额外操作）
   - 角色阵亡时：不自动弹出选卡界面
   - _需求：1.1, 1.4, 1.5, 2.1, 4.1, 4.2_

- [ ] 4. 修改 CardDragHandler / CardSplineDistributor 的点击逻辑以支持冷却检查
   - 在 `CardDragHandler.OnPointerUp` 或 `CardSplineDistributor.NotifyFocusCardClicked` 中，点击卡牌前检查 `CharacterCard.IsCooldown`
   - 如果卡牌处于冷却状态，阻止角色切换操作（静默忽略）
   - _需求：1.4, 5.4_

- [ ] 5. 修改 GameCharacterManager.SwitchPlayerCharacter 以支持在 PlayerSpawn 位置生成角色
   - 修改 `SwitchPlayerCharacter` 方法，使其在 PlayerSpawn 位置（而非当前角色位置）生成新角色
   - 添加获取 PlayerSpawn 位置的辅助方法
   - _需求：2.3, 3.1_

- [ ] 6. 实现角色重新登场时恢复培养属性与满血
   - 在角色切换成功后（`SwitchPlayerCharacter` 或新增的复活逻辑中），调用 `CultivationManager.Instance.ApplyCultivationBonuses(entity)` 恢复属性
   - 将角色血量设置为 `maxHealth`
   - _需求：3.2, 3.3_

- [ ] 7. 实现角色重新登场时默认以 AI 模式行动
   - 在角色切换/重新登场后，确保 `ControlModeManager.SetMode(ControlMode.AI_Auto)` 被调用
   - 或在 `GameCharacterManager.CreatePlayerCharacter` 中确认默认就是 AI 模式（当前已是，需验证切换场景下也生效）
   - _需求：3.4_

- [ ] 8. 确保选卡界面打开时正确显示所有卡牌状态
   - 在 `CardSplineDistributor.Show()` 时，遍历所有卡牌刷新其冷却视觉状态
   - 确保冷却中的卡牌显示灰色+倒计时，可用的卡牌显示正常颜色
   - 如果所有卡牌都在冷却中，允许打开界面查看但不允许选择
   - _需求：2.2, 2.4_

- [ ] 9. 在 GameCharacterManager 中注册玩家角色死亡事件
   - 在 `CreatePlayerCharacter` 中订阅 `CharacterEntity.OnDeath` 事件
   - 死亡时通知 `CharacterDeathManager` 处理冷却逻辑
   - 死亡时从 `PlayerCharacters` 列表中移除该角色（或标记为已阵亡）
   - _需求：1.1, 2.1_

- [ ] 10. 集成测试与边界情况处理
   - 处理当前角色阵亡后场景中无玩家角色的情况（摄像机跟随、敌人目标丢失等）
   - 处理所有角色都在冷却中时的游戏状态
   - 确保角色切换后旧角色的 UI（血条、按钮面板等）被正确清理
   - 确保新角色的 ControlModeManager 正确初始化为 AI 模式
   - _需求：2.4, 3.1, 3.4_
