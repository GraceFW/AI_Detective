using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通用引导遮罩宿主。
/// 负责使用四个矩形遮罩在目标区域挖出一个可穿透的矩形洞口。
/// </summary>
[DisallowMultipleComponent]
public class GuideMaskOverlayHost : MonoBehaviour
{
	[SerializeField] private Color maskColor = new Color(0f, 0f, 0f, 0.7f);
	[SerializeField] private Color frameColor = new Color(1f, 0.9f, 0.2f, 1f);
	[SerializeField] private float frameThickness = 4f;
	[SerializeField] private bool blockOutsideRaycasts = true;

	private RectTransform _rootRect;
	private Image _sourceImage;
	private bool _restoreSourceImageWhenHidden;
	private bool _sourceImageOriginalEnabled;
	private bool _sourceImageOriginalRaycastTarget;

	private GameObject _overlayRoot;
	private RectTransform _overlayRect;
	private readonly Image[] _blockers = new Image[4];
	private readonly Image[] _frameLines = new Image[4];

	private void Awake()
	{
		_rootRect = transform as RectTransform;
		EnsureVisualTree();
		HideHole();
	}

	public void Initialize(Image sourceImage, bool restoreSourceImageWhenHidden)
	{
		_rootRect = transform as RectTransform;
		_sourceImage = sourceImage;
		_restoreSourceImageWhenHidden = restoreSourceImageWhenHidden;

		if (_sourceImage != null)
		{
			_sourceImageOriginalEnabled = _sourceImage.enabled;
			_sourceImageOriginalRaycastTarget = _sourceImage.raycastTarget;
			maskColor = _sourceImage.color;
		}

		EnsureVisualTree();
		ApplyMaskStyle();
		HideHole();
	}

	public void SetStyle(Color blockerColor, Color focusFrameColor, float focusFrameThickness, bool shouldBlockOutsideRaycasts)
	{
		maskColor = blockerColor;
		frameColor = focusFrameColor;
		frameThickness = Mathf.Max(1f, focusFrameThickness);
		blockOutsideRaycasts = shouldBlockOutsideRaycasts;
		ApplyMaskStyle();
	}

	public void ShowHole(Rect screenRect)
	{
		if (_rootRect == null)
		{
			_rootRect = transform as RectTransform;
		}

		if (_rootRect == null)
		{
			Debug.LogWarning("[GuideMaskOverlayHost] 缺少 RectTransform，无法显示挖洞遮罩");
			return;
		}

		EnsureVisualTree();
		SyncMaskColorFromSourceImage();
		ApplyMaskStyle();

		if (_sourceImage != null)
		{
			_sourceImage.enabled = false;
			_sourceImage.raycastTarget = false;
		}

		_overlayRoot.SetActive(true);
		_overlayRoot.transform.SetAsLastSibling();

		if (!TryGetLocalHole(screenRect, out var localHole))
		{
			HideHole();
			return;
		}

		LayoutBlockers(localHole);
		LayoutFrame(localHole);
	}

	public void HideHole()
	{
		if (_overlayRoot != null)
		{
			_overlayRoot.SetActive(false);
		}

		RestoreSourceImage();
	}

	private void EnsureVisualTree()
	{
		if (_overlayRoot != null)
		{
			return;
		}

		_overlayRoot = new GameObject("GuideMaskOverlay");
		_overlayRoot.transform.SetParent(transform, false);
		_overlayRoot.transform.SetAsLastSibling();

		_overlayRect = _overlayRoot.AddComponent<RectTransform>();
		_overlayRect.anchorMin = Vector2.zero;
		_overlayRect.anchorMax = Vector2.one;
		_overlayRect.offsetMin = Vector2.zero;
		_overlayRect.offsetMax = Vector2.zero;
		_overlayRect.pivot = new Vector2(0.5f, 0.5f);

		CreateBlocker(0, "TopBlocker");
		CreateBlocker(1, "BottomBlocker");
		CreateBlocker(2, "LeftBlocker");
		CreateBlocker(3, "RightBlocker");

		CreateFrameLine(0, "TopFrame");
		CreateFrameLine(1, "BottomFrame");
		CreateFrameLine(2, "LeftFrame");
		CreateFrameLine(3, "RightFrame");

		ApplyMaskStyle();
	}

	private void CreateBlocker(int index, string name)
	{
		var blocker = new GameObject(name);
		blocker.transform.SetParent(_overlayRoot.transform, false);

		var rect = blocker.AddComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);

