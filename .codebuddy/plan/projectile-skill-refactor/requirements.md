# 需求文档：投掷型技能重构 — 预制体方案

## 引言

本次重构将**撤销**之前投掷型技能中"代码动态创建 GameObject 模板 + 对象池"的实现方案，改为采用**预制体（Prefab）驱动**的方案。

### 重构动机

之前的实现方案存在以下问题：
- 投射物和爆炸效果的 GameObject 是在代码中动态创建的（`new GameObject()` + `AddComponent`），无法在 Unity Editor 中预览和调整
- 爆炸效果是一个独立的 GameObject（`ExplosionController`），与投射物分离，导致对象池管理复杂、Animator 初始化时序问题频发
- 用户无法直观地在 Inspector 中看到小球的外观和爆炸范围

### 新方案概述

- **小球预制体**：在 `Prefabs` 目录下创建一个小球预制体，预制体上挂载一个脚本（`ProjectileController`），该脚本负责控制小球的飞行、爆炸状态切换和动画播放
- **SkillEffectData 挂载预制体**：`ProjectileSkillEffectData` 中不再使用 `projectileSprite` 和 `explosionAnimatorController` 等分散字段，而是直接引用小球预制体
- **小球自身状态切换**：小球到达敌人位置后，不再生成独立的爆炸 GameObject，而是小球自身进入"爆炸状态"，播放爆炸动画并造成范围伤害，动画播放完毕后回收
- **Inspector 可视化**：在 `ProjectileSkillEffectData` 的 Inspector 中显示小球预制体引用和爆炸范围（圆形 Gizmo），方便用户调整

### 现有系统概述

- **SkillEffectData**：抽象基类（ScriptableObject），定义 `Execute(CombatSystem, CharacterEntity, SkillData)` 接口 — **保持不变**
- **SkillData**：已有 `skillEffect` 字段引用 `SkillEffectData` — **保持不变**
- **ProjectileSkillEffectData**：当前使用动态创建 GameObject + 对象池方案 — **需要重构**
- **ProjectileController**：当前仅负责抛物线飞行 — **需要扩展，增加爆炸状态**
- **ExplosionController**：当前作为独立的爆炸效果组件 — **将被移除，功能合并到 ProjectileController**
- **CombatSystem**：已集成 `SkillEffectData` 委托 — **保持不变**
- **对象池 PoolMgr**：已有通用对象池管理器 — **继续使用，但改为注册预制体**

---

## 需求

### 需求 1：创建小球预制体

**用户故事：** 作为一名游戏设计师，我希望在 Prefabs 目录下有一个可编辑的小球预制体，以便我可以在 Unity Editor 中直观地调整小球的外观、动画和组件配置。

#### 验收标准

1. WHEN 项目中需要投掷型技能时 THEN 系统 SHALL 在 `Assets/Resources/Prefabs/Entity/Characters/` 目录下提供一个小球预制体（如 `ProjectileBall.prefab`）
2. WHEN 查看小球预制体时 THEN 预制体 SHALL 包含以下组件：
   - `SpriteRenderer`：用于显示小球的图片
   - `Animator`：用于播放小球的飞行动画和爆炸动画（由用户在 Animator Controller 中配置状态机）
   - `ProjectileController`（MonoBehaviour）：控制小球的飞行和爆炸逻辑
3. WHEN 用户需要为不同角色创建不同外观的小球时 THEN 用户 SHALL 可以复制该预制体并修改 SpriteRenderer 和 Animator Controller 来创建新的小球变体

### 需求 2：重构 ProjectileSkillEffectData — 挂载预制体引用

**用户故事：** 作为一名游戏设计师，我希望在 `ProjectileSkillEffectData` 中直接挂载小球预制体，并能在 Inspector 中看到爆炸范围的可视化，以便我可以直观地配置和调整技能效果。

#### 验收标准

1. WHEN 编辑 `ProjectileSkillEffectData` 时 THEN 系统 SHALL 提供以下可配置字段：
   - **小球预制体**（`projectilePrefab`，类型 `GameObject`）：引用小球预制体，由用户拖拽装配
   - **投射物飞行时间**（`projectileFlightDuration`，float）：小球从发射到落地的总时间（秒），默认 0.6s
   - **抛物线高度**（`projectileArcHeight`，float）：抛物线最高点相对于起点和终点连线的高度，默认 1.5
   - **爆炸伤害范围半径**（`explosionRadius`，float）：落地爆炸后造成伤害的圆形范围半径
2. WHEN 编辑 `ProjectileSkillEffectData` 时 THEN 系统 SHALL **移除**以下旧字段：
   - `projectileSprite`（投射物精灵）— 改为在预制体上直接配置
   - `explosionAnimatorController`（爆炸动画控制器）— 改为在预制体的 Animator 中配置
   - `projectileScale`（投射物大小）— 改为在预制体上直接调整 Transform.scale
   - `explosionScale`（爆炸效果大小）— 不再需要独立的爆炸对象
3. WHEN 在 Inspector 中选中 `ProjectileSkillEffectData` 资产时 THEN 系统 SHALL 显示小球预制体的引用和爆炸范围半径字段，方便用户调整
4. WHEN `ProjectileSkillEffectData.Execute()` 被调用时 THEN 系统 SHALL 使用挂载的 `projectilePrefab` 通过对象池生成小球实例（而非动态创建 GameObject）

