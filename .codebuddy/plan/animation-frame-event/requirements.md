# 需求文档：动画帧事件伤害系统

## 引言
本功能旨在将现有的直接伤害机制改造为基于Unity动画帧事件的伤害触发系统。通过在动画特定帧触发事件，实现伤害与动画的精确同步，提升游戏战斗体验的真实感和视觉效果。

## 需求

### 需求 1：动画帧事件接收器

**用户故事：** 作为一名游戏开发者，我希望创建一个动画帧事件接收器组件，以便在动画播放到特定帧时触发伤害计算

#### 验收标准
1. WHEN 动画播放到攻击命中帧 THEN 系统 SHALL 调用OnAttackHit()方法
2. WHEN 动画播放到技能命中帧 THEN 系统 SHALL 调用OnSkillHit(int skillIndex)方法
3. IF 攻击目标已失效或死亡 THEN 系统 SHALL 跳过伤害计算并清理状态
4. WHEN 帧事件触发时 THEN 系统 SHALL 确保伤害计算与动画视觉效果同步
5. IF 攻击被中断 THEN 系统 SHALL 取消待处理的伤害事件

### 需求 2：CombatSystem改造

**用户故事：** 作为一名战斗系统开发者，我希望改造CombatSystem以支持延迟伤害机制，以便伤害在动画正确帧触发

#### 验收标准
1. WHEN 执行普通攻击 THEN CombatSystem SHALL 缓存目标信息并播放攻击动画，而非直接造成伤害
2. WHEN 执行技能攻击 THEN CombatSystem SHALL 缓存目标和技能索引并播放技能动画，而非直接造成伤害
3. WHEN 收到OnAttackHit帧事件 THEN CombatSystem SHALL 对缓存目标应用普通攻击伤害
4. WHEN 收到OnSkillHit帧事件 THEN CombatSystem SHALL 对缓存目标应用对应技能伤害
5. IF 攻击过程中目标失效 THEN CombatSystem SHALL 安全地中止伤害处理

### 需求 3：AI行为树适配

**用户故事：** 作为一名AI系统开发者，我希望修改AI行为树攻击节点以兼容新的帧事件系统，以便AI攻击行为与动画同步

#### 验收标准
1. WHEN AI执行攻击行为 THEN 行为树节点 SHALL 调用CombatSystem.TryNormalAttack而非直接造成伤害
2. WHEN AI执行技能行为 THEN 行为树节点 SHALL 调用CombatSystem.TryUseSkill而非直接造成伤害

### 需求 4：动画系统增强

**用户故事：** 作为一名动画师，我希望确保所有攻击动画都正确配置帧事件，以便伤害触发时机准确

#### 验收标准
1. WHEN 查看攻击动画片段 THEN 每个片段 SHALL 包含正确配置的Animation Event
2. WHEN 动画播放完成 THEN 系统 SHALL 确保角色状态正确回到Idle状态

### 需求 5：技能特效同步

**用户故事：** 作为一名特效设计师，我希望技能特效的生成时机与帧事件同步，以便视觉效果与游戏逻辑一致

#### 验收标准
1. WHEN 技能帧事件触发 THEN 系统 SHALL 在此时生成技能特效
2. WHEN 特效生成后 THEN 系统 SHALL 在2秒后自动销毁特效对象
3. WHEN 攻击被中断 THEN 系统 SHALL 取消待生成的特效