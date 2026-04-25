# 实施计划：投掷型技能重构 — 预制体方案

- [ ] 1. 重构 `ProjectileController` — 合并飞行与爆炸状态
  - 修改 `Assets/Scripts/Skill/ProjectileController.cs`
  - 新增枚举状态 `Flying` / `Exploding` / `Idle`，替代原来的 `isFlying` 布尔标志
  - 保留现有的抛物线飞行逻辑（`Launch()`、`Update()` 中的插值和旋转计算）
  - 新增爆炸状态逻辑：到达落点后通过 `Animator` 触发爆炸动画（设置 Trigger 参数 `"Explode"`）
  - 新增 AOE 伤害方法：根据 `explosionRadius`、`damage`、`casterType` 对范围内敌方角色造成伤害（从 `ExplosionController.DealAOEDamage()` 迁移逻辑）
  - 新增爆炸动画完成检测：在 `Update()` 中检查 `Animator` 的 `normalizedTime >= 1`，完成后调用 `onFinish` 回调
  - 更新 `ResetState()` 方法：重置状态枚举、Animator 状态、SpriteRenderer 可见性等，确保对象池复用时行为正确
  - 新增 `Launch()` 重载或扩展参数：接收 `explosionRadius`、`damage`、`casterType`、`onFinish` 回调
  - _需求：3.1、3.2、3.3、3.4、3.5、5.1、5.2、5.3_

- [ ] 2. 重构 `ProjectileSkillEffectData` — 改为预制体引用
  - 修改 `Assets/Scripts/Data/ProjectileSkillEffectData.cs`
  - 新增字段 `projectilePrefab`（`GameObject` 类型），替代原来的 `projectileSprite`
  - 保留字段 `projectileFlightDuration`、`projectileArcHeight`、`explosionRadius`
  - 移除旧字段：`projectileSprite`、`explosionAnimatorController`、`projectileScale`、`explosionScale`
  - 移除旧逻辑：`EnsurePrefabsRegistered()` 方法、`PROJECTILE_POOL_KEY` / `EXPLOSION_POOL_KEY` 常量、`projectilePrefabRegistered` / `explosionPrefabRegistered` 静态标志、`ResetStaticFlags()` 编辑器方法
  - 移除 `OnProjectileArrived()` 方法（不再生成独立爆炸 GameObject）
  - 重写 `Execute()` 方法：使用 `projectilePrefab` 通过 `PoolMgr` 注册并获取小球实例，配置 `ProjectileController` 参数后调用 `Launch()`
  - 在 `Execute()` 中实现敌人位移预判：`预测位置 = 敌人当前位置 + 敌人速度向量 × 飞行时间`
  - 在 `Launch()` 的 `onFinish` 回调中通过 `PoolMgr.Instance.PutNode()` 回收小球
  - _需求：2.1、2.2、2.4、4.1、4.2、6.2、7.1、7.2、7.3_

- [ ] 3. 删除 `ExplosionController.cs`
  - 删除 `Assets/Scripts/Skill/ExplosionController.cs` 文件
  - 确认项目中无其他文件引用 `ExplosionController`（如有则清理引用）
  - _需求：6.1_

- [ ] 4. 编写 `ProjectileSkillEffectData` 的 Inspector 可视化（自定义 Editor）
  - 在 `Assets/Scripts/Editor/` 目录下创建 `ProjectileSkillEffectDataEditor.cs`
  - 继承 `UnityEditor.Editor`，为 `ProjectileSkillEffectData` 绘制自定义 Inspector
  - 在 Inspector 中显示小球预制体引用字段和爆炸范围半径字段
  - 当 `projectilePrefab` 已赋值时，在 Scene 视图中绘制圆形 Gizmo 表示 `explosionRadius`（使用 `Handles.DrawWireDisc` 或 `OnSceneGUI`）
  - 如果 `projectilePrefab` 为空，在 Inspector 中显示警告提示
  - _需求：2.3、5.4_

- [ ] 5. 创建小球预制体模板
  - 在 `Assets/Resources/Prefabs/Entity/Characters/` 目录下创建 `ProjectileBall.prefab`（通过代码创建编辑器脚本或手动说明）
  - 预制体包含组件：`SpriteRenderer`、`Animator`、`ProjectileController`
  - 编写创建预制体的 Editor 菜单工具脚本（`Assets/Scripts/Editor/CreateProjectilePrefab.cs`），方便用户一键生成预制体
  - 在脚本中设置 `SpriteRenderer` 的 `sortingOrder` 确保小球渲染在角色上方
  - _需求：1.1、1.2、1.3_

- [ ] 6. 验证对象池集成与状态重置
  - 确认 `ProjectileSkillEffectData.Execute()` 中正确调用 `PoolMgr.Instance.SetPrefab()` 注册预制体（仅首次）
  - 确认 `ProjectileController.ResetState()` 正确重置所有状态（位置、旋转、Animator、SpriteRenderer、状态枚举、回调引用）
  - 确认小球从对象池取出后 `SetActive(true)` 并正确初始化，回收时 `SetActive(false)` 并归还池中
  - 确认多次连续释放技能时，对象池复用的小球行为正确（无残留状态）
  - _需求：7.1、7.2、7.3、7.4_
