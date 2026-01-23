using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 第0关卡专用管理器
/// 负责处理第0关卡的特殊逻辑：
/// - 初始对话结束后的线索奖励（可配置）
/// </summary>
public class Level0Manager : MonoBehaviour
{
    [Header("事件监听")]
    [Tooltip("对话结束事件")]
    [SerializeField] private DialogueEndEventSO dialogueEndEvent;
    
    [Header("线索奖励配置")]
    [Tooltip("LevelStart对话结束后要添加的线索ID列表")]
    [SerializeField] private List<string> rewardClueIDs = new List<string>();
    
    private void OnEnable()
    {
        if (dialogueEndEvent != null)
        {
            dialogueEndEvent.OnEventRaised += OnDialogueEnd;
        }
    }
    
    private void OnDisable()
    {
        if (dialogueEndEvent != null)
        {
            dialogueEndEvent.OnEventRaised -= OnDialogueEnd;
        }
    }
    
    /// <summary>
    /// 对话结束事件处理
    /// </summary>
    /// <param name="levelNumber">关卡编号</param>
    /// <param name="triggerType">触发类型</param>
    private void OnDialogueEnd(int levelNumber, DialogueTriggerType triggerType)
    {
        // 只处理第0关卡的对话
        if (levelNumber != 0)
        {
            return;
        }
        
        Debug.Log($"[Level0Manager] 第0关卡对话结束：触发类型={triggerType}");
        
        // 只处理LevelStart对话结束，添加线索奖励
        if (triggerType == DialogueTriggerType.LevelStart)
        {
            HandleInitialDialogueEnd();
        }
    }
    
    /// <summary>
    /// 处理初始对话结束
    /// 根据配置的线索ID列表添加线索奖励
    /// </summary>
    private void HandleInitialDialogueEnd()
    {
        Debug.Log("[Level0Manager] 初始对话结束，开始添加线索奖励");
        
        if (ClueManager.instance == null)
        {
            Debug.LogError("[Level0Manager] ClueManager.instance未找到，无法添加线索");
            return;
        }
        
        if (rewardClueIDs == null || rewardClueIDs.Count == 0)
        {
            Debug.LogWarning("[Level0Manager] rewardClueIDs列表为空，没有线索需要添加");
            return;
        }
        
        int successCount = 0;
        int failCount = 0;
        
        foreach (string clueID in rewardClueIDs)
        {
            if (string.IsNullOrEmpty(clueID))
            {
                Debug.LogWarning("[Level0Manager] 发现空的线索ID，跳过");
                failCount++;
                continue;
            }
            
            bool revealed = ClueManager.instance.RevealClue(clueID);
            if (revealed)
            {
                Debug.Log($"[Level0Manager] 已添加线索：{clueID}");
                successCount++;
            }
            else
            {
                Debug.LogWarning($"[Level0Manager] 添加线索失败或线索已存在：{clueID}");
                failCount++;
            }
        }
        
        Debug.Log($"[Level0Manager] 线索奖励添加完成：成功{successCount}个，失败{failCount}个");
    }
}

