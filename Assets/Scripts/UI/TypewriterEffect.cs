using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 打字机效果脚本
/// 为 TMP Text 组件提供打字机效果，支持替换和追加两种模式
/// 可以挂载到包含 TMP Text 组件的对象上，或通过 Inspector 配置 Text 组件
/// </summary>
public class TypewriterEffect : MonoBehaviour, IPointerClickHandler
{
    [Header("组件配置")]
    [Tooltip("目标文本组件（如果为空，将自动获取）")]
    [SerializeField] private TextMeshProUGUI targetText;

    [Header("速度设置")]
    [Tooltip("每秒显示的字符数")]
    [SerializeField] private float charactersPerSecond = 30f;

    [Tooltip("是否跳过短文本的打字机效果")]
    [SerializeField] private bool skipTypingForShortText = false;

    [Tooltip("短文本的长度阈值")]
    [SerializeField] private int shortTextLength = 10;

    [Header("加速设置")]
    [Tooltip("加速倍数（第一次点击/空格时）")]
    [SerializeField] private float accelerationMultiplier = 4f;

    [Tooltip("双击时间窗口（秒）")]
    [SerializeField] private float doubleClickWindow = 0.5f;

    [Header("滚动设置")]
    [Tooltip("滚动频率（每N个字符滚动一次，默认30）")]
    [SerializeField] private int scrollFrequency = 30;

    [Tooltip("滚动偏移量（0-1，不完全到底部，保留可见区域，默认0.05即5%）")]
    [SerializeField] private float scrollOffset = 0.05f;

    [Tooltip("完成时是否滚动到底部（默认true）")]
    [SerializeField] private bool scrollToBottomOnComplete = true;

    // 私有变量
    private Coroutine _typewriterCoroutine;
    private string _targetText = string.Empty;  // 完整的目标文本
    private int _currentVisibleCharacters = 0;  // 当前已显示的字符数
    private float _lastClickTime = -1f;  // 上次点击时间
    private bool _isAccelerating = false;  // 是否正在加速
    private ScrollRect _scrollRect;  // 可选的滚动视图，用于自动滚动

    private void Awake()
    {
        // 如果没有手动指定，自动获取 Text 组件
        if (targetText == null)
        {
            targetText = GetComponent<TextMeshProUGUI>();
        }

        // 尝试查找 ScrollRect（用于自动滚动）
        if (targetText != null)
        {
            _scrollRect = targetText.GetComponentInParent<ScrollRect>();
        }

        // 初始化文本
        if (targetText != null)
        {
            targetText.text = string.Empty;
            targetText.maxVisibleCharacters = 0;
        }
    }

    /// <summary>
    /// 设置新文本（替换模式）
    /// </summary>
    public void SetText(string text)
    {
        if (targetText == null)
        {
            Debug.LogWarning("[TypewriterEffect] targetText 未配置");
            return;
        }

        // 停止当前打字机效果
        StopTypewriter();

        // 设置目标文本
        _targetText = text ?? string.Empty;
        _currentVisibleCharacters = 0;

        // 设置完整文本到 TMP（但隐藏所有字符）
        targetText.text = _targetText;
        targetText.maxVisibleCharacters = 0;
        targetText.ForceMeshUpdate();

        // 如果文本为空，直接返回
        if (string.IsNullOrEmpty(_targetText))
        {
            return;
        }

        // 检查是否需要跳过短文本
        if (skipTypingForShortText && _targetText.Length <= shortTextLength)
        {
            // 直接显示全部文本
            targetText.maxVisibleCharacters = _targetText.Length;
            _currentVisibleCharacters = _targetText.Length;
            return;
        }

        // 开始打字机效果
        StartTypewriter(0, _targetText.Length);
    }

