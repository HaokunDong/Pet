# 实施计划

- [ ] 1. 为 PlayerPrefab 的 Rigidbody2D 添加 FreezeRotation 约束
   - 修改 `Scripts/Character/CharacterEntity.cs` 的 `Initialize()` 方法，在初始化时为 `Rigidbody2D` 添加 `RigidbodyConstraints2D.FreezeRotation` 约束，同时重置 `velocity`、`angularVelocity` 和 `transform.rotation`
   - 这样无论是从 Prefab 新建还是从对象池取出，都能确保 FreezeRotation 生效
   - _需求：1.1、1.2、1.3_

- [ ] 2. MirrorEnemy 在客户端上禁用物理模拟
   - 修改 `Scripts/Network/NetworkEnemySpawner.cs` 的 `CreateMirrorEnemy()` 方法
   - 在创建 MirrorEnemy 后，将其 `Rigidbody2D` 设为 `Kinematic`（`bodyType = RigidbodyType2D.Kinematic`），防止重力掉落和物理碰撞推挤
   - MirrorEnemy 的位置完全由 `EnemyPositionMessage` 网络同步驱动，不需要物理模拟
   - _需求：2.1、2.2、2.3_

- [ ] 3. MirrorCharacter 在客户端上禁用物理模拟
   - 修改 `Scripts/Network/NetworkPlayer.cs` 的 `DisableMirrorCharacterControllers()` 方法
   - 将 MirrorCharacter 的 `Rigidbody2D` 设为 `Kinematic`，防止物理碰撞干扰网络同步的位置
   - MirrorCharacter 的位置完全由 `UpdateMirrorCharacter()` 的 `Vector3.Lerp` 插值驱动
   - _需求：3.1、3.2、3.3_

- [ ] 4. 创建 MirrorCharacterTag 标记组件
   - 在 `Scripts/Network/` 目录下新建 `MirrorCharacterTag.cs`，参照现有的 `MirrorEnemyTag.cs` 模式
   - 包含一个 `ownerConnectionId` 字段，用于标识该 MirrorCharacter 对应的远程客户端
   - 在 `NetworkPlayer.CreateMirrorCharacter()` 中，给 MirrorCharacter 添加 `MirrorCharacterTag` 组件并设置 `ownerConnectionId`
   - _需求：7.4_

- [ ] 5. 修改 Boss AI 目标搜索，跳过 MirrorCharacter
   - 修改 `Scripts/AI/Nodes/BTFindNearestEnemy.cs` 的 `FindNearest()` 方法
   - 在遍历 tag="Player" 的候选目标时，跳过带有 `MirrorCharacterTag` 组件的对象
   - 确保 Boss AI 只将房主的 LocalCharacter 作为攻击目标，不攻击 MirrorCharacter
   - _需求：7.2_

- [ ] 6. 修改 NetworkDamageHelper 支持 MirrorCharacter 伤害路由
   - 修改 `Scripts/Network/NetworkDamageHelper.cs` 的 `ApplyDamage()` 方法
   - 新增对 `MirrorCharacterTag` 的检测：如果目标是 MirrorCharacter，则通过网络将伤害路由到对应客户端的 LocalCharacter，而不是直接调用 `TakeDamage()`
   - 保持现有的 `MirrorEnemyTag` 逻辑不变
   - _需求：4.1、4.2、4.3、5.1、5.2_

- [ ] 7. 在 NetworkPlayer 中实现 MirrorCharacter 伤害路由的 RPC 机制
   - 在 `Scripts/Network/NetworkPlayer.cs` 中添加 `TargetTakeDamageFromBoss` 的 `[TargetRpc]` 方法，用于房主向特定客户端发送伤害
   - 在房主端添加查找 MirrorCharacter 对应 NetworkPlayer 的辅助方法
   - 客户端收到 RPC 后，对自己的 LocalCharacter 调用 `TakeDamage()`，然后通过 `CmdUpdateHealth` 将最新 HP 同步回服务器
   - _需求：4.1、4.2、5.3_

- [ ] 8. 修改战斗系统中所有伤害应用点，跳过或路由 MirrorCharacter 的伤害
   - 修改 `Scripts/Combat/CombatSystem.cs` 中的 AOE 伤害方法（如 `ApplyNormalAttackDamage`），在遍历命中目标时跳过带有 `MirrorCharacterTag` 的对象，或通过 `NetworkDamageHelper.ApplyDamage()` 路由伤害
   - 修改 `Scripts/Combat/MeleeSkillEffectData.cs` 中的技能伤害循环，同样跳过 MirrorCharacter
   - 修改 `Scripts/Combat/SkillDisplacementController.cs` 中的位移伤害检测，同样跳过 MirrorCharacter
   - _需求：7.1_

- [ ] 9. 修复 ClearAllEnemies 在客户端上的字典清理问题
   - 修改 `Scripts/Character/EnemySpawner.cs` 的 `ClearAllEnemies()` 方法
   - 在销毁 MirrorEnemy 时，同时通知 `NetworkEnemySpawner` 清理 `clientMirrorEnemies` 字典中对应的条目
   - 或者在 `NetworkEnemySpawner` 中添加 `ClearAllMirrorEnemies()` 方法，在 `BossFightManager.ExecuteBossFightLocally()` 中调用
   - _需求：8.1、8.2_

- [ ] 10. 为其他 Enemy Prefab 添加 FreezeRotation 约束
   - 检查 `EnemyPrefab` 和 `SlimeDeadMan` 的 `Rigidbody2D` 配置（当前 `m_Constraints: 0`）
   - 在 `EnemySpawner` 的敌人生成逻辑中，确保所有敌人的 `Rigidbody2D` 都包含 `FreezeRotation` 约束
   - 与任务 1 的方式一致，在 `CharacterEntity.Initialize()` 中统一处理
   - _需求：1.1、6.1_
