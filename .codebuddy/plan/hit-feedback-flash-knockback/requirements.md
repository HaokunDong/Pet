# 需求文档：受击反馈系统（闪白 + 击退）

## 引言

本功能旨在提升游戏的战斗手感，为角色（包括玩家和敌人）在被击中时添加两种视觉与物理反馈效果：

1. **闪白效果**：角色被击中瞬间，精灵图整体闪白，提供清晰的受击视觉反馈，增强打击感。
2. **击退效果**：角色被击中时，施加一个向后（远离攻击者）且向上的瞬时速度，模拟击退/击飞的物理效果，使战斗更具冲击力。

### 项目现状

- 已存在 `CharacterFlash.shader`（支持 `_FlashAmount` / `_FlashColor` 属性）
- 已存在 `FlashEffect.cs` 脚本（基于协程的闪白控制器，使用 `MaterialPropertyBlock`）
- 已存在 `CharacterFlash_Mat.mat` 材质
- `TakeDamage()` 中目前只播放 Hit 动画，**未触发闪白效果，也无击退逻辑**
- 角色移动目前通过直接修改 `transform.position` 实现，无物理系统（Rigidbody2D）

---

## 需求

### 需求 1：受击闪白效果

**用户故事：** 作为一名玩家，我希望敌人和我的角色在被击中时能闪白光，以便我能清晰感知到攻击命中，提升打击手感。

#### 验收标准

1. WHEN 角色（玩家或敌人）受到伤害 THEN 系统 SHALL 立即触发该角色精灵图的闪白效果。
2. WHEN 闪白效果触发 THEN 系统 SHALL 将精灵图在约 0.1~0.15 秒内从全白渐变回原色（快速闪烁感）。
3. IF 角色的 SpriteRenderer 当前未使用 `Game/CharacterFlash` shader 的材质 THEN 系统 SHALL 在初始化时自动为其设置正确的材质，确保闪白 shader 生效。
4. IF 角色 GameObject 上未挂载 `FlashEffect` 组件 THEN 系统 SHALL 在初始化阶段自动添加该组件。
5. WHEN 角色在闪白过程中再次受到伤害 THEN 系统 SHALL 重新开始闪白效果（中断当前闪白并重新播放），确保每次受击都有明确的视觉反馈。
6. WHEN 角色死亡 THEN 系统 SHALL 停止闪白效果并重置为正常颜色。

### 需求 2：受击击退效果

**用户故事：** 作为一名玩家，我希望角色被击中时有一个向后且向上的击退位移，以便战斗看起来更有冲击力和真实感。

#### 验收标准

1. WHEN 角色受到伤害 THEN 系统 SHALL 施加一个瞬时速度，方向为远离攻击者（水平向后）且略微向上，模拟击退效果。
2. WHEN 击退速度施加后 THEN 系统 SHALL 在后续帧中对该速度施加重力衰减（向下加速度），使角色呈现抛物线轨迹后回到地面。
3. WHEN 角色处于击退运动中 THEN 系统 SHALL 暂停 AI 行为树的移动控制，防止 AI 移动与击退位移冲突。
4. WHEN 角色的 Y 坐标回到初始地面高度（或更低） THEN 系统 SHALL 结束击退状态，将角色 Y 坐标钳制到地面高度，并恢复 AI 正常控制。
5. IF 击退方向会导致角色超出场景边界 THEN 系统 SHALL 将角色位置钳制在场景可活动范围内。
6. WHEN 击退效果触发 THEN 系统 SHALL 使用可配置的参数控制击退力度，包括：
   - `knockbackHorizontalSpeed`：水平击退速度（默认约 2.0 单位/秒）
   - `knockbackVerticalSpeed`：垂直击退速度（默认约 1.5 单位/秒）
   - `knockbackGravity`：击退过程中的重力加速度（默认约 8.0 单位/秒²）
7. IF 角色在击退过程中再次受到伤害 THEN 系统 SHALL 重置击退速度为新的击退方向和力度（允许连续击退）。
8. WHEN 击退参数需要调整 THEN 设计师 SHALL 能够在 `CharacterData` ScriptableObject 中配置每个角色的击退参数，不同角色可以有不同的击退表现。

### 需求 3：闪白与击退的协同

**用户故事：** 作为一名玩家，我希望闪白和击退效果能同时触发且互不干扰，以便获得完整流畅的受击体验。

#### 验收标准

1. WHEN 角色受到伤害 THEN 系统 SHALL 同时触发闪白效果和击退效果，两者独立运行互不阻塞。
2. WHEN 角色处于击退状态中 THEN 系统 SHALL 仍然允许 Hit 动画正常播放（击退是位移效果，不影响动画状态机）。
3. WHEN 角色处于击退状态中被再次攻击 THEN 系统 SHALL 同时重置闪白和击退效果。

---

## 技术约束与边界情况

- 当前项目不使用 Rigidbody2D 物理系统，击退效果需要通过手动修改 `transform.position` + 自定义速度/重力模拟实现。
- 击退过程中需要与 AI 行为树（`BTCombat` 的 `TickEngage` / `TickStrike`）协调，避免 AI 移动覆盖击退位移。
- `FlashEffect.cs` 已存在但当前不允许中断重播（`IsFlashing` 时返回 false），需要修改以支持需求 1.5 的重新触发。
- 所有角色 Prefab 的 SpriteRenderer 需要确保使用 `CharacterFlash` 材质才能使闪白生效。
