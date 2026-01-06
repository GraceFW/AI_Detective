using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 搜索输入框的拖放目标
/// 当线索词条被拖放到此组件时，自动填入 displayName
/// </summary>
public class SearchInputDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("目标输入框")]
    [Tooltip("如果为空，则尝试获取自身或子对象的 TMP_InputField")]
    [SerializeField] private TMP_InputField targetInputField;

    [Header("高亮效果")]
    [Tooltip("拖拽悬停时的高亮颜色")]
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.6f, 1f, 0.3f);

    [Tooltip("高亮显示的 Image（可选）")]
    [SerializeField] private Image highlightImage;

    private Color _originalColor;
    private bool _isHighlighted;

    private void Awake()
    {
        if (targetInputField == null)
        {
            targetInputField = GetComponent<TMP_InputField>();
            if (targetInputField == null)
            {
                targetInputField = GetComponentInChildren<TMP_InputField>();
            }
        }

        if (highlightImage != null)
        {
            _originalColor = highlightImage.color;
        }
    }

    /// <summary>
    /// 当有对象被拖放到此处时调用（Unity EventSystem 标准接口）
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        // 这个方法由 Unity EventSystem 在标准拖放流程中调用
        // 但我们使用自定义的 OnClueDrop 方法，因为 DraggableClueItem 手动检测
        ClearHighlight();
    }

    /// <summary>
    /// 当线索被拖放到此处时调用（由 DraggableClueItem 调用）
    /// </summary>
    public void OnClueDrop(ClueData clue)
    {
        if (clue == null)
        {
            return;
        }

        if (targetInputField == null)
        {
            Debug.LogWarning("SearchInputDropTarget: targetInputField 未配置。");
            return;
        }

        // 填入 displayName
        targetInputField.text = clue.displayName;
        targetInputField.ActivateInputField();
        targetInputField.MoveTextEnd(false);

        Debug.Log($"[SearchInputDropTarget] 填入线索: {clue.displayName}");

        ClearHighlight();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 检查是否有正在拖拽的线索
        if (DraggableClueItem.CurrentDragging != null)
        {
            ShowHighlight();
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        ClearHighlight();
    }

    private void ShowHighlight()
    {
        if (_isHighlighted)
        {
            return;
        }

        _isHighlighted = true;

        if (highlightImage != null)
        {
            highlightImage.color = highlightColor;
        }
    }

    private void ClearHighlight()
    {
        if (!_isHighlighted)
        {
            return;
        }

        _isHighlighted = false;

        if (highlightImage != null)
        {
            highlightImage.color = _originalColor;
        }
    }
}

