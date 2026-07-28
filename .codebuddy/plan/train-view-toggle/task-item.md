# 实施计划

- [ ] 1. 在 CharacterCard.cs 中添加 Train 按钮引用和 TrainView 实例管理字段
   - 添加 `[SerializeField] private Button trainButton;` 字段用于引用 Train 按钮
   - 添加 `private GameObject trainViewInstance;` 字段用于跟踪当前 TrainView 实例
   - 添加 TrainView 预制体路径常量 `private const string TrainViewPrefabPath = "Prefabs/UI/View/TrainView";`
   - 添加 X 偏移量常量 `private const float TrainViewXOffset = 280f;`
   - _需求：1.1, 3.2_

- [ ] 2. 在 CharacterCard.cs 中实现 Train 按钮事件绑定
   - 在 `Awake()` 或 `Start()` 方法中通过 `transform.Find("Train")` 获取 Train 按钮引用（若 SerializeField 未赋值时作为 fallback）
   - 使用 `trainButton.onClick.AddListener(OnTrainButtonClicked)` 注册点击回调
   - _需求：3.2, 3.3_

- [ ] 3. 在 CharacterCard.cs 中实现 OnTrainButtonClicked 方法（Toggle 逻辑）
   - 判断 `trainViewInstance` 是否存在（非 null 且未被销毁）
   - 若已存在：调用 `Destroy(trainViewInstance)` 销毁实例，并将引用置为 null
   - 若不存在：通过 `Resources.Load<GameObject>(TrainViewPrefabPath)` 加载预制体
   - 加载失败时输出 `Debug.LogWarning` 并 return
   - 加载成功后使用 `Instantiate` 实例化，设置父对象为当前 CharacterCard 的 parent（同级 Canvas 层级）
   - _需求：1.1, 1.2, 2.1, 3.1, 4.1_

- [ ] 4. 实现 TrainView 实例的位置设置逻辑
   - 获取 CharacterCard 的 `RectTransform.anchoredPosition`
   - 设置 TrainView 实例的 `RectTransform.anchoredPosition` 为卡片位置 + `new Vector2(TrainViewXOffset, 0)`
   - 确保 Y 坐标与 CharacterCard 保持一致（仅 X 方向偏移 280）
   - _需求：1.1, 1.3_

- [ ] 5. 实现 CharacterCard 销毁时的清理逻辑
   - 在 `OnDestroy()` 方法中检查 `trainViewInstance` 是否存在
   - 若存在则调用 `Destroy(trainViewInstance)` 进行清理
   - 在 `OnDestroy()` 中移除按钮事件监听 `trainButton.onClick.RemoveListener(OnTrainButtonClicked)`
   - _需求：2.2, 4.2_

- [ ] 6. 验证多卡片独立管理 TrainView 实例
   - 确认每个 CharacterCard 实例各自持有独立的 `trainViewInstance` 引用
   - 确认一个卡片的 Train 按钮操作不会影响其他卡片的 TrainView 状态
   - 在 CardSplineDistributor 实例化多张卡片的场景下测试互不干扰
   - _需求：4.3_
