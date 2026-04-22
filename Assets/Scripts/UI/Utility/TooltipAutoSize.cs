using TMPro;
using UnityEngine;

/// <summary>
/// 根据标题和正文的实际渲染内容，自动计算 Tooltip 根节点尺寸。
/// 这个组件不依赖 LayoutGroup，适合挂在悬浮提示这类轻量浮层上。
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
public class TooltipAutoSize : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private RectTransform rootRect;
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;

    [Header("Width")]
    [SerializeField] private float minWidth = 220f;
    [SerializeField] private float maxWidth = 420f;

    [Header("Padding")]
    [SerializeField] private float horizontalPadding = 24f;
    [SerializeField] private float topPadding = 20f;
    [SerializeField] private float bottomPadding = 20f;
    [SerializeField] private float textSpacing = 10f;

    [Header("Debug")]
    [SerializeField] private bool refreshEveryFrameInEditMode;

    public void Configure(RectTransform rootRect, TextMeshProUGUI titleText, TextMeshProUGUI bodyText)
    {
        this.rootRect = rootRect;
        this.titleText = titleText;
        this.bodyText = bodyText;
    }

    private void Awake()
    {
        AutoAssignIfNeeded();
    }

    private void OnEnable()
    {
        AutoAssignIfNeeded();
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying && refreshEveryFrameInEditMode)
        {
            RefreshLayout();
        }
    }

    public void RefreshLayout()
    {
        AutoAssignIfNeeded();
        if (rootRect == null)
        {
            return;
        }

        bool hasTitle = HasVisibleText(titleText);
        bool hasBody = HasVisibleText(bodyText);

        float contentMaxWidth = Mathf.Max(1f, maxWidth - (horizontalPadding * 2f));
        float contentMinWidth = Mathf.Max(1f, minWidth - (horizontalPadding * 2f));

        float preferredTitleWidth = hasTitle ? GetPreferredWidth(titleText, contentMaxWidth) : 0f;
        float preferredBodyWidth = hasBody ? GetPreferredWidth(bodyText, contentMaxWidth) : 0f;
        float contentWidth = Mathf.Clamp(Mathf.Max(preferredTitleWidth, preferredBodyWidth), contentMinWidth, contentMaxWidth);

        float titleHeight = hasTitle ? GetPreferredHeight(titleText, contentWidth) : 0f;
        float bodyHeight = hasBody ? GetPreferredHeight(bodyText, contentWidth) : 0f;
        float spacing = hasTitle && hasBody ? textSpacing : 0f;
        float totalHeight = topPadding + titleHeight + spacing + bodyHeight + bottomPadding;

        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth + (horizontalPadding * 2f));
        rootRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, totalHeight);

        LayoutText(titleText, contentWidth, titleHeight, topPadding);
        LayoutText(bodyText, contentWidth, bodyHeight, topPadding + titleHeight + spacing);
    }

    private void AutoAssignIfNeeded()
    {
        if (rootRect == null)
        {
            rootRect = transform as RectTransform;
        }
    }

    private static bool HasVisibleText(TMP_Text text)
    {
        return text != null && !string.IsNullOrWhiteSpace(text.text);
    }

    private static float GetPreferredWidth(TMP_Text text, float maxWidth)
    {
        Vector2 preferred = text.GetPreferredValues(text.text, maxWidth, 0f);
        return preferred.x;
    }

    private static float GetPreferredHeight(TMP_Text text, float width)
    {
        text.ForceMeshUpdate();
        Vector2 preferred = text.GetPreferredValues(text.text, width, 0f);
        return preferred.y;
    }

    private static void LayoutText(TextMeshProUGUI text, float width, float height, float topOffset)
    {
        if (text == null)
        {
            return;
        }

        RectTransform textRect = text.rectTransform;
        textRect.anchorMin = new Vector2(0.5f, 1f);
        textRect.anchorMax = new Vector2(0.5f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -topOffset);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, width);
        textRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
    }
}
