using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 打字机效果（TMP 版）
/// - 支持 TMP 富文本（<color> <link> 等）
/// - 打字机推进以“可见字符数”为准（避免富文本标签导致打字/音效拖延）
/// - 支持：SetText / AppendText / Clear
/// - 支持：单击加速、双击立即显示完
/// - 支持：打字机循环音效（开始播，最后一个可见字出现立刻停）
/// - 支持：对话框联动（打完后检测 link，否则 NextDialogue）
///
/// 重要约定：
/// - _targetText 保存 raw string（包含富文本标签）
/// - _currentVisibleCharacters 保存“已显示的可见字符数”
/// </summary>
public class TypewriterEffect : MonoBehaviour, IPointerClickHandler
{
    [Header("组件配置")]
    [Tooltip("目标文本组件（如果为空，将自动获取）")]
    [SerializeField] private TextMeshProUGUI targetText;

    [Header("速度设置")]
    [Tooltip("每秒显示的可见字符数")]
    [SerializeField] private float charactersPerSecond = 30f;

    [Tooltip("是否跳过短文本的打字机效果（按可见字符数判断）")]
    [SerializeField] private bool skipTypingForShortText = false;

    [Tooltip("短文本的可见字符长度阈值")]
    [SerializeField] private int shortTextLength = 10;

    [Tooltip("双击时间窗口（秒）")]
    [SerializeField] private float doubleClickWindow = 0.5f;

    [Header("滚动设置")]
    [Tooltip("滚动频率（每N个字符检查一次）")]
    [SerializeField] private int scrollFrequency = 30;

    [Tooltip("滚动偏移量（0-1，不完全到底部，保留可见区域，默认0.05即5%）")]
    [SerializeField] private float scrollOffset = 0.05f;

    [Tooltip("完成时是否滚动到底部（默认true）")]
    [SerializeField] private bool scrollToBottomOnComplete = true;

    [Header("音效设置")]
    [Tooltip("是否启用打字机音效（仅在问讯/搜索界面启用，黑底滚代码不启用）")]
    [SerializeField] private bool enableTypewriterSfx = false;

    [Header("对话框联动（可选）")]
    [Tooltip("是否启用：打完字后点击空白进入下一句；点击 link 收集线索")]
    [SerializeField] private bool enableDialoguePanelClick = false;

    [SerializeField] private DialogueController dialogueController;

    // --------- 内部状态（注意：可见字符 vs raw string） ---------
    private Coroutine _typewriterCoroutine;

    // raw string（包含富文本标签）
    private string _targetText = string.Empty;

    // 已显示的“可见字符数”
    private int _currentVisibleCharacters = 0;

    // 点击加速/双击
    private float _lastClickTime = -1f;
    private bool _isAccelerating = false;
    private float _acceleratedDelayPerChar = 0f;

    // 自动滚动（可选）
    private ScrollRect _scrollRect;

    private void Awake()
    {
        if (targetText == null)
            targetText = GetComponent<TextMeshProUGUI>();

        if (targetText != null)
            _scrollRect = targetText.GetComponentInParent<ScrollRect>();

        if (targetText != null)
        {
            targetText.text = string.Empty;
            targetText.maxVisibleCharacters = 0;
        }
    }

    /// <summary>
    /// 注入对话控制器（用于 NextDialogue）
    /// </summary>
    public void InitDialogue(DialogueController controller)
    {
        dialogueController = controller;
        enableDialoguePanelClick = controller != null;
    }

    /// <summary>
    /// 当前是否正在打字
    /// </summary>
    public bool IsTyping => _typewriterCoroutine != null;

