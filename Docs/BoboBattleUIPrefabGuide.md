# Bobo Battle UI 预制件配置指南

## 1. 当前这版 UI 支持什么

当前 `BoboBattlePanel` 已经支持下面这几类交互：

- 左侧行动按钮点击选牌
- 左侧行动按钮拖拽到玩家牌槽
- 左侧行动按钮悬停提示
- 玩家已放入牌槽的行动悬停提示
- AI 牌槽悬停提示
- 每回合结算后，玩家牌区和 AI 牌区同步重置显示

对应脚本入口：

- 面板主脚本：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattlePanel.cs`
- 悬停提示代理：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattleHoverTarget.cs`
- 行动拖拽源：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattleDragActionItem.cs`
- 玩家牌槽拖拽落点：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattleCardDropSlot.cs`

注意：

- `BoboBattleHoverTarget`
- `BoboBattleDragActionItem`
- `BoboBattleCardDropSlot`

这三个辅助组件会由 `BoboBattlePanel` 在运行时自动补到对应 UI 节点上。
也就是说，你通常不需要手动给按钮或卡槽一个个挂这些脚本。

## 2. 预制件路径

预制件仍然放在：

`Assets/Resources/BoboBattle/BoboBattlePanel.prefab`

小游戏服务仍通过：

`BoboBattleService.Open(...)`

去加载并显示这个 prefab。

## 3. 推荐层级

推荐继续保持这类层级：

```text
BoboBattlePanel
├─ Blocker
├─ Background
├─ MainRoot
│  ├─ TopBar
│  │  ├─ TitleText
│  │  ├─ RoundText
│  │  ├─ PlayerNameText
│  │  ├─ AiNameText
│  │  └─ CloseButton
│  ├─ LeftTopStatus
│  │  ├─ PlayerHpPips
│  │  └─ PlayerEnergyPips
│  ├─ LeftActionColumn
│  │  ├─ ChargeButton
│  │  ├─ GuardButton
│  │  ├─ AttackButton
│  │  └─ UltimateButton
│  ├─ PlayerPortrait
│  ├─ EnemyCardArea
│  │  ├─ EnemyCardSlot_1
│  │  ├─ EnemyCardSlot_2
│  │  └─ EnemyCardSlot_3
│  ├─ PlayerCardArea
│  │  ├─ PlayerCardSlot_1
│  │  ├─ PlayerCardSlot_2
│  │  └─ PlayerCardSlot_3
│  ├─ EnemyPortrait
│  ├─ RightBottomStatus
│  │  ├─ AiHpPips
│  │  └─ AiEnergyPips
│  ├─ ConfirmButton
│  ├─ RestartButton
│  ├─ StatusText
│  └─ ResultText
└─ TooltipRoot
   ├─ TooltipTitleText
   └─ TooltipBodyText
```

## 4. 你现在必须新增的 UI 节点

相比上一版 prefab，这次建议你新增一个 tooltip 组：

### 4.1 TooltipRoot

新建一个悬浮提示根节点，例如：

- `TooltipRoot`

建议组件：

- `RectTransform`
- `Image`
- `CanvasGroup`

建议设置：

- 默认 `SetActive(false)`
- 锚点建议居中或左上都可以
- 尺寸建议先做成 `320 x 140` 左右
- 背景图可以是深色半透明底
- `Raycast Target` 建议关闭，避免挡住下面的按钮事件

### 4.2 TooltipTitleText

挂在 `TooltipRoot` 下：

- `TextMeshProUGUI`

用途：

- 显示动作名或槽位标题

### 4.3 TooltipBodyText

挂在 `TooltipRoot` 下：

- `TextMeshProUGUI`

用途：

- 显示动作说明或槽位状态说明

## 5. 现有预制件需要保证的组件

### 5.1 根节点

`BoboBattlePanel` 根节点需要：

- `RectTransform`
- `CanvasGroup`
- `BoboBattlePanel`

并且它应当处于某个 `Canvas` 之下。

### 5.2 左侧行动按钮

每个行动按钮至少需要：

- `Button`
- `Image`

推荐按钮子节点结构：

```text
ChargeButton
├─ Bg
├─ Icon
├─ Label
└─ SelectedFrame
```

对应 `ActionButtonBinding` 字段：

- `ActionType`
- `Button`
- `Background`
- `IconImage`
- `Label`
- `SelectedFrame`

### 5.3 玩家牌槽

每个玩家牌槽必须有一个可点击对象，通常直接整张卡挂 `Button`。

推荐结构：

```text
PlayerCardSlot_1
├─ CardBg
├─ Highlight
├─ ActionIcon
├─ ActionText
└─ SlotIndexText
```

对应 `CardSlotBinding` 字段：

- `Button`
- `Background`
- `HighlightFrame`
- `ActionIcon`
- `SlotIndexText`
- `ActionText`

玩家牌槽不需要 `HiddenRoot / HiddenText`。

### 5.4 AI 牌槽

推荐结构：

```text
EnemyCardSlot_1
├─ CardBg
├─ Highlight
├─ HiddenRoot
│  └─ HiddenText
├─ ActionIcon
├─ ActionText
└─ SlotIndexText
```

对应 `CardSlotBinding` 字段：

- `Background`
- `HighlightFrame`
- `ActionIcon`
- `SlotIndexText`
- `ActionText`
- `HiddenRoot`
- `HiddenText`

AI 牌槽的 `Button` 可不绑。
如果你也给 AI 卡槽挂了 `Button`，也没问题，悬停仍可工作。

