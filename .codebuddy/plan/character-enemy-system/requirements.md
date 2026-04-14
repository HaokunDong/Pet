# 需求文档：角色与敌人系统

## 引言

本功能为 2D 横版桌面挂机游戏设计一套完整的**角色与敌人系统**。核心目标是建立一个可复用的角色模板（ScriptableObject），基于该模板可以快速创建玩家角色和敌人。系统涵盖角色属性（攻击力、生命值、防御力、移速、攻速）、品质分级（A/S/SS/SSS）、技能系统（含CD机制）、双操控模式（AI挂机 / 玩家手动操控）、敌人分类（小怪 / Boss）以及玩家数据持久化存储。

项目基于 Unity 引擎，当前已有 Singleton、对象池（PoolMgr）、资源管理（ResMgr）、事件派发（DispatcherBase）、UI 管理（WindowManager/BaseView）等基础框架，新系统需与现有架构兼容。

---

## 需求

### 需求 1：角色数据模板（ScriptableObject）

**用户故事：** 作为一名开发者，我希望有一个可复用的角色数据模板（ScriptableObject），以便后续可以通过该模板快速配置和创建新的角色与敌人，而无需重复编写代码。

#### 验收标准

1. WHEN 开发者在 Unity Editor 中右键创建资源 THEN 系统 SHALL 提供"Create > Game > CharacterData"菜单项，生成一个 CharacterData ScriptableObject 资产。
2. WHEN CharacterData 被创建 THEN 该资产 SHALL 包含以下可配置字段：角色ID（string）、角色名称（string）、攻击力（float）、生命值（float）、防御力（float）、移速（float）、攻速（float）、攻击范围（float）、品质等级（枚举：A/S/SS/SSS）、角色类型（枚举：Player/MinorEnemy/Boss）、技能列表（SkillData 数组）、精灵图资源引用（Sprite）、动画控制器引用（RuntimeAnimatorController）。
3. WHEN 同一个 CharacterData 模板被多个角色实例引用 THEN 系统 SHALL 确保运行时各实例的属性互不影响（通过运行时数据副本实现）。
4. IF 角色类型为 Boss THEN 系统 SHALL 允许该 Boss 数据后续被复用为玩家角色数据，无需额外转换。

---

### 需求 2：品质与技能系统

**用户故事：** 作为一名玩家，我希望角色有不同的品质等级和对应数量的技能，以便体验角色的差异化和成长感。

#### 验收标准

1. WHEN 角色品质为 A 级 THEN 系统 SHALL 限制该角色最多配置 1 个技能。
2. WHEN 角色品质为 S 级 THEN 系统 SHALL 限制该角色最多配置 2 个技能。
3. WHEN 角色品质为 SS 级 THEN 系统 SHALL 限制该角色最多配置 3 个技能。
4. WHEN 角色品质为 SSS 级 THEN 系统 SHALL 限制该角色最多配置 4 个技能。
5. WHEN 开发者在 Editor 中为角色配置的技能数量超过品质允许的上限 THEN 系统 SHALL 在 Inspector 中显示警告提示。
6. WHEN 技能数据（SkillData）被创建 THEN 该数据 SHALL 包含：技能ID（string）、技能名称（string）、冷却时间CD（float，秒）、伤害值（float）、技能范围（float）、技能图标（Sprite）、技能特效预制体引用（GameObject）。

---

### 需求 3：角色运行时实例化与生命周期

**用户故事：** 作为一名开发者，我希望角色在场景中被实例化后拥有独立的运行时状态，以便每个角色实例可以独立地战斗、受伤和死亡。

#### 验收标准

1. WHEN 角色被实例化到场景中 THEN 系统 SHALL 基于 CharacterData 创建一份运行时属性副本（RuntimeCharacterStats），包含当前生命值、技能CD状态等动态数据。
2. WHEN 角色当前生命值降至 0 或以下 THEN 系统 SHALL 触发角色死亡逻辑（播放死亡动画、从场景移除或回收到对象池）。
3. WHEN 角色受到攻击 THEN 系统 SHALL 根据公式 `实际伤害 = 攻击力 - 防御力`（最小为1）计算伤害并扣减当前生命值。
4. WHEN 角色实例被销毁或回收 THEN 系统 SHALL 正确清理所有运行时状态和事件监听。

---

### 需求 4：普通攻击系统

**用户故事：** 作为一名玩家，我希望通过鼠标右键点击敌人来进行普通攻击，以便直观地与敌人战斗。

#### 验收标准

