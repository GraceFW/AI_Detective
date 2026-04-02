using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 引导高亮控制器。
/// 负责把“当前应该被关注的 UI 列表”同步到多个遮罩宿主：
/// - Guide 自己的顶层遮罩
/// - DialogueManager 的背景遮罩
/// - NameInputDialog 的背景遮罩
///
/// 这样无论当前界面上层是谁，都能看到一致的“挖洞 + 描边”效果。
/// 同时这里也负责把高亮目标区域告诉 DialogueManager，
/// 让对话文本区域在必要时做避让。
/// </summary>
public class GuideHighlightController : MonoBehaviour
{
	private struct CanvasGroupLockState
	{
		public CanvasGroup canvasGroup;
		public bool hadComponent;
		public bool interactable;
		public bool blocksRaycasts;
		public bool ignoreParentGroups;
	}

	public static GuideHighlightController Instance;

	[SerializeField] private Vector2 holePadding = new Vector2(24f, 16f);
	[SerializeField] private Color frameColor = new Color(1f, 0.9f, 0.2f, 1f);
	[SerializeField] private float frameThickness = 4f;
	[SerializeField] private bool blockOutsideRaycasts = true;
	[SerializeField] private int guideLayerSortingOrder = 9000;

	// _currentTargets 保存逻辑目标，_currentScreenRects 保存每帧计算出来的屏幕矩形。
	// 之所以不直接缓存 Rect，是因为目标 UI 可能会移动、缩放、重排。
	private readonly List<RectTransform> _currentTargets = new();
	private readonly List<Rect> _currentScreenRects = new();
	private readonly Dictionary<CanvasGroup, CanvasGroupLockState> _lockedTargetInputStates = new();
	private bool _hasActiveHighlight;
	private bool _lockHighlightedTargetInput;

	private GuideMaskOverlayHost _guideLayerHost;
	private GuideMaskOverlayHost _dialogueHost;
	private GuideMaskOverlayHost _nameInputHost;

	private void Awake()
	{
		ClaimInstance();
		EnsureInitialized();
	}

	public void EnsureInitialized()
	{
		ClaimInstance();
		EnsureGuideLayerHost();
		EnsureDialogueHost();
		EnsureNameInputHost();
	}

