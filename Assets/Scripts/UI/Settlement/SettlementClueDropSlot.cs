using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 结算界面：线索填充框（拖拽投放目标）
/// - 接收 DraggableClueItem 拖拽投放
/// - 投放后显示线索名称
/// </summary>
public class SettlementClueDropSlot : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("显示")]
    [Tooltip("显示当前填入的线索名称")]
    [SerializeField] private TextMeshProUGUI clueNameText;

    [Header("高亮（可选）")]
    [SerializeField] private Image highlightImage;
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.6f, 1f, 0.3f);

    private Color _originalColor;
    private bool _isHighlighted;

    public ClueData CurrentClue { get; private set; }

    /// <summary>
    /// 当填入内容变化时触发
    /// </summary>
    public event Action<SettlementClueDropSlot, ClueData> OnClueChanged;

    private void Awake()
    {
        if (highlightImage != null)
        {
            _originalColor = highlightImage.color;
        }

        RefreshView();
    }

    /// <summary>
    /// Unity 标准接口（我们仍然实现，方便以后换成标准拖放流程）
    /// 实际投放由 DraggableClueItem 手动检测并调用 OnClueDrop
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        ClearHighlight();
    }

    /// <summary>
    /// 由 DraggableClueItem 调用：当线索拖拽投放到此槽位时
    /// </summary>
    public void OnClueDrop(ClueData clue)
    {
        if (clue == null)
        {
            ClearHighlight();
            return;
        }

        CurrentClue = clue;
        RefreshView();
        OnClueChanged?.Invoke(this, clue);

        ClearHighlight();
    }

    public void Clear()
    {
        CurrentClue = null;
        RefreshView();
        OnClueChanged?.Invoke(this, null);
    }

    private void RefreshView()
    {
        if (clueNameText == null)
        {
            return;
        }

        clueNameText.text = CurrentClue != null ? CurrentClue.displayName : "待填充";
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
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
