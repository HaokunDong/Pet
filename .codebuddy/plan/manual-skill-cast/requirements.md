# 需求文档：手动模式技能主动释放与技能栏UI

## 引言
本功能为"手动控制模式"下的玩家角色新增主动释放技能的能力。
玩家进入手动模式后，屏幕上会显示一个**技能栏UI预制体**，按角色当前技能数量动态展示对应数量的技能图标（最多4个）。
玩家可通过键盘 **Q / W / E / R** 按键分别触发技能 1 / 2 / 3 / 4。
技能释放后，对应图标进入置灰状态并显示倒计时（与 `SkillData.cooldown` 一致），冷却结束后图标自动恢复并可再次释放。

涉及的关键现有系统：
- `SkillData`：提供 `icon`、`cooldown` 字段
- `CharacterData.skills` + `GetMaxSkillCount()`：决定该角色实际拥有的技能数量
- `ControlModeManager`：判断当前是否处于 `Manual` 模式，并提供模式切换事件
- `CombatSystem.TryUseSkill(int, CharacterEntity)`：执行技能释放（含冷却、范围、动画等检查）
- `RuntimeCharacterStats.IsSkillReady` / `skillCooldowns`：技能冷却的运行时状态
- `ManualController`：手动模式行为控制器（鼠标右键移动 / 攻击）

## 需求

### 需求 1：技能栏 UI 预制体

**用户故事：** 作为一名玩家，我希望在手动控制模式下能够看到当前角色的技能图标列表，以便我清晰地知道自己拥有哪些技能可用。

#### 验收标准
1. WHEN 项目中尚未存在技能栏 UI 预制体时 THEN 开发者 SHALL 创建一个名为 `ManualSkillBar.prefab` 的 UI 预制体，存放在 `Assets/Resources/Prefabs/UI/` 目录下
2. WHEN 该预制体被实例化 THEN 该预制体 SHALL 包含 **4 个固定的技能槽位**（Slot1 / Slot2 / Slot3 / Slot4），每个槽位至少包含：技能图标 `Image`、置灰遮罩 `Image`（用于冷却时径向填充或半透明灰色覆盖）、CD 倒计时文本 `Text`
4. WHEN 该预制体被加载到场景中 THEN 该预制体 SHALL 锚定在屏幕底部居中位置（具体位置由 RectTransform 决定，可在编辑器中调整）
5. WHEN 该预制体被构建后 THEN 该预制体 SHALL 挂载一个新建的 `ManualSkillBarUI` 脚本，用于驱动整体逻辑

### 需求 2：技能图标动态显示

**用户故事：** 作为一名玩家，我希望技能栏只显示当前角色实际拥有的技能数量，并使用每个技能配置的图标，以便我直观地辨识每一个技能。

#### 验收标准
1. WHEN 玩家进入手动控制模式 THEN 系统 SHALL 读取当前被控制角色的 `CharacterData.skills` 与 `GetMaxSkillCount()`
2. WHEN 技能数量为 N（1 ≤ N ≤ 4）THEN 系统 SHALL 仅激活前 N 个技能槽位（`SetActive(true)`），其余槽位隐藏（`SetActive(false)`）
3. WHEN 某个槽位被激活 THEN 该槽位的图标 SHALL 显示为对应 `SkillData.icon` 所指向的 Sprite
4. IF 某个 `SkillData` 的 `icon` 为空 THEN 该槽位 SHALL 显示一个默认占位图标或保持空白（Sprite=null），不应抛出异常
5. WHEN 玩家切换控制的角色（即手动模式切换到另一个角色）THEN 技能栏 SHALL 立即刷新，重新按新角色的技能列表显示

### 需求 3：键盘按键触发技能释放

**用户故事：** 作为一名玩家，我希望通过 Q / W / E / R 按键分别释放技能 1 / 2 / 3 / 4，以便我能够主动控制技能时机。

