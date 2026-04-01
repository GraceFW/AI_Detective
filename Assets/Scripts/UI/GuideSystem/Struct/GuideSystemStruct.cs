using System;
using System.Collections.Generic;

/// <summary>
/// 引导步骤类型
/// </summary>
public enum GuideStepType
{
	Dialogue,      // 播放对话
	Highlight,     // 高亮UI（支持多个）
	WaitClick,     // 等待点击
	WaitDrag,      // 等待拖拽成功
	EndHighlight   // 清除高亮
}

/// <summary>
/// 单个引导步骤
/// </summary>
[System.Serializable]
public class GuideStep
{
	/// <summary>
	/// 当前步骤类型
	/// </summary>
	public GuideStepType stepType;

	/// <summary>
	/// 目标UI（支持多个）
	/// 例如：["clue_knife", "AnalysisPanel"]
	/// </summary>
	public List<string> targetKeys;

	/// <summary>
	/// 拖拽源（被拖的UI）
	/// </summary>
	public string dragSourceKey;

	/// <summary>
	/// 拖拽目标（接收拖拽的UI）
	/// </summary>
	public string dragTargetKey;

	/// <summary>
	/// 对话触发类型（复用你的Dialogue系统）
	/// </summary>
	public DialogueTriggerType dialogueTrigger;
}

/// <summary>
/// 引导流程（一个完整的新手步骤序列）
/// </summary>
[System.Serializable]
public class GuideSequence
{
	/// <summary>
	/// 触发条件：某个线索被揭露
	/// </summary>
	public string triggerClueId;

	/// <summary>
	/// 步骤列表（顺序执行）
	/// </summary>
	public List<GuideStep> steps;
}

public static class GuideDragEventBus
{
	public static event Action<string, string> OnDragSuccess;

	public static void Raise(string source, string target)
	{
		OnDragSuccess?.Invoke(source, target);
	}
}