# 实施计划

> 基于需求文档：`.codebuddy/plan/character-enemy-system/requirements.md`

---

- [ ] 1. 创建数据模型层（ScriptableObject 模板与枚举定义）
  - 在 `Assets/Scripts/` 下新建 `Data/` 目录
  - 创建枚举文件 `CharacterEnums.cs`，定义 `QualityLevel`（A/S/SS/SSS）、`CharacterType`（Player/MinorEnemy/Boss）
  - 创建 `SkillData.cs`（ScriptableObject），包含技能ID、名称、CD、伤害值、技能范围、图标、特效预制体引用等字段
  - 创建 `CharacterData.cs`（ScriptableObject），包含角色ID、名称、攻击力、生命值、防御力、移速、攻速、攻击范围、品质等级、角色类型、技能列表、精灵图、动画控制器引用等字段；添加 `[CreateAssetMenu]` 特性支持右键创建；实现 `OnValidate()` 校验技能数量是否超出品质上限并输出警告
  - _需求：1.1、1.2、2.1~2.6_

- [ ] 2. 创建运行时角色状态与实例化系统
  - 在 `Assets/Scripts/` 下新建 `Character/` 目录
  - 创建 `RuntimeCharacterStats.cs` 类，包含当前生命值、技能CD状态字典、当前位置等运行时动态数据，提供基于 `CharacterData` 的初始化方法
  - 创建 `CharacterEntity.cs` MonoBehaviour 组件，作为角色 GameObject 的核心组件，持有 `CharacterData` 引用和 `RuntimeCharacterStats` 实例，负责实例化时创建运行时数据副本
  - 实现伤害计算方法 `TakeDamage(float attackPower)`，公式为 `实际伤害 = attackPower - 防御力`（最小为1）
  - 实现死亡判定逻辑：生命值 ≤ 0 时触发死亡事件，播放死亡动画并通过 `PoolMgr` 回收
  - 实现 `OnDisable/OnDestroy` 中的状态清理和事件监听移除
  - _需求：3.1~3.4、1.3_

- [ ] 3. 实现动画控制系统
  - 在 `Character/` 目录下创建 `CharacterAnimator.cs` 组件，封装 Animator 的状态切换逻辑
  - 实现 `PlayIdle()`、`PlayWalk()`、`PlayAttack()`、`PlaySkill(int skillIndex)`、`PlayHit()`、`PlayDeath()` 等方法
  - 实现精灵图方向翻转逻辑：根据移动方向设置 `SpriteRenderer.flipX` 或 `transform.localScale.x`
  - 在 `CharacterEntity` 中集成 `CharacterAnimator`，确保各状态切换时自动调用对应动画
  - _需求：11.1~11.6_

- [ ] 4. 实现行为树框架
  - 在 `Assets/Scripts/` 下新建 `AI/` 目录
  - 创建行为树基础节点：`BTNode.cs`（抽象基类，定义 `Execute()` 返回 `BTState` 枚举：Success/Failure/Running）
  - 创建组合节点：`BTSelector.cs`（选择节点，子节点依次执行直到一个成功）、`BTSequence.cs`（序列节点，子节点依次执行直到一个失败）
  - 创建装饰节点：`BTInverter.cs`（取反节点）
  - 创建行为树运行器：`BehaviorTree.cs`，持有根节点并在 `Tick()` 中驱动执行
  - _需求：6.1~6.5（行为树基础设施）_

- [ ] 5. 实现 AI 行为树的具体行为节点
  - 创建条件节点：`BTCheckEnemyInRange.cs`（检测攻击范围内是否有敌人）、`BTFindNearestEnemy.cs`（搜索最近敌人）、`BTCheckSkillReady.cs`（检查是否有技能已冷却完毕）
  - 创建行动节点：`BTPatrol.cs`（巡逻行为，左右移动）、`BTMoveToTarget.cs`（向目标移动）、`BTAttack.cs`（执行普通攻击）、`BTUseSkill.cs`（释放技能）
  - 创建 `AIController.cs` 组件，负责构建角色/敌人的行为树结构，在 `Update` 中调用 `BehaviorTree.Tick()`
  - 为玩家角色 AI 构建行为树：巡逻 → 搜索敌人 → 接近 → 优先技能攻击 → 普通攻击
  - 为小怪构建行为树：搜索玩家 → 接近 → 普通攻击（无技能）
  - 为 Boss 构建行为树：与玩家角色 AI 相同（搜索 → 接近 → 技能/普通攻击）
  - _需求：6.2~6.5、9.2、9.4、9.5_

