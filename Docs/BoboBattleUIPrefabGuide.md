# Bobo Battle UI 预制件配置指南

## 1. 这次更新了什么

当前这版 `BoboBattlePanel` 的 Tooltip 机制已经改成两件事：

- Tooltip 文案支持在 Inspector 里可视化配置
- Tooltip 位置跟随被悬浮的 UI 元素，而不是跟随鼠标

这意味着你现在可以直接在 prefab 上配置：

- 每个行动的提示标题与正文
- 玩家空槽位可编辑时的提示
- 玩家空槽位锁定时的提示
- AI 未揭示槽位的提示
- 不同来源 Tooltip 出现在目标 UI 的哪一侧

## 2. 对应脚本

- 面板主脚本：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattlePanel.cs`
- 悬浮代理：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattleHoverTarget.cs`
- 拖拽源：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattleDragActionItem.cs`
- 拖拽落点：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattleCardDropSlot.cs`

## 3. Tooltip 相关字段

在 `BoboBattlePanel` 组件上，你现在会看到新的 Tooltip 配置区。

### 3.1 基础引用

这些引用还是必须绑定：

- `Tooltip Root`
- `Tooltip Canvas Group`
- `Tooltip Title Text`
- `Tooltip Body Text`

Tooltip 的宽高自适应现在由代码自动接入 `TooltipAutoSize` 完成。
如果 `TooltipRoot` 上没有这个组件，`BoboBattlePanel` 会在运行时自动补上并配置，不需要你手工添加。

建议结构：

```text
TooltipRoot
├─ TooltipTitleText
└─ TooltipBodyText
```

建议组件：

- `TooltipRoot`：`RectTransform + Image + CanvasGroup`
- `TooltipTitleText`：`TextMeshProUGUI`
- `TooltipBodyText`：`TextMeshProUGUI`

建议：

- `TooltipRoot` 默认隐藏
- `TooltipRoot` 不要放进会自动挤压位置的布局组
- Tooltip 背景图 `Raycast Target` 关闭

### 3.2 Action Tooltip Contents

这是行动提示的可视化配置数组。

你需要给 4 个行动各配一条：

1. `Charge`
2. `Guard`
3. `Attack`
4. `Ultimate`

每条包含：

- `ActionType`
- `Content.Title`
- `Content.Body`

说明：

- 左侧行动按钮悬浮时，直接用这里的标题和正文
- 玩家牌槽和 AI 牌槽里如果已经有牌，也复用这里的对应行动提示

所以这里就是整套小游戏“行动说明文案”的主配置表。

### 3.3 AI Action Tooltip Contents

这是敌人牌型专用的行动提示配置数组。

用途：

- 只在 `AI` 牌槽悬浮时使用
- 会覆盖默认的 `Action Tooltip Contents`

建议同样配置 4 条：

1. `Charge`
2. `Guard`
3. `Attack`
4. `Ultimate`

每条包含：

- `ActionType`
- `Content.Title`
- `Content.Body`

回退规则：

- 如果这里配置了某个动作，就优先显示这里的标题和正文
- 如果这里没配该动作，则自动回退到 `Action Tooltip Contents`

### 3.4 玩家空槽 Tooltip

你还会看到两个单独配置：

- `Player Editable Empty Slot Tooltip`
- `Player Locked Empty Slot Tooltip`

用途：

- 玩家槽位为空且当前可编辑时，用前者
- 玩家槽位为空但因为顺序未解锁时，用后者

每个都可以配置：

- `Title`
- `Body`

### 3.5 AI 隐藏槽 Tooltip

字段：

- `Ai Hidden Slot Tooltip`

用途：

- AI 槽位还没揭示时，悬浮显示这里的内容

### 3.6 Tooltip Placements

这是本次位置逻辑最关键的新数组。

你可以按来源分别配置 Tooltip 出现在哪一侧。

推荐配置 3 条：

1. `SourceType = ActionPalette`
2. `SourceType = PlayerSlot`
3. `SourceType = AiSlot`

每条包含：

- `SourceType`
- `Placement`
- `Offset`

`Placement` 可选：

- `Right`
- `Left`
- `Above`
- `Below`

建议初始值：

- `ActionPalette`：`Right`
- `PlayerSlot`：`Above`
- `AiSlot`：`Left` 或 `Below`

