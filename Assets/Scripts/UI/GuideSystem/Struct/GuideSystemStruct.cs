using System;
using System.Collections.Generic;

/// <summary>
/// 引导步骤类型。
/// 注意：这个枚举会直接序列化到场景/Prefab 中，
/// 因此新增类型时应尽量只追加，不要随意改动已有顺序，
/// 否则旧场景里的 stepType 数值会被解释成错误行为。
/// </summary>
public enum GuideStepType
{
	Dialogue,      // 播放对话
	Highlight,     // 高亮UI（支持多个）
	WaitClick,     // 等待点击
	WaitDrag,      // 等待拖拽成功
	EndHighlight,  // 清除高亮
	Delay,         // 延迟一段时间
	WaitInputSubmit // 等待输入框手动回车
}

/// <summary>
/// 单个引导步骤的数据定义。
/// 这里的字段是“超集”设计：不同 stepType 会只使用其中一部分字段。
/// 例如：
/// - Dialogue 使用 dialogueTrigger / dialogueLevelNumber / dialogueWaveNumber
/// - Highlight / WaitClick 使用 targetKeys
/// - WaitDrag 使用 dragSourceKey / dragTargetKey
/// - Delay 使用 delaySeconds
/// - WaitInputSubmit 使用 submitTargetKey / requireNonEmptySubmit
/// </summary>
[System.Serializable]
public class GuideStep
{
	public string stepDescribe;

	/// <summary>
	/// 当前步骤类型，决定 GuideManager 如何解释本 step 的其余字段。
	/// </summary>
	public GuideStepType stepType;

	/// <summary>
	/// 目标UI（支持多个）
	/// 例如：["clue_knife", "AnalysisPanel"]
	/// 约定：这里存的是 GuideTarget.key，而不是对象名。
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
	/// 对话触发类型（复用 DialogueManager 的触发枚举）
	/// </summary>
	public DialogueTriggerType dialogueTrigger;

	/// <summary>
	/// 对话所属关卡编号。
	/// 小于0时使用当前对话关卡，若当前关卡无效则回退到0。
	/// </summary>
	public int dialogueLevelNumber = -1;

	/// <summary>
	/// 对话波次编号（仅 WaveSpawn 类型有效）
	/// </summary>
	public int dialogueWaveNumber;

	/// <summary>
	/// 延迟节点的等待时长（秒）
	/// </summary>
	public float delaySeconds = 0.2f;

	/// <summary>
	/// 等待输入提交时要监听的输入框目标 key。
	/// 留空时默认使用 targetKeys[0]。
	/// </summary>
	public string submitTargetKey;

	/// <summary>
	/// 等待输入提交时是否要求输入内容非空。
	/// </summary>
	public bool requireNonEmptySubmit = true;
}

/// <summary>
/// 一条完整的引导流程。
/// 当前设计是一条 triggerClueId 对应一条顺序执行的 steps 列表。
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
	// 拖拽系统和 guide 系统是解耦的：
	// 拖拽成功后只通过总线抛出 sourceKey / targetKey，
	// GuideManager 在 WaitDrag 中按需订阅，不反向侵入拖拽业务代码。
	public static event Action<string, string> OnDragSuccess;

	public static void Raise(string source, string target)
	{
		OnDragSuccess?.Invoke(source, target);
	}
}

public static class GuideInputSubmitEventBus
{
	// 输入提交同理，guide 只关心“哪个输入框被提交了、提交文本是什么、是否手动提交”。
	// 这样 WaitInputSubmit 就能区分“手动回车”与“拖拽自动提交”。
	public static event Action<string, string, bool> OnInputSubmitted;

	public static void Raise(string targetKey, string inputText, bool isManualSubmit)
	{
		OnInputSubmitted?.Invoke(targetKey, inputText, isManualSubmit);
	}
}
