using TMPro;
using UnityEngine;

/// <summary>
/// 自动调整 Content 高度以适应 TextMeshPro 文本内容
/// 解决 Content Size Fitter 红叉问题
/// </summary>
[ExecuteAlways]
public class TextContentAutoHeight : MonoBehaviour
{
    [Tooltip("需要自适应高度的文本组件")]
    [SerializeField] private TextMeshProUGUI targetText;

    [Tooltip("Content 的 RectTransform")]
    [SerializeField] private RectTransform contentRect;

    // [Tooltip("额外的底部边距")]
    // [SerializeField] private float bottomPadding = 20f;

    private void Awake()
    {
        if (targetText == null)
        {
            targetText = GetComponentInChildren<TextMeshProUGUI>();
        }

        if (contentRect == null)
        {
            contentRect = GetComponent<RectTransform>();
        }
    }

    private void LateUpdate()
    {
        UpdateContentHeight();
    }

    /// <summary>
    /// 更新 Content 高度
    /// </summary>
    public void UpdateContentHeight()
    {
        if (targetText == null || contentRect == null)
        {
            return;
        }

        // 强制更新文本网格
        targetText.ForceMeshUpdate();

        // 获取文本的渲染高度
        var textHeight = targetText.preferredHeight;

        // 设置 Content 高度
        var sizeDelta = contentRect.sizeDelta;
        // sizeDelta.y = textHeight + bottomPadding;
        sizeDelta.y = textHeight;
        contentRect.sizeDelta = sizeDelta;
    }
}

