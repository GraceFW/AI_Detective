using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Clue/Person Clue")]
public class PersonClueData : ClueData
{
    [Header("Person Visual")]
    // 人物头像：用于 UI（档案/联系人/嫌疑人等页面）。
    public Sprite portrait;

    [Header("Summon")]
    [Tooltip("Can this person be summoned in gameplay")]
    // 是否可被玩家传唤/呼叫（玩法规则）。
    public bool canBeSummoned;

    [Header("Base Dialogue")]
    [Tooltip("Dialogue shown when talking to this person normally")]
    // 默认对话：正常与该人物交互时展示（未出示线索）。
    public List<DialogueNode> baseDialogues;

    [Header("Fallback Dialogue")]
    [Tooltip("Dialogue shown when the player shows a clue that has no specific dialogue entry")]
    // 兜底对话：当出示的线索未命中 clueDialogues 中任何条目时使用。
    public List<DialogueNode> fallbackDialogues;

    [Header("Clue-Triggered Dialogues")]
    [Tooltip("Dialogue triggered when showing other clues to this person")]
    // 线索触发对话：映射“出示线索 -> 对话节点列表”。当玩家出示特定线索给该人物时使用。
    public List<ClueDialogueEntry> clueDialogues;
}


[System.Serializable]
public class DialogueNode
{
    [Tooltip("Unique ID inside this person")]
    // 对话节点ID：在该人物的对话图谱内唯一。
    public string nodeId;

    [TextArea(3, 10)]
    // 对话内容。
    public string text;

    [Header("Flow Control")]
    [Tooltip("Next node if there are no options (click to continue)")]
    // 线性流程下一个节点ID（没有选项时）。
    public string nextNodeId;

    [Tooltip("Optional dialogue choices")]
    // 分支选项：为空则视为线性对话。
    public List<DialogueOption> options;
}

[System.Serializable]
public class DialogueOption
{
    // 选项按钮显示文本。
    public string optionText;

    [Tooltip("Next dialogue node id")]
    // 选择该选项后跳转到的节点ID。
    public string nextNodeId;
}

[System.Serializable]
public class ClueDialogueEntry
{
    [Tooltip("The clue shown to this person")]
    // 出示该线索时触发本条对话。
    public ClueData shownClue;

    [Tooltip("Dialogue nodes triggered by showing this clue")]
    // 出示该线索后要播放的对话节点列表。
    public List<DialogueNode> dialogues;

    [Tooltip("Can this dialogue be triggered only once")]
    // 是否仅能触发一次（玩法规则）。
    public bool singleUse = true;
}
