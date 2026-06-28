# SO 数据双向导入 Skill

## 概述
本 Skill 包含三项经过验证的工作能力：
1. **线索导入**：读取 xlsx 线索表，将内容写回对应的 ClueData SO 文件
2. **对话导入**：读取 xlsx 对话表，将内容写回 PersonClueData（NPC对话）和 OptionDialogueDB（主角插播对话）
3. **章节对话导入**：读取 xlsx 章节对话表，将内容写回 DialogueData SO 文件

---

## Skill 1：线索表 → SO 文件

### 适用场景
已有 `Case0_线索表.xlsx`（或同格式的其他案件线索表），需要将表格中修改过的字段同步回 Unity 的 `.asset` 文件。

### 表格格式约定
列顺序与导出时一致：

| 列 | 字段名 | 说明 |
|----|--------|------|
| A | 编号 (id) | 线索唯一ID，用于定位 SO 文件 |
| B | 显示名称 (displayName) | |
| C | 类型 (type) | Normal / Person / Camera |
| D | 可探测 | 是/否 |
| E | 可收集 | 是/否 |
| F | 已收集 | 是/否 |
| G | AttackKey | |
| H | 已解锁Attack内容 | 是/否 |
| I | 直接展示Attack内容 | 是/否 |
| J | Attack解锁内容 (attackUnlockContent) | 支持换行 |
| K | 摘要 (summary) | 支持换行 |
| L | 详细内容 (detailText) | 支持换行 |
| M | 富文本内容 (Detail_Mark) | 支持换行 |
| N | 可传唤 (Person专属) | |
| O | 有头像 (Person专属) | |
| P | 时间帧数量 (Camera专属) | |
| Q | 资产路径 (assetPath) | 用于精确定位 SO 文件 |

> **定位规则**：优先用 Q 列的 assetPath 精确定位文件；若 assetPath 为空，则用 C 列（类型）+ A 列（id）在 `Assets/DataSO/Clues/` 下搜索。

### 执行步骤

```
用户提供 xlsx 文件路径后，执行以下步骤：

1. 用 openpyxl 读取 xlsx，跳过第一行（列头）
2. 对每一数据行：
   a. 从 Q 列读取 assetPath，直接打开对应 .asset 文件
      若 assetPath 为空，则在 Assets/DataSO/Clues/ 下按 type/id 查找
   b. 读取 .asset 文件内容（UTF-8）
   c. 将需要修改的字段用正则替换（保持 \uXXXX 转义格式）：
      - summary, detailText, Detail_Mark, attackUnlockContent：多行内容需先转义再替换
      - detectable/collectable/collected/isAttackContentUnlocked/showAttackContentDirectly：是→1，否→0
      - attackKey：直接替换字符串
   d. 将修改后内容写回原 .asset 文件
3. 输出修改报告（哪些文件被修改了哪些字段）
```

### Unicode 转义规则
Unity YAML 中中文必须用 `\uXXXX` 格式存储：
```python
def to_unity_str(text):
    result = []
    for ch in text:
        cp = ord(ch)
        if cp > 127:
            result.append(f'\\u{cp:04X}')
        else:
            result.append(ch)
    return ''.join(result)
```

换行在 Unity YAML 字符串中用 `\r\n` 转义表示：
```python
text = text.replace('\n', '\\r\\n')
```

### 字段替换示例
```python
import re

def replace_field(content, field_name, new_value):
    # new_value 已经是 Unity 转义后的字符串
    pattern = rf'(^\s+{re.escape(field_name)}:\s*)(".*?"|)(\s*$)'
    replacement = rf'\g<1>"{new_value}"'
    return re.sub(pattern, replacement, content, count=1, flags=re.MULTILINE)
```

---

## Skill 2：对话表 → SO 文件

### 适用场景
已有 `人物对话表.xlsx`（或同格式对话表），需要将表格中修改过的对话内容同步回：
- `PersonClueData` `.asset` 文件（NPC的 baseDialogues / clueDialogues / fallbackDialogues）
- `OptionDialogueDB_LevelX` `.asset` 文件（主角插播台词）

### 表格格式约定
列顺序与导出时一致：

