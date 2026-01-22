using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算错误提示窗口
/// 当线索未填满或存在错误时显示
/// </summary>
public class SettlementErrorDialog : MonoBehaviour
{
    public enum ErrorType
    {
        Incomplete,  // 未填满
        Incorrect    // 有错误
    }

    [Header("UI组件")]
    [Tooltip("背景遮罩（半透明黑色）")]
    [SerializeField] private Image background;

    [Tooltip("对话框容器")]
    [SerializeField] private GameObject dialog;

    [Tooltip("标题文本")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("错误图标")]
    [SerializeField] private Image iconImage;

    [Tooltip("错误消息文本")]
    [SerializeField] private TextMeshProUGUI messageText;

    [Tooltip("确定按钮")]
    [SerializeField] private Button confirmButton;

    [Header("图标设置")]
    [Tooltip("未填满时的图标颜色")]
    [SerializeField] private Color incompleteIconColor = new Color(1f, 0.8f, 0f); // 黄色

    [Tooltip("有错误时的图标颜色")]
    [SerializeField] private Color incorrectIconColor = new Color(1f, 0.3f, 0.3f); // 红色

    [Header("动画设置")]
    [Tooltip("是否启用淡入动画")]
    [SerializeField] private bool enableFadeAnimation = true;

    [Tooltip("动画时长（秒）")]
    [SerializeField] private float animationDuration = 0.3f;

    [Header("默认消息")]
    [Tooltip("未填满时的默认消息")]
    [TextArea(2, 4)]
    [SerializeField] private string incompleteMessage = "请完成所有问题的线索填写";

    [Tooltip("有错误时的默认消息")]
    [TextArea(2, 4)]
    [SerializeField] private string incorrectMessage = "存在错误答案，请检查后重新提交";

    private CanvasGroup _dialogCanvasGroup;
    private CanvasGroup _backgroundCanvasGroup;

    private void Awake()
    {
        // 初始化 CanvasGroup（用于淡入动画）
        if (dialog != null)
        {
            _dialogCanvasGroup = dialog.GetComponent<CanvasGroup>();
            if (_dialogCanvasGroup == null)
            {
                _dialogCanvasGroup = dialog.gameObject.AddComponent<CanvasGroup>();
            }
        }

        if (background != null)
        {
            _backgroundCanvasGroup = background.GetComponent<CanvasGroup>();
            if (_backgroundCanvasGroup == null)
            {
                _backgroundCanvasGroup = background.gameObject.AddComponent<CanvasGroup>();
            }
        }

        // 绑定按钮事件
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(OnConfirmClicked);
        }

        // 初始隐藏
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(OnConfirmClicked);
        }
    }

    /// <summary>
    /// 显示错误对话框
    /// </summary>
    /// <param name="message">错误消息（如果为空，使用默认消息）</param>
    /// <param name="type">错误类型</param>
    public void Show(string message = null, ErrorType type = ErrorType.Incomplete)
    {
        // 设置消息文本
        if (messageText != null)
        {
            if (string.IsNullOrEmpty(message))
            {
                // 使用默认消息
                message = type == ErrorType.Incomplete ? incompleteMessage : incorrectMessage;
            }
            messageText.text = message;
        }

        // 设置标题
        if (titleText != null)
        {
            titleText.text = type == ErrorType.Incomplete ? "提示" : "错误";
        }

        // 设置图标颜色
        if (iconImage != null)
        {
            iconImage.color = type == ErrorType.Incomplete ? incompleteIconColor : incorrectIconColor;
        }

        gameObject.SetActive(true);

        if (enableFadeAnimation)
        {
            StartCoroutine(FadeInCoroutine());
        }
        else
        {
            // 直接显示
            if (_backgroundCanvasGroup != null)
            {
                _backgroundCanvasGroup.alpha = 1f;
            }
            if (_dialogCanvasGroup != null)
            {
                _dialogCanvasGroup.alpha = 1f;
            }
        }
    }

    /// <summary>
    /// 隐藏对话框
    /// </summary>
    public void Hide()
    {
        if (enableFadeAnimation)
        {
            StartCoroutine(FadeOutCoroutine());
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// 确定按钮点击处理
    /// </summary>
    private void OnConfirmClicked()
    {
        Hide();
    }

    /// <summary>
    /// 淡入动画协程
    /// </summary>
    private System.Collections.IEnumerator FadeInCoroutine()
    {
        // 初始状态：透明
        if (_backgroundCanvasGroup != null)
        {
            _backgroundCanvasGroup.alpha = 0f;
        }
        if (_dialogCanvasGroup != null)
        {
            _dialogCanvasGroup.alpha = 0f;
            if (dialog != null)
            {
                dialog.transform.localScale = Vector3.one * 0.9f; // 轻微缩放效果
            }
        }

        float elapsed = 0f;
        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            // 背景淡入
            if (_backgroundCanvasGroup != null)
            {
                _backgroundCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            }

            // 对话框淡入 + 缩放
            if (_dialogCanvasGroup != null)
            {
                _dialogCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                if (dialog != null)
                {
                    dialog.transform.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, t);
                }
            }

            yield return null;
        }

        // 确保最终状态
        if (_backgroundCanvasGroup != null)
        {
            _backgroundCanvasGroup.alpha = 1f;
        }
        if (_dialogCanvasGroup != null)
        {
            _dialogCanvasGroup.alpha = 1f;
            if (dialog != null)
            {
                dialog.transform.localScale = Vector3.one;
            }
        }
    }

    /// <summary>
    /// 淡出动画协程
    /// </summary>
    private System.Collections.IEnumerator FadeOutCoroutine()
    {
        float elapsed = 0f;
        float startAlpha = _backgroundCanvasGroup != null ? _backgroundCanvasGroup.alpha : 1f;

        while (elapsed < animationDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / animationDuration;

            // 背景和对话框淡出
            if (_backgroundCanvasGroup != null)
            {
                _backgroundCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }
            if (_dialogCanvasGroup != null)
            {
                _dialogCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }
}

