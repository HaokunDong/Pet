# 实施计划

- [ ] 1. 在 `CharacterData` 中添加 `attackDistance` 字段并移除 `attackRangeShapes`
   - 在 `CharacterData.cs` 中删除 `attackRangeShapes` 字段（含 Header 和 Tooltip）
   - 添加新字段 `[Min(0.1f)] public float attackDistance = 1.0f;`，放在 `minAttackDistance` 之后
   - 移除 `defaultFacesRight` 字段（不再需要形状镜像）
   - _需求：1.2、5.1_

- [ ] 2. 在 `MeleeSkillEffectData` 中添加 `skillAttackDistance` 字段并移除 `skillRangeShapes`
   - 在 `MeleeSkillEffectData.cs` 中删除 `skillRangeShapes` 字段和 `defaultFacesRight` 字段
   - 添加新字段 `[Min(0.1f)] public float skillAttackDistance = 1.5f;`
   - _需求：2.2、5.2_

- [ ] 3. 重写 `RuntimeCharacterStats.IsTargetInAttackRange` 为基于距离判定
   - 修改 `IsTargetInAttackRange` 方法签名，简化为接收 `ownerPos`、`facingSign`、`targetPos`、`attackDist` 参数
   - 实现逻辑：计算目标相对于自身在X轴上的有符号距离，判断目标是否在面朝方向的 `attackDistance` 范围内（忽略Y轴）
   - 移除对 `AttackRangeHelper.IsTargetInRange` 的调用
   - 移除 `attackRangeShapes` 字段引用，改为存储 `attackDistance`
   - 重写 `GetMaxAttackDistance()` 直接返回 `attackDistance`
   - 重写 `GetEngageDistance()` 返回 `attackDistance * 0.9f`
   - _需求：1.1、1.3、3.2_

- [ ] 4. 新增基于碰撞体边缘的距离计算工具方法
   - 在 `RuntimeCharacterStats` 或新建一个工具类中添加静态方法 `GetDistanceToColliderEdge(Vector2 ownerPos, Collider2D targetCollider)`
   - 实现逻辑：使用 `targetCollider.bounds` 在X轴上的最近边缘点（`bounds.min.x` 或 `bounds.max.x`）计算与 `ownerPos.x` 的水平距离
   - 该方法供 AI 系统（BTCombat）使用
   - _需求：3.1、3.5_

- [ ] 5. 修改 `BTCombat` 中的攻击距离判定逻辑
   - 将 `Execute()` 中的 `inAttackRange` 计算改为：使用 `owner.Transform.position.x` 到 `target` 碰撞体X轴最近边缘的水平距离，与 `owner.RuntimeStats.attackDistance` 比较
   - 将 `TickEngage()` 中的 `inRange` 计算改为同样的碰撞体边缘距离判定
   - 将 `HandleAIComboBuffer()` 中的 `inRange` 计算改为碰撞体边缘距离判定
   - 移除所有对 `IsTargetInAttackRange(ColliderCenter, facingSign, ColliderCenter, ColliderHalfExtentX)` 的调用
   - 移除 `ColliderCenter` 的使用，改为 `transform.position` + 碰撞体边缘
   - 保留面朝方向判定（仅攻击前方的敌人）
   - _需求：3.1、3.3、3.4_

- [ ] 6. 修改 `CombatSystem.TryNormalAttack` 为基于距离判定
   - 将攻击范围检查改为：计算 `entity.transform.position.x` 与 `target.transform.position.x` 的水平距离，与 `entity.RuntimeStats.attackDistance` 比较
   - 保留面朝方向检查（仅攻击面朝方向前方的目标）
   - 移除对 `IsTargetInAttackRange` 形状检测的调用
   - 简化重叠情况的处理（不再需要双方向检查）
   - _需求：6.1_

- [ ] 7. 修改 `CombatSystem.ApplyNormalAttackDamage` 为基于距离判定
   - 将伤害判定改为：使用缓存的攻击位置 `_cachedAttackPosition.x` 与目标 `transform.position.x` 的水平距离，与 `entity.RuntimeStats.attackDistance` 比较
   - 保留面朝方向检查（仅对面朝方向前方的敌人造成伤害）
   - 修改 `ApplyNormalAttackLockOnDisplacement` 中的伤害检测，改为距离判定
   - 修改 `ApplyNormalAttackDisplacement`（Fixed type）中的连续伤害检测，改为距离判定
   - _需求：6.2、6.3_

- [ ] 8. 修改 `MeleeSkillEffectData.Execute` 为基于距离判定
   - 将所有 `AttackRangeHelper.IsTargetInRange(casterPos, effectiveFacingSign, skillRangeShapes, candidatePos)` 调用替换为基于 `skillAttackDistance` 的水平距离判定
   - 修改 `ApplyDisplacementWithDamage` 调用，传入 `skillAttackDistance` 而非 `skillRangeShapes`
   - 修改 LockOn 位移中的伤害检测逻辑
   - _需求：2.1、2.3_

- [ ] 9. 修改 `SkillDisplacementController` 的连续伤害检测为基于距离判定
   - 将 `damageShapes` 字段替换为 `damageDistance`（float）
   - 修改 `StartDisplacementWithDamage` 和 `StartLockOnDisplacementWithDamage` 方法签名，接收 `float attackDistance` 而非 `AttackRangeShape[]`
   - 修改 `DetectDamageDuringDisplacement()` 方法，使用水平距离判定替代形状检测
   - 同步修改 `SkillEffectData.ApplyDisplacementWithDamage` 和 `ApplyLockOnDisplacementWithDamage` 的参数
   - 同步修改 `CombatSystem` 中调用这些方法的代码
   - _需求：2.3、5.2_

- [ ] 10. 实现攻击距离的场景 Gizmos 可视化绘制
   - 修改 `CharacterEntity.DrawAttackRangeGizmos()`：绘制从角色位置向面朝方向延伸的水平线段（带一定高度的矩形区域），长度为 `attackDistance`
   - 修改 `CharacterEntity.DrawSkillRangeGizmos()`：对 `MeleeSkillEffectData` 绘制 `skillAttackDistance` 的距离线段（使用不同颜色）
   - 保留 `minAttackDistance` 的绿色圆圈绘制
   - 选中时使用较亮颜色，未选中时使用较淡颜色
   - _需求：4.1、4.2、4.3、4.4、4.5_

- [ ] 11. 清理 `AttackRangeHelper` 和 `AttackRangeShape` 的引用
   - 检查 `ProjectileDamageArea` 是否仍使用 `AttackRangeHelper` 和 `AttackRangeShape`（是的，投射物仍需要）
   - 保留 `AttackRangeHelper.cs` 和 `AttackRangeShape.cs` 供投射物系统使用
   - 移除 `RuntimeCharacterStats` 中的 `attackRangeShapes` 字段
   - 确认所有普通攻击和近战技能的代码路径不再引用旧的形状系统
   - _需求：5.3、5.4、5.5_
