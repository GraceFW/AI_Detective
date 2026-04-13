using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 运行时 UI 创建辅助类。
/// 这套小游戏没有依赖额外 prefab，而是直接在代码里动态生成面板结构，
/// 所以把常用 UI 构造逻辑收敛在这里，能明显减少 BoboBattlePanel 里的重复代码。
/// </summary>
public static class BoboBattleUIFactory
{
    private const string PreferredFontResourcePath = "Fonts & Materials/msyhFin";

    private static TMP_FontAsset cachedPreferredFont;
    private static bool preferredFontResolved;

    /// <summary>
    /// 创建最基础的 RectTransform 节点。
    /// </summary>
    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject gameObject = new GameObject(name, typeof(RectTransform));
        RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
        rectTransform.SetParent(parent, false);
        rectTransform.localScale = Vector3.one;
        return rectTransform;
    }

    /// <summary>
    /// 创建一个带纯色 Image 的节点。
    /// </summary>
    public static Image CreateImage(string name, Transform parent, Color color)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        Image image = rectTransform.gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    /// <summary>
    /// 创建一个 TextMeshPro 文本节点，并填好常用样式。
    /// </summary>
    public static TextMeshProUGUI CreateText(string name, Transform parent, string text, float fontSize, FontStyles fontStyle, TextAlignmentOptions alignment, Color color)
    {
        RectTransform rectTransform = CreateRect(name, parent);
        TextMeshProUGUI label = rectTransform.gameObject.AddComponent<TextMeshProUGUI>();
        label.font = ResolvePreferredFont();

        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = fontStyle;
        label.alignment = alignment;
        label.color = color;
        label.enableWordWrapping = true;
        return label;
    }

    /// <summary>
    /// 小游戏运行时统一优先使用指定的微软雅黑 TMP 字体资源。
    /// 如果资源丢失，再回退到 TMP 的默认字体，避免 UI 因字体缺失而完全不可见。
    /// </summary>
    private static TMP_FontAsset ResolvePreferredFont()
    {
        if (!preferredFontResolved)
        {
            cachedPreferredFont = Resources.Load<TMP_FontAsset>(PreferredFontResourcePath);
            preferredFontResolved = true;

            if (cachedPreferredFont == null)
            {
                Debug.LogWarning("[BoboBattleUIFactory] 未找到 Msyh Fin (TMP_Font Asset)，已回退到 TMP 默认字体。");
            }
        }

        return cachedPreferredFont != null ? cachedPreferredFont : TMP_Settings.defaultFontAsset;
    }

    /// <summary>
    /// 创建一个按钮节点，并自动把文本铺满按钮区域。
    /// </summary>
    public static Button CreateButton(string name, Transform parent, string label, Color backgroundColor, Color labelColor, out TextMeshProUGUI labelText)
    {
        Image image = CreateImage(name, parent, backgroundColor);
        Button button = image.gameObject.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = backgroundColor;
        colors.highlightedColor = Tint(backgroundColor, 1.08f);
        colors.selectedColor = Tint(backgroundColor, 1.08f);
        colors.pressedColor = Tint(backgroundColor, 0.9f);
        colors.disabledColor = new Color(backgroundColor.r * 0.55f, backgroundColor.g * 0.55f, backgroundColor.b * 0.55f, 0.7f);
        button.colors = colors;

        labelText = CreateText("Label", image.transform, label, 22f, FontStyles.Bold, TextAlignmentOptions.Center, labelColor);
        StretchToParent(labelText.rectTransform, 12f, 12f, 8f, 8f);
        return button;
    }

    /// <summary>
    /// 统一配置 LayoutElement，避免外层每次手写重复的组件获取逻辑。
    /// </summary>
    public static LayoutElement AddLayoutElement(Component target, float preferredWidth = -1f, float preferredHeight = -1f, float flexibleWidth = -1f, float flexibleHeight = -1f, float minWidth = -1f, float minHeight = -1f)
    {
        LayoutElement layoutElement = target.GetComponent<LayoutElement>();
        if (layoutElement == null)
        {
            layoutElement = target.gameObject.AddComponent<LayoutElement>();
        }

        if (preferredWidth >= 0f)
        {
            layoutElement.preferredWidth = preferredWidth;
        }

        if (preferredHeight >= 0f)
        {
            layoutElement.preferredHeight = preferredHeight;
        }

        if (flexibleWidth >= 0f)
        {
            layoutElement.flexibleWidth = flexibleWidth;
        }

        if (flexibleHeight >= 0f)
        {
            layoutElement.flexibleHeight = flexibleHeight;
        }

        if (minWidth >= 0f)
        {
            layoutElement.minWidth = minWidth;
        }

        if (minHeight >= 0f)
        {
            layoutElement.minHeight = minHeight;
        }

        return layoutElement;
    }

    /// <summary>
    /// 让一个 RectTransform 拉伸铺满父节点。
    /// </summary>
    public static void StretchToParent(RectTransform rectTransform, float left = 0f, float right = 0f, float top = 0f, float bottom = 0f)
    {
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = new Vector2(left, bottom);
        rectTransform.offsetMax = new Vector2(-right, -top);
    }

    /// <summary>
    /// 简单的颜色亮度调整，主要用于 hover / selected 状态。
    /// </summary>
    public static Color Tint(Color color, float factor)
    {
        return new Color(
            Mathf.Clamp01(color.r * factor),
            Mathf.Clamp01(color.g * factor),
            Mathf.Clamp01(color.b * factor),
            color.a
        );
    }
}
