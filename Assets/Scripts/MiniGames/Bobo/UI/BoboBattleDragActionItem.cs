using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 左侧行动牌的拖拽组件。
/// 使用一份轻量的运行时预览，而不是直接挪动原按钮，避免打乱动作栏布局。
/// </summary>
[DisallowMultipleComponent]
public class BoboBattleDragActionItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public static BoboBattleDragActionItem CurrentDragging { get; private set; }

    private ActionType actionType;
    private Canvas rootCanvas;
    private CanvasGroup sourceCanvasGroup;
    private Image sourceBackground;
    private Image sourceIcon;
    private TextMeshProUGUI sourceLabel;
    private Action onBeginDrag;
    private Action onEndDrag;

    private bool isDragging;
    private RectTransform previewRoot;

    public ActionType ActionType
    {
        get { return actionType; }
    }

    public void Configure(
        ActionType actionType,
        Canvas rootCanvas,
        Image background,
        Image icon,
        TextMeshProUGUI label,
        Action onBeginDrag,
        Action onEndDrag)
    {
        this.actionType = actionType;
        this.rootCanvas = rootCanvas;
        sourceBackground = background;
        sourceIcon = icon;
        sourceLabel = label;
        this.onBeginDrag = onBeginDrag;
        this.onEndDrag = onEndDrag;

        sourceCanvasGroup = GetComponent<CanvasGroup>();
        if (sourceCanvasGroup == null)
        {
            sourceCanvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (rootCanvas == null || actionType == ActionType.None)
        {
            return;
        }

        isDragging = true;
        CurrentDragging = this;
        sourceCanvasGroup.blocksRaycasts = false;
        onBeginDrag?.Invoke();
        CreatePreview();
        UpdatePreviewPosition(eventData);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        UpdatePreviewPosition(eventData);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        isDragging = false;
        sourceCanvasGroup.blocksRaycasts = true;
        DestroyPreview();
        CurrentDragging = null;
        onEndDrag?.Invoke();
    }

    private void CreatePreview()
    {
        GameObject root = new GameObject("BoboDragPreview", typeof(RectTransform), typeof(CanvasGroup));
        previewRoot = root.GetComponent<RectTransform>();
        previewRoot.SetParent(rootCanvas.transform, false);
        previewRoot.sizeDelta = sourceBackground != null
            ? sourceBackground.rectTransform.rect.size
            : (transform as RectTransform) != null ? ((RectTransform)transform).rect.size : new Vector2(120f, 120f);

        CanvasGroup previewCanvasGroup = root.GetComponent<CanvasGroup>();
        previewCanvasGroup.blocksRaycasts = false;
        previewCanvasGroup.interactable = false;
        previewCanvasGroup.alpha = 0.88f;

        if (sourceBackground != null)
        {
            Image background = root.AddComponent<Image>();
            background.sprite = sourceBackground.sprite;
            background.type = sourceBackground.type;
            background.color = sourceBackground.color;
            background.raycastTarget = false;
        }

        if (sourceIcon != null)
        {
            GameObject iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            RectTransform iconRect = iconObject.GetComponent<RectTransform>();
            iconRect.SetParent(previewRoot, false);
            iconRect.anchorMin = new Vector2(0.18f, 0.18f);
            iconRect.anchorMax = new Vector2(0.82f, 0.82f);
            iconRect.offsetMin = Vector2.zero;
            iconRect.offsetMax = Vector2.zero;

            Image icon = iconObject.GetComponent<Image>();
            icon.sprite = sourceIcon.sprite;
            icon.color = sourceIcon.color;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
            icon.enabled = sourceIcon.enabled;
        }

        if (sourceLabel != null)
        {
            GameObject labelObject = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            RectTransform labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.SetParent(previewRoot, false);
            labelRect.anchorMin = new Vector2(0.08f, 0.02f);
            labelRect.anchorMax = new Vector2(0.92f, 0.24f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;

            TextMeshProUGUI previewLabel = labelObject.GetComponent<TextMeshProUGUI>();
            previewLabel.font = sourceLabel.font;
            previewLabel.fontSize = sourceLabel.fontSize;
            previewLabel.fontStyle = sourceLabel.fontStyle;
            previewLabel.alignment = sourceLabel.alignment;
            previewLabel.text = sourceLabel.text;
            previewLabel.color = sourceLabel.color;
            previewLabel.raycastTarget = false;
        }
    }

    private void UpdatePreviewPosition(PointerEventData eventData)
    {
        if (previewRoot == null || rootCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = rootCanvas.transform as RectTransform;
        if (canvasRect == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out Vector2 localPoint);

        previewRoot.anchoredPosition = localPoint;
    }

    private void DestroyPreview()
    {
        if (previewRoot != null)
        {
            Destroy(previewRoot.gameObject);
            previewRoot = null;
        }
    }
}
