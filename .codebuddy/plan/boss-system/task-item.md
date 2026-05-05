# 实施计划

- [ ] 1. 在 CharacterData 中添加 CharacterType 枚举支持 Boss 类型
   - 在 `CharacterData` ScriptableObject 中添加 `CharacterType` 枚举（包含 Normal、Enemy、Boss）
   - 添加 `characterType` 字段，使 Boss 类型角色可被系统识别
   - _需求：1.4_

- [ ] 2. 创建 BossPrefab 的 Editor 脚本
   - 参照 `EnemySpawner.SpawnEnemy()` 中的组件配置，创建一个 Editor 工具脚本 `CreateBossPrefab.cs`
   - BossPrefab 需包含：SpriteRenderer、Animator、BoxCollider2D、Rigidbody2D、CharacterEntity、CharacterAnimator、CombatSystem、AIController、FlashEffect、AnimEventReceiver
   - 设置 Tag 为 "Enemy"，Layer 为 "Character"
   - _需求：1.1、1.2_

- [ ] 3. 为 EnemySpawner 添加暂停/恢复和清除功能
   - 在 `EnemySpawner.cs` 中添加 `isPaused` 标志位，Update 中检查该标志跳过生成逻辑
   - 添加 `PauseSpawning()` 公共方法：设置暂停标志
   - 添加 `ResumeSpawning()` 公共方法：恢复生成并重置计时器
   - 添加 `ClearAllEnemies()` 公共方法：查找场景中所有 Tag 为 "Enemy" 的对象并回收到对象池
   - _需求：3.1、3.2、3.3_

- [ ] 4. 创建 BossFightManager 核心脚本
   - 创建 `BossFightManager.cs`，包含可配置字段：`bossCharacterData`（CharacterData）、`bossSpawnPoint`（Transform）、`bossPrefabName`（string）
   - 添加对 `EnemySpawner` 和 `CardSplineDistributor` 的引用字段
   - 实现 `StartBossFight()` 方法：调用 EnemySpawner.PauseSpawning() → 调用 EnemySpawner.ClearAllEnemies() → 生成 Boss → 监听 Boss 的 OnDeath 事件
   - 实现 Boss 生成逻辑：通过 PoolMgr 获取 BossPrefab，初始化 CharacterEntity，配置 AIController
   - _需求：5.1、5.2、2.3、2.4、2.5_

- [ ] 5. 实现 Boss 死亡处理与奖励发放
   - 在 `BossFightManager` 中实现 `OnBossDefeated()` 回调方法
   - 判断是否首次击败：检查 `CardSplineDistributor.characterDataList` 中是否已存在该 Boss 的 CharacterData
   - 首次击败时将 Boss 的 CharacterData 添加到 `characterDataList` 并刷新卡牌 UI
   - 调用 `EnemySpawner.ResumeSpawning()` 恢复小怪生成
   - 定义并触发 `OnBossDefeated` 事件供其他系统响应
   - _需求：4.1、4.2、4.3、4.5、5.3、5.4_

- [ ] 6. 为 CardSplineDistributor 添加动态添加卡牌的公共方法
   - 在 `CardSplineDistributor.cs` 中添加 `AddCharacterData(CharacterData data)` 公共方法
   - 该方法将新的 CharacterData 追加到 `characterDataList` 数组
   - 调用 `ClearCards()` 后重新调用 `Initialize()` 刷新所有卡牌
   - _需求：4.1、4.2、4.4_

- [ ] 7. 将 BossFightManager 集成到 PortalManager 的传送门生成流程
   - 修改 `PortalManager.OnSpawnButtonClicked()` 方法，在生成传送门后调用 `BossFightManager.StartBossFight()`
   - 添加对 `BossFightManager` 的引用（通过 FindObjectOfType 或 Inspector 赋值）
   - 处理 Boss 出生点未配置的情况：使用传送门位置作为默认值并输出警告日志
   - _需求：2.1、2.2、2.4_

- [ ] 8. 创建 Boss 的 CharacterData ScriptableObject 资源
   - 通过 Unity Editor 创建一个 Boss 类型的 CharacterData 资源文件
   - 配置 Boss 的基础属性（高血量、高攻击力等）、动画控制器、精灵图等
   - 设置 `characterType` 为 `CharacterType.Boss`
   - _需求：1.3、1.4_
