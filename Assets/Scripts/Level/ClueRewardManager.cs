using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 关卡通用管理器
/// 负责处理关卡的通用逻辑：
/// 初始对话结束后的线索奖励（可配置）
/// </summary>
public class ClueRewardManager : MonoBehaviour
{
    [Header("事件监听")]
    [Tooltip("对话结束事件")]
    [SerializeField] private DialogueEndEventSO dialogueEndEvent;
    
    [Header("线索奖励配置")]
    [Tooltip("LevelStart对话结束后要添加的线索ID的配置表")]
	[SerializeField] private LevelRewardClueConfigSO configSO;
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
		Debug.Log($"[LevelManager] 对话结束：关卡={levelNumber}，触发类型={triggerType}");

		var config = configSO.configs.Find(c =>
			c.levelNumber == levelNumber &&
			c.triggerType == triggerType);

		if (config == null)
		{
			return;
		}

		HandleClueReward(config);
	}

	/// <summary>
	/// 处理初始对话结束
	/// 根据配置的线索ID列表添加线索奖励
	/// </summary>
	private void HandleClueReward(LevelClueRewardConfig config)
	{
		Debug.Log($"[LevelManager] 添加线索奖励，关卡={config.levelNumber}");

		if (ClueManager.instance == null)
		{
			Debug.LogError("[LevelManager] ClueManager.instance未找到");
			return;
		}

		if (config.clueIDs == null || config.clueIDs.Count == 0)
		{
			Debug.LogWarning("[LevelManager] 没有配置线索");
			return;
		}

		int successCount = 0;
		int failCount = 0;

		foreach (var clueID in config.clueIDs)
		{
			if (string.IsNullOrEmpty(clueID))
			{
				failCount++;
				continue;
			}

			bool revealed = ClueManager.instance.RevealClue(clueID);

			if (revealed)
				successCount++;
			else
				failCount++;
		}

		Debug.Log($"[LevelManager] 完成：成功{successCount}，失败{failCount}");
	}
}

[System.Serializable]
public class LevelClueRewardConfig
{
	public int levelNumber;
	public DialogueTriggerType triggerType;
	public List<string> clueIDs;
}