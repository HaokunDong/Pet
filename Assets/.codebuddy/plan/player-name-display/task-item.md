# 实施计划

- [ ] 1. 创建 `PlayerNameTag` 组件
   - 在 `Scripts/UI/` 目录下创建 `PlayerNameTag.cs` 脚本
   - 使用 TextMesh（或 TextMeshPro）在角色头顶渲染名字文本
   - 参考 `HealthBar` 的实现模式：作为角色子对象，通过 `yOffset` 控制垂直位置（定位在 HealthBar 上方，如 yOffset=1.1f）
   - 提供 `Initialize(SpriteRenderer parentSprite)` 方法用于初始化
   - 提供 `SetName(string name)` 方法用于设置/更新显示的名字
   - 处理名字为空时显示默认值 "Player"
   - 设置 sortingOrder 高于 HealthBar（如 102），确保不被遮挡
   - _需求：1.4、3.1、3.2、3.3、3.4_

- [ ] 2. 实现名字标签的翻转补偿逻辑
   - 在 `PlayerNameTag` 中监听父级 SpriteRenderer 的 flipX 状态
   - 当角色翻转时，对名字标签的 localScale.x 取反，保持文字始终正向显示
   - 参考 `HealthBar` 中类似的翻转处理方式（LateUpdate 中检测）
   - _需求：2.1、2.2、2.3_

- [ ] 3. 实现名字标签的视觉效果
   - 设置合适的字体大小（与像素风格匹配，如 fontSize=3，characterSize=0.1）
   - 添加文字描边或阴影效果确保在各种背景下可读（可通过额外的偏移 TextMesh 实现描边）
   - 文本居中对齐（TextAlignment.Center + TextAnchor.MiddleCenter）
   - 处理过长名字的截断逻辑（超过一定字符数时截断并加 "..."）
   - _需求：3.1、3.2、3.3、3.4_

- [ ] 4. 在 `CharacterEntity` 中集成 `PlayerNameTag`
   - 在 `CharacterEntity` 中添加 `PlayerNameTag` 引用字段
   - 创建 `InitializeNameTag()` 方法，在 `Initialize()` 中于 `InitializeHealthBar()` 之后调用
   - 提供公开方法 `SetPlayerName(string name)` 供外部（NetworkPlayer）调用设置名字
   - 确保名字标签在角色初始化时创建，与 HealthBar 共存
   - _需求：1.1、1.2、2.3_

- [ ] 5. 在本地玩家角色创建流程中设置名字标签
   - 在 `NetworkPlayer.RegisterLocalCharacterDelayed()` 协程中，角色创建完成后调用 `CharacterEntity.SetPlayerName()` 设置本地玩家的 Steam 名称
   - 名字来源为已通过 `SteamFriends.GetPersonaName()` 获取并同步的 `playerName` SyncVar
   - _需求：1.1、1.2、4.1_

- [ ] 6. 在远程玩家 Mirror 角色创建流程中设置名字标签
   - 在 `NetworkPlayer.CreateMirrorCharacter()` 方法中，Mirror 角色创建完成后调用 `MirrorCharacter.SetPlayerName(playerName)` 设置远程玩家的 Steam 名称
   - 确保 `playerName` SyncVar 已同步到位后再设置（利用 CreateMirrorCharacterDelayed 协程中的延迟机制）
   - _需求：1.3、4.2、4.3_

- [ ] 7. 为 `playerName` SyncVar 添加 hook 回调以支持动态更新
   - 为 `NetworkPlayer` 的 `playerName` SyncVar 添加 `hook = nameof(OnPlayerNameChanged)` 
   - 实现 `OnPlayerNameChanged(string oldName, string newName)` 回调方法
   - 在回调中更新对应 MirrorCharacter 的名字标签显示
   - 处理角色尚未创建时名字变更的情况（缓存名字，待角色创建后设置）
   - _需求：4.1、4.2、4.3_

- [ ] 8. 实现名字标签的生命周期管理
   - 在 `CharacterEntity` 的清理/重置逻辑中处理名字标签的销毁或隐藏
   - 在 `NetworkPlayer.DestroyMirrorCharacter()` 中确保名字标签随角色一起被正确清理
   - 处理对象池场景：角色从池中取出时重新初始化名字标签，放回池中时重置名字标签状态
   - _需求：5.1、5.2、5.3、5.4_