1. WHEN 玩家在手动操控模式下鼠标右键点击一个敌人 AND 该敌人在角色的攻击范围内 THEN 系统 SHALL 立即执行一次普通攻击，对敌人造成角色攻击力对应的伤害。
2. WHEN 玩家鼠标右键点击一个敌人 AND 该敌人不在角色攻击范围内 THEN 系统 SHALL 使角色朝敌人方向移动，直到敌人进入攻击范围后自动执行普通攻击。
3. WHEN 普通攻击被执行 THEN 系统 SHALL 不施加任何冷却时间（普通攻击无CD），但攻击频率受角色攻速属性限制。
4. WHEN 普通攻击命中敌人 THEN 系统 SHALL 播放对应的攻击动画。

---

### 需求 5：技能系统

**用户故事：** 作为一名玩家，我希望角色可以释放技能，以便在战斗中使用更强力的攻击手段。

#### 验收标准

1. WHEN 技能被使用 THEN 系统 SHALL 启动该技能的冷却计时器，在冷却期间该技能不可再次使用。
2. WHEN 技能冷却结束 THEN 系统 SHALL 允许该技能再次被使用。
3. WHEN AI 模式下角色需要使用技能 THEN 系统 SHALL 由行为树自动判断并释放已冷却完毕的技能。
4. WHEN 手动操控模式下玩家触发技能 THEN 系统 SHALL 通过 UI 技能按钮或快捷方式释放技能（具体UI交互后续扩展）。
5. WHEN 技能被释放 THEN 系统 SHALL 播放对应的技能特效和动画。

---

### 需求 6：AI 挂机模式（行为树）

**用户故事：** 作为一名玩家，我希望角色默认以 AI 模式自动挂机打怪，以便我不需要时刻操控角色。

#### 验收标准

1. WHEN 角色被创建并进入场景 THEN 系统 SHALL 默认以 AI 挂机模式运行。
2. WHEN AI 模式激活 THEN 系统 SHALL 使用行为树控制角色执行以下行为循环：巡逻/移动 → 搜索敌人 → 接近敌人 → 攻击敌人（优先使用已冷却的技能，否则使用普通攻击）→ 敌人死亡后继续搜索。
3. WHEN AI 模式下角色没有发现敌人 THEN 系统 SHALL 使角色执行巡逻行为（在一定范围内左右移动）。
4. WHEN AI 模式下角色发现敌人 THEN 系统 SHALL 使角色自动移向最近的敌人并发起攻击。
5. WHEN AI 模式下角色正在攻击 AND 目标敌人死亡 THEN 系统 SHALL 使角色自动切换到下一个最近的敌人或回到巡逻状态。

---

### 需求 7：玩家手动操控模式

**用户故事：** 作为一名玩家，我希望可以手动操控角色移动和攻击，以便在需要时亲自指挥角色的行动。

#### 验收标准

1. WHEN 玩家点击角色后点击"操控"按钮 THEN 系统 SHALL 将该角色从 AI 模式切换为手动操控模式。
2. WHEN 手动操控模式下玩家鼠标右键点击角色右侧区域 THEN 系统 SHALL 使角色向右移动。
3. WHEN 手动操控模式下玩家鼠标右键点击角色左侧区域 THEN 系统 SHALL 使角色向左移动。
4. WHEN 手动操控模式下玩家鼠标右键点击一个敌人 THEN 系统 SHALL 使角色朝敌人方向移动，当敌人进入攻击范围后执行普通攻击。
5. WHEN 手动操控模式激活 THEN 系统 SHALL 在角色附近显示操控相关的 UI 提示（后续可扩展更多按钮）。

---

### 需求 8：操控模式切换交互

**用户故事：** 作为一名玩家，我希望通过鼠标点击角色来切换操控模式，并获得清晰的视觉反馈，以便操作直观且手感良好。

#### 验收标准

##### 8.1 AI 挂机模式下点击角色

1. WHEN 角色处于 AI 挂机模式 AND 玩家鼠标左键点击角色 THEN 系统 SHALL 首先通过 Shader 播放一个短暂的被点击高亮效果（如闪白/描边闪烁），用于提升操作手感。
2. WHEN 点击效果播放的同时 THEN 系统 SHALL 使角色立即停止当前 AI 行为、静止不动，并切换播放 Idle 动画。
3. WHEN 角色停止后 THEN 系统 SHALL 在角色左侧显示一个"操控"按钮。
4. WHEN "操控"按钮显示期间 AND 玩家鼠标左键点击任意非按钮区域 THEN 系统 SHALL 收回（隐藏）"操控"按钮，并使角色恢复 AI 行为树的控制，继续挂机打怪。
5. WHEN 玩家点击"操控"按钮 THEN 系统 SHALL 将角色切换为手动操控模式，隐藏"操控"按钮。

##### 8.2 手动操控模式下点击角色

