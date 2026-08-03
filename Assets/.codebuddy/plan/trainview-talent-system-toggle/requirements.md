# 需求文档

## 引言

在 TrainView 界面中，存在六个 Property（属性）节点：Attack、Defense、Health、Agility、AttackSpeed、CD。每个 Property 节点下方都有一个对应的 TalentSystem 子组件（分别为 AttackTalentSystem、DefenseTalentSystem、HealthTalentSystem、AgilityTalentSystem、AttackSpeedTalentSystem、CDTalentSystem）。

本功能需要实现：所有 TalentSystem 组件默认隐藏（SetActive(false)），当用户点击某个 Property 时，显示该 Property 对应的 TalentSystem 组件，同时隐藏上一个已打开的 TalentSystem 组件，确保同一时间只有一个 TalentSystem 处于激活状态。

## 需求

### 需求 1：TalentSystem 默认隐藏

**用户故事：** 作为一名玩家，我希望 TrainView 打开时所有 TalentSystem 面板默认隐藏，以便界面保持简洁，不会一次性展示过多信息。

#### 验收标准

1. WHEN TrainView 被实例化并初始化 THEN 系统 SHALL 将所有六个 TalentSystem 组件（AttackTalentSystem、DefenseTalentSystem、HealthTalentSystem、AgilityTalentSystem、AttackSpeedTalentSystem、CDTalentSystem）设置为 SetActive(false)。
2. IF 没有任何 Property 被点击过 THEN 系统 SHALL 保持所有 TalentSystem 组件处于隐藏状态。

### 需求 2：点击 Property 显示对应 TalentSystem

**用户故事：** 作为一名玩家，我希望点击某个属性（Property）时能看到该属性对应的天赋系统面板，以便我可以查看和操作该属性的天赋升级。

#### 验收标准

1. WHEN 用户点击 Attack Property THEN 系统 SHALL 将 AttackTalentSystem 设置为 SetActive(true)。
2. WHEN 用户点击 Defense Property THEN 系统 SHALL 将 DefenseTalentSystem 设置为 SetActive(true)。
3. WHEN 用户点击 Health Property THEN 系统 SHALL 将 HealthTalentSystem 设置为 SetActive(true)。
4. WHEN 用户点击 Agility Property THEN 系统 SHALL 将 AgilityTalentSystem 设置为 SetActive(true)。
5. WHEN 用户点击 AttackSpeed Property THEN 系统 SHALL 将 AttackSpeedTalentSystem 设置为 SetActive(true)。
6. WHEN 用户点击 CD Property THEN 系统 SHALL 将 CDTalentSystem 设置为 SetActive(true)。

### 需求 3：切换 Property 时隐藏上一个 TalentSystem

**用户故事：** 作为一名玩家，我希望切换到另一个属性时，之前打开的天赋系统面板自动关闭，以便界面不会同时显示多个面板造成混乱。

#### 验收标准

1. WHEN 用户点击一个新的 Property AND 当前已有一个 TalentSystem 处于激活状态 THEN 系统 SHALL 先将当前激活的 TalentSystem 设置为 SetActive(false)，再将新点击的 Property 对应的 TalentSystem 设置为 SetActive(true)。
2. WHEN 用户点击当前已激活的同一个 Property THEN 系统 SHALL 保持该 TalentSystem 的激活状态不变（不做关闭操作）。
3. IF 系统在任何时刻 THEN 系统 SHALL 确保最多只有一个 TalentSystem 组件处于 SetActive(true) 状态。

### 需求 4：Property 与 TalentSystem 的映射关系

**用户故事：** 作为一名开发者，我希望 Property 与 TalentSystem 之间有清晰的一一对应关系，以便代码维护和扩展。

#### 验收标准

1. WHEN 系统初始化 THEN 系统 SHALL 建立以下映射关系：
   - Attack → AttackTalentSystem
   - Defense → DefenseTalentSystem
   - Health → HealthTalentSystem
   - Agility → AgilityTalentSystem
   - AttackSpeed → AttackSpeedTalentSystem
   - CD → CDTalentSystem
2. IF 某个 Property 节点下找不到对应的 TalentSystem 子物体 THEN 系统 SHALL 输出警告日志但不影响其他 Property 的正常功能。

## 技术约束

- TrainView 是通过 `Resources.Load` 动态加载的 prefab，控制脚本需要挂载在 TrainView prefab 的根节点或通过代码动态添加。
- 每个 TalentSystem 是对应 Property 节点的子物体（child），可通过 Transform.Find 或 SerializeField 引用获取。
- 当前项目中没有独立的 TrainView 控制脚本，需要新建一个脚本来管理此逻辑。
- Property 节点上已有 Button 组件，可以直接监听 onClick 事件。
