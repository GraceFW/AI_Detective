using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// 引导流程总控。
/// 这个类只做“编排”，不直接关心业务 UI 的实现细节：
/// 1. 监听线索揭露事件，决定是否触发某条 GuideSequence。
/// 2. 逐步执行 step：对话、高亮、等待点击、等待拖拽、等待输入提交、延迟等。
/// 3. 当目标 UI 是运行时动态生成时，负责等待 GuideTarget 注册完成后再继续。
///
/// 设计约束：
/// - GuideManager 仍然是流程入口，业务 UI 不应直接控制 guide 步骤推进。
/// - 目标定位统一通过 GuideTargetRegistry + key 完成。
/// - 等待类步骤要在退出、禁用时正确清理监听，避免残留订阅导致串流程。
/// </summary>
public class GuideManager : MonoBehaviour
{
	[Header("Guide Sequences")]
	public List<GuideSequence> sequences;

	[Header("Debug")]
	[SerializeField] private bool guideEnabled = true;
	// 某些步骤依赖动态 UI（例如弹出的详情页、运行时生成的按钮）。
	// 为了避免目标刚创建还未来得及注册就立即执行失败，这里允许短暂等待目标出现。
	[SerializeField] private float targetResolveTimeout = 1f;

	// 当触发线索已经揭露、但对应的 GuideTarget 还没注册时，
	// 会先把信息暂存在这两个字段里，等 OnTargetRegistered 回调再真正启动 guide。
	private GuideSequence pendingSequence;
	private readonly HashSet<string> _pendingTargetKeys = new();

	// 当前序列会被转成队列逐步消费，保证 guide 始终是严格串行执行。
	private Queue<GuideStep> stepQueue;
	private bool isGuiding;

	// 同一条 GuideSequence 只消费一次，避免同一组触发线索重复拉起整条引导。
	private readonly HashSet<GuideSequence> _consumedSequences = new();
	private Coroutine _dependencyBindCoroutine;
	private Coroutine _guideCoroutine;
	private bool _isClueSubscribed;
	private bool _isRegistrySubscribed;

	// 当前活跃的等待监听。每类只保留一个，guide 停止或切步时统一清理。
	private ClueListItemUI _activeClickItem;
	private Action<ClueData> _activeClickHandler;
	private Button _activeClickButton;
	private UnityAction _activeButtonClickHandler;
	private Action<string, string> _activeDragHandler;
	private Action<string, string, bool> _activeInputSubmitHandler;

	public bool GuideEnabled => guideEnabled;

	private void Awake()
	{
		if (!guideEnabled)
		{
			return;
		}

		EnsureGuideServices();
	}

