# 实施计划

- [ ] 1. 创建 TrainViewController 脚本文件
   - 在 `Assets/Scripts/CardSelection/` 目录下新建 `TrainViewController.cs` 脚本
   - 定义类继承 MonoBehaviour，添加必要的 using 引用（UnityEngine、UnityEngine.UI）
   - _需求：4.1_

- [ ] 2. 实现 Property 与 TalentSystem 的映射和初始化逻辑
   - 定义六个 Property 名称数组：`["Attack", "Defense", "Health", "Agility", "AttackSpeed", "CD"]`
   - 在 Awake/Start 中通过 Transform.Find 查找每个 Property 节点及其子物体 TalentSystem（命名规则：`{PropertyName}TalentSystem`）
   - 将所有找到的 TalentSystem 设置为 SetActive(false)
   - 如果某个 Property 或 TalentSystem 找不到，输出 Debug.LogWarning 警告
   - _需求：1.1、1.2、4.1、4.2_

- [ ] 3. 实现 Property 按钮点击事件绑定
   - 遍历所有 Property 节点，获取其 Button 组件
   - 为每个 Button 的 onClick 事件添加监听器，回调方法传入对应的 TalentSystem 引用
   - _需求：2.1~2.6_

- [ ] 4. 实现 TalentSystem 切换显示逻辑
   - 维护一个 `currentActiveTalentSystem` 变量记录当前激活的 TalentSystem GameObject
   - 点击回调中：如果 currentActiveTalentSystem 不为 null 且不是当前点击的目标，则将其 SetActive(false)
   - 将新点击的 TalentSystem SetActive(true)，并更新 currentActiveTalentSystem 引用
   - 如果点击的是当前已激活的同一个 Property，保持不变不做操作
   - _需求：3.1、3.2、3.3_

- [ ] 5. 将 TrainViewController 脚本挂载到 TrainView prefab
   - 在 TrainView.prefab 的根节点上添加 TrainViewController 组件（通过 Unity Editor 手动操作，或在 CharacterCard.cs 实例化 TrainView 后通过代码 AddComponent）
   - 确保脚本在 TrainView 实例化后能正确执行初始化
   - _需求：1.1、2.1~2.6、3.1~3.3_
