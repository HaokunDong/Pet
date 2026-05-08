# 实施计划

- [ ] 1. 撤销 ManualController.cs 中的抖动修复代码和调试日志
   - 在 `HandleRightClickInput` 中移除双向范围检查逻辑（`Mathf.Abs(atkDx) < 0.3f` 分支），恢复为统一的单方向范围检查
   - 在 `HandleChaseAndAttack` 中移除重叠时直接进入攻击模式的特殊处理（`Mathf.Abs(dx) < 0.3f` 分支），恢复为标准的范围检查后进入攻击
   - 在 `HandleSustainedAttack` 中移除双向范围检查逻辑（`Mathf.Abs(dx) < 0.3f` 分支），恢复为使用当前朝向的单方向检查
   - 移除 `ManualController.cs` 中所有 `[AttackDebug]` 的 `Debug.Log` 语句
   - _需求：1.1、1.2、1.3、1.6_

- [ ] 2. 撤销 CharacterAnimator.cs 中的调试监控代码
   - 移除 `LateUpdate` 方法中的 flipX 变化监控逻辑（`_flipXTrackingInitialized`、`_lastFlipX` 相关字段和 `[FacingDebug]` 日志）
   - 如果 `LateUpdate` 方法在移除监控后为空，则整个方法移除
   - _需求：1.4_

- [ ] 3. 撤销 CombatSystem.cs 中的调试日志
   - 移除 `CombatSystem.cs` 中所有 `[AttackDebug]` 的 `Debug.Log` 语句
   - _需求：1.5_

- [ ] 4. 实现点击位置偏移核心逻辑
   - 在 `ManualController.cs` 中新增一个方法 `AdjustMoveTargetAwayFromEnemies(Vector2 clickPos)`，用于检测点击位置附近的敌人并计算偏移后的目标位置
   - 使用 `Physics2D.OverlapBoxAll` 或遍历场景中带 "Enemy" 标签的对象，找到碰撞体 X 轴范围覆盖点击位置的敌人
   - 选择距离点击位置最近的敌人作为偏移参考
   - 根据点击位置相对于敌人中心的左右关系，将目标偏移到敌人碰撞体边缘外侧（半宽 + 安全边距）
   - 当点击位置恰好在敌人中心时（水平差异 < 阈值），偏向玩家角色当前所在方向
   - 添加可配置的安全边距常量（如 `OffsetSafetyMargin = 0.15f`）
   - 当敌人没有 Collider2D 时使用默认偏移距离（如 `DefaultOffsetDistance = 0.5f`）
   - _需求：2.1、2.2、2.3、2.4、2.6、3.1、3.2、3.3、4.1、4.2、4.3_

- [ ] 5. 将偏移逻辑集成到右键点击移动流程中
   - 在 `HandleRightClickInput` 方法中"点击空地移动"分支里，在设置 `targetX` 之前调用偏移方法
   - 将偏移后的位置用于 `targetX` 赋值和箭头指示器的生成位置
   - 确保箭头指示器显示在偏移后的实际目标位置
   - _需求：2.5_

- [ ] 6. 实现软推开功能
   - 新增一个方法 `SoftPushEnemyAwayFromTarget(Vector2 targetPos)`，在偏移后检测目标位置是否仍与其他敌人碰撞体重叠
   - 如果重叠，计算推开方向（远离玩家目标位置的方向）和推开距离（使敌人碰撞体边缘不再与目标位置重叠）
   - 使用协程或在 Update 中以插值方式（`Vector3.MoveTowards` 或 `Vector3.Lerp`）平滑推开敌人，而非瞬间跳跃
   - 添加可配置的推开速度参数（如 `SoftPushSpeed = 3f`）
   - _需求：5.2、5.3、5.4_

- [ ] 7. 软推开边界限制
   - 在软推开过程中检查被推开的敌人是否会超出场景可移动范围
   - 如果有边界限制系统，将推开后的位置 clamp 到有效范围内
   - 如果没有边界限制系统，暂时跳过此步骤（添加 TODO 注释）
   - _需求：3.4、5.5_

- [ ] 8. 多敌人场景下的偏移优先级处理
   - 在偏移方法中，当多个敌人的碰撞体 X 轴范围都覆盖点击位置时，选择距离点击位置最近的敌人作为主要偏移参考
   - 偏移完成后，对偏移后的目标位置再次检测是否与其他敌人重叠，如果重叠则触发软推开
   - _需求：5.1、5.2_
