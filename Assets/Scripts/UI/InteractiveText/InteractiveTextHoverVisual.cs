using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 交互文本 Hover 视觉层（最终稳定版）
///
/// 功能：
/// 1. hover 时将 link 对应文字改为指定颜色
/// 2. hover 时在 link 下方绘制荧光笔底色
/// 3. 支持 link 跨行时分段绘制多个底色矩形
/// 4. 使用对象池复用 Image，避免频繁创建和销毁
///
/// 稳定性设计：
/// - 不修改 TMP 原始字符串，不插入 <color> / <b> 标签
/// - 文本强调通过“改顶点色 + 画底色”完成
/// - 使用 TMP_TextInfo.CopyMeshInfoVertexData() 缓存原始 mesh 颜色
/// - 在 LateUpdate 中持续重涂当前 hover link 的顶点色，避免被 TMP 后续刷新覆盖
///
/// 适用场景：
/// - TextMeshProUGUI
/// - Screen Space Overlay UI
/// - 与 TypewriterEffect / InteractiveTextView 配合使用
/// </summary>
public class InteractiveTextHoverVisual : MonoBehaviour
{
	[Header("Hover Text Color")]
	[Tooltip("鼠标悬浮时文字颜色。请设置成与正文明显不同的颜色。")]
	[SerializeField] private Color32 hoverTextColor = new Color32(255, 0, 0, 255);

	[Header("Marker")]
	[Tooltip("荧光笔底色父节点。建议使用 TMP 文本对象的同级兄弟节点，而不是 TMP 自己。")]
	[SerializeField] private RectTransform highlightParent;

	[Tooltip("荧光笔底色颜色。")]
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
	/// 当前高亮的 linkIndex。-1 表示当前没有 hover 目标。
	/// </summary>
	private int _currentLinkIndex = -1;

	/// <summary>
	/// 当前正在 hover 的 TMP 文本对象。
	/// 用于 LateUpdate 中持续重涂文字颜色。
	/// </summary>
	private TextMeshProUGUI _currentTmp;

	/// <summary>
	/// 原始 mesh 顶点数据缓存。
	/// 用于 Clear 时恢复文字原始颜色。
	/// </summary>
	private TMP_MeshInfo[] _cachedMeshInfo;

	/// <summary>
	/// 荧光笔 Image 对象池。
	/// </summary>
	private readonly Stack<Image> _pool = new Stack<Image>(16);

	/// <summary>
	/// 当前激活中的荧光笔 Image 列表。
	/// </summary>
	private readonly List<Image> _active = new List<Image>(16);

	private int _debugMeshIndex = -1;
	private int _debugVertexIndex = -1;	


	private void Awake()
	{
		// 如果没配置 highlightParent，就自动创建一个推荐层
		if (highlightParent == null)
		{
			TryAutoCreateHighlightLayer();
		}

		// 注册 TMP 的预渲染回调：
		// 在 TMP 即将把最终文字数据提交给渲染层之前，再做一次局部顶点改色。
		// 这是当前问题下最关键的修复点。
		if (TryGetComponent(out TextMeshProUGUI tmp))
		{
			tmp.OnPreRenderText += HandlePreRenderText;
		}
	}

	private void OnDestroy()
	{
		// 注销 TMP 的预渲染回调，避免对象销毁后残留事件绑定
		if (TryGetComponent(out TextMeshProUGUI tmp))
		{
			tmp.OnPreRenderText -= HandlePreRenderText;
		}
	}

	/// <summary>
	/// LateUpdate 中持续确保 hover 文字颜色存在。
	///
	/// 为什么需要这一步：
	/// - OnPointerMove 中改顶点色后，TMP/Canvas 可能在本帧后续阶段重建 mesh
	/// - 重建后局部顶点色会被冲掉
	/// - 因此这里在更靠后的时机重新把当前 hover link 的颜色补回去
	/// </summary>
	//private void LateUpdate()
	//{
	//	if (_currentTmp == null || _currentLinkIndex < 0)
	//		return;

	//	if (!_currentTmp.isActiveAndEnabled)
	//		return;

	//	_currentTmp.ForceMeshUpdate();

	//	if (_currentTmp.textInfo == null || _currentTmp.textInfo.linkCount <= 0)
	//		return;

	//	if (_currentLinkIndex >= _currentTmp.textInfo.linkCount)
	//		return;

	//	ApplyTextVertexColor(_currentTmp, _currentLinkIndex);
	//}