    /// <summary>
    /// 追加文本（追加模式）
    /// </summary>
    public void AppendText(string text)
    {
        if (targetText == null)
        {
            Debug.LogWarning("[TypewriterEffect] targetText 未配置");
            return;
        }

        if (string.IsNullOrEmpty(text))
        {
            return;
        }

        // 停止当前打字机效果
        StopTypewriter();

        // 追加到目标文本
        int startIndex = _currentVisibleCharacters;
        _targetText += text;
        _currentVisibleCharacters = startIndex;

        // 更新 TMP 文本
        targetText.text = _targetText;
        targetText.maxVisibleCharacters = startIndex;
        targetText.ForceMeshUpdate();

        // 检查是否需要跳过短文本
        if (skipTypingForShortText && text.Length <= shortTextLength)
        {
            // 直接显示全部文本
            _currentVisibleCharacters = _targetText.Length;
            targetText.maxVisibleCharacters = _targetText.Length;
            if (scrollToBottomOnComplete)
            {
                ScrollToBottom();
            }
            else
            {
                ScrollToBottomWithOffset(scrollOffset);
            }
            return;
        }

        // 开始打字机效果（只显示新增部分）
        StartTypewriter(startIndex, _targetText.Length);
    }

    /// <summary>
    /// 清空文本
    /// </summary>
    public void Clear()
    {
        StopTypewriter();
        _targetText = string.Empty;
        _currentVisibleCharacters = 0;
        if (targetText != null)
        {
            targetText.text = string.Empty;
            targetText.maxVisibleCharacters = 0;
        }
    }

