using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 遮罩挖洞宿主。
/// 输入是一组屏幕矩形，输出是：
/// - 覆盖整块区域的半透明遮罩
/// - 一个或多个真正可穿透的矩形洞
/// - 每个洞四周的高亮描边
///
/// 这里不负责“哪些 UI 该高亮”，只负责把最终的几何结果画出来。
/// 上层的 GuideHighlightController 会在每帧把目标 UI 转成屏幕矩形后交给这里。
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
	private readonly List<Image> _blockers = new();
	private readonly List<Image> _frameLines = new();
	// _localHoles 是转换到当前宿主本地坐标后的洞区域。
	// _blockerRects 是真正要绘制出来的“遮罩块”矩形集合。
	private readonly List<Rect> _localHoles = new();
	private readonly List<Rect> _blockerRects = new();
	private readonly List<float> _gridXs = new();
	private readonly List<float> _gridYs = new();
	private readonly List<Rect> _singleHole = new List<Rect>(1);

	private void Awake()
	{
		_rootRect = transform as RectTransform;
		EnsureVisualTree();
		HideHole();
	}

	public void Initialize(Image sourceImage, bool restoreSourceImageWhenHidden)
	{
		// 有些宿主是“附着在已有背景遮罩 Image 上”的，例如 Dialogue 背景。
		// 此时需要记住原始状态，隐藏时再恢复，避免破坏原有界面表现。
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
		_singleHole.Clear();
		_singleHole.Add(screenRect);
		ShowHoles(_singleHole);
	}

	public void ShowHoles(IReadOnlyList<Rect> screenRects)
	{
		if (_rootRect == null)
		{
			_rootRect = transform as RectTransform;
		}

		if (_rootRect == null)
		{
			Debug.LogWarning("[GuideMaskOverlayHost] Missing RectTransform, cannot show guide mask.");
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

		// 外部传进来的都是屏幕空间矩形，先换算为当前宿主的本地矩形。
		// 这样同一套挖洞逻辑可以复用在 Guide、Dialogue、NameInput 不同 Canvas 上。
		_localHoles.Clear();

		if (screenRects != null)
		{
			for (int i = 0; i < screenRects.Count; i++)
			{
				if (TryGetLocalHole(screenRects[i], out var localHole))
				{
					_localHoles.Add(localHole);
				}
			}
		}

		if (_localHoles.Count == 0)
		{
			HideHole();
			return;
		}

		// 如果多个洞相交或紧挨着，先做归并，避免生成一堆碎遮罩块。
		NormalizeHoles(_localHoles);
		LayoutBlockers(_localHoles);
		LayoutFrames(_localHoles);
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
		// 所有遮罩块和描边线都挂在一个独立子节点下，方便整体开关与排序。
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

		ApplyMaskStyle();
	}

	private void EnsureBlockerCount(int count)
	{
		while (_blockers.Count < count)
		{
			var blocker = new GameObject($"Blocker{_blockers.Count}");
			blocker.transform.SetParent(_overlayRoot.transform, false);

			var rect = blocker.AddComponent<RectTransform>();
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);

			var image = blocker.AddComponent<Image>();
			_blockers.Add(image);
		}
	}

	private void EnsureFrameCount(int count)
	{
		while (_frameLines.Count < count)
		{
			var frame = new GameObject($"Frame{_frameLines.Count}");
			frame.transform.SetParent(_overlayRoot.transform, false);

			var rect = frame.AddComponent<RectTransform>();
			rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
			rect.pivot = new Vector2(0.5f, 0.5f);

			var image = frame.AddComponent<Image>();
			image.raycastTarget = false;
			_frameLines.Add(image);
		}
	}

	private void ApplyMaskStyle()
	{
		for (int i = 0; i < _blockers.Count; i++)
		{
			var blocker = _blockers[i];
			if (blocker == null)
			{
				continue;
			}

			blocker.color = maskColor;
			blocker.raycastTarget = blockOutsideRaycasts;
		}

		for (int i = 0; i < _frameLines.Count; i++)
		{
			var frameLine = _frameLines[i];
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
		if (_sourceImage != null)
		{
			maskColor = _sourceImage.color;
		}
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
		// 屏幕矩形 -> 当前宿主局部矩形。
		// 这里会顺便裁剪到宿主 rect 内部，避免洞超出遮罩可视范围。
		localHole = default;

		var minScreen = new Vector2(screenRect.xMin, screenRect.yMin);
		var maxScreen = new Vector2(screenRect.xMax, screenRect.yMax);
		var eventCamera = GetEventCamera();

		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, minScreen, eventCamera, out var localMin))
		{
			return false;
		}

		if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_rootRect, maxScreen, eventCamera, out var localMax))
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

	private Camera GetEventCamera()
	{
		var canvas = _rootRect != null ? _rootRect.GetComponentInParent<Canvas>() : null;
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

	private void NormalizeHoles(List<Rect> holes)
	{
		// 连通或贴边的洞会被合并成一个更大的洞。
		// 这能显著减少后续需要生成的 blocker 数量，也避免边缘描边重叠。
		bool merged;
		do
		{
			merged = false;

			for (int i = 0; i < holes.Count; i++)
			{
				for (int j = i + 1; j < holes.Count; j++)
				{
					if (!RectsOverlapOrTouch(holes[i], holes[j]))
					{
						continue;
					}

					holes[i] = UnionRect(holes[i], holes[j]);
					holes.RemoveAt(j);
					merged = true;
					break;
				}

				if (merged)
				{
					break;
				}
			}
		}
		while (merged);
	}

	private static bool RectsOverlapOrTouch(Rect a, Rect b)
	{
		const float epsilon = 0.5f;
		return a.xMin <= b.xMax + epsilon &&
			   a.xMax >= b.xMin - epsilon &&
			   a.yMin <= b.yMax + epsilon &&
			   a.yMax >= b.yMin - epsilon;
	}

	private static Rect UnionRect(Rect a, Rect b)
	{
		return Rect.MinMaxRect(
			Mathf.Min(a.xMin, b.xMin),
			Mathf.Min(a.yMin, b.yMin),
			Mathf.Max(a.xMax, b.xMax),
			Mathf.Max(a.yMax, b.yMax)
		);
	}

	private void LayoutBlockers(IReadOnlyList<Rect> holes)
	{
		// 多洞挖空的核心做法：
		// 1. 用 root 边界和所有 hole 边界切出一张规则网格。
		// 2. 遍历每个网格单元。
		// 3. 落在任意 hole 内的单元不画，其余单元都画成遮罩块。
		//
		// 这样不用 Shader，也能稳定支持任意数量的矩形洞。
		_blockerRects.Clear();
		_gridXs.Clear();
		_gridYs.Clear();

		var root = _rootRect.rect;
		_gridXs.Add(root.xMin);
		_gridXs.Add(root.xMax);
		_gridYs.Add(root.yMin);
		_gridYs.Add(root.yMax);

		for (int i = 0; i < holes.Count; i++)
		{
			var hole = holes[i];
			_gridXs.Add(hole.xMin);
			_gridXs.Add(hole.xMax);
			_gridYs.Add(hole.yMin);
			_gridYs.Add(hole.yMax);
		}

		SortAndUnique(_gridXs);
		SortAndUnique(_gridYs);

		for (int x = 0; x < _gridXs.Count - 1; x++)
		{
			for (int y = 0; y < _gridYs.Count - 1; y++)
			{
				float xMin = _gridXs[x];
				float xMax = _gridXs[x + 1];
				float yMin = _gridYs[y];
				float yMax = _gridYs[y + 1];

				if (xMax - xMin <= 0.5f || yMax - yMin <= 0.5f)
				{
					continue;
				}

				var center = new Vector2((xMin + xMax) * 0.5f, (yMin + yMax) * 0.5f);
				if (IsInsideAnyHole(center, holes))
				{
					continue;
				}

				_blockerRects.Add(Rect.MinMaxRect(xMin, yMin, xMax, yMax));
			}
		}

		EnsureBlockerCount(_blockerRects.Count);

		for (int i = 0; i < _blockers.Count; i++)
		{
			if (i < _blockerRects.Count)
			{
				var blockerRect = _blockerRects[i];
				SetRect(_blockers[i].rectTransform, blockerRect.xMin, blockerRect.yMin, blockerRect.xMax, blockerRect.yMax);
			}
			else
			{
				_blockers[i].gameObject.SetActive(false);
			}
		}
	}

	private void LayoutFrames(IReadOnlyList<Rect> holes)
	{
		// 每个洞用 4 条简单矩形线段拼出描边，成本低且易控。
		EnsureFrameCount(holes.Count * 4);

		int frameIndex = 0;
		for (int i = 0; i < holes.Count; i++)
		{
			var hole = holes[i];
			float thickness = Mathf.Clamp(frameThickness, 1f, Mathf.Min(hole.width, hole.height));

			SetRect(_frameLines[frameIndex++].rectTransform, hole.xMin, hole.yMax - thickness, hole.xMax, hole.yMax);
			SetRect(_frameLines[frameIndex++].rectTransform, hole.xMin, hole.yMin, hole.xMax, hole.yMin + thickness);
			SetRect(_frameLines[frameIndex++].rectTransform, hole.xMin, hole.yMin, hole.xMin + thickness, hole.yMax);
			SetRect(_frameLines[frameIndex++].rectTransform, hole.xMax - thickness, hole.yMin, hole.xMax, hole.yMax);
		}

		for (int i = frameIndex; i < _frameLines.Count; i++)
		{
			_frameLines[i].gameObject.SetActive(false);
		}
	}

	private static void SortAndUnique(List<float> values)
	{
		values.Sort();

		for (int i = values.Count - 2; i >= 0; i--)
		{
			if (Mathf.Abs(values[i] - values[i + 1]) <= 0.5f)
			{
				values.RemoveAt(i + 1);
			}
		}
	}

	private static bool IsInsideAnyHole(Vector2 point, IReadOnlyList<Rect> holes)
	{
		for (int i = 0; i < holes.Count; i++)
		{
			if (holes[i].Contains(point))
			{
				return true;
			}
		}

		return false;
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
