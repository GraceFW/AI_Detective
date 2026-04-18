# Bobo Battle UI 预制件制作指南

## 1. 当前 UI 架构已经升级到什么状态

现在的 `BoboBattlePanel` 已经不是旧版的“每个槽位里塞四个按钮”结构了，而是改成了真正适配你那张参考图的战斗面板模型：

- 左侧：全局动作栏 `ActionButtons`
- 下方：玩家三张牌位 `PlayerCardSlots`
- 上方：AI 三张牌位 `AiCardSlots`
- 左上 / 右下：血量与能量圆饼 `PlayerHpPips / PlayerEnergyPips / AiHpPips / AiEnergyPips`
- 顶部：标题、回合、角色名
- 底部或中下：提示文本、结果文本、确认按钮、重开按钮

运行入口和资源路径不变：

- 入口：`BoboBattleService`
- 面板脚本：`Assets/Scripts/MiniGames/Bobo/UI/BoboBattlePanel.cs`
- 预制件路径：`Assets/Resources/BoboBattle/BoboBattlePanel.prefab`

只要 prefab 放到这个路径，并把 `BoboBattlePanel` 的 Inspector 字段绑完整，`BoboBattleService.Open(...)` 就能直接拉起。

## 2. 推荐层级

建议你按下面这个层级来搭，和你给的草图基本是一一对应的：

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
│  │  │  ├─ Hp_1
│  │  │  ├─ Hp_2
│  │  │  └─ Hp_3
│  │  └─ PlayerEnergyPips
│  │     ├─ En_1
│  │     ├─ En_2
│  │     └─ En_3
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
│  │  │  ├─ Hp_1
│  │  │  ├─ Hp_2
│  │  │  └─ Hp_3
│  │  └─ AiEnergyPips
│  │     ├─ En_1
│  │     ├─ En_2
│  │     └─ En_3
│  ├─ ConfirmButton
│  │  └─ Label
│  ├─ RestartButton
│  ├─ StatusText
│  └─ ResultText
```

## 3. 每个区域建议怎么做

### 3.1 背景层

- `Blocker`：全屏半透明遮罩，挡住底层点击
- `Background`：全屏背景图
- `MainRoot`：真正承载战斗 UI 的根

建议：

- `Background` 用大图
- `Blocker` 用低透明深色
- 背景和立绘不要被布局组件强行挤压，尽量用手工锚点

### 3.2 玩家状态区

放左上角，按你图里的样式做成两排圆饼：

- 第一排是 HP
- 第二排是 Energy

`BoboBattlePanel` 会直接刷新这些 `Image` 的颜色亮灭，所以这里已经不需要再额外放隐藏文本兼容了。

建议：

- HP：红色系
- Energy：蓝青色系
- 未点亮状态：灰色或低透明

### 3.3 左侧动作栏

放四个纵向方形按钮：

- `Charge`
- `Guard`
- `Attack`
- `Ultimate`

每个按钮建议结构：

```text
ChargeButton
├─ Bg
├─ Icon
├─ Label
└─ SelectedFrame
```

说明：

- `Bg` 绑定到 `ActionButtonBinding.Background`
- `Icon` 绑定到 `ActionButtonBinding.IconImage`
- `Label` 绑定到 `ActionButtonBinding.Label`
- `SelectedFrame` 绑定到 `ActionButtonBinding.SelectedFrame`

当前交互方式是：

1. 点左边动作按钮
2. 再点下方某一张玩家牌位
3. 动作会被放入该牌位

如果当前已经有聚焦牌位，点左边动作也会直接尝试填入。

### 3.4 玩家牌区

放中下位置，三张牌横排。

每个牌位建议结构：

```text
PlayerCardSlot_1
├─ CardBg
├─ Highlight
├─ ActionIcon
├─ ActionText
├─ SlotIndexText
└─ Button
```

字段对应：

- `Button`：整个牌位的点击按钮
- `CardBg`：绑定到 `Background`
- `Highlight`：绑定到 `HighlightFrame`
- `ActionIcon`：绑定到 `ActionIcon`
- `ActionText`：绑定到 `ActionText`
- `SlotIndexText`：绑定到 `SlotIndexText`

当前脚本支持：

- 牌位默认显示“未放置”
- 被选中的牌位高亮
- 当前结算到的牌位高亮
- 修改前面牌位时，后面牌位会自动清空

### 3.5 AI 牌区

放右上，三张牌横排。

结构和玩家牌位类似，但通常不需要 `Button`：

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

说明：

- 未确认前，`HiddenRoot` 会显示，通常放一个 `?`
- 玩家确认后，AI 三张牌会揭示
- 结算时会按 1 -> 2 -> 3 依次高亮

### 3.6 AI 状态区

放右下，与玩家状态区对角呼应。

建议和玩家状态区保持完全一致：

- 一排 HP
- 一排 Energy

### 3.7 立绘区

建议：

- 玩家立绘放中左偏下
- AI 立绘放右下偏上
- 立绘只做纯视觉，不需要绑 `BoboBattlePanel` 字段

注意图层：

- 背景最低
- 立绘在中层
- 牌区和按钮在立绘之上
- 结果文本和关闭按钮最高

### 3.8 底部交互区

建议：

- `ConfirmButton` 放中下偏右
- `StatusText` 放按钮上方或旁边
- `ResultText` 放画面中上，便于结束时提示
- `RestartButton` 放在确认按钮附近，默认隐藏

## 4. Inspector 绑定清单

这是你在 Unity 里最需要照着绑的部分。

### 4.1 Root

- `Canvas Group`：根节点自己的 `CanvasGroup`

### 4.2 Header

- `Title Text`
- `Round Text`
- `Player Name Text`
- `Ai Name Text`

### 4.3 Status Pips

- `Player Hp Pips`：绑定 3 个 HP 圆饼 `Image`
- `Player Energy Pips`：绑定 3 个能量圆饼 `Image`
- `Ai Hp Pips`：绑定 3 个 HP 圆饼 `Image`
- `Ai Energy Pips`：绑定 3 个能量圆饼 `Image`

### 4.4 Action Palette

`Action Buttons` 数组里放 4 个元素，顺序建议就是：

1. `Charge`
2. `Guard`
3. `Attack`
4. `Ultimate`

每个元素都要绑定：

- `ActionType`
- `Button`
- `Background`
- `IconImage`
- `Label`
- `SelectedFrame`

### 4.5 Player Card Slots

`Player Card Slots` 数组必须是 3 个。

每个元素绑定：

- `Button`
- `Background`
- `HighlightFrame`
- `ActionIcon`
- `SlotIndexText`
- `ActionText`

`HiddenRoot / HiddenText` 对玩家牌位通常可以留空。

### 4.6 AI Card Slots

`Ai Card Slots` 数组必须是 3 个。

每个元素绑定：

- `Background`
- `HighlightFrame`
- `ActionIcon`
- `SlotIndexText`
- `ActionText`
- `HiddenRoot`
- `HiddenText`

`Button` 对 AI 牌位可以不绑。

### 4.7 Action Visuals

如果你有动作图标，就在 `Action Visuals` 里加 4 个映射：

- `Charge -> sprite`
- `Guard -> sprite`
- `Attack -> sprite`
- `Ultimate -> sprite`

这样左侧动作栏和牌面 `ActionIcon` 都能自动复用。

如果你暂时没有 sprite，也没关系：

- 左侧按钮会回退显示文字
- 牌位也可以只显示 `ActionText`

### 4.8 Footer

- `Status Text`
- `Result Text`
- `Submit Button`
- `Submit Button Text`
- `Restart Button`
- `Close Button`

## 5. 推荐布局方式

这一版 UI 不建议用一个总的 `VerticalLayoutGroup` 自动排满全屏。

更合适的是：

- `MainRoot` 用手工锚点布局
- 各个局部区域再用布局组件

推荐用布局组件的地方：

- HP 圆饼一排：`HorizontalLayoutGroup`
- Energy 圆饼一排：`HorizontalLayoutGroup`
- 左侧动作栏：`VerticalLayoutGroup`
- 玩家牌区：`HorizontalLayoutGroup`
- AI 牌区：`HorizontalLayoutGroup`

推荐手工锚点的地方：

- `LeftTopStatus`
- `LeftActionColumn`
- `PlayerPortrait`
- `EnemyPortrait`
- `RightBottomStatus`
- `ConfirmButton`
- `StatusText`
- `ResultText`

## 6. 字体和颜色建议

字体统一使用：

`Msyh Fin (TMP_Font Asset)`

推荐字号：

- 标题：28~32
- 回合：22~26
- 角色名：18~22
- 按钮文字：20~24
- 卡面动作文字：20~24
- 提示文字：18~22

推荐配色：

- 玩家阵营：蓝 / 青
- AI 阵营：橙 / 红
- 确认按钮：亮绿
- 未激活圆饼：半透明灰白

## 7. 实际制作步骤

### 第一步

新建 UI 根节点 `BoboBattlePanel`，挂：

- `RectTransform`
- `CanvasGroup`
- `BoboBattlePanel`

### 第二步

创建：

- `Blocker`
- `Background`
- `MainRoot`

都按全屏或主区域铺开。

### 第三步

按层级创建：

- `TopBar`
- `LeftTopStatus`
- `LeftActionColumn`
- `PlayerPortrait`
- `EnemyCardArea`
- `PlayerCardArea`
- `EnemyPortrait`
- `RightBottomStatus`
- `ConfirmButton`
- `RestartButton`
- `StatusText`
- `ResultText`

### 第四步

做圆饼：

- 左上两排 3 + 3
- 右下两排 3 + 3

### 第五步

做左侧动作按钮 4 个。

### 第六步

做玩家三牌位和 AI 三牌位。

### 第七步

摆立绘和背景。

### 第八步

回到 `BoboBattlePanel` Inspector，把字段按第 4 节全部绑完。

### 第九步

把 prefab 保存到：

`Assets/Resources/BoboBattle/BoboBattlePanel.prefab`

## 8. 现在这套 UI 的交互逻辑

你后面调试时可以按这个顺序验证：

1. 打开小游戏
2. 左上和右下圆饼显示初始状态
3. 点击左侧动作按钮
4. 点击下方玩家牌位，动作进入对应牌位
5. 填满三张玩家牌后，点击“确定”
6. 右上 AI 牌位揭示
7. 按 1 -> 2 -> 3 逐张结算
8. 圆饼随结算变化
9. 对局结束后显示 `ResultText` 和 `RestartButton`

## 9. 最后给你的工程建议

现在这份代码和这份文档已经是同一套模型了，所以你可以直接开始做 prefab。

最稳的做法是：

- 先不追求一步到位的美术精修
- 先按绑定结构把一个可运行 prefab 搭出来
- 跑通交互和结算
- 再逐步替换背景、立绘、按钮图、卡槽边框、动作 sprite

这样节奏最稳，也最不容易在 UI 细节里卡很久。
