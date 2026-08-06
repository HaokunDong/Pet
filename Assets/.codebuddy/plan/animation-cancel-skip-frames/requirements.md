# 需求文档：动画跳帧/取消功能（Animation Cancel / Frame Skip）

## 引言

本功能在现有的多段普攻衔接系统（Combo Attack）基础上，实现一个"跳帧"机制。通过在动画中插入独立的帧事件 `OnCancellablePoint`，实现两个核心场景：

1. **连击衔接加速**：当玩家在 combo 窗口期已按下攻击键（combo 输入已缓冲），动画播放到 `OnCancellablePoint` 时立即跳过剩余帧，直接衔接下一段攻击，使连击更丝滑流畅
2. **非攻击操作取消**：当动画进入可取消状态后，如果玩家按下了非攻击操作（移动、技能等），则立即跳过剩余帧并进入对应状态

### 核心设计思路

- 在攻击动画中插入一个**独立的新帧事件** `OnCancellablePoint`，专门用于标记从该帧开始动画可以被跳过。该事件与 `OnComboWindowOpen` 独立，可以放在不同的时间点
- **连击加速场景**：当 `OnCancellablePoint` 触发时，如果系统检测到 combo 输入已被缓冲（`_comboInputBuffered = true`），则立即跳过剩余动画帧，直接执行 combo 衔接逻辑（`PlayComboNextAttack`），无需等待 `OnStateEnd`
- **非攻击取消场景**：当 `OnCancellablePoint` 触发后，如果玩家输入了非攻击操作（移动、技能等），则立即跳过剩余动画帧，执行 `ClearCurrentState()` 并进入对应的下一个状态
- `OnCancellablePoint` 和 `OnComboWindowOpen` 是两个独立的概念：前者控制"从此帧开始动画可以被跳过"，后者控制"从此帧开始可以接受下一段攻击输入"。两者可以放在同一帧，也可以放在不同帧

### 现有架构中的相关机制

当前攻击动画的帧事件顺序为：
1. `OnAttackHit` — 伤害帧（此时 `canBeInterrupted = false`，不可被打断）
2. `OnComboWindowOpen` — combo 窗口打开（此时可以接受下一段攻击输入）
3. `OnStateEnd` — 动画结束，回到 Idle 或衔接下一段

新增帧事件后，推荐的帧事件顺序为：
1. `OnAttackHit` — 伤害帧
2. `OnComboWindowOpen` — combo 窗口打开（从此帧开始允许缓冲下一段攻击输入）
3. `OnCancellablePoint` — 可取消/可跳过点（从此帧开始检测是否可以跳过剩余动画）
4. `OnStateEnd` — 动画结束

注意：`OnCancellablePoint` 和 `OnComboWindowOpen` 的先后顺序可以灵活调整：
- **推荐顺序**（连击加速场景）：`OnComboWindowOpen` 在前，`OnCancellablePoint` 在后。这样玩家先获得缓冲输入的窗口，然后动画播放到可取消点时检测到已有缓冲，立即跳帧衔接
- 如果希望"先允许取消再允许连击"，则 `OnCancellablePoint` 在前
- 也可以放在同一帧

当前系统中，在 `OnAttackHit` 之后到 `OnStateEnd` 之前，角色处于"保护期"（`canBeInterrupted = false`），无法被移动或技能打断。跳帧功能的目标是在 `OnCancellablePoint` 触发后放开这个保护，允许跳过剩余动画。

---

## 需求

### 需求 1：可取消标记点帧事件（OnCancellablePoint）

**用户故事：** 作为一名开发者，我希望通过一个独立的帧事件 `OnCancellablePoint` 来控制动画的"可跳过"状态，使其与 combo 窗口（`OnComboWindowOpen`）解耦，以便灵活配置不同动画的取消/跳帧时机。

#### 验收标准

