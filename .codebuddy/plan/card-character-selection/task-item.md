# 实施计划：卡牌角色选择系统

- [ ] 1. 扩展 CharacterData — 新增角色简介字段
   - 在 `Assets/Scripts/Data/CharacterData.cs` 的 Basic Info 区域下方新增 `[TextArea] public string characterDescription` 字段
   - 确保默认值为空字符串，保持与已有 `.asset` 文件的向后兼容
   - _需求：1.1、1.3_

- [ ] 2. 创建 CardSelectionSettings 全局配置 ScriptableObject
   - 新建 `Assets/Scripts/CardSelection/CardSelectionSettings.cs`，继承 ScriptableObject
   - 包含可配置字段：焦点缩放比例（默认 1.0）、非焦点缩放比例（默认 0.7）、DOTween 吸附动画持续时间（默认 0.3s）、缓动曲线（默认 Ease.OutCubic）、惯性衰减系数、卡牌间距（t 值间隔）
   - 在 `Assets/DATA/Setting/` 目录下创建对应的 `.asset` 资产文件（或提供 CreateAssetMenu 菜单供用户手动创建）
   - _需求：6.1、6.2、6.3_

- [ ] 3. 编写 CharacterCard 数据绑定脚本
   - 新建 `Assets/Scripts/CardSelection/CharacterCard.cs`（MonoBehaviour）
   - 通过 `[SerializeField]` 暴露 UI 引用字段：角色原画 Image、角色名称 Text、攻击力 Text、生命值 Text、防御力 Text、角色简介 Text
   - 实现 `SetData(CharacterData data)` 公共方法，将 CharacterData 的数据填充到各 UI 元素
   - 处理 null 数据的默认/空白状态；简介为空时显示占位文本"它还很神秘哦~"
   - _需求：1.2、2.2、2.3、2.4_

- [ ] 4. 实现 CardSplineDistributor — 卡牌沿 Spline 分布管理器
   - 新建 `Assets/Scripts/CardSelection/CardSplineDistributor.cs`（MonoBehaviour）
   - 引用 Unity Splines 的 `SplineContainer`，以及卡牌预制体引用和 `CharacterData[]` 列表
   - 在初始化时根据 CharacterData 列表实例化卡牌预制体，沿 Spline 等间距分布（使用 `SplineUtility.EvaluatePosition` 计算位置）
   - 提供 `UpdateCardPositions(float offset)` 方法，根据全局偏移量 t 重新计算所有卡牌在 Spline 上的位置
   - 处理卡牌数量为 0 或 1 的边界情况
   - _需求：3.1、3.2、3.3、3.4_

- [ ] 5. 实现焦点卡牌展示逻辑（缩放与层级）
   - 在 `CardSplineDistributor` 中（或新建辅助类）实现焦点判定：距离 Spline 中间位置（t=0.5）最近的卡牌为焦点卡牌
   - 焦点卡牌使用 `CardSelectionSettings` 中的焦点缩放比例，非焦点卡牌使用非焦点缩放比例
   - 根据卡牌距焦点的远近动态调整 Canvas sorting order / sibling index，越近层级越高
   - 焦点切换时使用 DOTween 实现平滑的缩放过渡动画
   - _需求：4.1、4.2、4.3、4.4_

- [ ] 6. 实现鼠标拖动交互 — 卡牌沿 Spline 滑动
   - 新建 `Assets/Scripts/CardSelection/CardDragHandler.cs`（MonoBehaviour），挂载在卡牌区域的透明遮罩或 Canvas 上
   - 监听鼠标按下、拖动、释放事件，将鼠标水平位移转换为 Spline 参数 t 的偏移量
   - 拖动过程中实时调用 `CardSplineDistributor.UpdateCardPositions()` 更新所有卡牌位置
   - 实现 Spline 边界限制，防止卡牌滑出 t=0 ~ t=1 范围
   - _需求：5.1、5.4_

- [ ] 7. 实现惯性滑动与吸附动画
   - 在 `CardDragHandler` 中记录拖动速度（最近几帧的平均速度）
   - 鼠标释放后，根据拖动速度计算惯性滑动距离，使用 `CardSelectionSettings` 中的衰减系数
   - 惯性结束后使用 DOTween 将最近的卡牌吸附到焦点位置（Snap 动画），缓动曲线和持续时间从全局配置读取
   - 吸附完成后触发焦点卡牌的翻牌展示动画（缩放 + 层级切换）
   - _需求：5.2、5.3、5.5、5.6_

- [ ] 8. DOTween 安全清理与性能优化
   - 在所有使用 DOTween 的脚本中，于 `OnDestroy()` 中调用 `DOTween.Kill(target)` 清理动画，避免空引用
   - 对超出摄像机可视范围的卡牌进行隐藏或禁用渲染优化
   - 验证卡牌数量为 0、1、多张时系统的正确行为
   - _需求：边界情况 1、3、5_
