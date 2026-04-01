using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引导系统核心控制器（稳定版）
/// 
/// 职责：
/// 1. 监听数据层（ClueManager）
/// 2. 等待UI生成（GuideTargetRegistry）
/// 3. 执行引导流程（状态机 + 协程）
/// 4. 防重复、防并发
/// </summary>
public class GuideManager : MonoBehaviour
{
	[Header("引导流程配置")]
	public List<GuideSequence> sequences;

	/// <summary>
	/// 当前等待的线索ID（用于等UI）
	/// </summary>
	private string waitingClueId;

	/// <summary>
	/// 待执行的引导流程
	/// </summary>
	private GuideSequence pendingSequence;

	/// <summary>
	/// 当前步骤队列
	/// </summary>
	private Queue<GuideStep> stepQueue;

	/// <summary>
	/// ⭐ 是否正在执行引导（防止重复触发）
	/// </summary>
	private bool isGuiding;

	private readonly HashSet<string> _consumedTriggerClueIds = new();
	private Coroutine _dependencyBindCoroutine;
	private Coroutine _guideCoroutine;
	private bool _isClueSubscribed;
	private bool _isRegistrySubscribed;
	private ClueListItemUI _activeClickItem;
	private Action<ClueData> _activeClickHandler;
	private Action<string, string> _activeDragHandler;

	private void Awake()
	{
		EnsureGuideServices();
	}

	private void OnEnable()
	{
		StartDependencyBinding();
	}

	private void OnDisable()
	{
		StopDependencyBinding();
		UnsubscribeDependencies();
		CleanupActiveWaiters();
		StopGuideRuntime();
	}

	private void EnsureGuideServices()
	{
		if (!GuideTargetRegistry.HasInstance)
		{
			var registryObject = new GameObject("GuideTargetRegistry");
			registryObject.transform.SetParent(transform, false);
			registryObject.AddComponent<GuideTargetRegistry>();
		}

		if (GuideHighlightController.Instance == null)
		{
			var existingHighlightController = FindSceneHighlightController();
			if (existingHighlightController != null)
			{
				existingHighlightController.gameObject.SetActive(true);
				existingHighlightController.EnsureInitialized();
			}
			else
			{
				var highlightObject = new GameObject("GuideHighlightController");
				highlightObject.transform.SetParent(transform, false);
				highlightObject.AddComponent<GuideHighlightController>();
			}
		}
	}

	private GuideHighlightController FindSceneHighlightController()
	{
		var controllers = Resources.FindObjectsOfTypeAll<GuideHighlightController>();
		foreach (var controller in controllers)
		{
			if (controller == null)
			{
				continue;
			}

			if (!controller.gameObject.scene.IsValid())
			{
				continue;
			}

			return controller;
		}

		return null;
	}

	private void StartDependencyBinding()
	{
		if (_dependencyBindCoroutine != null)
		{
			StopCoroutine(_dependencyBindCoroutine);
		}

		_dependencyBindCoroutine = StartCoroutine(BindDependenciesWhenReady());
	}

	private void StopDependencyBinding()
	{
		if (_dependencyBindCoroutine != null)
		{
			StopCoroutine(_dependencyBindCoroutine);
			_dependencyBindCoroutine = null;
		}
	}

	private IEnumerator BindDependenciesWhenReady()
	{
		while (isActiveAndEnabled)
		{
			if (!_isClueSubscribed && ClueManager.instance != null)
			{
				ClueManager.instance.OnClueRevealed += OnClueRevealed;
				_isClueSubscribed = true;
				ReplayTriggeredGuides();
			}

			if (!_isRegistrySubscribed && GuideTargetRegistry.HasInstance)
			{
				GuideTargetRegistry.Instance.OnTargetRegistered += OnTargetRegistered;
				_isRegistrySubscribed = true;
			}

			if (_isClueSubscribed && _isRegistrySubscribed)
			{
				_dependencyBindCoroutine = null;
				yield break;
			}

			yield return null;
		}

		_dependencyBindCoroutine = null;
	}

