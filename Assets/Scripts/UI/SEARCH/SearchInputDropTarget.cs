using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Drop target for the single search input field.
/// Dragging a clue here fills the input and auto-submits the current single-input command.
/// </summary>
public class SearchInputDropTarget : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Target Input")]
    [Tooltip("If empty, tries to find a TMP_InputField on self or children.")]
    [SerializeField] private TMP_InputField targetInputField;

    [Header("Search")]
    [Tooltip("Optional explicit reference. Falls back to a parent SearchPanelController.")]
    [SerializeField] private SearchPanelController searchPanelController;

    [Tooltip("When enabled, dropping a clue auto-submits the single-input search command and keeps the text in the input field.")]
    [SerializeField] private bool autoSubmitDroppedClue = true;

    [Header("Highlight")]
    [Tooltip("Highlight color while dragging over the target.")]
    [SerializeField] private Color highlightColor = new Color(0.3f, 0.6f, 1f, 0.3f);

    [Tooltip("Optional highlight image.")]
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

        if (searchPanelController == null)
        {
            searchPanelController = GetComponent<SearchPanelController>();
            if (searchPanelController == null)
            {
                searchPanelController = GetComponentInParent<SearchPanelController>();
            }
        }

        if (highlightImage != null)
        {
            _originalColor = highlightImage.color;
        }
    }

    public void OnDrop(PointerEventData eventData)
    {
        ClearHighlight();
    }

    public bool OnClueDrop(ClueData clue)
    {
        if (clue == null)
        {
            return false;
        }

        if (targetInputField == null)
        {
            Debug.LogWarning("SearchInputDropTarget: targetInputField is not assigned.");
            return false;
        }

        string displayName = clue.displayName ?? string.Empty;
        bool submitted = false;

        if (autoSubmitDroppedClue && searchPanelController != null)
        {
            searchPanelController.SetSearchText(displayName);
            submitted = searchPanelController.SubmitCurrentSingleInput(
                clearInputAfterSubmit: false,
                isManualSubmit: false,
                overrideTargetKey: ResolveGuideTargetKey());
        }

        if (!submitted)
        {
            targetInputField.text = displayName;
            targetInputField.ActivateInputField();
            targetInputField.MoveTextEnd(false);
        }

        if (submitted)
        {
            Debug.Log($"[SearchInputDropTarget] Auto submitted clue: {displayName}");
        }
        else
        {
            Debug.Log($"[SearchInputDropTarget] Filled clue text without auto submit: {displayName}");
        }

        ClearHighlight();
        return true;
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

    private string ResolveGuideTargetKey()
    {
        GuideTarget guideTarget = GetComponent<GuideTarget>();
        if (guideTarget == null)
        {
            guideTarget = GetComponentInParent<GuideTarget>();
        }

        if (guideTarget == null && targetInputField != null)
        {
            guideTarget = targetInputField.GetComponent<GuideTarget>();
            if (guideTarget == null)
            {
                guideTarget = targetInputField.GetComponentInParent<GuideTarget>();
            }
        }

        return guideTarget != null ? guideTarget.key : string.Empty;
    }
}