    /// <summary>
    /// 开始打字机效果
    /// </summary>
    private void StartTypewriter(int startIndex, int endIndex)
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
        }

        _isAccelerating = false;
        _typewriterCoroutine = StartCoroutine(TypewriterCoroutine(startIndex, endIndex));
    }

    /// <summary>
    /// 停止打字机效果
    /// </summary>
    private void StopTypewriter()
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        // 立即完成当前显示
        if (targetText != null && _currentVisibleCharacters < _targetText.Length)
        {
            _currentVisibleCharacters = _targetText.Length;
            targetText.maxVisibleCharacters = _targetText.Length;
        }

        _isAccelerating = false;
    }

    /// <summary>
    /// 打字机协程
    /// </summary>
    private IEnumerator TypewriterCoroutine(int startIndex, int endIndex)
    {
        float delayPerCharacter = 1f / charactersPerSecond;

        for (int i = startIndex; i < endIndex; i++)
        {
            // 检查是否被中断（文本可能已更改）
            if (i >= _targetText.Length)
            {
                break;
            }

            _currentVisibleCharacters = i + 1;
            targetText.maxVisibleCharacters = _currentVisibleCharacters;

            // 计算延迟（如果正在加速，减少延迟）
            float currentDelay = _isAccelerating ? delayPerCharacter / accelerationMultiplier : delayPerCharacter;

            yield return new WaitForSeconds(currentDelay);

            // 检查是否需要滚动：只有当文本填满可见区域时才滚动
            // 等待一帧让 TextContentAutoHeight 更新完成
            if (i % scrollFrequency == 0)
            {
                yield return StartCoroutine(CheckAndScrollIfNeeded());
            }
        }

        // 确保显示所有字符
        _currentVisibleCharacters = _targetText.Length;
        targetText.maxVisibleCharacters = _targetText.Length;
        
        // 完成时根据配置决定是否滚动到底部
        if (scrollToBottomOnComplete)
        {
            ScrollToBottom();
        }
        else
        {
            ScrollToBottomWithOffset(scrollOffset);
        }

        _typewriterCoroutine = null;
        _isAccelerating = false;
    }

    /// <summary>
    /// 立即显示全部文本
    /// </summary>
    private void SkipToEnd()
    {
        StopTypewriter();
        if (targetText != null)
        {
            _currentVisibleCharacters = _targetText.Length;
            targetText.maxVisibleCharacters = _targetText.Length;
            if (scrollToBottomOnComplete)
            {
                ScrollToBottom();
            }
            else
            {
                ScrollToBottomWithOffset(scrollOffset);
            }
        }
    }

    /// <summary>
    /// 处理点击事件（加速功能）
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        HandleAcceleration();
    }

    /// <summary>
    /// 检测空格键（加速功能）
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            HandleAcceleration();
        }
    }

    /// <summary>
    /// 处理加速逻辑
    /// </summary>
    private void HandleAcceleration()
    {
        // 如果没有正在进行的打字机效果，忽略
        if (_typewriterCoroutine == null)
        {
            return;
        }

        float currentTime = Time.time;

        // 检查是否为双击（在时间窗口内）
        if (_lastClickTime > 0 && (currentTime - _lastClickTime) < doubleClickWindow)
        {
            // 双击：立即显示全部文本
            SkipToEnd();
            _lastClickTime = -1f;
        }
        else
        {
            // 单击：加速打字机效果
            _isAccelerating = true;
            _lastClickTime = currentTime;
        }
    }

    /// <summary>
    /// 滚动到底部（完全到底部）
    /// </summary>
    private void ScrollToBottom()
    {
        ScrollToBottomWithOffset(0f);
    }

    /// <summary>
    /// 滚动到底部（带偏移量）
    /// </summary>
    /// <param name="offset">偏移量（0-1，0表示完全到底部）</param>
    private void ScrollToBottomWithOffset(float offset)
    {
        if (_scrollRect != null)
        {
            // 使用协程延迟滚动，确保文本已更新
            StartCoroutine(ScrollToBottomCoroutine(offset));
        }
    }

    /// <summary>
    /// 滚动到底部协程
    /// </summary>
    /// <param name="offset">偏移量（0-1，0表示完全到底部）</param>
    private IEnumerator ScrollToBottomCoroutine(float offset = 0f)
    {
        yield return null;  // 等待一帧，确保 TextMeshPro 已更新

        // 强制更新 Canvas
        Canvas.ForceUpdateCanvases();

        // 滚动到底部（verticalNormalizedPosition = 0 表示底部）
        // offset 值越大，保留的可见区域越多
        if (_scrollRect != null)
        {
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(offset);
        }
    }

    /// <summary>
    /// 检查打字机效果是否正在进行
    /// </summary>
    public bool IsTyping => _typewriterCoroutine != null;

    /// <summary>
    /// 检查并滚动（如果需要）
    /// 考虑 TextContentAutoHeight 脚本的影响
    /// 检测当前显示的文本是否填满可见区域
    /// </summary>
    private IEnumerator CheckAndScrollIfNeeded()
    {
        if (targetText == null || _scrollRect == null)
        {
            yield break;
        }

        // 等待一帧，让 TextContentAutoHeight 在 LateUpdate 中更新完成
        yield return null;

        // 强制更新文本网格和布局
        targetText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        // 获取 ScrollRect 的 viewport 和 content
        RectTransform viewport = _scrollRect.viewport;
        if (viewport == null)
        {
            viewport = _scrollRect.GetComponent<RectTransform>();
        }

        RectTransform content = _scrollRect.content;
        if (viewport == null || content == null)
        {
            yield break;
        }

        // 获取 viewport 的本地坐标高度
        float viewportHeight = viewport.rect.height;

        // 获取 content 的本地坐标高度（由 TextContentAutoHeight 根据完整文本设置）
        float contentHeight = content.rect.height;

        // 如果 content 高度小于等于 viewport 高度，说明还没填满，不需要滚动
        if (contentHeight <= viewportHeight)
        {
            yield break;
        }

        // 获取当前显示的文本的实际渲染高度
        // 使用 textInfo 来获取当前显示的行数和高度
        if (targetText.textInfo == null || targetText.textInfo.lineCount == 0)
        {
            yield break;
        }

        // 获取当前显示的最后一行
        int lastVisibleLine = Mathf.Min(targetText.textInfo.lineCount - 1, 
            targetText.maxVisibleLines > 0 ? targetText.maxVisibleLines - 1 : targetText.textInfo.lineCount - 1);
        
        if (lastVisibleLine < 0)
        {
            yield break;
        }

        // 获取最后一行的高度信息
        var lastLineInfo = targetText.textInfo.lineInfo[lastVisibleLine];
        // 计算当前显示的文本的实际高度（从第一行到最后一行的底部）
        float currentDisplayHeight = lastLineInfo.ascender - targetText.textInfo.lineInfo[0].descender;

        // 如果当前显示的文本高度超过 viewport 高度，说明已经填满，需要滚动
        if (currentDisplayHeight > viewportHeight)
        {
            ScrollToBottomWithOffset(scrollOffset);
        }
    }
}

