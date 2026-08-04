# 需求文档

## 引言

本功能为 TrainView 面板中各 Property 节点下的 Text 组件实现属性数值的填充与动态更新。每个属性的文本格式为 `"属性名：X（A+B+C）"`，其中 X 为属性总和，A 为基础属性值（读取 CharacterData），B 为成长属性值（每升一级提升基础值的 1%），C 为天赋点属性值（每加一级天赋点提升 1 点数值）。同时实现 TalentSystem 面板中 PlusButton（加天赋点）、MinusButton（减天赋点）和 SquareFrame（确认按钮）的交互逻辑。

### 天赋点机制
- 角色初始天赋点为 0
- 每升一级获得 1 个天赋点
- 天赋点可以自由分配到各个 Property 属性上
- SquareFrame 中的文本默认显示为 0，且数值始终 ≥ 0

### 相关资源
- **TrainView 预制体路径**：`Resources/Prefabs/UI/View/TrainView`
- **TrainViewController 脚本**：`Assets/Scripts/CardSelection/TrainViewController.cs`
- **CharacterData 脚本**：`Assets/Scripts/Data/CharacterData.cs`
- **CultivationData 脚本**：`Assets/Scripts/Cultivation/CultivationData.cs`
- **CultivationConfig 脚本**：`Assets/Scripts/Data/CultivationConfig.cs`
- **CharacterCard 脚本**：`Assets/Scripts/CardSelection/CharacterCard.cs`

### 属性映射关系
| Property 节点 | 中文名 | CharacterData 字段 | CultivationData 天赋等级字段 |
|---|---|---|---|
| Attack | 攻击力 | attackPower | attackLevel |
| Defense | 防御力 | defense | defenseLevel |
| Health | 生命值 | maxHealth | healthLevel |
| Agility | 敏捷 | moveSpeed | moveSpeedLevel |
| AttackSpeed | 攻速 | attackSpeed | attackSpeedLevel |
| CD | 冷却缩减 | 无基础值（纯百分比属性） | skillCDLevel |

### 公式说明

#### 通用属性公式（Attack、Defense、Health、Agility、AttackSpeed）
- **X（总和）** = A + B + C
- **A（基础属性）** = CharacterData 中对应字段的值
- **B（成长属性）** = A × (level - 1) × 1%，即每升一级提升基础值的 1%
- **C（天赋点属性）** = 对应天赋等级 × 1，即每加一级天赋点提升 1 点数值

#### CD 属性特殊公式
- 显示格式为 `"冷却缩减：X（B+C）"`，无基础属性 A
- **X（总冷却缩减）** = B + C，以百分比显示
- **B（成长属性）** = (level - 1) × 1%，即每升一级加 1% 冷却缩减
- **C（天赋点属性）** = skillCDLevel × 1%，即每加一级天赋点加 1% 冷却缩减
- X、B、C 均显示为百分比格式（如 `"冷却缩减：5%（3%+2%）"`）
- **最终结算效果**：对应角色的所有技能 CD 减少 X%

## 需求

### 需求 1：Property 文本填充显示

**用户故事：** 作为一名玩家，我希望在 TrainView 面板中看到每个属性的详细数值分解，以便我清楚了解角色属性的构成来源。

#### 验收标准

1. WHEN TrainView 面板被实例化并显示 THEN 系统 SHALL 在每个 Property 节点下的 Text 组件中显示格式为 `"属性名：X（A+B+C）"` 的文本
2. WHEN 显示 Attack 属性文本 THEN 系统 SHALL 计算 X = attackPower + attackPower × (level-1) × 0.01 + attackLevel × 1，并显示为 `"攻击力：X（A+B+C）"`
3. WHEN 显示 Defense 属性文本 THEN 系统 SHALL 计算 X = defense + defense × (level-1) × 0.01 + defenseLevel × 1，并显示为 `"防御力：X（A+B+C）"`
4. WHEN 显示 Health 属性文本 THEN 系统 SHALL 计算 X = maxHealth + maxHealth × (level-1) × 0.01 + healthLevel × 1，并显示为 `"生命值：X（A+B+C）"`
5. WHEN 显示 Agility 属性文本 THEN 系统 SHALL 计算 X = moveSpeed + moveSpeed × (level-1) × 0.01 + moveSpeedLevel × 1，并显示为 `"敏捷：X（A+B+C）"`
6. WHEN 显示 AttackSpeed 属性文本 THEN 系统 SHALL 计算 X = attackSpeed + attackSpeed × (level-1) × 0.01 + attackSpeedLevel × 1，并显示为 `"攻速：X（A+B+C）"`
7. WHEN 显示 CD 属性文本 THEN 系统 SHALL 计算 B = (level-1) × 1%，C = skillCDLevel × 1%，X = B + C，并显示为 `"冷却缩减：X%（B%+C%）"`（例如 `"冷却缩减：5%（3%+2%）"`）
8. WHEN CD 属性的冷却缩减生效 THEN 系统 SHALL 将角色所有技能的实际 CD 时间减少 X%
9. WHEN 数值包含小数 THEN 系统 SHALL 将数值保留一位小数显示

### 需求 2：TalentSystem PlusButton 加天赋点交互