	private void UnsubscribeDependencies()
	{
		if (_isClueSubscribed && ClueManager.instance != null)
		{
			ClueManager.instance.OnClueRevealed -= OnClueRevealed;
		}

		if (_isRegistrySubscribed && GuideTargetRegistry.HasInstance)
		{
			GuideTargetRegistry.Instance.OnTargetRegistered -= OnTargetRegistered;
		}

		_isClueSubscribed = false;
		_isRegistrySubscribed = false;
	}

	// =========================
	// 数据层触发（线索揭露）
	// =========================
	private void OnClueRevealed(ClueData clue)
	{
		if (clue == null || string.IsNullOrEmpty(clue.id))
		{
			return;
		}

		if (isGuiding)
		{
			Debug.Log("[GuideManager] 当前已有引导进行中，忽略新触发");
			return;
		}

		if (_consumedTriggerClueIds.Contains(clue.id))
		{
			return;
		}

		var seq = sequences?.Find(s => s != null && s.triggerClueId == clue.id);
		if (seq == null)
		{
			return;
		}

		Debug.Log($"[GuideManager] 触发引导：{clue.id}");

		if (SequenceCanStartWithoutTarget(seq, clue.id))
		{
			StartGuide(seq);
			return;
		}

		waitingClueId = clue.id;
		pendingSequence = seq;

		var target = GuideTargetRegistry.HasInstance
			? GuideTargetRegistry.Instance.Get(clue.id)
			: null;

		if (target != null)
		{
			StartGuide(seq);
		}
	}

	// =========================
	// 表现层触发（UI注册）
	// =========================
	private void OnTargetRegistered(string key, RectTransform target)
	{
		if (isGuiding)
		{
			return;
		}

		if (!string.IsNullOrEmpty(waitingClueId) && key == waitingClueId)
		{
			Debug.Log($"[GuideManager] UI已生成，开始引导: {key}");
			StartGuide(pendingSequence);
		}
	}

	// =========================
	// 启动引导
	// =========================
	private void StartGuide(GuideSequence seq)
	{
		if (seq == null)
		{
			return;
		}

		if (isGuiding)
		{
			Debug.LogWarning("[GuideManager] 引导已在进行中");
			return;
		}

		isGuiding = true;
		pendingSequence = null;
		waitingClueId = null;

		if (!string.IsNullOrEmpty(seq.triggerClueId))
		{
			_consumedTriggerClueIds.Add(seq.triggerClueId);
		}

		stepQueue = new Queue<GuideStep>(seq.steps ?? new List<GuideStep>());

		if (_guideCoroutine != null)
		{
			StopCoroutine(_guideCoroutine);
		}

		_guideCoroutine = StartCoroutine(RunGuide());
	}

	// =========================
	// 引导主循环
	// =========================
	private IEnumerator RunGuide()
	{
		Debug.Log("[GuideManager] 开始执行引导流程");

		while (stepQueue != null && stepQueue.Count > 0)
		{
			yield return ExecuteStep(stepQueue.Dequeue());
		}

		Debug.Log("[GuideManager] 引导结束");

		_guideCoroutine = null;
		isGuiding = false;
	}

