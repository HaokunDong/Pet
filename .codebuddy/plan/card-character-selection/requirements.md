# 需求文档：卡牌角色选择系统

## 引言

本系统旨在为游戏提供一个基于卡牌的角色选择机制。玩家通过浏览沿 Spline 曲线分布的角色卡牌来选择出场角色。卡牌展示角色的原画、名称、攻击力、生命值、防御力和角色简介等信息。卡牌沿 Spline 排列，最中间的卡牌位于牌堆顶部并展示全貌，玩家可通过鼠标拖动使卡牌沿 Spline 丝滑滑动，切换当前展示的角色卡牌。

### 技术背景

- 项目已安装 **Unity Splines 包**（`com.unity.splines: 2.8.4`）
- 项目已安装 **DOTween / DOTween Pro**
- 角色数据使用 `CharacterData`（ScriptableObject），现有字段包括：`characterId`、`characterName`、`attackPower`、`maxHealth`、`defense`、`sprite`、`animatorController` 等
- `CharacterData` 中**尚无角色简介字段**，需要新增

---

## 需求

### 需求 1：角色数据扩展 — 新增角色简介字段

**用户故事：** 作为一名开发者，我希望在 `CharacterData` 中新增一个角色简介文本字段，以便卡牌 UI 能够展示角色的背景描述信息。

#### 验收标准

1. WHEN `CharacterData` ScriptableObject 被查看时 THEN 系统 SHALL 在 Inspector 中显示一个名为 `characterDescription` 的多行文本字段，位于 Basic Info 区域下方。
2. IF `characterDescription` 字段为空 THEN 卡牌 UI SHALL 显示默认占位文本：“它还很神秘哦~”。
3. WHEN 已有的 CharacterData 资产被加载时 THEN 系统 SHALL 保持向后兼容，`characterDescription` 默认为空字符串。

---

### 需求 2：卡牌预制体与数据绑定脚本

**用户故事：** 作为一名开发者，我希望有一个卡牌预制体，能够展示角色的关键信息（原画、名称、攻击力、生命值、防御力、简介），以便玩家在选择角色时能直观了解角色属性。

#### 验收标准

1. 卡牌预制体 SHALL 由用户在 Unity Editor 中**手动创建**（非代码生成），预制体的 UI 结构包含以下子 GameObject：
   - 一个 Image 组件作为卡牌背景图片
   - 一个 Image 子对象用于显示角色原画（对应 `CharacterData.sprite`）
   - 一个 Text 子对象显示角色名称（对应 `CharacterData.characterName`）
   - 一个 Text 子对象显示攻击力（对应 `CharacterData.attackPower`）
   - 一个 Text 子对象显示最大生命值（对应 `CharacterData.maxHealth`）
   - 一个 Text 子对象显示防御力（对应 `CharacterData.defense`）
   - 一个 Text 子对象显示角色简介（对应 `CharacterData.characterDescription`）
2. 代码 SHALL 提供一个 `CharacterCard` 数据绑定脚本（MonoBehaviour），该脚本通过 `[SerializeField]` 暴露上述各 UI 组件的引用字段，用户在预制体上手动拖拽绑定对应的 UI 组件。
3. WHEN 卡牌被赋予一个 `CharacterData` 引用时 THEN `CharacterCard` 脚本 SHALL 自动将数据填充到已绑定的 UI 元素中。
4. IF 卡牌的 `CharacterData` 引用为 null THEN 卡牌 SHALL 显示默认/空白状态。
5. 卡牌上各子组件的具体位置、大小和布局 SHALL 完全由用户在 Unity Editor 中手动调整。

---

### 需求 3：卡牌沿 Spline 分布

**用户故事：** 作为一名玩家，我希望所有可选角色的卡牌沿一条 Spline 曲线排列，以便我能以直观的方式浏览所有可选角色。

#### 验收标准

1. WHEN 卡牌选择系统初始化时 THEN 系统 SHALL 根据可用的 `CharacterData` 列表，沿 Spline 曲线等间距生成对应数量的卡牌实例。
2. WHEN 卡牌被分布到 Spline 上时 THEN 每张卡牌 SHALL 被放置在 Spline 上对应的等分点位置。
3. IF Spline 上的卡牌数量发生变化 THEN 系统 SHALL 重新计算并调整所有卡牌的间距分布。
4. WHEN Spline 的形状在 Editor 中被修改时 THEN 卡牌的分布 SHALL 跟随 Spline 的新形状自动更新。

