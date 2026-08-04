# 需求文档

## 引言

本功能为 TrainView 面板中 `LevelSystem` 节点下的三个文本组件实现数据填充与动态更新。`LevelSystem` 是 TrainView 预制体中的一个子面板，包含三个 Text (Legacy) 子节点：`Level`、`TalentPoint` 和 `Experience`，分别用于显示角色当前等级、可用天赋点数和经验值进度。

### 相关资源
- **TrainView 预制体路径**：`Resources/Prefabs/UI/View/TrainView`
- **TrainViewController 脚本**：`Assets/Scripts/CardSelection/TrainViewController.cs`
- **CultivationData 脚本**：`Assets/Scripts/Cultivation/CultivationData.cs`（包含 `level`、`currentExp`、`talentPoints` 字段和 `GetRequiredExp()` 方法）

### 预制体层级结构
```
TrainView
└── LevelSystem
    ├── Level          (Text Legacy, 默认文本: "等级：")
    ├── TalentPoint    (Text Legacy, 默认文本: "天赋点：")
    └── Experience     (Text Legacy)
```

### 文本格式说明
| 节点名 | 显示格式 | 数据来源 |
|---|---|---|
| Level | `"等级：X"` | X = CultivationData.level |
| TalentPoint | `"天赋点：X"` | X = CultivationData.GetAvailableTalentPoints()（即当前可用天赋点） |
| Experience | `"A / B"` | A = CultivationData.currentExp，B = CultivationData.GetRequiredExp() |

## 需求

### 需求 1：Level 文本显示当前角色等级

**用户故事：** 作为一名玩家，我希望在 TrainView 面板中看到角色当前的等级信息，以便我了解角色的成长进度。

#### 验收标准

1. WHEN TrainView 面板被实例化并显示 THEN 系统 SHALL 在 LevelSystem/Level 节点的 Text 组件中显示格式为 `"等级：X"` 的文本，其中 X 为 CultivationData.level 的值
2. WHEN 角色等级为 1（初始等级） THEN 系统 SHALL 显示 `"等级：1"`
3. WHEN 角色升级后重新打开 TrainView THEN 系统 SHALL 显示更新后的等级值

### 需求 2：TalentPoint 文本显示当前可用天赋点

**用户故事：** 作为一名玩家，我希望在 TrainView 面板中看到当前可用的天赋点数量，以便我知道还有多少天赋点可以分配。

#### 验收标准

1. WHEN TrainView 面板被实例化并显示 THEN 系统 SHALL 在 LevelSystem/TalentPoint 节点的 Text 组件中显示格式为 `"天赋点：X"` 的文本，其中 X 为当前可用天赋点数
2. WHEN 玩家通过 TalentSystem 面板点击 PlusButton 分配天赋点（临时） THEN 系统 SHALL 实时更新 TalentPoint 文本，显示减少后的可用天赋点数
3. WHEN 玩家通过 TalentSystem 面板点击 MinusButton 回收天赋点（临时） THEN 系统 SHALL 实时更新 TalentPoint 文本，显示增加后的可用天赋点数
4. WHEN 玩家点击 SquareFrame 确认天赋分配后 THEN 系统 SHALL 更新 TalentPoint 文本为确认后的最终可用天赋点数
5. WHEN 角色初始等级为 1 且未分配任何天赋点 THEN 系统 SHALL 显示 `"天赋点：0"`

### 需求 3：Experience 文本显示经验值进度

**用户故事：** 作为一名玩家，我希望在 TrainView 面板中看到角色当前的经验值和升级所需的最大经验值，以便我了解距离下一次升级还需要多少经验。

#### 验收标准

1. WHEN TrainView 面板被实例化并显示 THEN 系统 SHALL 在 LevelSystem/Experience 节点的 Text 组件中显示格式为 `"A / B"` 的文本，其中 A 为 CultivationData.currentExp，B 为 CultivationData.GetRequiredExp()
2. WHEN 角色初始状态（等级 1，经验 0） THEN 系统 SHALL 显示 `"0 / B"`，其中 B 为等级 1 升级所需经验值
3. WHEN 角色获得经验值后重新打开 TrainView THEN 系统 SHALL 显示更新后的经验值进度

### 需求 4：LevelSystem 文本与天赋系统联动

**用户故事：** 作为一名玩家，我希望 LevelSystem 面板中的天赋点文本能与 TalentSystem 面板的操作实时联动，以便我在分配天赋点时能直观看到剩余可用点数的变化。

#### 验收标准

1. WHEN 玩家在任意 TalentSystem 面板中点击 PlusButton THEN 系统 SHALL 将 TalentPoint 文本中的可用天赋点数减 1
2. WHEN 玩家在任意 TalentSystem 面板中点击 MinusButton THEN 系统 SHALL 将 TalentPoint 文本中的可用天赋点数加 1
3. WHEN 玩家点击 SquareFrame 确认天赋分配 THEN 系统 SHALL 确保 TalentPoint 文本反映最终确认后的可用天赋点数
4. IF 可用天赋点为 0 THEN TalentPoint 文本 SHALL 显示 `"天赋点：0"`

### 需求 5：边界情况处理

**用户故事：** 作为一名玩家，我希望在各种边界情况下 LevelSystem 文本都能正确显示，以便不会出现异常或误导性信息。

#### 验收标准

1. IF CultivationData 为 null 或未正确初始化 THEN 系统 SHALL 显示默认值（等级：1，天赋点：0，经验：0 / 100）
2. IF 角色已达到最大等级 THEN 系统 SHALL 正常显示当前等级和经验值（经验值可能被封顶）
3. WHEN TrainView 面板初始化时 THEN 系统 SHALL 确保 LevelSystem 节点下的所有 Text 组件都能被正确查找到（使用 `transform.Find` 精确查找子节点名称）
4. WHEN 天赋点数值变化 THEN 系统 SHALL 确保显示的数值始终 ≥ 0