    /// <summary>
    /// 设定新文本（替换模式）
    /// 注意：打字机的终点按“可见字符数”计算，避免富文本标签造成拖延
    /// </summary>
    public void SetText(string text)
    {
        if (targetText == null)
        {
            Debug.LogWarning("[TypewriterEffect] targetText 未配置");
            return;
        }

        StopTypewriterInternal(completeVisible: false);

        _targetText = text ?? string.Empty;
        _currentVisibleCharacters = 0;

        // 先把 raw string 设置给 TMP，然后 ForceMeshUpdate，让 TMP 计算 textInfo.characterCount
        targetText.text = _targetText;
        targetText.maxVisibleCharacters = 0;
        targetText.ForceMeshUpdate();

        int totalVisible = GetTotalVisibleCharacters();

        if (totalVisible <= 0)
            return;

        // 以可见字符数判断“短文本”
        if (skipTypingForShortText && totalVisible <= shortTextLength)
        {
            targetText.maxVisibleCharacters = totalVisible;
            _currentVisibleCharacters = totalVisible;
            return;
        }

        StartTypewriter(0, totalVisible);
    }

    /// <summary>
    /// 追加文本（追加模式）
    /// 注意：start/end 都以“可见字符索引”计算，而不是 raw string length
    /// </summary>
    public void AppendText(string text)
    {
        if (targetText == null)
        {
            Debug.LogWarning("[TypewriterEffect] targetText 未配置");
            return;
        }

        if (string.IsNullOrEmpty(text))
            return;

        // 在追加前，先让 TMP 计算当前可见字符数（旧终点）
        targetText.ForceMeshUpdate();
        int oldVisible = GetTotalVisibleCharacters();

        StopTypewriterInternal(completeVisible: false);

        _targetText += text;

        // 更新 TMP，计算追加后的可见字符数（新终点）
        targetText.text = _targetText;
        targetText.ForceMeshUpdate();
        int newVisible = GetTotalVisibleCharacters();

        // 从 oldVisible 开始打新增部分
        _currentVisibleCharacters = Mathf.Clamp(oldVisible, 0, newVisible);
        targetText.maxVisibleCharacters = _currentVisibleCharacters;

        int addedVisible = Mathf.Max(0, newVisible - oldVisible);

        if (skipTypingForShortText && addedVisible <= shortTextLength)
        {
            _currentVisibleCharacters = newVisible;
            targetText.maxVisibleCharacters = newVisible;

            if (scrollToBottomOnComplete) ScrollToBottom();
            else ScrollToBottomWithOffset(scrollOffset);

            return;
        }

        if (newVisible > oldVisible)
            StartTypewriter(oldVisible, newVisible);
    }

    /// <summary>
    /// 清空文本
    /// </summary>
    public void Clear()
    {
        StopTypewriterInternal(completeVisible: false);
        _targetText = string.Empty;
        _currentVisibleCharacters = 0;

        if (targetText != null)
        {
            targetText.text = string.Empty;
            targetText.maxVisibleCharacters = 0;
        }
    }

    /// <summary>
    /// 开始打字机（start/end 都是“可见字符索引”）
    /// </summary>
    private void StartTypewriter(int startVisibleIndex, int endVisibleIndex)
    {
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _isAccelerating = false;
        _acceleratedDelayPerChar = 0f;

        // [SFX] 开始播放循环音效
        if (enableTypewriterSfx && SfxManager.Instance != null)
            SfxManager.Instance.PlayLoop(SfxId.TypewriterLoop, this);

        _typewriterCoroutine = StartCoroutine(TypewriterCoroutine(startVisibleIndex, endVisibleIndex));
    }

    /// <summary>
    /// 停止打字机（内部用）
    /// completeVisible=true 表示立即显示完“全部可见字符”
    /// </summary>
    private void StopTypewriterInternal(bool completeVisible)
    {
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        // [SFX] 停止循环音效（StopTypewriter 不允许漏停）
        if (enableTypewriterSfx && SfxManager.Instance != null)
            SfxManager.Instance.StopLoop(SfxId.TypewriterLoop, this);

        _isAccelerating = false;
        _acceleratedDelayPerChar = 0f;

        if (targetText == null)
            return;

        // 如果需要立即完成显示：按“可见字符总数”完成（而不是 raw string length）
        if (completeVisible)
        {
            targetText.ForceMeshUpdate();
            int totalVisible = GetTotalVisibleCharacters();
            _currentVisibleCharacters = totalVisible;
            targetText.maxVisibleCharacters = totalVisible;
            return;
        }
    }

