using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 交互文本的 Hover 视觉层。
///
/// 功能：
/// 1. 将当前 hover 的 link 文本改为指定颜色。
/// 2. 在 link 文本下方绘制“荧光笔底色”。
/// 3. 支持 link 跨行时分段绘制多个底色矩形。
/// 4. 使用对象池复用 Image，避免频繁创建和销毁。
///
/// 设计原则：
/// - 不修改 TMP 原始字符串，不插入 <b> / <color> 等富文本标签。
/// - 文本强调通过“改顶点色 + 画底色”完成，兼容打字机、追加文本、link 命中。
/// - 荧光笔层建议使用“与 TMP 同级的兄弟节点”作为父物体，而不是 TMP 自己。
/// </summary>
public class InteractiveTextHoverVisual : MonoBehaviour
{
	[Header("Hover Text Color")]
	[Tooltip("鼠标悬浮时，link 文字显示的颜色。请设置为和正文明显不同的颜色。")]
	[SerializeField] private Color32 hoverTextColor = new Color32(255, 235, 140, 255);

	[Header("Marker")]
	[Tooltip("荧光笔底色的父节点。强烈建议使用 TMP 文本对象的同级兄弟节点，而不是 TMP 自己。")]
	[SerializeField] private RectTransform highlightParent;

	[Tooltip("荧光笔底色颜色。建议使用半透明黄色。")]
	[SerializeField] private Color markerColor = new Color(1f, 1f, 0f, 0.45f);

	[Tooltip("荧光笔左右额外留白（像素）。")]
	[SerializeField] private float paddingX = 6f;

	[Tooltip("荧光笔上下额外留白（像素）。")]
	[SerializeField] private float paddingY = 2f;

	[Tooltip("若大于 0，则荧光笔使用固定高度；若为 0，则使用字符实际高度。")]
	[SerializeField] private float fixedMarkerHeight = 0f;

	[Tooltip("是否跳过不可见字符。建议开启。")]
	[SerializeField] private bool ignoreInvisibleCharacters = true;

	[Header("Marker Sprite (Optional)")]
	[Tooltip("可选：圆角底图。若不为空，Image 将使用 Sliced 模式。")]
	[SerializeField] private Sprite markerSprite;

	/// <summary>
	/// 被修改过的顶点原始颜色缓存。
	/// key: (meshIndex, vertexIndex)
	/// value: 原始颜色
	/// </summary>
	private readonly Dictionary<(int meshIndex, int vertexIndex), Color32> _originalVertexColors = new();

	/// <summary>
	/// 当前高亮的 linkIndex。
	/// </summary>
	private int _currentLinkIndex = -1;

	/// <summary>
	/// 荧光笔 Image 对象池。
	/// </summary>
	private readonly Stack<Image> _pool = new Stack<Image>(16);

	/// <summary>
	/// 当前激活中的荧光笔 Image 列表。
	/// </summary>
	private readonly List<Image> _active = new List<Image>(16);

	private void Awake()
	{
		// 如果没配置 highlightParent，就尝试自动创建一个推荐层。
		if (highlightParent == null)
		{
			TryAutoCreateHighlightLayer();
		}
	}

	/// <summary>
	/// 应用 hover 视觉效果。
	/// </summary>
	public void Apply(TextMeshProUGUI tmp, int linkIndex)
	{
		if (tmp == null)
			return;

		if (linkIndex == _currentLinkIndex)
			return;

		Clear(tmp);

		tmp.ForceMeshUpdate();

		if (tmp.textInfo == null || tmp.textInfo.linkCount <= 0)
			return;

		if (linkIndex < 0 || linkIndex >= tmp.textInfo.linkCount)
			return;

		_currentLinkIndex = linkIndex;

		ApplyTextVertexColor(tmp, linkIndex);
		DrawMarkerRects(tmp, linkIndex);
	}

	/// <summary>
	/// 清除 hover 视觉效果。
	/// </summary>
	public void Clear(TextMeshProUGUI tmp)
	{
		if (tmp != null)
			RestoreTextVertexColor(tmp);

		RecycleAllMarkers();
		_currentLinkIndex = -1;
	}