| 列 | 字段名 | 说明 |
|----|--------|------|
| A | 人物编号 (personId) | 如 0102，用于定位 SO 文件 |
| B | 人物名称 | |
| C | 触发条件 | 基础对话 / 出示线索:xxx / 兜底对话 |
| D | 节点ID (nodeId) | |
| E | 说话人 | |
| F | 说话人角色 | NPC / 主角（选项）/ 主角（插播）|
| G | 对话内容 (text/dialogueText) | 要修改的主要字段 |
| H | 选项ID (optionId) | 主角（选项）行专用 |
| I | 选项文本 (optionText) | 主角（选项）行专用 |
| J | 跳转节点 (nextNodeId) | |
| K | 备注 | |

### 定位逻辑

```
根据 F 列（说话人角色）判断写入目标：

■ NPC / （段落标题行跳过）
  目标文件：Assets/DataSO/Clues/CaseX/Person/{personId}*.asset
  定位路径：A列(personId) → 找到对应 PersonClueData
  定位节点：D列(nodeId) → 在 baseDialogues / clueDialogues / fallbackDialogues 中找到 nodeId 匹配的节点
  修改字段：G列 → node.text

■ 主角（选项）
  目标文件：同上 PersonClueData
  定位：D列(nodeId) → 对应节点的 options 列表 → H列(optionId) 匹配
  修改字段：I列 → option.optionText

■ 主角（插播）
  目标文件：Assets/DataSO/DIalogueData/OptionDialogueDB_LevelX.asset
             X = Sheet名中的案件编号（如Case0→Level0）
  四个锁定条件（缺一不可）：
    levelNumber = 案件编号（0/1/2）
    personId    = A列
    nodeId      = D列
    optionId    = H列（从同组上方的"主角（选项）"行读取）
  修改字段：G列 → sequence.entries[0].dialogueText
```

### 主角插播对话的 YAML 替换
OptionDialogueDB 中每条 entry 的结构：
```yaml
- levelNumber: 0
  personId: 0102
  nodeId: 1
  optionId: "..."
  sequence:
    entries:
    - speakerName: "..."
      dialogueText: "【这里是要修改的内容】"
```

用正则定位到四个条件全匹配的 entry 块后，替换其中的 `dialogueText` 行：
```python
# 已验证的做法：用 str_replace 精确替换
# 旧值："\u53EF\u4EE5..."
# 新值："\u60A8\u597D\uFF0C..."
```

### 注意事项
1. 段落标题行（备注列以【开头）和空行直接跳过，不写入
2. `singleUse` 字段不在表格中，修改时保留原值
3. `shownClue` 是对象引用（fileID/guid），不在表格中，保留原值
4. 修改前建议备份原始 .asset 文件
5. 换行符处理：表格单元格中的换行（`\n`）写入 YAML 时需转为 `\r\n` 再转义为 `\\r\\n`

---

## Skill 3：章节对话表 xlsx → DialogueData SO

### 适用场景
已有 `章节对话表.xlsx`（或同格式文件），需要将表格中修改过的对话内容同步回 `DialogueData_Level0/1/2.asset`。

### 表格格式约定
列顺序与导出时一致：

| 列 | 字段名 | 说明 |
|----|--------|------|
| A | 序列编号 (seq_idx) | 第几个 dialogueSequence，用于定位 |
| B | 对话序列 (触发类型) | 如"关卡开始 (LevelStart)"，辅助识别 |
| C | 条目序号 (entry_idx) | 该序列内第几条 entry，用于定位 |
| D | 说话人 (speakerName) | 可修改 |
| E | 对话内容 (dialogueText) | **主要修改字段**，支持换行 |
| F | 节点类型 | 普通对话/起名弹窗/自定义动作，辅助识别 |
| G | 打字机 | 是/否 |
| H | 打字机速度 | 数值 |
| I | 自定义动作ID (customActionId) | 可修改 |
| J | 动作参数 (customActionArgument) | 可修改 |

> 黄色段落标题行（▼ 开头）直接跳过，不写入。

### SO 文件结构
```
DialogueData_Level{X}.asset
  levelNumber: X
  dialogueSequences:
  - triggerType: 0        ← seq_idx=1
    waveNumber: 0
    entries:
    - speakerName: "..."  ← entry_idx=1，D列
      dialogueText: "..." ← E列（要修改的内容）
      ...
    - speakerName: "..."  ← entry_idx=2
      ...
  - triggerType: 1        ← seq_idx=2
    waveNumber: 0
    ...
```

### 定位规则
- 用 **A 列（seq_idx）** 定位是第几个 `dialogueSequences` 块（从 1 开始计）
- 用 **C 列（entry_idx）** 定位该块内第几个 `entries` 条目（从 1 开始计）
- 修改对应条目的 `speakerName` 和 `dialogueText`