6. WHEN 角色处于手动操控模式 AND 玩家鼠标左键点击角色 THEN 系统 SHALL 同样通过 Shader 播放短暂的被点击高亮效果，角色停止并切换播放 Idle 动画。
7. WHEN 角色停止后 THEN 系统 SHALL 在角色左侧显示一个"退出操控"按钮。
8. WHEN "退出操控"按钮显示期间 AND 玩家鼠标左键点击任意非按钮区域 THEN 系统 SHALL 收回（隐藏）"退出操控"按钮，角色恢复手动操控模式的待命状态。
9. WHEN 玩家点击"退出操控"按钮 THEN 系统 SHALL 将角色从手动操控模式切换回 AI 挂机模式，隐藏"退出操控"按钮，角色恢复行为树控制继续挂机打怪。

##### 8.3 Shader 点击效果

10. WHEN 角色被点击触发 Shader 高亮效果 THEN 系统 SHALL 使用自定义 Shader 实现短暂的视觉反馈（如闪白、亮度提升或描边闪烁），持续时间约 0.1~0.2 秒，效果结束后自动恢复正常渲染。
11. IF Shader 效果正在播放期间角色再次被点击 THEN 系统 SHALL 忽略重复点击，避免效果叠加或状态异常。

---

### 需求 9：敌人系统

**用户故事：** 作为一名玩家，我希望游戏中有不同类型的敌人（小怪和Boss），以便获得多样化的战斗体验。

#### 验收标准

1. WHEN 小怪被实例化 THEN 系统 SHALL 使用与角色相同的 CharacterData 模板配置其属性，角色类型设为 MinorEnemy。
2. WHEN 小怪进入战斗 THEN 系统 SHALL 使用行为树控制小怪，仅执行普通攻击（无技能）。
3. WHEN Boss 被实例化 THEN 系统 SHALL 使用与角色相同的 CharacterData 模板配置其属性，角色类型设为 Boss，可配置技能。
4. WHEN Boss 进入战斗 THEN 系统 SHALL 使用行为树控制 Boss，可执行普通攻击和技能攻击（与玩家角色的 AI 行为类似）。
5. WHEN 敌人（小怪或Boss）的行为树激活 THEN 系统 SHALL 使敌人自动搜索并攻击最近的玩家角色。
6. IF Boss 的 CharacterData 需要被转化为玩家角色 THEN 系统 SHALL 仅需将角色类型字段从 Boss 改为 Player 即可复用，无需修改其他数据结构。

---

### 需求 10：数据持久化

**用户故事：** 作为一名玩家，我希望我的角色数据（属性、状态等）可以被保存和加载，以便下次打开游戏时能继续之前的进度。

#### 验收标准

1. WHEN 玩家退出游戏或触发保存操作 THEN 系统 SHALL 将所有玩家角色的运行时数据（当前生命值、技能CD状态、位置等）序列化并保存到本地文件（JSON 格式）。
2. WHEN 玩家启动游戏并加载存档 THEN 系统 SHALL 从本地文件反序列化数据，恢复所有玩家角色的状态。
3. IF 存档文件不存在或损坏 THEN 系统 SHALL 使用 CharacterData 模板的默认值初始化角色数据。
4. WHEN 新角色被添加到游戏中 THEN 系统 SHALL 确保存档系统能兼容新增角色数据，不影响已有存档的加载。

---

### 需求 11：角色与敌人的动画系统

**用户故事：** 作为一名玩家，我希望角色和敌人有流畅的动画表现，以便游戏视觉体验良好。

#### 验收标准

1. WHEN 角色或敌人处于静止状态 THEN 系统 SHALL 播放 Idle 动画。
2. WHEN 角色或敌人正在移动 THEN 系统 SHALL 播放 Walk/Run 动画，并根据移动方向翻转精灵图（面朝移动方向）。
3. WHEN 角色或敌人执行普通攻击 THEN 系统 SHALL 播放 Attack 动画。
4. WHEN 角色或敌人释放技能 THEN 系统 SHALL 播放对应的 Skill 动画。
5. WHEN 角色或敌人死亡 THEN 系统 SHALL 播放 Death 动画。
6. WHEN 角色或敌人受到伤害 THEN 系统 SHALL 播放 Hit 受击动画。

---

## 技术方案概述

### 推荐架构

- **数据层**：使用 ScriptableObject（CharacterData / SkillData）作为静态配置模板
- **运行时层**：RuntimeCharacterStats 作为运行时数据副本，挂载在角色 GameObject 上
- **控制层**：CharacterController 组件统一管理角色行为，内部持有 AI 控制器和手动操控控制器，通过状态切换
- **行为树**：使用简易自实现行为树（BehaviorTree），包含 Selector / Sequence / Action 等基础节点，避免引入第三方插件
- **数据持久化**：使用 JSON 序列化 + Unity 的 Application.persistentDataPath 存储

### 与现有架构的兼容

- 角色/敌人预制体通过 `PoolMgr` 进行对象池管理
- 角色相关 UI（操控按钮等）通过现有的 `WindowManager` / `BaseView` 体系管理
- 事件通信使用现有的 `DispatcherBase` / `CommonDispatcher`