	private void OnEnable()
	{
		if (!guideEnabled)
		{
			return;
		}

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
		// GuideTargetRegistry 和 GuideHighlightController 都是当前场景级服务。
		// 新手关不是 persistent 系统，所以这里按需在场景内兜底创建。
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
		// 依赖对象（尤其是 ClueManager）可能比 GuideManager 晚初始化，
		// 因此这里用协程轮询绑定，而不是在 Awake/Start 里一次性假设它一定已存在。
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

	private void OnClueRevealed(ClueData clue)
	{
		// 这是整套 guide 的外部入口：当某个线索被 Reveal 后，
		// 去 sequences 里寻找“触发线索集合已全部满足”的那条序列。
		if (!guideEnabled || clue == null || string.IsNullOrEmpty(clue.id))
		{
			return;
		}

		if (isGuiding || pendingSequence != null)
		{
			Debug.Log("[GuideManager] A guide is already running, ignore new trigger.");
			return;
		}

		if (sequences == null)
		{
			return;
		}

		for (int i = 0; i < sequences.Count; i++)
		{
			var sequence = sequences[i];
			if (sequence == null || _consumedSequences.Contains(sequence))
			{
				continue;
			}

			var triggerIds = ResolveSequenceTriggerClueIds(sequence);
			if (triggerIds.Count == 0 || !triggerIds.Contains(clue.id))
			{
				continue;
			}

			if (!AreAllCluesRevealed(triggerIds))
			{
				continue;
			}

			Debug.Log($"[GuideManager] Trigger guide by clues: {string.Join(", ", triggerIds)}");

			if (SequenceCanStartWithoutTargets(sequence, triggerIds))
			{
				StartGuide(sequence);
				return;
			}

			pendingSequence = sequence;
			_pendingTargetKeys.Clear();

			foreach (var targetKey in CollectTriggerDependentKeys(sequence, triggerIds))
			{
				_pendingTargetKeys.Add(targetKey);
			}

			if (_pendingTargetKeys.Count == 0 || AreTargetsRegistered(_pendingTargetKeys))
			{
				StartGuide(sequence);
			}

			return;
		}
	}

	private void OnTargetRegistered(string key, RectTransform target)
	{
		if (!guideEnabled || isGuiding)
		{
			return;
		}

		if (pendingSequence != null && _pendingTargetKeys.Contains(key) && AreTargetsRegistered(_pendingTargetKeys))
		{
			Debug.Log($"[GuideManager] Waiting target registered, start guide: {key}");
			StartGuide(pendingSequence);
		}
	}

	private void StartGuide(GuideSequence sequence)
	{
		if (!guideEnabled || sequence == null)
		{
			return;
		}

		if (isGuiding)
		{
			Debug.LogWarning("[GuideManager] Guide is already running.");
			return;
		}

		isGuiding = true;
		pendingSequence = null;
		_pendingTargetKeys.Clear();

		_consumedSequences.Add(sequence);

		// Queue 的好处是能清晰表达“消费完当前 step 再进下一个 step”的串行语义。
		stepQueue = new Queue<GuideStep>(sequence.steps ?? new List<GuideStep>());

		if (_guideCoroutine != null)
		{
			StopCoroutine(_guideCoroutine);
		}

		_guideCoroutine = StartCoroutine(RunGuide());
	}

	private IEnumerator RunGuide()
	{
		Debug.Log("[GuideManager] Start running guide.");

		// 每个步骤自己决定是否 yield：
		// - Highlight/EndHighlight 往往是瞬时的。
		// - WaitClick/WaitDrag/Dialogue/Delay 则会阻塞后续步骤。
		while (stepQueue != null && stepQueue.Count > 0)
		{
			yield return ExecuteStep(stepQueue.Dequeue());
		}

		Debug.Log("[GuideManager] Guide finished.");

		_guideCoroutine = null;
		isGuiding = false;
	}

	private IEnumerator ExecuteStep(GuideStep step)
	{
		// stepType 是真正驱动行为的枚举。
		// 如果场景里的 stepType 配错，即使文案描述写着“延时”，也会按错误类型执行。
		switch (step.stepType)
		{
			case GuideStepType.Dialogue:
				yield return PlayDialogue(step);
				break;

			case GuideStepType.Highlight:
				yield return Highlight(step);
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

			case GuideStepType.Delay:
				yield return Delay(step);
				break;

			case GuideStepType.WaitInputSubmit:
				yield return WaitInputSubmit(step);
				break;

			case GuideStepType.WaitCluesCollected:
				yield return WaitCluesCollected(step);
				break;
		}
	}

	private IEnumerator PlayDialogue(GuideStep step)
	{
		if (step == null || step.dialogueTrigger == DialogueTriggerType.None)
		{
			yield break;
		}

		if (DialogueManager.Instance == null)
		{
			Debug.LogError("[GuideManager] DialogueManager.Instance is null, cannot play guide dialogue.");
			yield break;
		}

		// Guide 对话要求串行播放，不能和当前已有对话叠在一起。
		// 因此会先等待 DialogueManager 空闲，再用强制模式播放。
		yield return new WaitUntil(() => DialogueManager.Instance == null || !DialogueManager.Instance.IsDialogueActive());

		if (DialogueManager.Instance == null)
		{
			yield break;
		}

		bool done = false;
		var highlightController = GuideHighlightController.Instance;
		int dialogueLevelNumber = ResolveDialogueLevelNumber(step);
		int dialogueWaveNumber = step.dialogueTrigger == DialogueTriggerType.WaveSpawn
			? Mathf.Max(0, step.dialogueWaveNumber)
			: 0;

		highlightController?.SetHighlightedTargetsInputLocked(true);

		DialogueManager.Instance.ShowDialogue(
			dialogueLevelNumber,
			step.dialogueTrigger,
			dialogueWaveNumber,
			onComplete: () => done = true,
			isForced: true
		);

		yield return new WaitUntil(() => !isActiveAndEnabled || done);
		highlightController?.SetHighlightedTargetsInputLocked(false);
	}

	private IEnumerator Highlight(GuideStep step)
	{
		// 运行时弹出的 UI（例如 PopOut）往往不是同一帧就能被 Registry 查到，
		// 所以高亮前先等目标就绪，避免“场景配置正确但执行时序过快”导致的空高亮。
		yield return WaitForTargets(step?.targetKeys, "Highlight");
		HighlightKeys(step?.targetKeys, "Highlight");
	}

	private IEnumerator WaitClick(GuideStep step)
	{
		if (step?.targetKeys == null || step.targetKeys.Count == 0)
		{
			Debug.LogError("[GuideManager] WaitClick is missing target configuration.");
			yield break;
		}

		if (!GuideTargetRegistry.HasInstance)
		{
			Debug.LogError("[GuideManager] GuideTargetRegistry is not ready during WaitClick.");
			yield break;
		}

		// WaitClick 既支持线索列表项，也支持通用 Button。
		// 这样 guide 可以等待点击列表项、关闭按钮、确认按钮等不同 UI。
		yield return WaitForTargets(step.targetKeys, "WaitClick");

		string targetKey = step.targetKeys[0];
		var target = GuideTargetRegistry.Instance.Get(targetKey);
		if (target == null)
		{
			Debug.LogError($"[GuideManager] WaitClick target not found: {targetKey}");
			yield break;
		}

		var item = target.GetComponent<ClueListItemUI>();
		if (item != null)
		{
			bool clueClicked = false;

			_activeClickItem = item;
			_activeClickHandler = clue =>
			{
				if (clue != null && clue.id == targetKey)
				{
					clueClicked = true;
				}
			};

			item.OnClicked += _activeClickHandler;
			yield return new WaitUntil(() => !isActiveAndEnabled || clueClicked);
			CleanupActiveClickWaiter();
			yield break;
		}

		var button = target.GetComponent<Button>() ?? target.GetComponentInParent<Button>();
		if (button != null)
		{
			bool buttonClicked = false;

			_activeClickButton = button;
			_activeButtonClickHandler = () => buttonClicked = true;
			button.onClick.AddListener(_activeButtonClickHandler);

			yield return new WaitUntil(() => !isActiveAndEnabled || buttonClicked);
			CleanupActiveClickWaiter();
			yield break;
		}

		Debug.LogError($"[GuideManager] WaitClick target '{targetKey}' is not a ClueListItemUI or Button.");
	}

	private IEnumerator WaitDrag(GuideStep step)
	{
		if (step == null || string.IsNullOrWhiteSpace(step.dragSourceKey) || string.IsNullOrWhiteSpace(step.dragTargetKey))
		{
			Debug.LogError("[GuideManager] WaitDrag is missing source or target configuration.");
			yield break;
		}

		// 拖拽步骤默认会把源和目标一起高亮，避免还依赖前一个 Highlight step 的状态。
		HighlightKeys(new[] { step.dragSourceKey, step.dragTargetKey }, "WaitDrag");

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

	private IEnumerator Delay(GuideStep step)
	{
		if (step == null)
		{
			yield break;
		}

		float delaySeconds = Mathf.Max(0f, step.delaySeconds);
		if (delaySeconds <= 0f)
		{
			yield break;
		}

		yield return new WaitForSeconds(delaySeconds);
	}

	private IEnumerator WaitInputSubmit(GuideStep step)
	{
		if (step == null)
		{
			yield break;
		}

		// 这个步骤只监听“手动输入后回车”的事件。
		// 拖拽自动提交虽然也会走搜索命令，但 isManualSubmit=false，不会推进本步骤。
		string submitTargetKey = ResolveSubmitTargetKey(step);
		if (string.IsNullOrWhiteSpace(submitTargetKey))
		{
			Debug.LogError("[GuideManager] WaitInputSubmit is missing submitTargetKey or targetKeys[0].");
			yield break;
		}

		if (step.targetKeys != null && step.targetKeys.Count > 0)
		{
			HighlightKeys(step.targetKeys, "WaitInputSubmit");
		}
		else
		{
			HighlightKeys(new[] { submitTargetKey }, "WaitInputSubmit");
		}

		bool done = false;
		_activeInputSubmitHandler = (targetKey, inputText, isManualSubmit) =>
		{
			if (!isManualSubmit)
			{
				return;
			}

			if (targetKey != submitTargetKey)
			{
				return;
			}

			if (step.requireNonEmptySubmit && string.IsNullOrWhiteSpace(inputText))
			{
				return;
			}

			done = true;
		};

		GuideInputSubmitEventBus.OnInputSubmitted += _activeInputSubmitHandler;
		yield return new WaitUntil(() => !isActiveAndEnabled || done);
		CleanupActiveInputSubmitWaiter();
	}

	private IEnumerator WaitCluesCollected(GuideStep step)
	{
		if (step == null)
		{
			yield break;
		}

		var requiredClueIds = ResolveRequiredClueIds(step);
		if (requiredClueIds.Count == 0)
		{
			Debug.LogError("[GuideManager] WaitCluesCollected is missing requiredClueIds.");
			yield break;
		}

		if (step.targetKeys != null && step.targetKeys.Count > 0)
		{
			yield return WaitForTargets(step.targetKeys, "WaitCluesCollected");
			HighlightKeys(step.targetKeys, "WaitCluesCollected");
		}

		yield return new WaitUntil(() => !isActiveAndEnabled || AreAllCluesRevealed(requiredClueIds));
	}

	private void ReplayTriggeredGuides()
	{
		if (!guideEnabled || ClueManager.instance == null || sequences == null)
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

	private bool SequenceCanStartWithoutTarget(GuideSequence sequence, string triggerKey)
	{
		// 如果序列里后续步骤会直接引用 triggerKey 对应的 UI，
		// 就不能在 Reveal 的瞬间立刻开跑，而要等目标先注册到 Registry。
		if (sequence == null || sequence.steps == null || sequence.steps.Count == 0)
		{
			return true;
		}

		foreach (var step in sequence.steps)
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

	private int ResolveDialogueLevelNumber(GuideStep step)
	{
		if (step != null && step.dialogueLevelNumber >= 0)
		{
			return step.dialogueLevelNumber;
		}

		if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentLevelNumber >= 0)
		{
			return DialogueManager.Instance.CurrentLevelNumber;
		}

		return 0;
	}

	private string ResolveSubmitTargetKey(GuideStep step)
	{
		if (step == null)
		{
			return string.Empty;
		}

		if (!string.IsNullOrWhiteSpace(step.submitTargetKey))
		{
			return step.submitTargetKey;
		}

		if (step.targetKeys != null && step.targetKeys.Count > 0)
		{
			return step.targetKeys[0];
		}

		return string.Empty;
	}

	private List<string> ResolveRequiredClueIds(GuideStep step)
	{
		HashSet<string> ids = new(StringComparer.Ordinal);
		if (step?.requiredClueIds != null)
		{
			for (int i = 0; i < step.requiredClueIds.Count; i++)
			{
				var clueId = step.requiredClueIds[i];
				if (!string.IsNullOrWhiteSpace(clueId))
				{
					ids.Add(clueId);
				}
			}
		}

		return new List<string>(ids);
	}

	private List<string> ResolveSequenceTriggerClueIds(GuideSequence sequence)
	{
		HashSet<string> ids = new(StringComparer.Ordinal);
		if (sequence?.triggerClueIds != null)
		{
			for (int i = 0; i < sequence.triggerClueIds.Count; i++)
			{
				var clueId = sequence.triggerClueIds[i];
				if (!string.IsNullOrWhiteSpace(clueId))
				{
					ids.Add(clueId);
				}
			}
		}

		return new List<string>(ids);
	}

	private bool AreAllCluesRevealed(IReadOnlyCollection<string> clueIds)
	{
		if (clueIds == null || clueIds.Count == 0 || ClueManager.instance == null)
		{
			return false;
		}

		foreach (var clueId in clueIds)
		{
			if (string.IsNullOrWhiteSpace(clueId) || !ClueManager.instance.IsRevealed(clueId))
			{
				return false;
			}
		}

		return true;
	}

	private bool SequenceCanStartWithoutTargets(GuideSequence sequence, IReadOnlyCollection<string> triggerKeys)
	{
		if (sequence == null || sequence.steps == null || sequence.steps.Count == 0 || triggerKeys == null || triggerKeys.Count == 0)
		{
			return true;
		}

		foreach (var triggerKey in triggerKeys)
		{
			foreach (var step in sequence.steps)
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
		}

		return true;
	}

	private List<string> CollectTriggerDependentKeys(GuideSequence sequence, IReadOnlyCollection<string> triggerKeys)
	{
		HashSet<string> dependentKeys = new(StringComparer.Ordinal);
		if (sequence?.steps == null || triggerKeys == null || triggerKeys.Count == 0)
		{
			return new List<string>();
		}

		foreach (var triggerKey in triggerKeys)
		{
			foreach (var step in sequence.steps)
			{
				if (step == null)
				{
					continue;
				}

				if (step.targetKeys != null && step.targetKeys.Contains(triggerKey))
				{
					dependentKeys.Add(triggerKey);
				}

				if (step.dragSourceKey == triggerKey || step.dragTargetKey == triggerKey)
				{
					dependentKeys.Add(triggerKey);
				}
			}
		}

		return new List<string>(dependentKeys);
	}

	private bool AreTargetsRegistered(IEnumerable<string> keys)
	{
		if (!GuideTargetRegistry.HasInstance || keys == null)
		{
			return false;
		}

		foreach (var key in keys)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			if (GuideTargetRegistry.Instance.Get(key) == null)
			{
				return false;
			}
		}

		return true;
	}

	private IEnumerator WaitForTargets(IEnumerable<string> keys, string context)
	{
		// 这是处理动态 UI 时序问题的公共入口：
		// 例如详情页、弹窗按钮、运行时生成的 clue item，都可能比 guide 晚一帧注册。
		// 这里等待的是“GuideTargetRegistry 能够按 key 查询到目标”，而不是目标 GameObject 单独存在。
		if (!GuideTargetRegistry.HasInstance || keys == null)
		{
			yield break;
		}

		HashSet<string> unresolvedKeys = new();
		foreach (var key in keys)
		{
			if (string.IsNullOrWhiteSpace(key))
			{
				continue;
			}

			if (GuideTargetRegistry.Instance.Get(key) == null)
			{
				unresolvedKeys.Add(key);
			}
		}

		if (unresolvedKeys.Count == 0)
		{
			yield break;
		}

		float timeout = Mathf.Max(0f, targetResolveTimeout);
		float startTime = Time.unscaledTime;

		while (unresolvedKeys.Count > 0 && isActiveAndEnabled)
		{
			unresolvedKeys.RemoveWhere(key => GuideTargetRegistry.Instance.Get(key) != null);
			if (unresolvedKeys.Count == 0)
			{
				yield break;
			}

			if (timeout <= 0f || Time.unscaledTime - startTime >= timeout)
			{
				Debug.LogWarning($"[GuideManager] {context} timed out waiting for targets: {string.Join(", ", unresolvedKeys)}");
				yield break;
			}

			yield return null;
		}
	}

	private void HighlightKeys(IEnumerable<string> keys, string context)
	{
		if (!GuideTargetRegistry.HasInstance)
		{
			Debug.LogError($"[GuideManager] {context} failed because GuideTargetRegistry is not ready.");
			return;
		}

		if (GuideHighlightController.Instance == null)
		{
			Debug.LogError($"[GuideManager] {context} failed because GuideHighlightController is not ready.");
			return;
		}

		if (keys == null)
		{
			GuideHighlightController.Instance.ClearHighlight();
			return;
		}

		// 这里统一做 key 去重与丢失目标兜底。
		// 真正的遮罩/挖洞实现全部交给 GuideHighlightController 处理。
		HashSet<string> uniqueKeys = new();
		List<RectTransform> targets = new();

		foreach (var key in keys)
		{
			if (string.IsNullOrWhiteSpace(key) || !uniqueKeys.Add(key))
			{
				continue;
			}

			var target = GuideTargetRegistry.Instance.Get(key);
			if (target != null)
			{
				targets.Add(target);
			}
			else
			{
				Debug.LogWarning($"[GuideManager] {context} target not found: {key}");
			}
		}

		if (targets.Count == 0)
		{
			GuideHighlightController.Instance.ClearHighlight();
			return;
		}

		GuideHighlightController.Instance.HighlightMultiple(targets);
	}

	private void CleanupActiveWaiters()
	{
		// Guide 退出或被打断时一定要撤销所有临时监听。
		// 否则下一次 guide 可能会被上一次残留的点击/拖拽/输入事件误推进。
		CleanupActiveClickWaiter();
		CleanupActiveDragWaiter();
		CleanupActiveInputSubmitWaiter();
	}

	private void CleanupActiveClickWaiter()
	{
		if (_activeClickItem != null && _activeClickHandler != null)
		{
			_activeClickItem.OnClicked -= _activeClickHandler;
		}

		if (_activeClickButton != null && _activeButtonClickHandler != null)
		{
			_activeClickButton.onClick.RemoveListener(_activeButtonClickHandler);
		}

		_activeClickItem = null;
		_activeClickHandler = null;
		_activeClickButton = null;
		_activeButtonClickHandler = null;
	}

	private void CleanupActiveDragWaiter()
	{
		if (_activeDragHandler != null)
		{
			GuideDragEventBus.OnDragSuccess -= _activeDragHandler;
		}

		_activeDragHandler = null;
	}

	private void CleanupActiveInputSubmitWaiter()
	{
		if (_activeInputSubmitHandler != null)
		{
			GuideInputSubmitEventBus.OnInputSubmitted -= _activeInputSubmitHandler;
		}

		_activeInputSubmitHandler = null;
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
		_pendingTargetKeys.Clear();
		isGuiding = false;

		if (GuideHighlightController.Instance != null)
		{
			GuideHighlightController.Instance.ClearHighlight();
		}
	}
}
