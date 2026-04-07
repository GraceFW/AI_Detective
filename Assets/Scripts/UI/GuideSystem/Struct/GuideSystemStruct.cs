using System;
using System.Collections.Generic;
using UnityEngine;

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
	WaitInputSubmit, // 等待输入框手动回车
	WaitCluesCollected // 等待若干线索被收集
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
	[Header("步骤简介")]
	public string stepDescribe;

	[Header("步骤类型")]
	[Tooltip("当前步骤节点的类型，选好类型后只需要配置对应类型的配置")]
	public GuideStepType stepType;

	[Header("高亮UI节点配置，支持多个")]
	[Tooltip("目标UI（支持多个），例如：['clue_knife', 'AnalysisPanel']，约定：这里存的是 GuideTarget.key，而不是对象名。")]
	public List<string> targetKeys;

	[Header("拖拽UI节点配置，支持多个")]
	[Tooltip("拖拽源（被拖的UI）")]
	public string dragSourceKey;

	[Tooltip("拖拽目标（接收拖拽的UI）")]
	public string dragTargetKey;

	[Header("对话节点配置")]
	[Tooltip("对话触发类型（复用 DialogueManager 的触发枚举）")]
	public DialogueTriggerType dialogueTrigger;

	[Tooltip("对话所属关卡编号，小于0时使用当前对话关卡，若当前关卡无效则回退到0。")]
	public int dialogueLevelNumber = -1;

	[Tooltip("对话波次编号，仅 WaveSpawn 类型有效。")]
	public int dialogueWaveNumber;

	[Header("延迟节点配置")]
	[Tooltip("延迟节点的等待时长（秒）")]
	public float delaySeconds = 0.2f;

	[Header("输入提交节点配置")]
	[Tooltip("等待输入提交时要监听的输入框目标 key，留空时默认使用 targetKeys[0]。")]
	public string submitTargetKey;

	[Tooltip("等待输入提交时是否要求输入内容非空。")]
	public bool requireNonEmptySubmit = true;

	[Header("线索收集节点配置")]
	[Tooltip("等待线索收集步骤使用的线索 id 列表，所有配置的线索都已被收集后，步骤才会继续。")]
	public List<string> requiredClueIds;
}

/// <summary>
/// 一条完整的引导流程。
/// 当前设计是：triggerClueIds 中列出的所有线索都被揭露后，顺序执行 steps。
/// 即使只需要单线索触发，也统一写成只包含 1 个元素的列表。
/// </summary>
[System.Serializable]
public class GuideSequence
{
	[Tooltip("触发条件：列表中的所有线索都被揭露后才触发。")]
	public List<string> triggerClueIds;

	[Tooltip("步骤列表（顺序执行）")]
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

/// <summary>
/// 可接收线索拖拽的统一接口。
/// 
/// 目的：
/// - 让 DraggableClueItem 不再硬编码识别具体 DropTarget 类型
/// - 让 WaitDrag 能复用到任意业务 UI，只要该 UI 能消费线索拖拽并返回成功
/// 
/// 约定：
/// - 返回 true：表示本次拖拽已被当前目标成功处理，GuideDragEventBus 应该推进
/// - 返回 false：表示当前目标不接受这条线索，或业务条件未满足，不应推进 guide
/// </summary>
public interface IClueDropTarget
{
	bool OnClueDrop(ClueData clue);
}