### 注意事项
1. `triggerType: 1`（WaveSpawn）的序列有多个（Wave 0/1/2...），seq_idx 是所有序列的全局编号，不要按 wave 号混淆
2. `nodeType: 2`（CustomAction）的条目有 `customActionId` 和 `customActionArgument`，这些也可以在表格中修改
3. `speakerImage` 是图片资源引用（fileID/guid），不在表格中，保留原值
4. 换行：表格单元格中的 `\n` 写入 YAML 时转为 `\r\n` 再 unicode 转义

### 对应的导出脚本
`gen_dialogue_xlsx.py`（位于项目根目录）

### Script GUID
`DialogueData Script: 155679d8aafb59145a4bf6382aed73ad`

---

## 快速参考：Script GUIDs

| SO 类型 | Script GUID |
|---------|-------------|
| NormalClueData | `a797a7bbc187b28408d77a69535b4456` |
| PersonClueData | `b12059b2bd14e98479ce2b924993e13e` |
| CameraClueData | `e0cbeb00849e6e844a21966c127b1526` |
| OptionDialogueDB | `22dfe2c4deda8244b9002f158d8f754a` |
| DialogueData | `155679d8aafb59145a4bf6382aed73ad` |

## 文件路径约定

```
线索 SO：
  Assets/DataSO/Clues/Case{X}/Normal/
  Assets/DataSO/Clues/Case{X}/Person/
  Assets/DataSO/Clues/Case{X}/Camera/

对话 SO：
  Assets/DataSO/DIalogueData/OptionDialogueDB_Level{X}.asset

章节对话 SO：
  Assets/DataSO/DIalogueData/DialogueData_Level{X}.asset
```

---

## Skill 4：DialogueData SO → 章节对话表 xlsx

### 适用场景
需要将 `DialogueData_LevelX.asset` 中的章节对话导出为 xlsx 表格，便于文案编辑和查看。

### 表格格式约定
列顺序与导出时一致：

| 列 | 字段名 | 说明 |
|----|--------|------|
| A | 序列编号 (seq_idx) | 第几个 dialogueSequence，用于定位 |
| B | 对话序列 (触发类型) | 如"关卡开始 (LevelStart)"，辅助识别 |
| C | 条目序号 (entry_idx) | 该序列内第几条 entry，用于定位 |
| D | 说话人 (speakerName) | 可修改 |
| E | 对话内容 (dialogueText) | **主要修改字段**，支持换行 |
| F | 节点类型 | 普通对话/起名弹窗/自定义动作，辅助识别 |
| G | 打字机 | 是/否 |
| H | 打字机速度 | 数值 |
| I | 自定义动作ID (customActionId) | 可修改 |
| J | 动作参数 (customActionArgument) | 可修改 |

> 黄色段落标题行（▼ 开头）直接跳过，不写入。

### SO 文件结构
```
DialogueData_Level{X}.asset
  levelNumber: X
  dialogueSequences:
  - triggerType: 0        ← seq_idx=1
    waveNumber: 0
    entries:
    - speakerName: "..."  ← entry_idx=1，D列
      dialogueText: "..." ← E列（要修改的内容）
      ...
    - speakerName: "..."  ← entry_idx=2
      ...
  - triggerType: 1        ← seq_idx=2
    waveNumber: 0
    ...
```

### 定位规则
- 用 **A 列（seq_idx）** 定位是第几个 `dialogueSequences` 块（从 1 开始计）
- 用 **C 列（entry_idx）** 定位该块内第几个 `entries` 条目（从 1 开始计）
- 修改对应条目的 `speakerName` 和 `dialogueText`

### 注意事项
1. `triggerType: 1`（WaveSpawn）的序列有多个（Wave 0/1/2...），seq_idx 是所有序列的全局编号，不要按 wave 号混淆
2. `nodeType: 2`（CustomAction）的条目有 `customActionId` 和 `customActionArgument`，这些也可以在表格中修改
3. `speakerImage` 是图片资源引用（fileID/guid），不在表格中，保留原值
4. 换行：表格单元格中的 `\n` 写入 YAML 时转为 `\r\n` 再 unicode 转义

### 对应的导出脚本
`ChapterDialogueExporter.cs`（位于 `Assets/Editor/` 目录）

### 菜单入口
Unity 编辑器菜单：`Tools > 线索导出 > 导出章节对话表为 Excel (.xlsx)`

### Script GUID
`DialogueData Script: 155679d8aafb59145a4bf6382aed73ad`