#### 验收标准
1. WHEN 当前角色处于 `ControlMode.Manual` 模式 AND 玩家按下 `Q` 键 THEN 系统 SHALL 尝试释放索引为 0 的技能
2. WHEN 当前角色处于 `Manual` 模式 AND 玩家按下 `W` 键 THEN 系统 SHALL 尝试释放索引为 1 的技能
3. WHEN 当前角色处于 `Manual` 模式 AND 玩家按下 `E` 键 THEN 系统 SHALL 尝试释放索引为 2 的技能
4. WHEN 当前角色处于 `Manual` 模式 AND 玩家按下 `R` 键 THEN 系统 SHALL 尝试释放索引为 3 的技能
5. IF 当前角色处于 `AI_Auto` 或 `Paused_ShowingButton` 模式 THEN Q/W/E/R 按键 SHALL 不触发任何技能释放
6. IF 按下的技能索引 ≥ 当前角色 `GetMaxSkillCount()` THEN 系统 SHALL 忽略该输入，不报错
7. WHEN 玩家按下技能键 AND 该技能仍在冷却中 THEN 系统 SHALL 不触发释放（可选：播放无效提示音或图标抖动，本期不要求）
8. WHEN 玩家按下技能键 AND 角色当前正处于其他技能或攻击动画中（`CharAnimator.IsInSkillState` 为 true）THEN 系统 SHALL 不触发释放
9. WHEN 释放条件均满足 THEN 系统 SHALL 调用 `CombatSystem.TryUseSkill(skillIndex, target)` 进行释放
10. IF 释放需要目标 AND 当前没有指定目标 THEN 系统 SHALL 自动选择当前最近的存活敌人作为目标（参考 `ManualController` 中已有的目标查找逻辑）
11. IF 在范围内找不到任何存活敌人 THEN 系统 SHALL 不触发释放（与现有 `TryUseSkill` 范围检查一致）

### 需求 4：冷却置灰与倒计时显示

**用户故事：** 作为一名玩家，我希望技能释放后图标变灰并显示剩余冷却秒数，以便我清晰地知道还要等多久才能再次使用。

#### 验收标准
1. WHEN 技能成功被释放（`TryUseSkill` 返回 true）THEN 对应槽位 SHALL 进入"冷却显示"状态
2. WHEN 槽位处于冷却显示状态 THEN 图标 SHALL 通过置灰遮罩呈现灰色/半透明效果（建议使用径向 Filled 的覆盖层或将 `Image.color` 调暗）
3. WHEN 槽位处于冷却显示状态 THEN CD 文本 SHALL 显示剩余冷却时间（向上取整为整数秒，例如剩余 4.3s 显示 "5"，或保留 1 位小数如 "4.3"，由实现决定，本需求要求至少显示整数秒）
4. WHEN 剩余冷却时间归零（`RuntimeStats.IsSkillReady(i)` 返回 true）THEN 该槽位 SHALL 退出冷却显示状态：移除置灰、隐藏倒计时文本，恢复可点亮状态
5. WHEN 玩家切换被控角色 OR 退出手动模式 THEN 技能栏 UI 当前的冷却显示状态 SHALL 重新基于新角色的 `RuntimeStats.skillCooldowns` 即时刷新（不残留旧角色数据）
6. WHEN 倒计时数据来源 THEN 系统 SHALL 直接读取 `RuntimeStats.skillCooldowns[i]` 的剩余值，避免另行维护一份计时器导致与实际冷却不同步

### 需求 5：手动模式生命周期与 UI 显隐

**用户故事：** 作为一名玩家，我希望技能栏只在手动模式下出现，AI 模式下不显示，以保持界面整洁。

#### 验收标准
1. WHEN 任意一个玩家角色进入 `Manual` 模式 THEN 技能栏 UI SHALL 显示，并绑定到该角色
2. WHEN 当前手动控制的角色退出 `Manual` 模式（切回 `AI_Auto` 或 `Paused_ShowingButton`）THEN 技能栏 UI SHALL 隐藏
3. WHEN 手动控制的角色阵亡 THEN 技能栏 UI SHALL 隐藏
4. WHEN 同一时刻只允许一个角色处于 `Manual` 模式（项目当前规则）THEN 技能栏 UI 同一时刻 SHALL 只绑定一个角色
5. WHEN 场景中尚未存在技能栏实例 AND 第一次有角色进入手动模式 THEN 系统 SHALL 通过 `Resources.Load` 加载预制体并实例化到场景的 UI Canvas 下（若不存在 Canvas 则自动创建或复用现有 Canvas）

## 设计约束与边界
- **不修改** 现有 `SkillData`、`CharacterData`、`CombatSystem`、`RuntimeCharacterStats` 的字段定义
- 新增脚本应放置在 `Assets/Scripts/UI/ManualSkillBarUI.cs`（或类似路径）
- 键盘输入使用 Unity 的 `Input.GetKeyDown(KeyCode.Q/W/E/R)` 即可（沿用项目已有 `Input` API 风格）
- 不在本期实现：技能键的可重映射、鼠标点击图标释放、技能提示 Tooltip、技能升级显示

