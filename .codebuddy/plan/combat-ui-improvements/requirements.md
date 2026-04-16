# 需求文档：战斗系统修复与UI增强

## 引言
本次改动包含两大类工作：**战斗系统Bug修复**和**调试/游戏UI增强**。

Bug修复部分解决两个问题：(1) 角色受伤后一直卡在Hit动画状态无法恢复；(2) 攻击命中敌人时敌人没有播放Hit动画。

UI增强部分新增两个功能：(1) 在Scene视图中可视化显示每个角色的攻击范围，方便开发者调整数值；(2) 在所有角色头顶显示生命值血条，方便实时观察战斗中双方的生命值变化。

---

## 需求

### 需求 1：修复Hit动画循环问题

**用户故事：** 作为一名玩家，我希望角色只在受到伤害的瞬间播放Hit动画并自动恢复，以便战斗动画表现自然流畅

#### 验收标准
1. WHEN 角色受到伤害 THEN CharacterAnimator SHALL 播放Hit动画一次（非循环）
2. WHEN Hit动画播放完毕 THEN 角色 SHALL 自动回到Idle状态
3. WHEN 角色在Hit动画播放过程中再次受到伤害 THEN 系统 SHALL 重新从头播放Hit动画
4. IF Hit动画状态在Animator Controller中被设置为Loop THEN 开发者 SHALL 将其改为非循环（Loop Time = false）
5. WHEN Hit动画播放期间 THEN 系统 SHALL 不因行为树Tick而打断Hit动画（Hit动画应有短暂的保护期）

### 需求 2：修复敌人不播放Hit动画的问题

**用户故事：** 作为一名玩家，我希望攻击命中敌人时敌人能正确播放Hit动画，以便获得清晰的战斗反馈

#### 验收标准
1. WHEN 玩家的攻击帧事件触发并对敌人造成伤害 THEN 敌人 SHALL 播放Hit动画
2. WHEN CharacterEntity.TakeDamage()被调用 THEN 系统 SHALL 确保目标的CharAnimator引用不为null
3. WHEN 敌人通过对象池生成时 THEN 系统 SHALL 确保AnimEventReceiver组件已正确挂载并初始化
4. IF 攻击动画未配置帧事件 THEN 伤害将不会触发（需确认动画帧事件已正确配置）
5. WHEN 攻击帧事件触发时 THEN CombatSystem SHALL 确保_cachedTarget仍然有效且_isAttacking为true

### 需求 3：攻击范围可视化

**用户故事：** 作为一名游戏开发者，我希望在Scene视图中看到每个角色的攻击范围，以便方便地调整和平衡所有角色的攻击距离

#### 验收标准
1. WHEN 在Unity Editor的Scene视图中查看角色 THEN 系统 SHALL 以线框圆形（Gizmo）显示该角色的攻击范围
2. WHEN 角色的attackRange数值改变 THEN Gizmo SHALL 实时更新显示范围
3. WHEN 角色被选中时 THEN 攻击范围Gizmo SHALL 以醒目颜色显示（如红色半透明圆）
4. WHEN 角色未被选中时 THEN 攻击范围Gizmo SHALL 以较淡颜色显示（如半透明线框）
5. IF 角色未初始化或RuntimeStats为null THEN 系统 SHALL 不绘制Gizmo（避免空引用异常）

### 需求 4：角色头顶生命值血条

**用户故事：** 作为一名玩家/开发者，我希望在所有角色头顶看到生命值血条，以便实时了解战斗中双方的生命值变化

#### 验收标准
1. WHEN 角色被生成并初始化后 THEN 系统 SHALL 在角色头顶上方显示一个生命值血条
2. WHEN 角色受到伤害 THEN 血条 SHALL 实时更新，反映当前生命值与最大生命值的比例
3. WHEN 角色生命值为满 THEN 血条 SHALL 显示为全满状态（绿色）
4. WHEN 角色生命值低于50% THEN 血条颜色 SHALL 变为黄色
5. WHEN 角色生命值低于25% THEN 血条颜色 SHALL 变为红色
6. WHEN 角色移动时 THEN 血条 SHALL 跟随角色位置移动，始终保持在头顶上方
7. WHEN 角色死亡或被回收 THEN 血条 SHALL 被销毁或隐藏
8. WHEN 角色的SpriteRenderer发生flipX翻转时 THEN 血条 SHALL 不受翻转影响，始终保持正向显示
9. IF 使用World Space Canvas方式实现 THEN 血条 SHALL 作为角色的子物体，使用世界空间UI渲染
10. IF 使用SpriteRenderer方式实现 THEN 血条 SHALL 使用简单的精灵缩放来表示生命值比例