## 6. BoboBattlePanel Inspector 绑定清单

下面这些是你需要在 `BoboBattlePanel` 组件上确认的字段。

### 6.1 Root

- `Canvas Group`

### 6.2 Header

- `Title Text`
- `Round Text`
- `Player Name Text`
- `Ai Name Text`

### 6.3 Status Pips

- `Player Hp Pips`
- `Player Energy Pips`
- `Ai Hp Pips`
- `Ai Energy Pips`

每组都应该是 3 个 `Image`，并且顺序要正确。
尤其要确认玩家能量豆中间那个引用不要再次绑到第一个豆，否则会出现“中间永远亮”的问题。

### 6.4 Action Palette

`Action Buttons` 数组固定 4 个，建议顺序：

1. `Charge`
2. `Guard`
3. `Attack`
4. `Ultimate`

### 6.5 Player Card Slots

`Player Card Slots` 数组固定 3 个，顺序必须和画面从左到右一致。

### 6.6 AI Card Slots

`Ai Card Slots` 数组固定 3 个，顺序必须和画面从左到右一致。

### 6.7 Action Visuals

如果玩家和 AI 使用不同图：

- 为玩家图标配一组 `owner = Player`
- 为 AI 图标配一组 `owner = AI`

如果某动作只有一张通用图：

- 可以使用 `owner = Shared`

### 6.8 Footer

- `Status Text`
- `Result Text`
- `Submit Button`
- `Submit Button Text`
- `Restart Button`
- `Close Button`

### 6.9 Tooltip

这是这次新增的重点字段：

- `Tooltip Root` 绑定到 `TooltipRoot`
- `Tooltip Canvas Group` 绑定到 `TooltipRoot` 的 `CanvasGroup`
- `Tooltip Title Text` 绑定到标题 TMP
- `Tooltip Body Text` 绑定到正文 TMP
- `Tooltip Offset` 视你的 UI 调整，默认可先用 `(26, -18)`

## 7. 拖拽功能的工作方式

这次拖拽没有直接复用线索系统那套业务耦合逻辑，而是为 Bobo 单独做了轻量实现。

原因很简单：

- 线索拖拽和 `ClueData`、特定投放目标接口耦合较深
- Bobo 只需要“动作源 -> 卡槽”这类单一交互
- 单独做一个轻量层，侵入更小，也更容易维护

现在的拖拽链路是：

1. 左侧行动按钮运行时自动挂上 `BoboBattleDragActionItem`
2. 玩家牌槽运行时自动挂上 `BoboBattleCardDropSlot`
3. 当你把左侧行动拖到玩家卡槽上时，面板会直接给该槽位写入对应 `ActionType`
4. 之后仍会走原有的能量校验、顺序校验和后续槽位清空逻辑

所以你在 prefab 侧需要保证的只有两件事：

- 左侧行动按钮是可响应事件的 `Button`
- 玩家卡槽是可响应事件的 `Button`

## 8. 悬浮提示的工作方式

悬浮提示现在覆盖三类对象：

### 8.1 左侧行动按钮

显示内容：

- 行动名
- 该行动的规则说明

### 8.2 玩家牌槽

如果该槽已经放牌：

- 显示槽位标题
- 显示当前行动的规则说明

如果该槽为空：

- 显示该槽是否可编辑
- 提示可点击或拖拽放牌

### 8.3 AI 牌槽

如果尚未揭示：

- 提示该牌仍然隐藏

如果已经揭示：

- 显示 AI 当前行动说明

## 9. 推荐布局细节

### 9.1 左侧行动列

- 使用 `VerticalLayoutGroup`
- 四个按钮尺寸尽量一致
- `Spacing` 建议 16 到 24

### 9.2 玩家牌区与 AI 牌区

- 使用 `HorizontalLayoutGroup`
- 3 个卡槽等宽
- `Spacing` 建议 18 到 28

### 9.3 状态圆饼

- HP 一排 3 个
- Energy 一排 3 个
- 各自使用 `HorizontalLayoutGroup`

### 9.4 TooltipRoot

建议不要放进会自动重新排版的位置组里。
最好作为根节点下独立浮层，由脚本直接改 `anchoredPosition`。

## 10. 制作完成后的自测顺序

你可以按这个顺序快速验收：

1. 打开小游戏面板
2. 悬停左侧四个行动按钮，确认都能弹出不同提示
3. 把任意行动拖到玩家第 1 槽，确认成功放入
4. 再拖到第 2、3 槽，确认按顺序可放
5. 悬停玩家已放入的牌，确认提示与动作一致
6. 悬停 AI 牌槽，未揭示时应提示“隐藏中”
7. 点击确认后，AI 三张牌揭示
8. 回合结算完毕后，确认玩家牌槽和 AI 牌槽一起回到空状态
9. 再次进入下一回合，确认拖拽与悬停仍然正常

## 11. 这次你需要实际新增或确认的东西

如果你已经有旧 prefab，这次最关键的改动只有这些：

- 新增 `TooltipRoot`
- 在 `TooltipRoot` 下新增 `TooltipTitleText`
- 在 `TooltipRoot` 下新增 `TooltipBodyText`
- 给 `TooltipRoot` 挂 `CanvasGroup`
- 回到 `BoboBattlePanel` Inspector，把 Tooltip 区域的 4 个引用绑上
- 检查所有玩家卡槽都绑定了 `Button`
- 再检查玩家能量豆数组顺序是否正确

除此之外，拖拽和悬停辅助脚本会在运行时自动补齐，不要求你手工挂满。
