# 需求文档

## 引言

本功能为现有技能效果系统新增一种"召唤"（Summon）类型的 `SkillEffectData` 子类。当技能释放时，在施法者周围随机生成指定数量的召唤物实体，每个召唤物拥有独立的属性数据（来自其预制体上的 `CharacterData`）、独立的动画状态机，并完全由 AI 行为树（`AIController`）自主操控。召唤物存活一段时间后自动消失。

### 现有架构概述

- `SkillEffectData`：抽象基类（ScriptableObject），子类通过 `Execute()` 方法实现具体技能效果
- 现有子类：`MeleeSkillEffectData`（近战AOE）、`ProjectileSkillEffectData`（抛射物）
- `CharacterData`：角色数据模板，包含攻击力、生命值、防御、移速、攻击速度、技能列表、动画控制器等
- `CharacterEntity`：角色核心组件，持有 `CharacterData` 引用并创建 `RuntimeCharacterStats`
- `AIController`：AI 控制器，构建并驱动行为树（Wander → Combat → PostCombat）

---

## 需求

### 需求 1：创建 SummonSkillEffectData 类

**用户故事：** 作为一名游戏设计师，我希望能在 Unity 编辑器中创建"召唤"类型的技能效果资产，以便配置召唤物的预制体、数量和持续时间。

#### 验收标准

1. WHEN 设计师在 Unity 编辑器中右键创建资产 THEN 系统 SHALL 在 "Game/SkillEffect/Summon" 菜单下提供创建选项
2. WHEN 创建 SummonSkillEffectData 资产 THEN 系统 SHALL 暴露以下可配置字段：
   - `summonPrefab`（GameObject）：召唤物预制体引用
   - `summonCount`（int）：召唤物数量，表示一次技能释放生成的召唤物个数
   - `summonDuration`（float）：召唤物存活持续时间（秒）
   - `allowMultipleWaves`（bool）：是否允许同时存在多轮召唤物。勾选为 True 时允许多轮召唤物共存；不勾选为 False 时每次施法会先销毁场上已有的该施法者的召唤物再生成新一轮
3. IF `summonPrefab` 未赋值 THEN 系统 SHALL 在 Execute 时输出错误日志并中止执行
4. IF `summonDuration` ≤ 0 THEN 系统 SHALL 使用 Inspector 中的 [Min] 约束确保最小值为 0.1 秒
5. IF `summonCount` ≤ 0 THEN 系统 SHALL 使用 Inspector 中的 [Min] 约束确保最小值为 1

---

### 需求 2：召唤物生成逻辑

**用户故事：** 作为一名玩家，我希望释放召唤技能时能在角色周围随机生成指定数量的召唤物，以便它们帮助我战斗。

#### 验收标准

1. WHEN 技能动画到达命中帧事件（Execute 被调用）THEN 系统 SHALL 在施法者周围随机实例化 `summonCount` 个召唤物预制体
2. WHEN 召唤物生成 THEN 系统 SHALL 将每个召唤物随机放置在施法者周围一定半径范围内的不同位置（避免重叠）
3. WHEN 召唤物生成 THEN 系统 SHALL 确保每个召唤物的 `CharacterEntity` 组件正确初始化，使用预制体上配置的 `CharacterData`
4. WHEN 召唤物生成 THEN 系统 SHALL 确保所有召唤物的 Tag 与施法者相同（Player 召唤的为 "Player" 阵营，Enemy 召唤的为 "Enemy" 阵营）
5. WHEN 多个召唤物生成 THEN 系统 SHALL 确保每个召唤物拥有独立的 `RuntimeCharacterStats` 实例（互不影响）

---

### 需求 3：召唤物属性继承

**用户故事：** 作为一名游戏设计师，我希望召唤物拥有其预制体对应 `CharacterData` 中定义的独立属性，以便可以为不同召唤物配置不同的战斗数据。

#### 验收标准