1. WHEN 攻击/技能动画播放到 `OnCancellablePoint` 帧事件 THEN AnimEventReceiver SHALL 通知 CombatSystem 标记当前动画为"可取消"状态（`_canBeCancelled = true`）
2. WHEN 新的攻击段开始播放（combo 衔接或新攻击发起）THEN 系统 SHALL 重置可取消标记为 false
3. WHEN 角色不在攻击/技能状态 THEN 可取消标记 SHALL 始终为 false
4. IF 动画中未配置 `OnCancellablePoint` 事件 THEN 该段动画 SHALL 不可被跳过（只能等 `OnStateEnd` 自然结束）
5. WHEN `OnCancellablePoint` 和 `OnComboWindowOpen` 配置在同一动画中 THEN 两者 SHALL 独立生效，互不影响（一个控制"可跳过"，一个控制"可连击"）

### 需求 2：连击衔接加速（Combo Skip）

**用户故事：** 作为一名玩家，我希望在连击窗口期按下攻击键后，角色能尽快衔接到下一段攻击而不是等待当前动画完全播完，以便获得更丝滑流畅的连击体验。

#### 验收标准

1. WHEN `OnCancellablePoint` 帧事件触发 AND combo 输入已被缓冲（`_comboInputBuffered = true`）THEN 系统 SHALL 立即跳过剩余动画帧，直接执行 combo 衔接逻辑（调用 `PlayComboNextAttack`），无需等待 `OnStateEnd`
2. WHEN combo 跳帧衔接被触发 THEN 系统 SHALL 正确递增 combo 步数并播放下一段攻击动画
3. WHEN combo 跳帧衔接被触发 THEN 系统 SHALL 重置可取消标记（`_canBeCancelled = false`），为下一段攻击做准备
4. IF `OnCancellablePoint` 触发时没有 combo 缓冲输入 THEN 系统 SHALL 仅标记为可取消状态，不执行跳帧，等待后续输入或 `OnStateEnd` 自然结束
5. WHEN 已处于最后一段攻击（无下一段可衔接）AND combo 输入被缓冲 THEN 系统 SHALL 不执行跳帧加速（因为没有下一段），按正常流程在 `OnStateEnd` 时重置 combo

### 需求 3：非攻击操作跳帧取消

**用户故事：** 作为一名玩家，我希望在攻击动画的后摇阶段，如果我按下了移动或技能等其他操作，角色能立即响应而不是等待动画播完，以便获得更灵敏的操控体验。

#### 验收标准

1. WHEN 动画处于"可取消"状态 AND 玩家输入了移动指令（右键点击空地）THEN 系统 SHALL 立即跳过剩余动画帧，执行状态清理（等同于 `OnStateEnd` 被触发），并进入移动/Idle 状态
2. WHEN 动画处于"可取消"状态 AND 玩家输入了技能释放指令 THEN 系统 SHALL 立即跳过剩余动画帧，执行状态清理，并进入技能状态
3. WHEN 动画处于"可取消"状态 AND 玩家输入了攻击指令（右键点击敌人）THEN 系统 SHALL 不触发跳帧取消，而是走正常的 combo 缓冲逻辑（已有功能）
4. WHEN 动画未处于"可取消"状态（`OnCancellablePoint` 尚未触发）AND 玩家输入了其他操作 THEN 系统 SHALL 忽略该操作，不打断当前攻击动画
5. WHEN 跳帧取消被触发 THEN 系统 SHALL 正确重置 combo 状态（因为玩家选择了非攻击操作，combo 链断裂）
6. WHEN 跳帧取消被触发 THEN 系统 SHALL 解锁朝向（`UnlockFacing`）并清理攻击状态

### 需求 4：ManualController 输入检测与跳帧触发

**用户故事：** 作为一名开发者，我希望 ManualController 能在攻击动画的可取消阶段检测到非攻击操作输入，并触发跳帧取消流程，以便将输入检测与动画取消逻辑正确衔接。

#### 验收标准