		var image = blocker.AddComponent<Image>();
		_blockers[index] = image;
	}

	private void CreateFrameLine(int index, string name)
	{
		var frame = new GameObject(name);
		frame.transform.SetParent(_overlayRoot.transform, false);

		var rect = frame.AddComponent<RectTransform>();
		rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);

		var image = frame.AddComponent<Image>();
		image.raycastTarget = false;
		_frameLines[index] = image;
	}

	private void ApplyMaskStyle()
	{
		foreach (var blocker in _blockers)
		{
			if (blocker == null)
			{
				continue;
			}

			blocker.color = maskColor;
			blocker.raycastTarget = blockOutsideRaycasts;
		}

		foreach (var frameLine in _frameLines)
		{
			if (frameLine == null)
			{
				continue;
			}

			frameLine.color = frameColor;
			frameLine.raycastTarget = false;
		}
	}

	private void SyncMaskColorFromSourceImage()
	{
		if (_sourceImage == null)
		{
			return;
		}

		maskColor = _sourceImage.color;
	}

	private void RestoreSourceImage()
	{
		if (_sourceImage == null)
		{
			return;
		}

		if (_restoreSourceImageWhenHidden)
		{
			_sourceImage.color = maskColor;
			_sourceImage.enabled = _sourceImageOriginalEnabled;
			_sourceImage.raycastTarget = _sourceImageOriginalRaycastTarget;
		}
		else
		{
			_sourceImage.enabled = false;
			_sourceImage.raycastTarget = false;
		}
	}

	private bool TryGetLocalHole(Rect screenRect, out Rect localHole)
	{
		localHole = default;

		var minScreen = new Vector2(screenRect.xMin, screenRect.yMin);
		var maxScreen = new Vector2(screenRect.xMax, screenRect.yMax);

		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, minScreen, null, out var localMin))
		{
			return false;
		}

		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, maxScreen, null, out var localMax))
		{
			return false;
		}

		var xMin = Mathf.Min(localMin.x, localMax.x);
		var xMax = Mathf.Max(localMin.x, localMax.x);
		var yMin = Mathf.Min(localMin.y, localMax.y);
		var yMax = Mathf.Max(localMin.y, localMax.y);

		var root = _rootRect.rect;
		xMin = Mathf.Clamp(xMin, root.xMin, root.xMax);
		xMax = Mathf.Clamp(xMax, root.xMin, root.xMax);
		yMin = Mathf.Clamp(yMin, root.yMin, root.yMax);
		yMax = Mathf.Clamp(yMax, root.yMin, root.yMax);

		if (xMax - xMin <= 0.5f || yMax - yMin <= 0.5f)
		{
			return false;
		}

		localHole = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
		return true;
	}

	private void LayoutBlockers(Rect hole)
	{
		var root = _rootRect.rect;

		SetRect(_blockers[0].rectTransform, root.xMin, hole.yMax, root.xMax, root.yMax);
		SetRect(_blockers[1].rectTransform, root.xMin, root.yMin, root.xMax, hole.yMin);
		SetRect(_blockers[2].rectTransform, root.xMin, hole.yMin, hole.xMin, hole.yMax);
		SetRect(_blockers[3].rectTransform, hole.xMax, hole.yMin, root.xMax, hole.yMax);
	}

	private void LayoutFrame(Rect hole)
	{
		float thickness = Mathf.Clamp(frameThickness, 1f, Mathf.Min(hole.width, hole.height));

		SetRect(_frameLines[0].rectTransform, hole.xMin, hole.yMax - thickness, hole.xMax, hole.yMax);
		SetRect(_frameLines[1].rectTransform, hole.xMin, hole.yMin, hole.xMax, hole.yMin + thickness);
		SetRect(_frameLines[2].rectTransform, hole.xMin, hole.yMin, hole.xMin + thickness, hole.yMax);
		SetRect(_frameLines[3].rectTransform, hole.xMax - thickness, hole.yMin, hole.xMax, hole.yMax);
	}

	private static void SetRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
	{
		float width = Mathf.Max(0f, xMax - xMin);
		float height = Mathf.Max(0f, yMax - yMin);

		rect.gameObject.SetActive(width > 0f && height > 0f);
		if (width <= 0f || height <= 0f)
		{
			return;
		}

		rect.anchoredPosition = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
		rect.sizeDelta = new Vector2(width, height);
	}
}
