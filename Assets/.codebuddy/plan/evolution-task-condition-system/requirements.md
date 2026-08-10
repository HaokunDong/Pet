# 需求文档

## 引言

本功能为 TrainView 界面中的角色进化系统设计一套任务/条件机制。玩家在满足特定进化条件后，可通过点击 TrainView 中的 EvolutionButton 按钮，将当前角色的进化体作为一个新角色添加到玩家的卡牌列表中。

系统核心包括：
1. **进化关联配置**：在 CharacterData 中新增可配置字段，允许一个角色关联其进化体（另一个 CharacterData），支持多级进化链。
2. **进化条件系统**：可配置的条件列表，支持多种条件类型（等级、材料、击杀数等），每个角色可独立配置不同的进化条件。
3. **条件显示**：在 TrainView 的 RequirmentFrame 区域以文本形式展示当前角色的进化条件及其完成状态。
4. **进化执行**：条件全部满足后，点击 EvolutionButton 将进化体角色通过 `CardSplineDistributor.AddCharacterData()` 添加到玩家卡牌中。

## 需求

### 需求 1：进化体关联配置

**用户故事：** 作为一名游戏设计师，我希望能在 CharacterData 中配置角色的进化体关联关系，以便定义角色之间的进化链。

#### 验收标准

1. WHEN 在 Unity Inspector 中编辑 CharacterData 时 THEN 系统 SHALL 提供一个可选的 CharacterData 引用字段（evolutionTarget），用于指定该角色的进化体。
2. IF evolutionTarget 字段为空 THEN 系统 SHALL 认为该角色没有进化体，EvolutionButton 不可交互。
3. WHEN evolutionTarget 被设置为另一个 CharacterData THEN 系统 SHALL 建立从当前角色到目标角色的进化关系。
4. IF 需要支持多级进化链（A→B→C）THEN 系统 SHALL 允许进化体自身也拥有 evolutionTarget 字段，形成链式关系。

### 需求 2：进化条件数据配置

**用户故事：** 作为一名游戏设计师，我希望能为每个角色配置独立的进化条件列表，以便灵活控制不同角色的进化难度和方式。

#### 验收标准

1. WHEN 在 CharacterData 中配置进化条件时 THEN 系统 SHALL 提供一个可序列化的条件列表（evolutionRequirements），每个条件包含类型、目标值和描述文本。
2. WHEN 条件类型为"等级要求"时 THEN 系统 SHALL 检查角色的 CultivationData.level 是否达到指定等级。
3. WHEN 条件类型为"材料要求"时 THEN 系统 SHALL 检查玩家是否拥有足够数量的指定材料（材料ID + 数量）。
4. WHEN 条件类型为"击杀数要求"时 THEN 系统 SHALL 检查玩家使用该角色的累计击杀数是否达到指定数量。
5. IF 条件列表为空且 evolutionTarget 不为空 THEN 系统 SHALL 默认进化条件已满足（无条件进化）。
6. WHEN 设计新的条件类型时 THEN 系统 SHALL 提供可扩展的枚举和检查接口，方便后续添加更多条件类型。

### 需求 3：进化条件检测逻辑

**用户故事：** 作为一名开发者，我希望有一个统一的条件检测服务，以便在运行时判断角色是否满足所有进化条件。

#### 验收标准

1. WHEN TrainView 打开时 THEN 系统 SHALL 逐一检查当前角色的所有进化条件，并返回每个条件的完成状态（已满足/未满足）。
2. WHEN 所有条件均已满足 THEN 系统 SHALL 将 EvolutionButton 设为可交互状态（interactable = true）。
3. IF 任一条件未满足 THEN 系统 SHALL 将 EvolutionButton 设为不可交互状态（interactable = false）。
4. WHEN 条件类型为"击杀数要求"时 THEN 系统 SHALL 从一个击杀统计数据源中读取该角色的累计击杀数。
5. WHEN 条件类型为"材料要求"时 THEN 系统 SHALL 从材料/背包数据源中读取对应材料的持有数量。

### 需求 4：RequirmentFrame 条件文本显示

**用户故事：** 作为一名玩家，我希望在 TrainView 中看到清晰的进化条件列表及其完成状态，以便了解还需要完成哪些任务才能进化。

#### 验收标准

1. WHEN TrainView 打开且角色有进化体时 THEN 系统 SHALL 在 RequirmentFrame 区域显示所有进化条件的文本描述。
2. WHEN 显示条件文本时 THEN 系统 SHALL 对每个条件显示格式为"[条件描述]: [当前值]/[目标值]"的文本。
3. IF 某个条件已满足 THEN 系统 SHALL 以视觉区分方式（如绿色文本或添加✓标记）标识该条件已完成。
4. IF 某个条件未满足 THEN 系统 SHALL 以默认或警示方式（如红色文本或添加✗标记）标识该条件未完成。
5. IF 角色没有进化体（evolutionTarget 为空）THEN 系统 SHALL 在 RequirmentFrame 中显示"无可用进化"。
6. WHEN 条件状态发生变化（如等级提升）THEN 系统 SHALL 实时更新 RequirmentFrame 中的文本显示。

### 需求 5：EvolutionButton 进化执行

**用户故事：** 作为一名玩家，我希望在满足所有进化条件后点击进化按钮，将进化体角色添加到我的卡牌列表中，以便获得新角色。

#### 验收标准

1. WHEN 玩家点击 EvolutionButton 且所有条件已满足 THEN 系统 SHALL 将 evolutionTarget 对应的 CharacterData 通过 `CardSplineDistributor.AddCharacterData()` 添加到玩家卡牌列表中。
2. WHEN 进化成功后 THEN 系统 SHALL 消耗对应的材料（如果条件中包含材料要求）。
3. WHEN 进化成功后 THEN 系统 SHALL 给予玩家明确的成功反馈（如日志提示或UI提示）。
4. IF 进化体已经存在于卡牌列表中 THEN 系统 SHALL 阻止重复添加，并给予提示。
5. IF 角色没有进化体 THEN 系统 SHALL 使 EvolutionButton 不可交互。
6. WHEN 进化成功后 THEN 系统 SHALL 更新 RequirmentFrame 的显示状态（如显示"已进化"或切换到下一级进化条件）。

### 需求 6：击杀统计数据支持

**用户故事：** 作为一名开发者，我希望有一个击杀统计系统来记录每个角色的累计击杀数，以便进化条件中的击杀数要求能正确检测。

#### 验收标准

1. WHEN 玩家控制的角色击杀敌人时 THEN 系统 SHALL 累加该角色的击杀计数。
2. WHEN 查询击杀数时 THEN 系统 SHALL 按 characterId 返回对应角色的累计击杀数。
3. WHEN 游戏重启或场景切换时 THEN 系统 SHALL 持久化保存击杀统计数据（与 CultivationData 类似的管理方式）。

### 需求 7：材料/背包系统基础支持

**用户故事：** 作为一名开发者，我希望有一个基础的材料/物品持有数据接口，以便进化条件中的材料要求能正确检测。

#### 验收标准

1. WHEN 检查材料条件时 THEN 系统 SHALL 提供一个接口/管理器来查询玩家持有的指定材料数量。
2. WHEN 进化消耗材料时 THEN 系统 SHALL 从玩家持有数据中扣除对应数量的材料。
3. IF 项目中尚无材料/背包系统 THEN 系统 SHALL 提供一个最小化的材料管理器（MaterialManager），支持按材料ID查询和扣除数量。
