using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

/// <summary>
/// 对话数据（ScriptableObject）
/// </summary>
[CreateAssetMenu(fileName = "DialogueData", menuName = "Game/DialogueData", order = 1)]
[Preserve]
public class DialogueData : ScriptableObject
{
    [Header("关卡（案件）信息")]
    [Tooltip("案件编号（1表示第一案件，2表示第二案件等）")]
    public int levelNumber = 1;
    
    [Header("对话序列")]
    [Tooltip("该案件的所有对话序列")]
    public DialogueSequence[] dialogueSequences;
    
    /// <summary>
    /// 获取指定触发类型的对话序列
    /// </summary>
    public DialogueSequence GetDialogueSequence(DialogueTriggerType triggerType, int waveNumber = 0)
    {
        foreach (var sequence in dialogueSequences)
        {
            if (sequence.triggerType == triggerType)
            {
                // 如果是波次生成类型，检查波次编号
                if (triggerType == DialogueTriggerType.WaveSpawn)
                {
                    if (sequence.waveNumber == waveNumber)
                    {
                        return sequence;
                    }
                }
                else
                {
                    return sequence;
                }
            }
        }
        return null;
    }
}