`Offset` 是在目标 UI 锚点基础上的微调。
比如：

- 右侧弹出可先试 `(24, 0)`
- 上方弹出可先试 `(0, 18)`
- 左侧弹出可先试 `(-24, 0)`

## 4. Tooltip 现在的工作方式

### 4.1 左侧行动按钮

悬浮时：

- 定位基于按钮自身的 `RectTransform`
- 文案来自 `Action Tooltip Contents`
- 位置来自 `Tooltip Placements` 中的 `ActionPalette`

### 4.2 玩家牌槽

悬浮时：

- 如果槽里已有行动牌，文案复用对应行动的 Tooltip 配置
- 如果槽是空的，则根据“可编辑 / 锁定”状态选择空槽 Tooltip
- 位置来自 `Tooltip Placements` 中的 `PlayerSlot`

### 4.3 AI 牌槽

悬浮时：

- 如果槽位尚未揭示，显示 `Ai Hidden Slot Tooltip`
- 如果已揭示，复用对应行动的 Tooltip 配置
- 位置来自 `Tooltip Placements` 中的 `AiSlot`

## 5. 为什么现在不跟随鼠标了

当前实现已经改成“跟随目标 UI”：

- Tooltip 的锚点取自被悬浮 UI 的矩形边缘
- 然后根据 `Placement` 决定挂在左、右、上、下哪一侧
- 最后再叠加你配置的 `Offset`

所以现在 Tooltip 会稳定贴着行动按钮或卡槽出现，不会随着鼠标抖动。

## 6. 你在 Unity 里应该怎么配

### 第一步

确认 `BoboBattlePanel.prefab` 上已经绑定：

- `Tooltip Root`
- `Tooltip Canvas Group`
- `Tooltip Title Text`
- `Tooltip Body Text`

### 第二步

展开 `Action Tooltip Contents`，填满 4 条行动说明。

建议至少先填：

- 标题：行动名
- 正文：规则说明

### 第三步

填写：

- `Player Editable Empty Slot Tooltip`
- `Player Locked Empty Slot Tooltip`
- `Ai Hidden Slot Tooltip`

### 第四步

展开 `Tooltip Placements`，新增 3 条。

推荐先这样配：

1. `ActionPalette / Right / (24, 0)`
2. `PlayerSlot / Above / (0, 18)`
3. `AiSlot / Left / (-24, 0)`

### 第五步

进 Play Mode 逐个试：

- 左侧行动按钮悬浮
- 玩家空槽悬浮
- 玩家已放牌槽位悬浮
- AI 未揭示槽位悬浮
- AI 已揭示槽位悬浮

## 7. 其他 UI 绑定不变

这次没有改动你原本这些核心绑定方式：

- `Action Buttons`
- `Player Card Slots`
- `Ai Card Slots`
- `Action Visuals`
- `PlayerHpPips / PlayerEnergyPips / AiHpPips / AiEnergyPips`

拖拽逻辑也不需要你手动给按钮逐个挂脚本，运行时会自动补：

- `BoboBattleHoverTarget`
- `BoboBattleDragActionItem`
- `BoboBattleCardDropSlot`

## 8. 推荐的自测顺序

1. 左侧四个行动按钮分别悬浮，确认内容取自 Inspector 配置。
2. Tooltip 是否固定贴着按钮右侧，而不是跟着鼠标跑。
3. 玩家第 1 槽为空时悬浮，确认显示“可编辑空槽”配置。
4. 玩家第 2 或第 3 槽在未解锁时悬浮，确认显示“锁定空槽”配置。
5. 把行动拖进玩家槽位后再次悬浮，确认显示对应行动配置。
6. AI 牌未揭示时悬浮，确认显示隐藏提示。
7. 确认回合后 AI 牌揭示，再悬浮检查是否改为对应行动提示。

## 9. 这次你最需要补的就是这些

- 给 `TooltipRoot` 绑定完整
- 在 `Action Tooltip Contents` 里配齐 4 个行动
- 配置 3 个状态 Tooltip
- 配置 `Tooltip Placements`

如果你愿意，我下一步可以继续帮你把这 4 个行动的 Tooltip 文案直接整理成一份适合放进 Inspector 的中文版配置稿，你只要照着填就行。 