**用户故事：** 作为一名玩家，我希望点击 TalentSystem 面板中的 PlusButton 按钮来增加对应属性的天赋点，以便我可以预览天赋点分配效果。

#### 验收标准

1. WHEN 用户点击某个 TalentSystem 面板中的 PlusButton THEN 系统 SHALL 将该属性的临时天赋等级加 1（未确认状态）
2. WHEN PlusButton 被点击后 THEN 系统 SHALL 实时更新对应 Property 的 Text 文本，反映新的 C 值和 X 总和（预览效果）
3. IF 玩家没有可用的天赋点 THEN 系统 SHALL 不响应 PlusButton 的点击操作

### 需求 3：TalentSystem MinusButton 减天赋点交互

**用户故事：** 作为一名玩家，我希望点击 TalentSystem 面板中的 MinusButton 按钮来减少对应属性的天赋点，以便我可以撤销错误的天赋点分配并回收天赋点。

#### 验收标准

1. WHEN 用户点击某个 TalentSystem 面板中的 MinusButton THEN 系统 SHALL 将该属性的临时天赋等级减 1（未确认状态）
2. WHEN MinusButton 被点击后 THEN 系统 SHALL 实时更新对应 Property 的 Text 文本，反映新的 C 值和 X 总和（预览效果）
3. WHEN MinusButton 被点击后 THEN 系统 SHALL 将减少的天赋点返还到可用天赋点池中（可用天赋点 +1）
4. IF 该属性的临时天赋等级已经为 0（或等于已确认的天赋等级） THEN 系统 SHALL 不响应 MinusButton 的点击操作（不能减到负数或低于已确认值）

### 需求 4：SquareFrame 确认按钮与文本显示

**用户故事：** 作为一名玩家，我希望在 SquareFrame 中看到当前天赋等级数值，并通过点击 SquareFrame 按钮来确认天赋点的分配，以便我的天赋点修改被正式保存生效。

#### 验收标准

1. WHEN TalentSystem 面板显示时 THEN 系统 SHALL 在 SquareFrame 的文本中显示该属性当前的天赋等级数值（默认为 0）
2. WHEN SquareFrame 中的数值显示 THEN 系统 SHALL 确保数值始终 ≥ 0
3. WHEN 用户点击 PlusButton 或 MinusButton THEN 系统 SHALL 实时更新 SquareFrame 中的文本为临时天赋等级数值（预览）
4. WHEN 用户点击 SquareFrame 按钮 THEN 系统 SHALL 将当前临时天赋等级的变更正式写入 CultivationData
5. WHEN 天赋点确认生效后 THEN 系统 SHALL 从 CultivationData 中扣除已消耗的天赋点数
6. WHEN 天赋点确认生效后 THEN 系统 SHALL 更新 Property 的 Text 文本为最终确认后的数值
7. IF 用户未进行任何 PlusButton 或 MinusButton 操作就点击 SquareFrame THEN 系统 SHALL 不执行任何操作

### 需求 5：数据获取与传递

**用户故事：** 作为一名开发者，我希望 TrainView 能正确获取当前角色的 CharacterData 和 CultivationData，以便属性文本能正确计算和显示。

#### 验收标准

1. WHEN TrainView 被实例化 THEN 系统 SHALL 从当前聚焦的 CharacterCard 获取对应的 CharacterData 引用
2. WHEN TrainView 被实例化 THEN 系统 SHALL 获取或创建该角色对应的 CultivationData 实例（包含 level 和各属性天赋等级）
3. IF CharacterData 为 null THEN 系统 SHALL 输出警告日志并显示默认值（0）

### 需求 6：天赋点获取与管理

**用户故事：** 作为一名玩家，我希望通过升级来获取天赋点，并将天赋点自由分配到各个属性上，以便我可以自定义角色的成长方向。

#### 验收标准

1. WHEN 角色初始创建时 THEN 系统 SHALL 设置天赋点为 0
2. WHEN 角色升级时 THEN 系统 SHALL 每升一级增加 1 个可用天赋点
3. WHEN 玩家拥有可用天赋点 THEN 系统 SHALL 允许将天赋点分配到任意 Property 属性上
4. WHEN 天赋点被分配确认后 THEN 系统 SHALL 从可用天赋点中扣除对应数量

### 需求 7：边界情况处理

**用户故事：** 作为一名玩家，我希望在各种边界情况下系统都能正确处理天赋点操作，以便不会出现异常行为。

#### 验收标准

1. IF 角色等级为 1（初始等级） THEN 系统 SHALL 显示成长属性 B 为 0，且天赋点为 0
2. IF 所有天赋点已分配完毕（talentPoints = 0） THEN 系统 SHALL 禁用所有 PlusButton 的点击响应
3. WHEN 用户关闭 TrainView 面板时存在未确认的天赋点变更 THEN 系统 SHALL 丢弃未确认的临时变更，恢复到确认前的状态
4. WHEN 数值计算结果为浮点数 THEN 系统 SHALL 正确处理浮点精度问题，避免显示异常
5. IF SquareFrame 中的天赋等级数值为 0 THEN 系统 SHALL 不响应 MinusButton 的点击操作（数值不能为负）