---

### 需求 4：牌堆顶部卡牌展示

**用户故事：** 作为一名玩家，我希望 Spline 最中间位置的卡牌被放在牌堆顶部并展示全貌，以便我能清楚地看到当前聚焦的角色信息。

#### 验收标准

1. WHEN 卡牌选择系统处于静止状态时 THEN 位于 Spline 中间位置（t=0.5 或自定义焦点位置）的卡牌 SHALL 被视为"焦点卡牌"。
2. WHEN 一张卡牌成为焦点卡牌时 THEN 该卡牌 SHALL 以全尺寸展示，层级（sorting order）置于最顶层，使其完全可见且不被其他卡牌遮挡。
3. WHEN 一张卡牌不在焦点位置时 THEN 该卡牌 SHALL 以缩小的尺寸显示（可配置缩放比例），并位于焦点卡牌的下层。
4. IF 多张卡牌重叠时 THEN 系统 SHALL 根据卡牌距离焦点位置的远近自动调整层级顺序，越靠近焦点的卡牌层级越高。

---

### 需求 5：鼠标拖动卡牌沿 Spline 滑动

**用户故事：** 作为一名玩家，我希望通过鼠标拖动卡牌使所有卡牌沿 Spline 曲线左右丝滑移动，以便我能流畅地浏览不同角色。

#### 验收标准

1. WHEN 玩家在卡牌区域按下鼠标并拖动时 THEN 所有卡牌 SHALL 沿 Spline 曲线方向跟随鼠标移动（整体偏移 Spline 参数 t 值）。
2. WHEN 玩家释放鼠标时 THEN 系统 SHALL 使用 DOTween 动画将最近的一张卡牌自动吸附到焦点位置（Spline 中间），实现丝滑的缓动效果。
3. WHEN 下一张卡牌移动到 Spline 中央焦点位置时 THEN 系统 SHALL 使用 DOTween 动画将该卡牌翻转到牌堆顶部并展示全貌（包括缩放动画和层级切换）。
4. WHEN 拖动过程中卡牌移动时 THEN 卡牌的位置 SHALL 始终贴合 Spline 曲线路径，不会脱离曲线。
5. IF 拖动速度较快 THEN 系统 SHALL 支持惯性滑动，释放鼠标后卡牌会根据拖动速度继续滑动一段距离后减速停止，并吸附到最近的焦点位置。
6. WHEN DOTween 吸附动画播放时 THEN 动画的缓动曲线（Ease）和持续时间 SHALL 可在全局配置中调节。

---

### 需求 6：全局配置

**用户故事：** 作为一名开发者，我希望卡牌选择系统的关键参数可以通过一个全局配置（ScriptableObject）进行调节，以便在不修改代码的情况下快速调整系统表现。

#### 验收标准

1. WHEN 开发者需要调整卡牌系统参数时 THEN 系统 SHALL 提供一个 `CardSelectionSettings`（ScriptableObject）资产，包含以下可配置项：
   - 焦点卡牌缩放比例（默认 1.0）
   - 非焦点卡牌缩放比例（默认 0.7）
   - DOTween 吸附动画持续时间（默认 0.3s）
   - DOTween 吸附动画缓动曲线（默认 Ease.OutCubic）
   - 惯性滑动衰减系数
   - 卡牌间距（Spline 参数 t 值间隔）
2. WHEN `CardSelectionSettings` 资产的值被修改时 THEN 系统 SHALL 在下次初始化或运行时自动应用新的配置值。
3. IF `CardSelectionSettings` 资产不存在于 Resources 文件夹中 THEN 系统 SHALL 在控制台输出错误日志并使用默认值。

---

## 边界情况与注意事项

1. **卡牌数量为 0 或 1**：当没有可用角色数据时，系统应优雅处理（不生成卡牌）；当只有 1 张卡牌时，该卡牌直接显示在焦点位置，拖动无效果。
2. **Spline 边界**：当拖动到 Spline 的起点或终点时，应有边界限制，防止卡牌滑出 Spline 范围。
3. **性能**：卡牌数量较多时（如 20+），应考虑对不可见的卡牌进行优化（如隐藏或降低渲染开销）。
4. **UI 层级**：卡牌系统应运行在 UI Canvas 上，确保与游戏世界中的其他元素不冲突。
5. **DOTween 安全性**：所有 DOTween 动画应在对象销毁时正确清理，避免空引用异常。
