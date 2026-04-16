# 实施计划：可组合多形状攻击范围系统

## 涉及文件概览

| 文件 | 操作 |
|------|------|
| `Assets/Scripts/Data/AttackRangeShape.cs` | **新建** — 形状枚举 + 可序列化数据类 |
| `Assets/Scripts/Data/AttackRangeHelper.cs` | **新建** — 静态工具类，范围判定 & 最大距离计算 |
| `Assets/Scripts/Data/CharacterData.cs` | **修改** — 添加 `AttackRangeShape[]` 数组字段 |
| `Assets/Scripts/Character/RuntimeCharacterStats.cs` | **修改** — 添加 `AttackRangeShape[]` 字段，InitFromData 中加载 |
| `Assets/Scripts/Character/CharacterEntity.cs` | **修改** — 移除旧 `attackRangeOverride`，重写 Gizmo 绘制 |
| `Assets/Scripts/Character/CombatSystem.cs` | **修改** — 替换 `Vector2.Distance` 为新判定 |
| `Assets/Scripts/Character/ManualController.cs` | **修改** — 替换 `Vector2.Distance` 为新判定 |
| `Assets/Scripts/AI/Nodes/BTCheckEnemyInRange.cs` | **修改** — 替换范围检测 |
| `Assets/Scripts/AI/Nodes/BTAttack.cs` | **修改** — 替换范围检测 |
| `Assets/Scripts/AI/Nodes/BTMoveToTarget.cs` | **修改** — 替换范围检测 |
| `Assets/Editor/CharacterDataEditor.cs` | **新建** — 自定义 Inspector，带 Sprite + 攻击范围预览 |

---

## 任务清单

- [ ] 1. 创建攻击范围形状数据结构 `AttackRangeShape`
  - 新建 `Assets/Scripts/Data/AttackRangeShape.cs`
  - 定义枚举 `AttackShapeType { Circle, Box }`
  - 定义 `[System.Serializable]` 类 `AttackRangeShape`，包含字段：`shapeType`、`offset`（Vector2）、`radius`（Circle 用）、`size`（Vector2，Box 用宽高）
  - 添加 `Contains(Vector2 ownerPos, float facingSign, Vector2 targetPos)` 实例方法，根据形状类型判断目标点是否在范围内（facingSign 用于镜像 offset.x）
  - 添加 `GetMaxReach()` 实例方法，返回该形状从角色中心到最远点的距离（含 offset）
  - _需求：1.1, 1.2, 1.3, 1.4, 1.5_

- [ ] 2. 创建攻击范围静态工具类 `AttackRangeHelper`
  - 新建 `Assets/Scripts/Data/AttackRangeHelper.cs`
  - 实现 `static bool IsTargetInRange(Vector2 ownerPos, float facingSign, AttackRangeShape[] shapes, float fallbackRange, Vector2 targetPos)` — 遍历所有形状取并集，任一命中即返回 true；若 shapes 为空或 null 则回退到 fallbackRange 圆形检测
  - 实现 `static float GetMaxAttackDistance(AttackRangeShape[] shapes, float fallbackRange)` — 返回所有形状中最大外接距离；若 shapes 为空则返回 fallbackRange
  - _需求：2.1, 2.2, 2.3, 2.4_

- [ ] 3. 在 `CharacterData` 中添加攻击范围形状数组
  - 修改 `Assets/Scripts/Data/CharacterData.cs`
  - 在 `Combat Stats` Header 区域添加 `[Header("Attack Range Shapes")]` 和 `public AttackRangeShape[] attackRangeShapes;` 字段
  - 保留原有 `float attackRange` 字段作为回退默认值
  - _需求：3.1, 3.3, 3.4_

- [ ] 4. 修改 `RuntimeCharacterStats` 加载攻击范围形状
  - 修改 `Assets/Scripts/Character/RuntimeCharacterStats.cs`
  - 添加 `public AttackRangeShape[] attackRangeShapes;` 字段
  - 在 `InitFromData()` 中将 `data.attackRangeShapes` 赋值到运行时字段
  - 添加便捷方法 `bool IsTargetInAttackRange(Vector2 ownerPos, float facingSign, Vector2 targetPos)` — 内部调用 `AttackRangeHelper.IsTargetInRange`
  - 添加便捷方法 `float GetMaxAttackDistance()` — 内部调用 `AttackRangeHelper.GetMaxAttackDistance`
  - _需求：3.2, 2.3, 2.4_

