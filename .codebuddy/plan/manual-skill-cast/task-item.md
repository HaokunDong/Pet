# 实施计划

- [ ] 1. 创建 `ManualSkillBarUI` 脚本骨架
   - 在 `Assets/Scripts/UI/ManualSkillBarUI.cs` 中新建 `MonoBehaviour` 类
   - 定义 4 个槽位结构体/类：包含 `GameObject root`、`Image iconImage`、`Image cooldownMask`、`Text cooldownText` 字段（通过 Inspector 拖拽绑定）
   - 暴露 `BindCharacter(CharacterEntity)` 与 `Unbind()` 公开方法，作为外部调用入口
   - 提供单例引用（`public static ManualSkillBarUI Instance`），方便其它系统访问
   - _需求：1.5_

- [ ] 2. 制作 `ManualSkillBar.prefab` 预制体
   - 在 `Assets/Resources/Prefabs/UI/` 下创建预制体，根节点带 `Canvas`（或挂在已有 Canvas 之下，以 Screen Space - Overlay 为准）
   - 在预制体下排布 4 个槽位（`Slot1`~`Slot4`），每个槽位包含 Icon、CooldownMask（半透明灰色覆盖层，`Image.fillMethod = Radial360`）、CooldownText
   - 预制体根节点挂载步骤 1 创建的 `ManualSkillBarUI` 脚本，并绑定 4 个槽位的引用
   - 预制体 RectTransform 默认锚定屏幕底部居中
   - _需求：1.1、1.2、1.4、1.5_

- [ ] 3. 实现技能图标动态显示逻辑
   - 在 `BindCharacter` 中读取 `CharacterEntity.Data.skills` 与 `Data.GetMaxSkillCount()`
   - 根据数量 N（1~4）激活前 N 个槽位、隐藏其余槽位
   - 将每个激活槽位的 `iconImage.sprite` 设置为对应 `SkillData.icon`，icon 为空时容错处理（保持空白且不报错）
   - _需求：2.1、2.2、2.3、2.4、2.5_

- [ ] 4. 实现冷却置灰与倒计时刷新（Update）
   - 在 `Update()` 中遍历当前绑定角色的激活槽位，读取 `RuntimeStats.skillCooldowns[i]` 剩余时间
   - 当剩余时间 > 0：显示 CooldownMask（按比例填充 `fillAmount = remaining / cooldown`），CooldownText 显示 `Mathf.CeilToInt(remaining)` 秒
   - 当剩余时间 ≤ 0：隐藏 CooldownMask、清空 CooldownText，恢复图标常态
   - 切换角色或解绑时，立刻基于新角色状态刷新一次，不残留旧数据
   - _需求：4.1、4.2、4.3、4.4、4.5、4.6_

- [ ] 5. 在 `ManualController` 中实现 Q/W/E/R 输入与技能释放
   - 在 `ManualController.Update()` 中检测 `Input.GetKeyDown(KeyCode.Q/W/E/R)`，对应技能索引 0/1/2/3
   - 调用前置校验：`ControlMode == Manual`、索引 < `GetMaxSkillCount()`、`RuntimeStats.IsSkillReady(i)`、`!CharAnimator.IsInSkillState`
   - 自动选择最近存活敌人作为目标（复用现有 `FindNearestEnemy` 类逻辑），无目标则不释放
   - 校验通过后调用 `CombatSystem.TryUseSkill(index, target)`
   - _需求：3.1、3.2、3.3、3.4、3.5、3.6、3.7、3.8、3.9、3.10、3.11_

- [ ] 6. 实现技能栏 UI 的生命周期管理
   - 在 `ControlModeManager`（或 `ManualController.OnEnable/OnDisable`）的模式切换钩子中：
     - 当某角色切到 `Manual`：若 `ManualSkillBarUI.Instance` 不存在，则通过 `Resources.Load<GameObject>("Prefabs/UI/ManualSkillBar")` 实例化；调用 `BindCharacter(character)` 并显示
     - 当退出 `Manual` 或角色阵亡：调用 `Unbind()` 并隐藏 UI
   - 监听角色 `OnDeath` 事件，确保阵亡时即时隐藏
   - 保证同一时刻仅绑定一个角色（切换角色时先 Unbind 再 Bind）
   - _需求：5.1、5.2、5.3、5.4、5.5_

- [ ] 7. 联调与回归测试
   - 进入手动模式：验证技能栏正确显示，技能数量与图标匹配
   - 按 Q/W/E/R 释放技能：验证仅手动模式生效；冷却中再按无效；动画中再按无效
   - 冷却显示：验证置灰遮罩与倒计时数字与 `RuntimeStats.skillCooldowns` 完全同步
   - 切换控制角色：验证 UI 即时刷新，不残留前一个角色的状态
   - 阵亡 / 退出手动模式：验证 UI 即时隐藏
   - _需求：2.5、3.5、3.6、3.7、3.8、4.4、4.5、5.2、5.3_
