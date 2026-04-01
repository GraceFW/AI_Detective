using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 引导高亮控制器。
/// 负责维护当前聚焦目标，并将同一个洞口同步到 GuideLayer 与对话遮罩。
/// </summary>
public class GuideHighlightController : MonoBehaviour
{
	public static GuideHighlightController Instance;

	[SerializeField] private Vector2 holePadding = new Vector2(24f, 16f);
	[SerializeField] private Color frameColor = new Color(1f, 0.9f, 0.2f, 1f);
	[SerializeField] private float frameThickness = 4f;
	[SerializeField] private bool blockOutsideRaycasts = true;
	[SerializeField] private int guideLayerSortingOrder = 9000;

	private RectTransform _currentTarget;
	private bool _hasActiveHighlight;

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

		if (!_hasActiveHighlight || _currentTarget == null)
		{
			HideAllHosts();
			return;
		}

		if (!_currentTarget.gameObject.activeInHierarchy)
		{
			HideAllHosts();
			return;
		}

		if (!TryGetTargetScreenRect(_currentTarget, out var screenRect))
		{
			HideAllHosts();
			return;
		}

		var paddedRect = ExpandRect(screenRect, holePadding);
		SyncHosts(paddedRect);
	}

	public void HighlightMultiple(List<RectTransform> targets)
	{
		RectTransform firstValidTarget = null;
		int validCount = 0;

		if (targets != null)
		{
			foreach (var target in targets)
			{
				if (target == null)
				{
					continue;
				}

				validCount++;
				if (firstValidTarget == null)
				{
					firstValidTarget = target;
				}
			}
		}

		if (validCount > 1)
		{
			Debug.LogWarning("[GuideHighlightController] 当前版本仅支持单个挖洞目标，将只使用第一个有效目标。");
		}

		if (firstValidTarget == null)
		{
			ClearHighlight();
			return;
		}

		_currentTarget = firstValidTarget;
		_hasActiveHighlight = true;
	}

	public void ClearHighlight()
	{
		_currentTarget = null;
		_hasActiveHighlight = false;
		HideAllHosts();
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

	private void EnsureGuideLayerHost()
	{
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

	private void SyncHosts(Rect screenRect)
	{
		_guideLayerHost?.ShowHole(screenRect);

		if (_dialogueHost != null)
		{
			if (_dialogueHost.gameObject.activeInHierarchy)
			{
				_dialogueHost.ShowHole(screenRect);
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
				_nameInputHost.ShowHole(screenRect);
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

	private void OnDestroy()
	{
		if (Instance == this)
		{
			HideAllHosts();
			Instance = null;
		}
	}
}
