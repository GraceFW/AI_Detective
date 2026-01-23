using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// 对话触发类型
/// </summary>
public enum DialogueTriggerType
{
    LevelStart,      // 关卡开始
    WaveSpawn,       // 波次生成（波次：案件中不同时间点的对话）
    LevelComplete    // 关卡完成
}

/// <summary>
/// 对话节点类型
/// </summary>
public enum DialogueNodeType
{
    Normal,         // 普通对话
    NameInput,      // 起名弹窗节点
    CustomAction    // 自定义动作节点（预留）
}

/// <summary>
/// 对话条目
/// </summary>
[System.Serializable]
[Preserve]
public class DialogueEntry
{
    [Header("说话人信息")]
    [Tooltip("说话人名称")]
    public string speakerName;
    
    [Tooltip("说话人头像")]
    public Sprite speakerImage;
    
    [Header("对话内容")]
    [Tooltip("对话文本")]
    [TextArea(3, 10)]
    public string dialogueText;
    
    [Header("显示设置")]
    [Tooltip("是否使用打字机效果")]
    public bool useTypewriterEffect = true;
    
    [Tooltip("打字机速度（字符/秒）")]
    public float typewriterSpeed = 30f;
    
    [Header("文本框样式")]
    [Tooltip("文本框背景图（DialogueTextBG的Source Image，不同角色可使用不同颜色/样式）")]
    public Sprite textBoxBackground;
    
    [Header("节点类型")]
    [Tooltip("节点类型（Normal=普通对话，NameInput=起名弹窗，CustomAction=自定义动作）")]
    public DialogueNodeType nodeType = DialogueNodeType.Normal;
}

/// <summary>
/// 对话序列
/// </summary>
[System.Serializable]
[Preserve]
public class DialogueSequence
{
    [Header("触发设置")]
    [Tooltip("触发类型")]
    public DialogueTriggerType triggerType;
    
    [Tooltip("波次编号（仅WaveSpawn类型有效，0表示第一波，1表示第二波）")]
    public int waveNumber = 0;
    
    [Header("对话内容")]
    [Tooltip("对话条目列表")]
    public DialogueEntry[] entries;
}