	// =========================
	// 执行单步骤
	// =========================
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
				if (GuideHighlightController.Instance != null)
				{
					GuideHighlightController.Instance.ClearHighlight();
				}
				break;
		}
	}

	// =========================
	// 对话步骤
	// =========================
	private IEnumerator PlayDialogue(GuideStep step)
	{
		if (DialogueManager.Instance == null)
		{
			Debug.LogError("[GuideManager] DialogueManager.Instance 为空，无法执行对话步骤");
			yield break;
		}

		yield return new WaitUntil(() => DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueActive());

		if (DialogueManager.Instance == null)
		{
			yield break;
		}

		bool done = false;

		DialogueManager.Instance.ShowDialogue(
			0,
			step.dialogueTrigger,
			onComplete: () => done = true,
			isForced: true
		);

		yield return new WaitUntil(() => !isActiveAndEnabled || done);
	}

	// =========================
	// 高亮步骤
	// =========================
	private void Highlight(GuideStep step)
	{
		if (!GuideTargetRegistry.HasInstance)
		{
			Debug.LogError("[GuideManager] GuideTargetRegistry 未就绪，无法高亮目标");
			return;
		}

		if (GuideHighlightController.Instance == null)
		{
			Debug.LogError("[GuideManager] GuideHighlightController 未就绪，无法高亮目标");
			return;
		}

		List<RectTransform> targets = new();

		foreach (var key in step.targetKeys)
		{
			var t = GuideTargetRegistry.Instance.Get(key);
			if (t != null)
			{
				targets.Add(t);
			}
			else
			{
				Debug.LogWarning($"[GuideManager] 高亮目标未找到: {key}");
			}
		}

		GuideHighlightController.Instance.HighlightMultiple(targets);
	}

	// =========================
	// 等待点击
	// =========================
	private IEnumerator WaitClick(GuideStep step)
	{
		if (step.targetKeys == null || step.targetKeys.Count == 0)
		{
			Debug.LogError("[GuideManager] WaitClick 缺少目标配置");
			yield break;
		}

		if (!GuideTargetRegistry.HasInstance)
		{
			Debug.LogError("[GuideManager] WaitClick 时 GuideTargetRegistry 未就绪");
			yield break;
		}

		string targetKey = step.targetKeys[0];
		var target = GuideTargetRegistry.Instance.Get(targetKey);

		if (target == null)
		{
			Debug.LogError("[GuideManager] WaitClick目标不存在");
			yield break;
		}

		var item = target.GetComponent<ClueListItemUI>();

		if (item == null)
		{
			Debug.LogError("[GuideManager] 目标没有ClueListItemUI");
			yield break;
		}

		bool clicked = false;

		_activeClickItem = item;
		_activeClickHandler = clue =>
		{
			if (clue != null && clue.id == targetKey)
			{
				clicked = true;
			}
		};

		item.OnClicked += _activeClickHandler;

		yield return new WaitUntil(() => !isActiveAndEnabled || clicked);

		CleanupActiveClickWaiter();
	}

	// =========================
	// 等待拖拽
	// =========================
	private IEnumerator WaitDrag(GuideStep step)
	{
		bool done = false;

		_activeDragHandler = (sourceKey, targetKey) =>
		{
			if (sourceKey == step.dragSourceKey && targetKey == step.dragTargetKey)
			{
				done = true;
			}
		};

		GuideDragEventBus.OnDragSuccess += _activeDragHandler;

		yield return new WaitUntil(() => !isActiveAndEnabled || done);

		CleanupActiveDragWaiter();
	}

	private void ReplayTriggeredGuides()
	{
		if (ClueManager.instance == null || sequences == null)
		{
			return;
		}

		foreach (var clue in ClueManager.instance.GetRevealedClues())
		{
			if (clue != null)
			{
				OnClueRevealed(clue);
			}
		}
	}

	private bool SequenceCanStartWithoutTarget(GuideSequence seq, string triggerKey)
	{
		if (seq == null || seq.steps == null || seq.steps.Count == 0)
		{
			return true;
		}

		foreach (var step in seq.steps)
		{
			if (step == null)
			{
				continue;
			}

			if (step.targetKeys != null && step.targetKeys.Contains(triggerKey))
			{
				return false;
			}

			if (step.dragSourceKey == triggerKey || step.dragTargetKey == triggerKey)
			{
				return false;
			}
		}

		return true;
	}

	private void CleanupActiveWaiters()
	{
		CleanupActiveClickWaiter();
		CleanupActiveDragWaiter();
	}

	private void CleanupActiveClickWaiter()
	{
		if (_activeClickItem != null && _activeClickHandler != null)
		{
			_activeClickItem.OnClicked -= _activeClickHandler;
		}

		_activeClickItem = null;
		_activeClickHandler = null;
	}

	private void CleanupActiveDragWaiter()
	{
		if (_activeDragHandler != null)
		{
			GuideDragEventBus.OnDragSuccess -= _activeDragHandler;
		}

		_activeDragHandler = null;
	}

	private void StopGuideRuntime()
	{
		if (_guideCoroutine != null)
		{
			StopCoroutine(_guideCoroutine);
			_guideCoroutine = null;
		}

		stepQueue = null;
		pendingSequence = null;
		waitingClueId = null;
		isGuiding = false;

		if (GuideHighlightController.Instance != null)
		{
			GuideHighlightController.Instance.ClearHighlight();
		}
	}
}