1. WHEN 召唤物被实例化 THEN 系统 SHALL 从预制体的 `CharacterEntity.characterData` 创建独立的 `RuntimeCharacterStats`（攻击力、生命值、防御、移速、攻击速度、攻击距离等）
2. WHEN 召唤物受到伤害 THEN 系统 SHALL 使用其自身的 `RuntimeCharacterStats` 进行伤害计算
3. IF 召唤物生命值降为 0 THEN 系统 SHALL 触发召唤物死亡流程（播放死亡动画并销毁），无需等待持续时间结束

---

### 需求 4：召唤物动画状态

**用户故事：** 作为一名游戏设计师，我希望召唤物使用其预制体配置的动画控制器和状态机，以便召唤物有完整的动画表现。

#### 验收标准

1. WHEN 召唤物被实例化 THEN 系统 SHALL 使用预制体上配置的 `RuntimeAnimatorController` 驱动动画
2. WHEN 召唤物执行行为（待机、行走、攻击、受击、死亡）THEN 系统 SHALL 通过现有的 `CharacterAnimator` 和状态机系统播放对应动画
3. WHEN 召唤物预制体包含 `EnemyStateMachine` 或类似状态机组件 THEN 系统 SHALL 正确初始化该状态机

---

### 需求 5：召唤物 AI 行为树控制

**用户故事：** 作为一名游戏设计师，我希望召唤物完全由 AI 行为树自主操控，以便它能自动寻敌、追击和攻击。

#### 验收标准

1. WHEN 召唤物被实例化 THEN 系统 SHALL 确保其 `AIController` 组件被正确初始化并激活
2. WHEN AI 行为树 Tick THEN 召唤物 SHALL 按照标准行为树逻辑运行（Wander → Combat → PostCombat）
3. WHEN 召唤物检测到敌方目标 THEN 系统 SHALL 驱动召唤物自动接近并攻击目标
4. WHEN 召唤物的 AI 寻敌 THEN 系统 SHALL 基于召唤物自身的 Tag 确定敌方 Tag（与施法者阵营一致）

---

### 需求 6：召唤物生命周期管理

**用户故事：** 作为一名游戏设计师，我希望召唤物在持续时间结束后自动消失，以便控制战场上的实体数量。

#### 验收标准

1. WHEN 召唤物存活时间达到 `summonDuration` THEN 系统 SHALL 自动销毁（或回收）该召唤物
2. WHEN 召唤物被销毁（无论是超时还是被击杀）THEN 系统 SHALL 清理所有相关引用，避免内存泄漏
3. IF 施法者在召唤物存活期间死亡 THEN 召唤物 SHALL 继续存活直到持续时间结束或被击杀（不随施法者死亡而消失）
4. WHEN 召唤物持续时间结束 THEN 系统 SHALL 播放消失效果（可选）后销毁 GameObject

---

### 需求 7：边界情况处理

**用户故事：** 作为一名开发者，我希望系统能正确处理各种边界情况，以便游戏运行稳定。

#### 验收标准

1. IF 同一施法者多次释放召唤技能 AND `allowMultipleWaves` 为 True THEN 系统 SHALL 允许同时存在多轮召唤物实例（每次释放独立计时，每次生成 `summonCount` 个）
2. IF 同一施法者多次释放召唤技能 AND `allowMultipleWaves` 为 False THEN 系统 SHALL 先销毁该施法者场上已有的所有召唤物，再生成新一轮 `summonCount` 个召唤物（刷新机制）
3. IF 召唤物预制体缺少必要组件（CharacterEntity、AIController）THEN 系统 SHALL 输出错误日志并安全降级处理
4. WHEN 场景切换或战斗结束 THEN 系统 SHALL 正确清理所有存活的召唤物
5. IF 召唤物的 `CharacterData` 中配置了技能 THEN 召唤物 SHALL 能够使用这些技能（由 AI 行为树的 Strike 节点触发）
6. IF 生成位置被障碍物阻挡 THEN 系统 SHALL 尝试在附近寻找可用位置放置召唤物
