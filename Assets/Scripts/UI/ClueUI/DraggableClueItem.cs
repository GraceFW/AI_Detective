using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 可拖拽的线索词条组件
/// 
/// 职责：
/// 1. 处理拖拽逻辑（开始/移动/结束）
/// 2. 负责与各种 DropTarget 交互
/// 3. 对外抛出“拖拽成功事件”（供引导系统使用）
/// 
/// 注意：
/// - UI层行为驱动组件（不处理业务逻辑）
/// - Guide系统只依赖 OnDragSuccess，不关心具体DropTarget类型
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DraggableClueItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
	[Header("拖拽表现设置")]

	[Tooltip("拖拽时的透明度")]
	[SerializeField] private float dragAlpha = 0.6f;

	[Tooltip("回弹动画时长")]
	[SerializeField] private float snapBackDuration = 0.15f;

	// =========================
	// 缓存组件
	// =========================
	private RectTransform _rectTransform;
	private CanvasGroup _canvasGroup;
	private Canvas _canvas;

	// =========================
	// 拖拽前状态（用于回弹）
	// =========================
	private Transform _originalParent;
	private Vector2 _originalAnchoredPosition;
	private int _originalSiblingIndex;

	// =========================
	// 数据
	// =========================
	private ClueData _clueData;

	// =========================
	// 拖拽状态
	// =========================
	private bool _isDragging;
	private Vector2 _dragOffset;

	/// <summary>
	/// 当前正在被拖拽的对象（全局静态）
	/// 供 DropTarget 使用（比如 OnDrop 时读取）
	/// </summary>
	public static DraggableClueItem CurrentDragging { get; private set; }

	public ClueData ClueData => _clueData;

	private void Awake()
	{
		_rectTransform = GetComponent<RectTransform>();
		_canvasGroup = GetComponent<CanvasGroup>();
		_canvas = GetComponentInParent<Canvas>();
	}

	/// <summary>
	/// 绑定线索数据
	/// </summary>
	public void Bind(ClueData clue)
	{
		_clueData = clue;
	}

	/// <summary>
	/// 开始拖拽
	/// </summary>
	public void OnBeginDrag(PointerEventData eventData)
	{
		if (_clueData == null)
		{
			return;
		}

		if (_canvas == null)
		{
			_canvas = GetComponentInParent<Canvas>();
			if (_canvas == null)
			{
				Debug.LogWarning("[DraggableClueItem] 未找到父级 Canvas，无法开始拖拽");
				return;
			}
		}

		_isDragging = true;
		CurrentDragging = this;

		// 记录原始状态（用于回弹）
		_originalParent = transform.parent;
		_originalAnchoredPosition = _rectTransform.anchoredPosition;
		_originalSiblingIndex = transform.GetSiblingIndex();

		// 提升到Canvas最上层（避免被遮挡）
		if (_canvas != null)
		{
			transform.SetParent(_canvas.transform, true);
			transform.SetAsLastSibling();
		}

		// 计算拖拽偏移（避免UI“跳到鼠标中心”）
		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			_canvas.transform as RectTransform,
			eventData.position,
			eventData.pressEventCamera,
			out var canvasLocalPoint
		);

		_dragOffset = canvasLocalPoint - _rectTransform.anchoredPosition;

		// 设置视觉状态（半透明 + 穿透射线）
		_canvasGroup.alpha = dragAlpha;
		_canvasGroup.blocksRaycasts = false;
	}

	/// <summary>
	/// 拖拽中
	/// </summary>
	public void OnDrag(PointerEventData eventData)
	{
		if (!_isDragging || _canvas == null)
		{
			return;
		}

		RectTransformUtility.ScreenPointToLocalPointInRectangle(
			_canvas.transform as RectTransform,
			eventData.position,
			eventData.pressEventCamera,
			out var canvasLocalPoint
		);

		_rectTransform.anchoredPosition = canvasLocalPoint - _dragOffset;
	}

	/// <summary>
	/// 拖拽结束
	/// </summary>
	public void OnEndDrag(PointerEventData eventData)
	{
		if (!_isDragging)
		{
			return;
		}

		_isDragging = false;
		CurrentDragging = null;

		// 恢复视觉状态
		_canvasGroup.alpha = 1f;
		_canvasGroup.blocksRaycasts = true;

		// 检测拖放目标
		if (_clueData != null)
		{
			CheckAllDropTargets(eventData);
		}

		// 回弹
		SnapBack();
	}

	/// <summary>
	/// 检测所有拖放目标
	/// 核心逻辑：
	/// 1. 使用 EventSystem 射线检测当前鼠标下所有UI
	/// 2. 逐个判断是否是合法 DropTarget
	/// 3. 一旦命中 → 执行交互 + 触发引导事件
	/// </summary>
	private void CheckAllDropTargets(PointerEventData eventData)
	{
		var results = new System.Collections.Generic.List<RaycastResult>();
		EventSystem.current.RaycastAll(eventData, results);

		foreach (var result in results)
		{
			// ⭐ 获取 GuideTarget（用于引导系统）
			string targetKey = ResolveGuideTargetKey(result.gameObject);

			var dropTarget = ResolveClueDropTarget(result.gameObject);
			if (dropTarget != null)
			{
				if (dropTarget.OnClueDrop(_clueData))
				{
					RaiseDragSuccess(ResolveSourceKey(), targetKey);
					return;
				}
			}
		}
	}

	private static IClueDropTarget ResolveClueDropTarget(GameObject hitObject)
	{
		Transform current = hitObject != null ? hitObject.transform : null;
		while (current != null)
		{
			var behaviours = current.GetComponents<MonoBehaviour>();
			for (int i = 0; i < behaviours.Length; i++)
			{
				if (behaviours[i] is IClueDropTarget dropTarget)
				{
					return dropTarget;
				}
			}

			current = current.parent;
		}

		return null;
	}

	private static string ResolveGuideTargetKey(GameObject hitObject)
	{
		Transform current = hitObject != null ? hitObject.transform : null;
		while (current != null)
		{
			var guideTargets = current.GetComponents<GuideTarget>();
			for (int i = guideTargets.Length - 1; i >= 0; i--)
			{
				if (guideTargets[i] != null && !string.IsNullOrWhiteSpace(guideTargets[i].key))
				{
					return guideTargets[i].key;
				}
			}

			current = current.parent;
		}

		return string.Empty;
	}

	private string ResolveSourceKey()
	{
		string sourceKey = ResolveGuideTargetKey(gameObject);
		if (!string.IsNullOrWhiteSpace(sourceKey))
		{
			return sourceKey;
		}

		return _clueData != null ? _clueData.id : string.Empty;
	}

	/// <summary>
	/// ⭐ 封装拖拽成功事件触发
	/// </summary>
	private void RaiseDragSuccess(string sourceKey, string targetKey)
	{
		if (string.IsNullOrEmpty(sourceKey))
		{
			Debug.LogWarning("[DraggableClueItem] 拖拽成功但未找到拖拽源 GuideTarget.key");
			return;
		}

		if (string.IsNullOrEmpty(targetKey))
		{
			Debug.LogWarning("[DraggableClueItem] 拖拽成功但未找到 GuideTarget.key");
			return;
		}

		GuideDragEventBus.Raise(sourceKey, targetKey);
	}

	/// <summary>
	/// 回弹到原位置
	/// </summary>
	private void SnapBack()
	{
		transform.SetParent(_originalParent, true);
		transform.SetSiblingIndex(_originalSiblingIndex);

		if (snapBackDuration > 0f)
		{
			StartCoroutine(SnapBackCoroutine());
		}
		else
		{
			_rectTransform.anchoredPosition = _originalAnchoredPosition;
		}
	}

	/// <summary>
	/// 平滑回弹动画
	/// </summary>
	private System.Collections.IEnumerator SnapBackCoroutine()
	{
		var startPos = _rectTransform.anchoredPosition;
		var endPos = _originalAnchoredPosition;
		var elapsed = 0f;

		while (elapsed < snapBackDuration)
		{
			elapsed += Time.deltaTime;
			var t = Mathf.Clamp01(elapsed / snapBackDuration);

			// 缓动（easeOut）
			t = 1f - Mathf.Pow(1f - t, 3f);

			_rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
			yield return null;
		}

		_rectTransform.anchoredPosition = endPos;
	}
}