### 需求 3：重构 ProjectileController — 合并飞行与爆炸状态

**用户故事：** 作为一名开发者，我希望 `ProjectileController` 同时管理小球的飞行和爆炸两个状态，以便简化对象管理，避免生成独立的爆炸 GameObject 带来的时序问题。

#### 验收标准

1. WHEN 小球被生成并发射后 THEN `ProjectileController` SHALL 进入**飞行状态**：
   - 沿抛物线轨迹从起点飞向预测落点
   - 水平方向线性插值，垂直方向叠加抛物线弧度（`height = arcHeight × 4 × t × (1 - t)`）
   - 投射物旋转朝向运动方向（切线方向）
2. WHEN 小球到达落点时 THEN `ProjectileController` SHALL 进入**爆炸状态**：
   - 停止飞行运动
   - 通过 Animator 触发爆炸动画（例如设置 Trigger 参数 "Explode"，或切换到爆炸动画状态）
   - 对落点周围 `explosionRadius` 范围内的所有敌方角色造成伤害
3. WHEN 爆炸动画播放完毕后 THEN `ProjectileController` SHALL 通知外部（通过回调），由外部将小球回收到对象池
4. WHEN 小球处于飞行状态时 THEN 系统 SHALL 显示小球的飞行外观（SpriteRenderer 显示小球图片）
5. IF 敌人在小球飞行期间已死亡或被回收 THEN 系统 SHALL 让小球继续飞向原预测落点，到达后仍然进入爆炸状态并造成范围伤害

### 需求 4：敌人位移预判

**用户故事：** 作为一名玩家，我希望投掷的小球能准确命中正在移动的敌人，以便技能不会因敌人移动而落空。

#### 验收标准

1. WHEN 小球生成时 THEN 系统 SHALL 获取目标敌人当前的移动速度和方向，使用公式 `预测位置 = 敌人当前位置 + 敌人速度向量 × 飞行时间` 计算预测落点
2. IF 敌人在小球飞行期间已死亡或被回收 THEN 系统 SHALL 让小球继续飞向原预测落点（不中途消失）

### 需求 5：爆炸范围伤害

**用户故事：** 作为一名玩家，我希望小球落地爆炸后对范围内的敌人造成伤害，以便感受到技能的冲击力。

#### 验收标准

1. WHEN 小球进入爆炸状态时 THEN 系统 SHALL 对落点周围 `explosionRadius`（圆形）范围内的所有敌方角色造成 `SkillData.damage` 的伤害
2. WHEN 范围伤害判定时 THEN 系统 SHALL 根据施法者的 `characterType` 确定敌方标签（Player 攻击 Enemy，Enemy/Boss 攻击 Player），只对敌方造成伤害
3. IF 爆炸范围内没有任何敌方角色 THEN 系统 SHALL 仍然播放爆炸动画（视觉反馈），但不造成伤害
4. WHEN 爆炸范围在 `ProjectileSkillEffectData` 中被修改时 THEN 系统 SHALL 在 Inspector 中通过自定义 Editor 或 Gizmo 显示圆形范围可视化，方便用户调整

### 需求 6：移除 ExplosionController 及旧的动态创建逻辑

**用户故事：** 作为一名开发者，我希望移除不再需要的 `ExplosionController` 组件和动态创建 GameObject 的逻辑，以便保持代码整洁。

#### 验收标准

1. WHEN 重构完成后 THEN 系统 SHALL 删除 `ExplosionController.cs` 文件
2. WHEN 重构完成后 THEN `ProjectileSkillEffectData` SHALL 移除以下旧逻辑：
   - `EnsurePrefabsRegistered()` 方法（动态创建 GameObject 模板）
   - `PROJECTILE_POOL_KEY` / `EXPLOSION_POOL_KEY` 常量（改为使用预制体名称作为池键）
   - `projectilePrefabRegistered` / `explosionPrefabRegistered` 静态标志
   - `ResetStaticFlags()` 编辑器方法
   - `OnProjectileArrived()` 中生成独立爆炸 GameObject 的逻辑
3. WHEN 重构完成后 THEN 系统 SHALL 保持 `SkillEffectData` 基类、`SkillData`、`CombatSystem` 集成逻辑不变

### 需求 7：对象池集成 — 使用预制体

**用户故事：** 作为一名开发者，我希望小球预制体通过对象池管理，以便减少频繁实例化和销毁的性能开销。

#### 验收标准

1. WHEN `ProjectileSkillEffectData.Execute()` 首次被调用时 THEN 系统 SHALL 将 `projectilePrefab` 注册到 `PoolMgr`（如果尚未注册）
2. WHEN 需要生成小球时 THEN 系统 SHALL 通过 `PoolMgr.Instance.GetNode()` 从对象池获取小球实例
3. WHEN 小球爆炸动画播放完毕后 THEN 系统 SHALL 通过 `PoolMgr.Instance.PutNode()` 将小球归还到对象池
4. WHEN 小球从对象池取出复用时 THEN `ProjectileController` SHALL 正确重置所有状态（位置、旋转、动画状态、飞行/爆炸标志等），确保复用时行为正确