	/// <summary>
	/// 应用 hover 效果。
	///
	/// 流程：
	/// 1. 清理旧状态
	/// 2. 更新 TMP mesh
	/// 3. 缓存当前原始 mesh 颜色
	/// 4. 设置当前 hover 状态
	/// 5. 立即应用文字颜色
	/// 6. 绘制荧光笔底色
	/// </summary>
	public void Apply(TextMeshProUGUI tmp, int linkIndex)
	{
		if (tmp == null)
			return;

		if (linkIndex == _currentLinkIndex && _currentTmp == tmp)
			return;

		Clear(tmp);

		tmp.ForceMeshUpdate();

		if (tmp.textInfo == null || tmp.textInfo.linkCount <= 0)
			return;

		if (linkIndex < 0 || linkIndex >= tmp.textInfo.linkCount)
			return;

		_currentTmp = tmp;
		_currentLinkIndex = linkIndex;

		// 缓存原始 mesh 信息，用于 Clear 时恢复
		_cachedMeshInfo = tmp.textInfo.CopyMeshInfoVertexData();

		// 注意：
		// 这里不再立即调用 ApplyTextVertexColor。
		// 局部顶点改色统一交给 OnPreRenderText 回调 HandlePreRenderText()。
		DrawMarkerRects(tmp, linkIndex);

		// 触发一次 mesh 刷新，让 TMP 在下一次预渲染时走到 HandlePreRenderText
		tmp.SetVerticesDirty();;
	}

	/// <summary>
	/// 清除 hover 效果。
	///
	/// 内容：
	/// - 恢复原始文字颜色
	/// - 回收所有 marker
	/// - 清理当前 hover 状态
	/// </summary>
	public void Clear(TextMeshProUGUI tmp)
	{
		if (tmp != null)
		{
			RestoreTextVertexColor(tmp);
			// 通知 TMP 重新提交顶点数据，确保恢复后的颜色生效
			tmp.SetVerticesDirty();
		}

		RecycleAllMarkers();

		_currentTmp = null;
		_currentLinkIndex = -1;
	}

	/// <summary>
	/// 自动创建荧光笔层。
	///
	/// 规则：
	/// - 在当前 TMP 对象的父物体下创建一个同级兄弟节点
	/// - 尺寸、锚点、pivot 对齐到 TMP
	/// - 放到 TMP 前面，通常即可位于文字视觉下方
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

		layer.SetSiblingIndex(tmpRect.GetSiblingIndex());