- [ ] 5. 改造 `CharacterEntity` — 移除旧 override 逻辑，重写 Gizmo
  - 修改 `Assets/Scripts/Character/CharacterEntity.cs`
  - 移除 `attackRangeOverride` 字段及其在 `Initialize()` 和 `OnValidate()` 中的同步逻辑（攻击范围现在完全由 CharacterData 中的 shapes 数组定义）
  - 重写 `OnDrawGizmosSelected()` 和 `OnDrawGizmos()`：遍历 `RuntimeStats.attackRangeShapes`，根据形状类型绘制圆形或矩形 Gizmo（考虑角色朝向对 offset 的镜像）；若 shapes 为空则回退绘制 `attackRange` 圆形
  - _需求：5.1, 5.2, 5.3, 5.4, 5.5, 5.6_

- [ ] 6. 改造 `CombatSystem` — 替换范围检测逻辑
  - 修改 `Assets/Scripts/Character/CombatSystem.cs`
  - 在 `TryNormalAttack()` 中：将 `Vector2.Distance` 范围检测替换为 `entity.RuntimeStats.IsTargetInAttackRange()`，需要获取角色朝向 facingSign（从 CharAnimator.FacingDirection 或根据目标方向计算）
  - 在 `TryUseSkill()` 中：技能范围仍使用 `skillData.skillRange` 的圆形距离检测（技能有独立范围定义），保持不变
  - _需求：6.1_

- [ ] 7. 改造 AI 行为树节点 — 替换范围检测逻辑
  - 修改 `Assets/Scripts/AI/Nodes/BTCheckEnemyInRange.cs`：将 `Vector2.Distance <= attackRange` 替换为 `owner.RuntimeStats.IsTargetInAttackRange()`，facingSign 根据目标相对位置计算
  - 修改 `Assets/Scripts/AI/Nodes/BTAttack.cs`：将范围检测替换为 `owner.RuntimeStats.IsTargetInAttackRange()`
  - 修改 `Assets/Scripts/AI/Nodes/BTMoveToTarget.cs`：将 `dist <= owner.RuntimeStats.attackRange` 替换为 `owner.RuntimeStats.IsTargetInAttackRange()`；将 AI 追击停止距离改为使用 `owner.RuntimeStats.GetMaxAttackDistance()` 计算
  - _需求：6.2, 6.3, 6.4, 6.6_

- [ ] 8. 改造 `ManualController` — 替换范围检测逻辑
  - 修改 `Assets/Scripts/Character/ManualController.cs`
  - 在 `HandleRightClickInput()` 中：将 `dist <= entity.RuntimeStats.attackRange` 替换为 `entity.RuntimeStats.IsTargetInAttackRange()`
  - 在 `HandleChaseAndAttack()` 中：将范围检测替换为新方法
  - 在 `HandleSustainedAttack()` 中：将范围检测替换为新方法
  - _需求：6.5, 6.6_

- [ ] 9. 创建 `CharacterDataEditor` 自定义 Inspector — 带 Sprite + 攻击范围预览
  - 新建 `Assets/Editor/CharacterDataEditor.cs`
  - 继承 `UnityEditor.Editor`，使用 `[CustomEditor(typeof(CharacterData))]`
  - 在 `OnInspectorGUI()` 中：先绘制默认 Inspector（`DrawDefaultInspector()`），然后在底部绘制预览区域
  - 预览区域实现：使用 `GUILayout.BeginVertical("box")` 创建预览框；获取 CharacterData 的 `sprite` 字段，若存在则在预览中心绘制 Sprite 纹理；遍历 `attackRangeShapes` 数组，用半透明彩色区域叠加绘制每个形状（圆形用 `Handles.DrawSolidDisc` 风格的 GUI 绘制，矩形用 `GUI.DrawTexture`）
  - 若 `sprite` 为 null，显示默认占位图标（如 Unity 内置的角色图标）
  - 使用 `Repaint()` 确保参数修改时预览实时更新
  - _需求：4.1, 4.2, 4.3, 4.4, 4.5, 4.6_