- [ ] 6. 实现普通攻击与技能系统
  - 在 `Character/` 目录下创建 `CombatSystem.cs` 组件，管理角色的攻击和技能逻辑
  - 实现普通攻击：无CD限制，但受攻速属性控制攻击间隔（`攻击间隔 = 1 / 攻速`），攻击时播放 Attack 动画并对目标调用 `TakeDamage()`
  - 实现技能系统：每个技能独立维护 CD 计时器，CD 期间不可使用；技能释放时播放技能动画和特效；CD 结束后标记为可用
  - 提供 `TryNormalAttack(CharacterEntity target)` 和 `TryUseSkill(int skillIndex, CharacterEntity target)` 公共接口，供 AI 和手动操控调用
  - _需求：4.1~4.4、5.1~5.5_

- [ ] 7. 实现操控模式切换与交互系统
  - 在 `Character/` 目录下创建 `ControlModeManager.cs` 组件，管理角色的操控模式状态机（枚举：AI_Auto / Manual / Paused_ShowingButton）
  - 实现鼠标左键点击角色检测（通过 Collider2D + `OnMouseDown` 或射线检测）
  - 点击角色时：触发 Shader 高亮效果 → 暂停当前行为 → 切换 Idle 动画 → 在角色左侧显示按钮（AI 模式下显示"操控"，手动模式下显示"退出操控"）
  - 实现点击任意非按钮区域收回按钮并恢复原状态的逻辑
  - 实现点击"操控"按钮切换到手动模式、点击"退出操控"按钮切换回 AI 模式的逻辑
  - 按钮 UI 通过现有 `WindowManager` 体系管理，使用世界空间 Canvas 跟随角色位置
  - _需求：8.1~8.9_

- [ ] 8. 实现 Shader 点击高亮效果
  - 在 `Assets/Shaders/` 下创建 `CharacterFlash.shader`（或 ShaderGraph），实现闪白/亮度提升效果
  - Shader 接收一个 `_FlashAmount`（float 0~1）参数，控制闪白强度
  - 在 `Character/` 目录下创建 `FlashEffect.cs` 组件，通过 `MaterialPropertyBlock` 或材质实例控制 `_FlashAmount` 参数
  - 实现 `TriggerFlash()` 方法：通过协程在 0.1~0.2 秒内将 `_FlashAmount` 从 1 渐变回 0
  - 实现防重复点击保护：效果播放期间忽略新的点击触发
  - _需求：8.10~8.11_

- [ ] 9. 实现玩家手动操控系统
  - 在 `Character/` 目录下创建 `ManualController.cs` 组件，仅在手动操控模式下激活
  - 实现鼠标右键点击检测：判断点击位置相对于角色的左右方向，控制角色向对应方向移动
  - 实现鼠标右键点击敌人检测：角色朝敌人移动，进入攻击范围后自动执行普通攻击
  - 移动时播放 Walk 动画，攻击时播放 Attack 动画，无操作时播放 Idle 动画
  - _需求：7.1~7.5、4.2_

- [ ] 10. 实现敌人生成与管理
  - 在 `Assets/Scripts/` 下新建 `Enemy/` 目录（或复用 `Character/` 目录，因为敌人与角色共用模板）
  - 创建 `EnemySpawner.cs` 组件，负责根据配置在场景中生成小怪和 Boss（通过 `PoolMgr` 获取实例）
  - 小怪实例化时：设置 `CharacterType = MinorEnemy`，挂载 `AIController` 并构建仅含普通攻击的行为树
  - Boss 实例化时：设置 `CharacterType = Boss`，挂载 `AIController` 并构建含技能的完整行为树
  - 确保 Boss 的 `CharacterData` 可通过修改 `CharacterType` 字段直接复用为玩家角色
  - _需求：9.1~9.6、1.4_

- [ ] 11. 实现数据持久化系统
  - 在 `Assets/Scripts/` 下新建 `Save/` 目录
  - 创建 `SaveData.cs` 数据类，定义可序列化的玩家角色存档结构（角色ID、当前生命值、技能CD状态、位置等）
  - 创建 `SaveManager.cs` 单例（继承现有 `Singleton<T>`），实现 `SaveGame()` 和 `LoadGame()` 方法
  - `SaveGame()`：收集所有玩家角色的 `RuntimeCharacterStats`，序列化为 JSON，写入 `Application.persistentDataPath`
  - `LoadGame()`：从本地文件读取 JSON 并反序列化，恢复各角色的运行时状态；若文件不存在或损坏则使用 `CharacterData` 默认值
  - 确保存档结构支持新增角色的向前兼容（使用可选字段 + 默认值策略）
  - _需求：10.1~10.4_

- [ ] 12. 集成测试与场景搭建
  - 使用 `CharacterData` 模板创建至少 1 个玩家角色配置（如 S 级角色，含 2 个技能）和 2 个敌人配置（1 个小怪 + 1 个 Boss）
  - 在游戏场景中集成 `EnemySpawner`，生成测试敌人
  - 验证完整流程：角色 AI 挂机打怪 → 点击角色显示按钮 → 切换手动操控 → 右键移动和攻击 → 退出操控恢复 AI → 存档/读档
  - 验证 Shader 点击效果、动画切换、技能CD、伤害计算、死亡回收等核心功能
  - _需求：全部需求的集成验证_
