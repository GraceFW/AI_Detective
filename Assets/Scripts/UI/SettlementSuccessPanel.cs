using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算成功面板
/// 当所有线索都正确时显示，仅包含下一关按钮
/// </summary>
public class SettlementSuccessPanel : MonoBehaviour
{
    [Header("UI组件")]
    [Tooltip("背景遮罩（半透明黑色）")]
    [SerializeField] private Image background;

    [Tooltip("主面板容器")]
    [SerializeField] private GameObject panel;

    [Tooltip("标题文本（可选）")]
    [SerializeField] private TextMeshProUGUI titleText;

    [Tooltip("成功图标（可选）")]
    [SerializeField] private Image iconImage;

    [Tooltip("下一关按钮")]
    [SerializeField] private Button nextLevelButton;

    [SerializeField] private GameSceneEventSO _loadSceneEvent;
	[SerializeField] private GameSceneSO _nextLevelScene;

    [Header("动画设置")]
    [Tooltip("是否启用淡入动画")]
    [SerializeField] private bool enableFadeAnimation = true;

    [Tooltip("动画时长（秒）")]
    [SerializeField] private float animationDuration = 0.3f;

    /// <summary>
    /// 下一关按钮点击事件
    /// </summary>
    public event Action OnNextLevelClicked;

    private CanvasGroup _panelCanvasGroup;
    private CanvasGroup _backgroundCanvasGroup;

    private void Awake()
    {
        // 初始化 CanvasGroup（用于淡入动画）
        if (panel != null)
        {
            _panelCanvasGroup = panel.GetComponent<CanvasGroup>();
            if (_panelCanvasGroup == null)
            {
                _panelCanvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
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
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.AddListener(OnNextLevelButtonClicked);
        }

        // 初始隐藏
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (nextLevelButton != null)
        {
            nextLevelButton.onClick.RemoveListener(OnNextLevelButtonClicked);
        }
    }

    /// <summary>
    /// 显示结算成功面板
    /// </summary>
    /// <param name="results">结算结果列表</param>
    public void Show(List<SettlementPanelUI.SettlementAnswerResult> results)
    {
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
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = 1f;
            }
        }
    }

    /// <summary>
    /// 隐藏面板
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
    /// 下一关按钮点击处理
    /// </summary>
    private void OnNextLevelButtonClicked()
    {
        OnNextLevelClicked?.Invoke();
        _loadSceneEvent.RaiseEvent(_nextLevelScene);
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
        if (_panelCanvasGroup != null)
        {
            _panelCanvasGroup.alpha = 0f;
            if (panel != null)
            {
                panel.transform.localScale = Vector3.one * 0.9f; // 轻微缩放效果
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

            // 面板淡入 + 缩放
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
                if (panel != null)
                {
                    panel.transform.localScale = Vector3.Lerp(Vector3.one * 0.9f, Vector3.one, t);
                }
            }

            yield return null;
        }

        // 确保最终状态
        if (_backgroundCanvasGroup != null)
        {
            _backgroundCanvasGroup.alpha = 1f;
        }
        if (_panelCanvasGroup != null)
        {
            _panelCanvasGroup.alpha = 1f;
            if (panel != null)
            {
                panel.transform.localScale = Vector3.one;
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

            // 背景和面板淡出
            if (_backgroundCanvasGroup != null)
            {
                _backgroundCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }
            if (_panelCanvasGroup != null)
            {
                _panelCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, t);
            }

            yield return null;
        }

        gameObject.SetActive(false);
    }
}