		highlightParent = layer;
	}

	#region Text Color

	/// <summary>
	/// 将指定 link 内所有字符的顶点颜色统一改为 hoverTextColor。
	/// 方法已弃用
	/// </summary>
	private void ApplyTextVertexColor(TextMeshProUGUI tmp, int linkIndex)
	{
		TMP_LinkInfo linkInfo = tmp.textInfo.linkInfo[linkIndex];
		int start = linkInfo.linkTextfirstCharacterIndex;
		int end = start + linkInfo.linkTextLength;
		Debug.Log($"linkText={linkInfo.GetLinkText()}");
		Debug.Log($"start={linkInfo.linkTextfirstCharacterIndex}, len={linkInfo.linkTextLength}");
		for (int i = start; i < end; i++)
		{
			var ch = tmp.textInfo.characterInfo[i];
			Debug.Log($"charIndex={i}, char='{ch.character}', visible={ch.isVisible}, meshIndex={ch.materialReferenceIndex}, vertexIndex={ch.vertexIndex}");
		}
		bool logged = false;

		for (int charIndex = start; charIndex < end; charIndex++)
		{
			if (charIndex < 0 || charIndex >= tmp.textInfo.characterCount)
				continue;

			TMP_CharacterInfo charInfo = tmp.textInfo.characterInfo[charIndex];
			if (ignoreInvisibleCharacters && !charInfo.isVisible)
				continue;

			int meshIndex = charInfo.materialReferenceIndex;
			int vertexIndex = charInfo.vertexIndex;
			_debugMeshIndex = meshIndex;
			_debugVertexIndex = vertexIndex;
			Color32[] colors = tmp.textInfo.meshInfo[meshIndex].colors32;

			if (!logged)
			{
				Debug.Log($"[Before] charIndex={charIndex}, meshIndex={meshIndex}, vertexIndex={vertexIndex}, v0={colors[vertexIndex + 0]}");

			}

			colors[vertexIndex + 0] = hoverTextColor;
			colors[vertexIndex + 1] = hoverTextColor;
			colors[vertexIndex + 2] = hoverTextColor;
			colors[vertexIndex + 3] = hoverTextColor;

			if (!logged)
			{
				Debug.Log($"[After ] charIndex={charIndex}, meshIndex={meshIndex}, vertexIndex={vertexIndex}, v0={colors[vertexIndex + 0]}");
				logged = true;
			}
		}

		PushVertexColorsToMesh(tmp);
	}

	/// <summary>
	/// 恢复 hover 前缓存的原始顶点颜色。
	/// </summary>
	private void RestoreTextVertexColor(TextMeshProUGUI tmp)
	{
		if (tmp == null || _cachedMeshInfo == null)
			return;

		for (int i = 0; i < _cachedMeshInfo.Length; i++)
		{
			if (i >= tmp.textInfo.meshInfo.Length)
				continue;

			tmp.textInfo.meshInfo[i].colors32 = _cachedMeshInfo[i].colors32;
		}

		PushVertexColorsToMesh(tmp);
		_cachedMeshInfo = null;
	}

	private void PushVertexColorsToMesh(TextMeshProUGUI tmp)
	{
		for (int i = 0; i < tmp.textInfo.meshInfo.Length; i++)
		{
			TMP_MeshInfo meshInfo = tmp.textInfo.meshInfo[i];

			if (meshInfo.mesh == null || meshInfo.colors32 == null || meshInfo.colors32.Length == 0)
				continue;

			//if (i == _debugMeshIndex)
			//{
			//	Debug.Log($"[Push Before] meshInfo.colors32[{_debugVertexIndex}]={meshInfo.colors32[_debugVertexIndex]}");
			//}

			meshInfo.mesh.colors32 = meshInfo.colors32;
			tmp.UpdateGeometry(meshInfo.mesh, i);

			//if (i == _debugMeshIndex && meshInfo.mesh.colors32 != null && _debugVertexIndex < meshInfo.mesh.colors32.Length)
			//{
			//	Debug.Log($"[Push After ] mesh.colors32[{_debugVertexIndex}]={meshInfo.mesh.colors32[_debugVertexIndex]}");
			//}
		}
	}

	/// <summary>
	/// TMP 预渲染回调。
	///
	/// 触发时机：
	/// - TextMeshPro 已经完成文本解析和 mesh 准备
	/// - 即将把最终数据提交去渲染
	///
	/// 这是当前最适合做“局部 hover 改色”的时机，
	/// 因为它晚于普通的 ForceMeshUpdate / Update / LateUpdate 逻辑，
	/// 可以避免前面已经改好的局部顶点色被后续 TMP 重建覆盖。
	/// </summary>
	/// <param name="textInfo">TMP 当前即将渲染的文本信息</param>
	private void HandlePreRenderText(TMP_TextInfo textInfo)
	{
		if (_currentTmp == null || _currentLinkIndex < 0)
			return;

		// 防御：确保当前回调对应的对象就是正在 hover 的 TMP
		if (_currentTmp.textInfo == null || textInfo.textComponent != _currentTmp)
			return;

		if (textInfo.linkCount <= 0)
			return;

		if (_currentLinkIndex >= textInfo.linkCount)
			return;

		TMP_LinkInfo linkInfo = textInfo.linkInfo[_currentLinkIndex];
		int start = linkInfo.linkTextfirstCharacterIndex;
		int end = start + linkInfo.linkTextLength;

		for (int charIndex = start; charIndex < end; charIndex++)
		{
			if (charIndex < 0 || charIndex >= textInfo.characterCount)
				continue;

			TMP_CharacterInfo charInfo = textInfo.characterInfo[charIndex];

			if (ignoreInvisibleCharacters && !charInfo.isVisible)
				continue;

			int meshIndex = charInfo.materialReferenceIndex;
			int vertexIndex = charInfo.vertexIndex;

			Color32[] colors = textInfo.meshInfo[meshIndex].colors32;

			colors[vertexIndex + 0] = hoverTextColor;
			colors[vertexIndex + 1] = hoverTextColor;
			colors[vertexIndex + 2] = hoverTextColor;
			colors[vertexIndex + 3] = hoverTextColor;
		}
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
		{
			CreateMarkerRect(tmp.rectTransform, segMin, segMax);
		}
	}

	/// <summary>
	/// 创建一个荧光笔矩形。
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

		Vector3 worldBL = tmpRect.TransformPoint(new Vector3(xMin, yMin, 0f));
		Vector3 worldTR = tmpRect.TransformPoint(new Vector3(xMin + width, yMin + height, 0f));

		Vector3 localBL = highlightParent.InverseTransformPoint(worldBL);
		Vector3 localTR = highlightParent.InverseTransformPoint(worldTR);

		rt.anchorMin = new Vector2(0f, 0f);
		rt.anchorMax = new Vector2(0f, 0f);
		rt.pivot = new Vector2(0f, 0f);
		rt.anchoredPosition = new Vector2(localBL.x, localBL.y);
		rt.sizeDelta = new Vector2(
			Mathf.Max(0.01f, localTR.x - localBL.x),
			Mathf.Max(0.01f, localTR.y - localBL.y)
		);
	}

	/// <summary>
	/// 从对象池获取一个 marker Image。
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