	/// <summary>
	/// 自动创建一个荧光笔层。
	///
	/// 规则：
	/// - 在当前 TMP 对象的父物体下创建一个同级兄弟节点
	/// - 尺寸、锚点、pivot 对齐到 TMP
	/// - 放在 TMP 前面，确保视觉上位于文字下方
	/// </summary>
	private void TryAutoCreateHighlightLayer()
	{
		RectTransform tmpRect = GetComponent<RectTransform>();
		if (tmpRect == null || tmpRect.parent == null)
			return;

		GameObject go = new GameObject($"{name}_HoverMarkerLayer", typeof(RectTransform));
		RectTransform layer = go.GetComponent<RectTransform>();
		layer.SetParent(tmpRect.parent, false);

		layer.anchorMin = tmpRect.anchorMin;
		layer.anchorMax = tmpRect.anchorMax;
		layer.pivot = tmpRect.pivot;
		layer.anchoredPosition = tmpRect.anchoredPosition;
		layer.sizeDelta = tmpRect.sizeDelta;
		layer.localScale = Vector3.one;
		layer.localRotation = Quaternion.identity;

		// 放到 TMP 前面，通常就能显示在文字后面。
		layer.SetSiblingIndex(tmpRect.GetSiblingIndex());

		highlightParent = layer;
	}

	#region Text Color

	/// <summary>
	/// 将指定 link 内的字符顶点颜色改为 hoverTextColor。
	/// </summary>
	private void ApplyTextVertexColor(TextMeshProUGUI tmp, int linkIndex)
	{
		TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];
		int start = linkInfo.linkTextfirstCharacterIndex;
		int end = start + linkInfo.linkTextLength;

		for (int charIndex = start; charIndex < end; charIndex++)
		{
			if (charIndex < 0 || charIndex >= tmp.textInfo.characterCount)
				continue;

			TMP_CharacterInfo charInfo = tmp.textInfo.characterInfo[charIndex];

			if (ignoreInvisibleCharacters && !charInfo.isVisible)
				continue;

			int meshIndex = charInfo.materialReferenceIndex;
			int vertexIndex = charInfo.vertexIndex;

			Color32[] colors = tmp.textInfo.meshInfo[meshIndex].colors32;

			for (int v = 0; v < 4; v++)
			{
				var key = (meshIndex, vertexIndex + v);

				if (!_originalVertexColors.ContainsKey(key))
					_originalVertexColors[key] = colors[vertexIndex + v];

				colors[vertexIndex + v] = hoverTextColor;
			}
		}

		tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
	}

	/// <summary>
	/// 恢复之前修改过的顶点颜色。
	/// </summary>
	private void RestoreTextVertexColor(TextMeshProUGUI tmp)
	{
		if (_originalVertexColors.Count == 0)
			return;

		tmp.ForceMeshUpdate();

		foreach (var kv in _originalVertexColors)
		{
			int meshIndex = kv.Key.meshIndex;
			int vertexIndex = kv.Key.vertexIndex;

			if (meshIndex < 0 || meshIndex >= tmp.textInfo.meshInfo.Length)
				continue;

			Color32[] colors = tmp.textInfo.meshInfo[meshIndex].colors32;

			if (vertexIndex < 0 || vertexIndex >= colors.Length)
				continue;

			colors[vertexIndex] = kv.Value;
		}

		tmp.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32);
		_originalVertexColors.Clear();
	}

	#endregion

	#region Marker

	/// <summary>
	/// 绘制荧光笔底色矩形。
	///
	/// 逻辑：
	/// - 获取 link 中每个字符的 bottomLeft / topRight
	/// - 按 lineNumber 分组
	/// - 每一行合并为一个矩形
	/// - 将 TMP 局部坐标转换到 highlightParent 局部坐标
	/// </summary>
	private void DrawMarkerRects(TextMeshProUGUI tmp, int linkIndex)
	{
		if (highlightParent == null)
			return;

		TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];
		int start = linkInfo.linkTextfirstCharacterIndex;
		int end = start + linkInfo.linkTextLength;

		bool hasSegment = false;
		int currentLine = -1;

		Vector2 segMin = Vector2.zero;
		Vector2 segMax = Vector2.zero;

		for (int i = start; i < end; i++)
		{
			if (i < 0 || i >= tmp.textInfo.characterCount)
				continue;

			TMP_CharacterInfo ch = tmp.textInfo.characterInfo[i];

			if (ignoreInvisibleCharacters && !ch.isVisible)
				continue;

			int line = ch.lineNumber;
			Vector2 bl = ch.bottomLeft;
			Vector2 tr = ch.topRight;

			if (!hasSegment)
			{
				hasSegment = true;
				currentLine = line;
				segMin = bl;
				segMax = tr;
				continue;
			}

			if (line == currentLine)
			{
				segMin = Vector2.Min(segMin, bl);
				segMax = Vector2.Max(segMax, tr);
			}
			else
			{
				CreateMarkerRect(tmp.rectTransform, segMin, segMax);

				currentLine = line;
				segMin = bl;
				segMax = tr;
			}
		}

		if (hasSegment)
			CreateMarkerRect(tmp.rectTransform, segMin, segMax);
	}

	/// <summary>
	/// 创建一个荧光笔矩形。
	///
	/// 注意：
	/// 这里不能直接把 TMP 的局部坐标拿来当 highlightParent 的局部坐标。
	/// 必须先：
	/// TMP局部坐标 -> 世界坐标 -> highlightParent局部坐标
	/// 才能保证不同层级、不同 pivot 下都能对齐。
	/// </summary>
	private void CreateMarkerRect(RectTransform tmpRect, Vector2 tmpLocalMin, Vector2 tmpLocalMax)
	{
		Image img = GetMarkerImage();
		RectTransform rt = img.rectTransform;

		float xMin = tmpLocalMin.x - paddingX;
		float xMax = tmpLocalMax.x + paddingX;
		float yMin = tmpLocalMin.y - paddingY;
		float yMax = tmpLocalMax.y + paddingY;

		float width = Mathf.Max(0.01f, xMax - xMin);
		float height = Mathf.Max(0.01f, yMax - yMin);

		if (fixedMarkerHeight > 0f)
		{
			float centerY = (yMin + yMax) * 0.5f;
			height = fixedMarkerHeight;
			yMin = centerY - height * 0.5f;
		}

		// TMP 局部空间四角 -> 世界空间
		Vector3 worldBL = tmpRect.TransformPoint(new Vector3(xMin, yMin, 0f));
		Vector3 worldTR = tmpRect.TransformPoint(new Vector3(xMin + width, yMin + height, 0f));

		// 世界空间 -> highlightParent 局部空间
		Vector3 localBL = highlightParent.InverseTransformPoint(worldBL);
		Vector3 localTR = highlightParent.InverseTransformPoint(worldTR);

		float finalX = localBL.x;
		float finalY = localBL.y;
		float finalW = Mathf.Max(0.01f, localTR.x - localBL.x);
		float finalH = Mathf.Max(0.01f, localTR.y - localBL.y);

		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(0f, 0f);
		rt.pivot = new Vector2(0f, 0f);
		rt.anchoredPosition = new Vector2(finalX, finalY);
		rt.sizeDelta = new Vector2(finalW, finalH);
	}

	/// <summary>
	/// 从对象池中获取一个 marker Image。
	/// </summary>
	private Image GetMarkerImage()
	{
		Image img = _pool.Count > 0 ? _pool.Pop() : CreateNewMarker();

		img.gameObject.SetActive(true);
		img.color = markerColor;

		_active.Add(img);
		return img;
	}

	/// <summary>
	/// 创建新的 marker Image。
	/// </summary>
	private Image CreateNewMarker()
	{
		GameObject go = new GameObject("HoverMarker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
		Image img = go.GetComponent<Image>();

		img.raycastTarget = false;

		if (markerSprite != null)
		{
			img.sprite = markerSprite;
			img.type = Image.Type.Sliced;
		}

		RectTransform rt = img.rectTransform;
		rt.SetParent(highlightParent, false);
		rt.localScale = Vector3.one;
		rt.localRotation = Quaternion.identity;

		return img;
	}

	/// <summary>
	/// 回收所有激活中的 marker。
	/// </summary>
	private void RecycleAllMarkers()
	{
		for (int i = 0; i < _active.Count; i++)
		{
			Image img = _active[i];
			if (img == null)
				continue;

			img.gameObject.SetActive(false);
			_pool.Push(img);
		}

		_active.Clear();
	}

	#endregion
}