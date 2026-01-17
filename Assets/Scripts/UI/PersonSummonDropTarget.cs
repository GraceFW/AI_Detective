using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 人物传唤拖放目标
/// 挂载在name对象上，只接受人物线索，检查是否可传唤
/// </summary>
public class PersonSummonDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
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
            Debug.LogWarning("[PersonSummonDropTarget] 拖放的线索为空");
            ClearHighlight();
            return;
        }

        // 只接受人物线索
        if (!(clue is PersonClueData personClue))
        {
            Debug.Log($"[PersonSummonDropTarget] 只能拖拽人物线索到此处，当前类型: {clue.GetType().Name}");
            ClearHighlight();
            return;
        }

        // 检查是否可以传唤
        if (!personClue.canBeSummoned)
        {
            Debug.Log($"[PersonSummonDropTarget] {personClue.displayName} 此人暂不可传唤");
            ClearHighlight();
            return;
        }

        // 检查对话控制器
        if (dialogueController == null)
        {
            Debug.LogError("[PersonSummonDropTarget] 对话控制器未配置");
            ClearHighlight();
            return;
        }

        // 启动基础对话
        dialogueController.StartBaseDialogue(personClue);
        Debug.Log($"[PersonSummonDropTarget] 成功传唤 {personClue.displayName}");

        ClearHighlight();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // 检查是否有正在拖拽的线索
        if (DraggableClueItem.CurrentDragging != null)
        {
            var clueData = DraggableClueItem.CurrentDragging.ClueData;
            
            // 只对人物线索显示高亮
            if (clueData is PersonClueData personClue)
            {
                // 进一步检查是否可传唤
                if (personClue.canBeSummoned)
                {
                    ShowHighlight();
                }
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

