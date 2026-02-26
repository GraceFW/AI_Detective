using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 线索出示拖放目标
/// 挂载在person对象上，接受任意线索（包括人物线索），显示对应对话
/// </summary>
public class ClueShowDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("组件引用")]
    [Tooltip("对话控制器")]
    [SerializeField] private DialogueController dialogueController;

    [Header("高亮效果（可选）")]
    [Tooltip("拖拽悬停时的高亮颜色")]
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.6f, 1f, 0.3f);

    [Tooltip("高亮显示的Image（可选）")]
    [SerializeField] private Image highlightImage;

    private Color _originalColor;
    private bool _isHighlighted;

    private void Awake()
    {
        if (dialogueController == null)
        {
            dialogueController = FindObjectOfType<DialogueController>();
        }

        if (highlightImage != null)
        {
            _originalColor = highlightImage.color;
        }
    }

    /// <summary>
    /// Unity EventSystem标准拖放接口
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        ClearHighlight();
    }

    /// <summary>
    /// 当线索被拖放到此处时调用（由DraggableClueItem调用）
    /// </summary>
    public void OnClueDrop(ClueData clue)
    {
        if (clue == null)
        {
            Debug.LogWarning("[ClueShowDropTarget] 拖放的线索为空");
            ClearHighlight();
            return;
        }

        // 检查对话控制器
        if (dialogueController == null)
        {
            Debug.LogError("[ClueShowDropTarget] 对话控制器未配置");
            ClearHighlight();
            return;
        }

        // 必须先有对话人物
        if (!dialogueController.HasCurrentPerson)
        {
            Debug.Log("[ClueShowDropTarget] 请先传唤人物");
            ClearHighlight();
            return;
        }

        // 不允许在浏览历史时出示线索
        if (dialogueController.IsBrowsingHistory)
        {
            Debug.Log("[ClueShowDropTarget] 正在浏览历史，无法出示线索。请先回到最新对话。");
            ClearHighlight();
            return;
        }

        // 显示线索对话
        dialogueController.ShowClueDialogue(clue);
        Debug.Log($"[ClueShowDropTarget] 出示线索: {clue.displayName}");

        ClearHighlight();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 检查是否有正在拖拽的线索
        if (DraggableClueItem.CurrentDragging != null)
        {
            // 只有在已有对话人物且不在浏览历史时才显示高亮
            if (dialogueController != null && dialogueController.HasCurrentPerson && !dialogueController.IsBrowsingHistory)
            {
                ShowHighlight();
            }
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