    /// <summary>
    /// 打字机协程（start/end 都是“可见字符索引”）
    /// 关键：结束时音效必须立即停止（用 finally 兜底）
    /// </summary>
    private IEnumerator TypewriterCoroutine(int startVisibleIndex, int endVisibleIndex)
    {
        // 防御：确保速度合法
        float delayPerCharacter = charactersPerSecond > 0f ? (1f / charactersPerSecond) : 0f;
        _acceleratedDelayPerChar = 0f;

        try
        {
            for (int i = startVisibleIndex; i < endVisibleIndex; i++)
            {
                _currentVisibleCharacters = i + 1;
                targetText.maxVisibleCharacters = _currentVisibleCharacters;

                // 计算每个可见字符的等待时间
                float currentDelay;
                if (_isAccelerating)
                {
                    // 加速：剩余字符固定在 1 秒内打完
                    if (_acceleratedDelayPerChar <= 0f)
                    {
                        int remainingChars = endVisibleIndex - i;
                        if (remainingChars > 0)
                        {
                            float remainingTime = 1.0f;
                            _acceleratedDelayPerChar = Mathf.Max(remainingTime / remainingChars, 0.001f);
                        }
                        else
                        {
                            _acceleratedDelayPerChar = 0f;
                        }
                    }
                    currentDelay = _acceleratedDelayPerChar;
                }
                else
                {
                    currentDelay = delayPerCharacter;
                    _acceleratedDelayPerChar = 0f;
                }

                if (currentDelay > 0f)
                    yield return new WaitForSeconds(currentDelay);
                else
                    yield return null;

                // 适度检查滚动（不是每个字符都做）
                if (scrollFrequency > 0 && (i % scrollFrequency == 0))
                    yield return StartCoroutine(CheckAndScrollIfNeeded());
            }

            // 确保显示到 endVisibleIndex（可见字符数）
            _currentVisibleCharacters = endVisibleIndex;
            targetText.maxVisibleCharacters = endVisibleIndex;

            // 完成后滚动
            if (scrollToBottomOnComplete) ScrollToBottom();
            else ScrollToBottomWithOffset(scrollOffset);

            // 刷新 mesh，保证 link 的几何信息可用于点击命中
            targetText.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }
        finally
        {
            // [SFX] 无论如何，打字结束/中断都必须停循环音效
            if (enableTypewriterSfx && SfxManager.Instance != null)
                SfxManager.Instance.StopLoop(SfxId.TypewriterLoop, this);

            _typewriterCoroutine = null;
            _isAccelerating = false;
            _acceleratedDelayPerChar = 0f;
        }
    }

    /// <summary>
    /// 双击/单击加速逻辑
    /// </summary>
    private void HandleAcceleration()
    {
        if (_typewriterCoroutine == null)
            return;

        float currentTime = Time.time;

        if (_lastClickTime > 0 && (currentTime - _lastClickTime) < doubleClickWindow)
        {
            // 双击：立即显示完“可见字符”
            SkipToEnd();
            _lastClickTime = -1f;
        }
        else
        {
            // 单击：进入加速状态
            _isAccelerating = true;
            _lastClickTime = currentTime;
        }
    }

    /// <summary>
    /// 立即显示全部文本（按可见字符总数）
    /// </summary>
    public void SkipToEnd()
    {
        // StopTypewriterInternal 会兜底停循环音效
        StopTypewriterInternal(completeVisible: true);

        if (targetText != null)
        {
            if (scrollToBottomOnComplete) ScrollToBottom();
            else ScrollToBottomWithOffset(scrollOffset);

            targetText.ForceMeshUpdate();
            Canvas.ForceUpdateCanvases();
        }
    }

    /// <summary>
    /// 点击逻辑（你指定的交互规则）
    /// 1) 打字中：任何点击先加速/双击跳过（禁止点 link）
    /// 2) 打字结束：允许检测 link（命中则收集线索）
    /// 3) 未命中 link：NextDialogue
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (targetText == null)
            return;

