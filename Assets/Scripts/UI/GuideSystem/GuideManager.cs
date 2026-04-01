using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引导系统核心控制器
/// 负责：
/// 1. 监听触发（ClueManager）
/// 2. 等待UI生成（Registry）
/// 3. 执行引导流程（Coroutine）
/// </summary>
public class GuideManager : MonoBehaviour
{
	/// <summary>
	/// 所有引导流程配置
	/// </summary>
	public List<GuideSequence> sequences;

	// 当前等待的触发
	private string waitingClueId;
	private GuideSequence pendingSequence;

	// 当前步骤队列
	private Queue<GuideStep> stepQueue;

	private void OnEnable()
	{
		// 监听“线索揭露”
		ClueManager.instance.OnClueRevealed += OnClueRevealed;

		// 监听“UI注册完成”
		GuideTargetRegistry.Instance.OnTargetRegistered += OnTargetRegistered;
	}

	private void OnDisable()
	{
		ClueManager.instance.OnClueRevealed -= OnClueRevealed;
		GuideTargetRegistry.Instance.OnTargetRegistered -= OnTargetRegistered;
	}

	/// <summary>
	/// 当线索被揭露（数据层触发）
	/// </summary>
	private void OnClueRevealed(ClueData clue)
	{
		// 查找是否有对应引导流程
		var seq = sequences.Find(s => s.triggerClueId == clue.id);
		if (seq == null) return;

		// 记录等待状态
		waitingClueId = clue.id;
		pendingSequence = seq;

		// 如果UI已经存在，直接开始
		var target = GuideTargetRegistry.Instance.Get(clue.id);
		if (target != null)
		{
			StartGuide(seq);
		}
	}

	/// <summary>
	/// 当UI注册完成（表现层触发）
	/// </summary>
	private void OnTargetRegistered(string key, RectTransform target)
	{
		// 如果正好是我们在等的UI
		if (key == waitingClueId)
		{
			StartGuide(pendingSequence);
		}
	}

	/// <summary>
	/// 开始执行引导
	/// </summary>
	private void StartGuide(GuideSequence seq)
	{
		waitingClueId = null;

		// 转换为队列（顺序执行）
		stepQueue = new Queue<GuideStep>(seq.steps);

		StartCoroutine(RunGuide());
	}

	/// <summary>
	/// 执行引导流程（核心循环）
	/// </summary>
	private IEnumerator RunGuide()
	{
		while (stepQueue.Count > 0)
		{
			yield return ExecuteStep(stepQueue.Dequeue());
		}
	}

	/// <summary>
	/// 执行单个步骤
	/// </summary>
	private IEnumerator ExecuteStep(GuideStep step)
	{
		switch (step.stepType)
		{
			case GuideStepType.Dialogue:
				yield return PlayDialogue(step);
				break;

			case GuideStepType.Highlight:
				Highlight(step);
				break;

			case GuideStepType.WaitClick:
				yield return WaitClick(step);
				break;

			case GuideStepType.WaitDrag:
				yield return WaitDrag(step);
				break;

			case GuideStepType.EndHighlight:
				GuideHighlightController.Instance.ClearHighlight();
				break;
		}
	}

	/// <summary>
	/// 播放对话（复用现有系统）
	/// </summary>
	private IEnumerator PlayDialogue(GuideStep step)
	{
		bool done = false;

		DialogueManager.Instance.ShowDialogue(
			0,
			step.dialogueTrigger,
			onComplete: () => done = true,
			isForced: true
		);

		yield return new WaitUntil(() => done);
	}

	/// <summary>
	/// 高亮多个UI
	/// </summary>
	private void Highlight(GuideStep step)
	{
		List<RectTransform> targets = new();

		foreach (var key in step.targetKeys)
		{
			var t = GuideTargetRegistry.Instance.Get(key);
			if (t != null)
				targets.Add(t);
		}

		GuideHighlightController.Instance.HighlightMultiple(targets);
	}

	/// <summary>
	/// 等待点击
	/// </summary>
	private IEnumerator WaitClick(GuideStep step)
	{
		bool clicked = false;

		var target = GuideTargetRegistry.Instance.Get(step.targetKeys[0]);
		var item = target.GetComponent<ClueListItemUI>();

		void OnClick(ClueData clue)
		{
			if (clue.id == step.targetKeys[0])
				clicked = true;
		}

		item.OnClicked += OnClick;

		yield return new WaitUntil(() => clicked);

		item.OnClicked -= OnClick;
	}

	/// <summary>
	/// 等待拖拽成功（核心逻辑）
	/// </summary>
	private IEnumerator WaitDrag(GuideStep step)
	{
		bool done = false;

		var source = GuideTargetRegistry.Instance.Get(step.dragSourceKey);
		var drag = source.GetComponent<DraggableClueItem>();

		void OnDrag(string s, string t)
		{
			// 只有拖到“正确目标”才算完成
			if (s == step.dragSourceKey && t == step.dragTargetKey)
			{
				done = true;
			}
		}

		drag.OnDragSuccess += OnDrag;

		yield return new WaitUntil(() => done);

		drag.OnDragSuccess -= OnDrag;
	}
}