using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 可拖拽的线索词条组件
/// 拖拽松手后自动回到原位置
/// 拖拽到搜索框时自动填入 displayName
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class DraggableClueItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("设置")]
    [Tooltip("拖拽时的透明度")]
    [SerializeField] private float dragAlpha = 0.6f;

    [Tooltip("回弹动画时长")]
    [SerializeField] private float snapBackDuration = 0.15f;

    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Canvas _canvas;
    private Transform _originalParent;
    private Vector2 _originalAnchoredPosition;
    private int _originalSiblingIndex;

    private ClueData _clueData;
    private bool _isDragging;
    private Vector2 _dragOffset;

    // 静态引用：当前被拖拽的线索数据（供 DropTarget 检测）
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

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (_clueData == null)
        {
            return;
        }

        _isDragging = true;
        CurrentDragging = this;

        // 保存原始位置信息
        _originalParent = transform.parent;
        _originalAnchoredPosition = _rectTransform.anchoredPosition;
        _originalSiblingIndex = transform.GetSiblingIndex();

        // 先移动到 Canvas 根节点，确保显示在最上层
        // 必须在计算偏移之前移动，否则坐标系统会错乱
        if (_canvas != null)
        {
            transform.SetParent(_canvas.transform, true);
            transform.SetAsLastSibling();
        }

        // 计算拖拽偏移（在移动到新父级之后）
        // 将屏幕坐标转换为 Canvas 本地坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out var canvasLocalPoint
        );

        // 计算偏移量：鼠标位置 - 对象当前位置
        _dragOffset = canvasLocalPoint - _rectTransform.anchoredPosition;

        // 设置透明度，允许射线穿透
        _canvasGroup.alpha = dragAlpha;
        _canvasGroup.blocksRaycasts = false;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging || _canvas == null)
        {
            return;
        }

        // 将屏幕坐标转换为 Canvas 本地坐标
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvas.transform as RectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out var canvasLocalPoint
        );

        // 设置位置：鼠标位置 - 偏移量
        _rectTransform.anchoredPosition = canvasLocalPoint - _dragOffset;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging)
        {
            return;
        }

        _isDragging = false;
        CurrentDragging = null;

        // 恢复透明度和射线检测
        _canvasGroup.alpha = 1f;
        _canvasGroup.blocksRaycasts = true;

        // 检测是否拖到了任何有效的拖放目标
        if (_clueData != null)
        {
            CheckAllDropTargets(eventData);
        }

        // 回到原位置
        SnapBack();
    }

    /// <summary>
    /// 检查所有拖放目标类型
    /// </summary>
    private void CheckAllDropTargets(PointerEventData eventData)
    {
        var results = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            // 检查 SearchInputDropTarget
            var searchTarget = result.gameObject.GetComponent<SearchInputDropTarget>();
            if (searchTarget == null)
            {
                searchTarget = result.gameObject.GetComponentInParent<SearchInputDropTarget>();
            }
            if (searchTarget != null)
            {
                searchTarget.OnClueDrop(_clueData);
                return; // 找到目标后立即返回
            }

            // 检查 CameraDropTarget
            var cameraTarget = result.gameObject.GetComponent<CameraDropTarget>();
            if (cameraTarget == null)
            {
                cameraTarget = result.gameObject.GetComponentInParent<CameraDropTarget>();
            }
            if (cameraTarget != null)
            {
                cameraTarget.OnClueDrop(_clueData);
                return; // 找到目标后立即返回
            }

            // 检查 PersonSummonDropTarget (name对象)
            var summonTarget = result.gameObject.GetComponent<PersonSummonDropTarget>();
            if (summonTarget == null)
            {
                summonTarget = result.gameObject.GetComponentInParent<PersonSummonDropTarget>();
            }
            if (summonTarget != null)
            {
                summonTarget.OnClueDrop(_clueData);
                return; // 找到目标后立即返回
            }

            // 检查 ClueShowDropTarget (person对象)
            var clueShowTarget = result.gameObject.GetComponent<ClueShowDropTarget>();
            if (clueShowTarget == null)
            {
                clueShowTarget = result.gameObject.GetComponentInParent<ClueShowDropTarget>();
            }
            if (clueShowTarget != null)
            {
                clueShowTarget.OnClueDrop(_clueData);
                return; // 找到目标后立即返回
            }
        }
    }

    /// <summary>
    /// 回弹到原位置
    /// </summary>
    private void SnapBack()
    {
        // 恢复父级
        transform.SetParent(_originalParent, true);
        transform.SetSiblingIndex(_originalSiblingIndex);

        // 使用协程平滑回弹（可选：直接设置位置）
        if (snapBackDuration > 0f)
        {
            StartCoroutine(SnapBackCoroutine());
        }
        else
        {
            _rectTransform.anchoredPosition = _originalAnchoredPosition;
        }
    }

    private System.Collections.IEnumerator SnapBackCoroutine()
    {
        var startPos = _rectTransform.anchoredPosition;
        var endPos = _originalAnchoredPosition;
        var elapsed = 0f;

        while (elapsed < snapBackDuration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / snapBackDuration);
            // 使用缓动函数
            t = 1f - Mathf.Pow(1f - t, 3f);
            _rectTransform.anchoredPosition = Vector2.Lerp(startPos, endPos, t);
            yield return null;
        }

        _rectTransform.anchoredPosition = endPos;
    }
}