        // 1) 正在打字：任何点击都先加速（不允许点 link）
        if (IsTyping)
        {
            HandleAcceleration();
            return;
        }

        // 2) 打字结束后，才允许检测 link
        targetText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        Camera cam = null;
        if (eventData != null && eventData.pressEventCamera != null)
        {
            cam = eventData.pressEventCamera;
        }
        else
        {
            var canvas = targetText.canvas;
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                cam = canvas.worldCamera;
        }

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(targetText, eventData.position, cam);
        if (linkIndex != -1)
        {
            var linkInfo = targetText.textInfo.linkInfo[linkIndex];
            string clueId = linkInfo.GetLinkID();

            Debug.Log($"[TypewriterEffect] 点击线索链接: {clueId}");

            if (ClueManager.instance != null)
                ClueManager.instance.RevealClue(clueId);

            return;
        }

        // 3) 空白：下一句
        if (enableDialoguePanelClick && dialogueController != null)
            dialogueController.NextDialogue();
    }

    /// <summary>
    /// 空格键加速（保留你的习惯）
    /// </summary>
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
            HandleAcceleration();
    }

    /// <summary>
    /// 计算当前文本的可见字符总数
    /// 注意：必须在 targetText.text 设置后 ForceMeshUpdate() 过，textInfo 才可靠
    /// </summary>
    private int GetTotalVisibleCharacters()
    {
        if (targetText == null || targetText.textInfo == null)
            return 0;

        // TMP 这里的 characterCount 是“可见字符数量”（不包含富文本标签字符）
        return Mathf.Max(0, targetText.textInfo.characterCount);
    }

    // ---------- 滚动相关（保留你原来的实现，略微做了语义对齐） ----------

    private void ScrollToBottom()
    {
        ScrollToBottomWithOffset(0f);
    }

    private void ScrollToBottomWithOffset(float offset)
    {
        if (_scrollRect != null)
            StartCoroutine(ScrollToBottomCoroutine(offset));
    }

    private IEnumerator ScrollToBottomCoroutine(float offset = 0f)
    {
        yield return null;
        Canvas.ForceUpdateCanvases();

        if (_scrollRect != null)
            _scrollRect.verticalNormalizedPosition = Mathf.Clamp01(offset);
    }

    /// <summary>
    /// 检查并滚动（如果需要）
    /// </summary>
    private IEnumerator CheckAndScrollIfNeeded()
    {
        if (targetText == null || _scrollRect == null)
            yield break;

        // 等待一帧，让布局更新完成
        yield return null;

        targetText.ForceMeshUpdate();
        Canvas.ForceUpdateCanvases();

        RectTransform viewport = _scrollRect.viewport != null ? _scrollRect.viewport : _scrollRect.GetComponent<RectTransform>();
        RectTransform content = _scrollRect.content;

        if (viewport == null || content == null)
            yield break;

        float viewportHeight = viewport.rect.height;
        float contentHeight = content.rect.height;

        if (contentHeight <= viewportHeight)
            yield break;

        if (targetText.textInfo == null || targetText.textInfo.lineCount == 0)
            yield break;

        int lastLine = targetText.textInfo.lineCount - 1;
        if (lastLine < 0)
            yield break;

        var lastLineInfo = targetText.textInfo.lineInfo[lastLine];
        float currentDisplayHeight = lastLineInfo.ascender - targetText.textInfo.lineInfo[0].descender;

        if (currentDisplayHeight > viewportHeight)
            ScrollToBottomWithOffset(scrollOffset);
    }

    /// <summary>
    /// 对象禁用时兜底停止循环音效（防止切界面残留）
    /// </summary>
    private void OnDisable()
    {
        if (enableTypewriterSfx && SfxManager.Instance != null)
            SfxManager.Instance.StopLoop(SfxId.TypewriterLoop, this);

        // 可选：禁用时也强制停协程，避免残留状态
        if (_typewriterCoroutine != null)
        {
            StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = null;
        }

        _isAccelerating = false;
        _acceleratedDelayPerChar = 0f;
    }
}
