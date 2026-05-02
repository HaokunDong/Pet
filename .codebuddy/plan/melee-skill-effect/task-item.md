# 实施计划

- [ ] 1. 创建 MeleeSkillEffectData ScriptableObject 类
   - 在 `Assets/Scripts/Data/` 目录下创建 `MeleeSkillEffectData.cs`
   - 继承 `SkillEffectData` 抽象基类
   - 添加 `AttackRangeShape[] skillRangeShapes` 字段，用于定义技能攻击范围（支持多个 Box/Circle 形状）
   - 添加 `Sprite previewSprite` 字段，用于 Editor 预览中显示角色精灵图
   - 添加 `bool defaultFacesRight` 字段，用于指示精灵图默认朝向
   - 配置 `[CreateAssetMenu]` 特性，菜单路径为 `Game/SkillEffect/Melee`
   - _需求：1.1, 1.2, 1.3, 1.4, 1.5, 1.6_

- [ ] 2. 实现 MeleeSkillEffectData 的 Execute() 伤害逻辑
   - 在 `MeleeSkillEffectData` 中实现 `Execute(CombatSystem caster, CharacterEntity target, SkillData skillData)` 方法
   - 根据施法者的 `CharacterType` 确定目标标签（Player 找 Enemy，Enemy 找 Player）
   - 获取施法者的朝向信息，计算有效朝向符号（考虑 `defaultFacesRight`）
   - 使用 `AttackRangeHelper.IsTargetInRange()` 对所有候选目标进行联合范围检测
   - 对范围内的存活目标调用 `target.TakeDamage(skillData.damage, casterEntity)` 造成伤害
   - 处理 `skillRangeShapes` 为空或 null 的边界情况（直接返回不造成伤害）
   - _需求：2.1, 2.2, 2.3, 2.4, 2.5, 2.6_

- [ ] 3. 创建 MeleeSkillEffectDataEditor 自定义 Inspector
   - 在 `Assets/Scripts/Editor/` 目录下创建 `MeleeSkillEffectDataEditor.cs`
   - 使用 `[CustomEditor(typeof(MeleeSkillEffectData))]` 特性
   - 绘制默认 Inspector 字段
   - _需求：3.1, 4.4_

- [ ] 4. 实现 Editor 可视化预览区域
   - 在自定义 Inspector 中添加预览区域（参考 `CharacterDataEditor` 的实现风格）
   - 绘制背景和十字准线标记角色中心位置
   - 当 `previewSprite` 已赋值时，在预览区域居中显示精灵图
   - 在精灵图上叠加绘制所有 `skillRangeShapes` 形状（Box 蓝色、Circle 红色半透明）
   - 根据 `defaultFacesRight` 字段正确镜像形状偏移
   - 添加颜色含义的提示信息（HelpBox）
   - 确保参数变化时预览实时更新（`GUI.changed` 检测 + `Repaint()`）
   - _需求：3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8_