1. WHEN ManualController 检测到右键点击空地 AND 当前动画处于可取消状态 THEN ManualController SHALL 调用跳帧取消接口，立即结束攻击并开始移动
2. WHEN ManualController 检测到技能按键输入 AND 当前动画处于可取消状态 THEN ManualController SHALL 调用跳帧取消接口，立即结束攻击并释放技能
3. WHEN ManualController 检测到右键点击空地 AND 当前动画未处于可取消状态 THEN ManualController SHALL 仅记录移动意图，等待动画自然结束后再执行移动（或按现有逻辑处理）
4. IF 跳帧取消成功执行 THEN ManualController SHALL 确保后续状态转换（移动/技能）在同一帧内生效，不产生额外延迟

### 需求 5：AI 模式兼容

**用户故事：** 作为一名开发者，我希望 AI 模式下也能利用跳帧机制（连击加速和操作取消），以便 AI 行为更加灵活。

#### 验收标准

1. WHEN AI 行为树在 combo 窗口期缓冲了下一段攻击 AND `OnCancellablePoint` 触发 THEN AI SHALL 自动享受连击跳帧加速，立即衔接下一段攻击
2. WHEN AI 行为树在 combo 窗口期决定释放技能（而非继续连击）THEN AI SHALL 能够触发跳帧取消，立即结束当前攻击并释放技能
3. WHEN AI 行为树在 combo 窗口期决定追击移动目标 THEN AI SHALL 能够触发跳帧取消，立即结束当前攻击并开始移动
4. IF AI 不需要跳帧取消功能（默认打完全套 combo）THEN 现有 AI 行为 SHALL 不受影响，且自动享受连击加速

### 需求 6：技能动画的跳帧取消（扩展）

**用户故事：** 作为一名开发者，我希望跳帧取消机制不仅适用于普攻动画，也能扩展到技能动画的后摇阶段，以便未来可以实现技能后摇取消。

#### 验收标准

1. IF 技能动画中配置了类似的"可取消点"帧事件 THEN 系统 SHALL 支持在该点之后取消技能后摇
2. IF 技能动画中未配置可取消点 THEN 技能动画 SHALL 保持当前行为（不可被取消，必须播完）
3. WHEN 技能后摇被取消 THEN 系统 SHALL 确保技能伤害已经正常结算（取消点必须在伤害帧之后）

---

## 边界情况与技术约束

1. **伤害帧保护**：`OnCancellablePoint` 必须在伤害帧（`OnAttackHit`）之后，确保伤害已经正常结算后才允许跳帧
2. **跳帧触发的优先级**：`OnCancellablePoint` 触发时的判断优先级为：① 有 combo 缓冲 → 立即跳帧衔接下一段攻击；② 无 combo 缓冲但有非攻击操作 → 跳帧取消并进入对应状态；③ 都没有 → 仅标记可取消，等待后续输入
3. **Combo 缓冲与取消互斥**：在 combo 窗口期内，攻击输入走 combo 缓冲逻辑，非攻击输入走跳帧取消逻辑，两者互不冲突
4. **状态一致性**：跳帧取消后，所有相关状态（`_isAttacking`、`facingLocked`、`canBeInterrupted`、combo 状态）必须被正确清理，不能留下脏状态
5. **动画事件顺序**：跳帧取消相当于提前触发了 `OnStateEnd` 的效果，但不会真正触发 `OnStateEnd` 帧事件（因为动画被跳过了），所以取消逻辑需要手动执行等效的清理操作
6. **向后兼容**：未配置 `OnCancellablePoint` 的攻击动画不会进入可取消状态，行为与当前完全一致。未配置 `OnComboWindowOpen` 的攻击动画不会进入可连击状态，行为与当前完全一致
7. **多次输入防抖**：跳帧取消在一次攻击动画中只能触发一次，防止重复触发导致状态异常
8. **连击加速不影响 combo 计数**：跳帧衔接下一段攻击时，combo 步数正常递增，与 `OnStateEnd` 触发时的行为一致