	private void LateUpdate()
	{
		if (Instance != this)
		{
			return;
		}

		EnsureGuideLayerHost();
		EnsureDialogueHost();
		EnsureNameInputHost();

		// 选择在 LateUpdate 里刷新，是为了尽量等布局系统、动画和拖拽位置都更新完，
		// 避免高亮框比真实 UI 慢一帧或者抖动。
		if (!_hasActiveHighlight || _currentTargets.Count == 0)
		{
			HideAllHosts();
			DialogueManager.Instance?.ClearGuideLayoutAvoidance();
			return;
		}

		_currentScreenRects.Clear();

		for (int i = 0; i < _currentTargets.Count; i++)
		{
			var target = _currentTargets[i];
			if (target == null || !target.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (!TryGetTargetScreenRect(target, out var screenRect))
			{
				continue;
			}

			_currentScreenRects.Add(ExpandRect(screenRect, holePadding));
		}

		if (_currentScreenRects.Count == 0)
		{
			HideAllHosts();
			DialogueManager.Instance?.ClearGuideLayoutAvoidance();
			return;
		}

		SyncHosts(_currentScreenRects);
		DialogueManager.Instance?.ApplyGuideLayoutAvoidance(_currentScreenRects);
	}

	public void HighlightMultiple(List<RectTransform> targets)
	{
		// 引导层支持多目标高亮，例如“拖拽源 + 拖拽目标”同时聚焦。
		_currentTargets.Clear();

		if (targets == null)
		{
			ClearHighlight();
			return;
		}

		HashSet<RectTransform> uniqueTargets = new();
		for (int i = 0; i < targets.Count; i++)
		{
			var target = targets[i];
			if (target == null || !uniqueTargets.Add(target))
			{
				continue;
			}

			_currentTargets.Add(target);
		}

		if (_currentTargets.Count == 0)
		{
			ClearHighlight();
			return;
		}

		_hasActiveHighlight = true;
		RefreshTargetInputLockState();
	}

	public void ClearHighlight()
	{
		RestoreTargetInputLockState();
		_currentTargets.Clear();
		_currentScreenRects.Clear();
		_hasActiveHighlight = false;
		HideAllHosts();
		DialogueManager.Instance?.ClearGuideLayoutAvoidance();
	}

	public void SetHighlightedTargetsInputLocked(bool locked)
	{
		if (_lockHighlightedTargetInput == locked)
		{
			return;
		}

		_lockHighlightedTargetInput = locked;
		RefreshTargetInputLockState();
	}

	public bool IsScreenPointOverHighlightedTarget(Vector2 screenPosition)
	{
		// DialogueManager 会用这个方法判断：
		// 当前鼠标点击是否命中了高亮目标。
		// 如果命中，则这次点击不应该同时推进引导对话文本。
		if (!_hasActiveHighlight || _currentTargets.Count == 0)
		{
			return false;
		}

		for (int i = 0; i < _currentTargets.Count; i++)
		{
			var target = _currentTargets[i];
			if (target == null || !target.gameObject.activeInHierarchy)
			{
				continue;
			}

			if (RectTransformUtility.RectangleContainsScreenPoint(target, screenPosition, GetTargetEventCamera(target)))
			{
				return true;
			}
		}

		return false;
	}

	private void ClaimInstance()
	{
		if (Instance != null && Instance != this)
		{
			enabled = false;
			return;
		}

		Instance = this;
		enabled = true;
	}

	private void RefreshTargetInputLockState()
	{
		RestoreTargetInputLockState();

		if (!_lockHighlightedTargetInput || !_hasActiveHighlight || _currentTargets.Count == 0)
		{
			return;
		}

		HashSet<CanvasGroup> processedGroups = new();
		for (int i = 0; i < _currentTargets.Count; i++)
		{
			var target = _currentTargets[i];
			if (target == null)
			{
				continue;
			}

			var canvasGroup = target.GetComponent<CanvasGroup>();
			bool hadComponent = canvasGroup != null;
			if (canvasGroup == null)
			{
				canvasGroup = target.gameObject.AddComponent<CanvasGroup>();
			}

			if (!processedGroups.Add(canvasGroup))
			{
				continue;
			}

			_lockedTargetInputStates[canvasGroup] = new CanvasGroupLockState
			{
				canvasGroup = canvasGroup,
				hadComponent = hadComponent,
				interactable = canvasGroup.interactable,
				blocksRaycasts = canvasGroup.blocksRaycasts,
				ignoreParentGroups = canvasGroup.ignoreParentGroups
			};

			canvasGroup.interactable = false;
			canvasGroup.blocksRaycasts = false;
		}
	}

	private void RestoreTargetInputLockState()
	{
		foreach (var state in _lockedTargetInputStates.Values)
		{
			if (state.canvasGroup == null)
			{
				continue;
			}

			if (state.hadComponent)
			{
				state.canvasGroup.interactable = state.interactable;
				state.canvasGroup.blocksRaycasts = state.blocksRaycasts;
				state.canvasGroup.ignoreParentGroups = state.ignoreParentGroups;
			}
			else
			{
				Destroy(state.canvasGroup);
			}
		}

		_lockedTargetInputStates.Clear();
	}

	private void EnsureGuideLayerHost()
	{
		// Guide 自己的遮罩层挂在单独的顶层 Canvas 上，
		// 确保不会被普通 UI 排序遮住。
		if (_guideLayerHost != null)
		{
			_guideLayerHost.SetStyle(GetGuideLayerMaskColor(), frameColor, frameThickness, blockOutsideRaycasts);
			return;
		}

		EnsureGuideLayerCanvas();
		transform.SetAsLastSibling();

		_guideLayerHost = GetComponent<GuideMaskOverlayHost>();
		if (_guideLayerHost == null)
		{
			_guideLayerHost = gameObject.AddComponent<GuideMaskOverlayHost>();
		}

		_guideLayerHost.Initialize(FindGuideLayerDarkMask(), false);
		_guideLayerHost.SetStyle(GetGuideLayerMaskColor(), frameColor, frameThickness, blockOutsideRaycasts);
	}

	private void EnsureGuideLayerCanvas()
	{
		var canvas = GetComponent<Canvas>();
		if (canvas == null)
		{
			canvas = gameObject.AddComponent<Canvas>();
		}

		canvas.overrideSorting = true;
		canvas.sortingOrder = guideLayerSortingOrder;

		if (GetComponent<GraphicRaycaster>() == null)
		{
			gameObject.AddComponent<GraphicRaycaster>();
		}
	}

	private void EnsureDialogueHost()
	{
		// Dialogue 的遮罩宿主直接复用现有 backgroundMask，
		// 不改 Dialogue 主逻辑，只在需要时附加“多洞遮罩能力”。
		if (DialogueManager.Instance == null || DialogueManager.Instance.backgroundMask == null)
		{
			return;
		}

		var backgroundMask = DialogueManager.Instance.backgroundMask;
		if (_dialogueHost == null || _dialogueHost.gameObject != backgroundMask.gameObject)
		{
			_dialogueHost = backgroundMask.GetComponent<GuideMaskOverlayHost>();
			if (_dialogueHost == null)
			{
				_dialogueHost = backgroundMask.gameObject.AddComponent<GuideMaskOverlayHost>();
			}

			_dialogueHost.Initialize(backgroundMask, true);
		}

		_dialogueHost.SetStyle(backgroundMask.color, frameColor, frameThickness, blockOutsideRaycasts);
	}

	private void EnsureNameInputHost()
	{
		// NameInputDialog 与 Dialogue 一样，都是“在现有遮罩上增量扩展”。
		if (NameInputDialog.Instance == null || NameInputDialog.Instance.BackgroundMask == null)
		{
			return;
		}

		var backgroundMask = NameInputDialog.Instance.BackgroundMask;
		if (_nameInputHost == null || _nameInputHost.gameObject != backgroundMask.gameObject)
		{
			_nameInputHost = backgroundMask.GetComponent<GuideMaskOverlayHost>();
			if (_nameInputHost == null)
			{
				_nameInputHost = backgroundMask.gameObject.AddComponent<GuideMaskOverlayHost>();
			}

			_nameInputHost.Initialize(backgroundMask, true);
		}

		_nameInputHost.SetStyle(backgroundMask.color, frameColor, frameThickness, blockOutsideRaycasts);
	}

	private void SyncHosts(IReadOnlyList<Rect> screenRects)
	{
		// Guide 顶层一定同步；
		// Dialogue / NameInput 只有在对应界面处于激活时才显示洞，否则隐藏即可。
		_guideLayerHost?.ShowHoles(screenRects);

		if (_dialogueHost != null)
		{
			if (_dialogueHost.gameObject.activeInHierarchy)
			{
				_dialogueHost.ShowHoles(screenRects);
			}
			else
			{
				_dialogueHost.HideHole();
			}
		}

		if (_nameInputHost != null)
		{
			if (_nameInputHost.gameObject.activeInHierarchy)
			{
				_nameInputHost.ShowHoles(screenRects);
			}
			else
			{
				_nameInputHost.HideHole();
			}
		}
	}

	private void HideAllHosts()
	{
		_guideLayerHost?.HideHole();
		_dialogueHost?.HideHole();
		_nameInputHost?.HideHole();
	}

	private Image FindGuideLayerDarkMask()
	{
		var images = GetComponentsInChildren<Image>(true);
		foreach (var image in images)
		{
			if (image != null && image.gameObject.name == "DarkMask")
			{
				return image;
			}
		}

		return null;
	}

	private Color GetGuideLayerMaskColor()
	{
		var darkMask = FindGuideLayerDarkMask();
		return darkMask != null ? darkMask.color : new Color(0f, 0f, 0f, 0.7f);
	}

	private static Rect ExpandRect(Rect rect, Vector2 padding)
	{
		return Rect.MinMaxRect(
			rect.xMin - padding.x,
			rect.yMin - padding.y,
			rect.xMax + padding.x,
			rect.yMax + padding.y
		);
	}

	private static bool TryGetTargetScreenRect(RectTransform target, out Rect screenRect)
	{
		// 统一把任意 UI 目标转换为屏幕空间矩形，
		// 后续 GuideMaskOverlayHost 只需要处理“屏幕矩形挖洞”这一件事。
		screenRect = default;
		if (target == null)
		{
			return false;
		}

		var corners = new Vector3[4];
		target.GetWorldCorners(corners);

		var canvas = target.GetComponentInParent<Canvas>();
		Camera eventCamera = null;
		if (canvas != null)
		{
			var rootCanvas = canvas.rootCanvas;
			if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
			{
				eventCamera = rootCanvas.worldCamera;
			}
		}

		var min = new Vector2(float.MaxValue, float.MaxValue);
		var max = new Vector2(float.MinValue, float.MinValue);

		for (int i = 0; i < corners.Length; i++)
		{
			var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
			min = Vector2.Min(min, screenPoint);
			max = Vector2.Max(max, screenPoint);
		}

		if (max.x - min.x <= 0.5f || max.y - min.y <= 0.5f)
		{
			return false;
		}

		screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
		return true;
	}

	private static Camera GetTargetEventCamera(RectTransform target)
	{
		if (target == null)
		{
			return null;
		}

		var canvas = target.GetComponentInParent<Canvas>();
		if (canvas == null)
		{
			return null;
		}

		var rootCanvas = canvas.rootCanvas;
		if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
		{
			return rootCanvas.worldCamera;
		}

		return null;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			RestoreTargetInputLockState();
			HideAllHosts();
			DialogueManager.Instance?.ClearGuideLayoutAvoidance();
			Instance = null;
		}
	}
}
