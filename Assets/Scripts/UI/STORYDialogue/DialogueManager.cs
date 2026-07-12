using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 对话管理器
/// 负责管理对话的显示、切换和交互
/// </summary>
public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }
    
    [Header("对话数据")]
    [Tooltip("所有关卡的对话数据")]
    public List<DialogueData> dialogueDataList;
    
    [Header("UI引用")]
    [Tooltip("对话面板")]
    public GameObject dialoguePanel;
    
    [Tooltip("背景遮罩")]
    public Image backgroundMask;
    
    [Tooltip("说话人头像")]
    public Image speakerImage;
    
    [Tooltip("说话人名称")]
    public TextMeshProUGUI speakerNameText;
    
    [Tooltip("对话文本")]
    public TextMeshProUGUI dialogueText;
    
    [Tooltip("文本框背景（DialogueTextBG的Image组件）")]
    public Image dialogueTextBG;

    [Header("左右人物版式")]
    [Tooltip("左人物版式根节点。启用右版式时会隐藏它。")]
    [SerializeField] private GameObject leftLayoutRoot;

    [Tooltip("右人物版式根节点。启用左版式时会隐藏它。")]
    [SerializeField] private GameObject rightLayoutRoot;

    [Tooltip("左侧人物立绘。未配置时兼容使用上方 speakerImage。")]
    [SerializeField] private Image leftSpeakerImage;

    [Tooltip("右侧人物立绘。")]
    [SerializeField] private Image rightSpeakerImage;

    [Tooltip("左版式中位于立绘后方的对话框图片。")]
    [SerializeField] private Image leftDialogueTextBGBack;

    [Tooltip("左版式中位于立绘前方的对话框图片。")]
    [SerializeField] private Image leftDialogueTextBGFront;

    [Tooltip("右版式中位于立绘后方的对话框图片。")]
    [SerializeField] private Image rightDialogueTextBGBack;

    [Tooltip("右版式中位于立绘前方的对话框图片。")]
    [SerializeField] private Image rightDialogueTextBGFront;

    [Header("左右版式文字与操作组件")]
    [Tooltip("左版式的说话人名称。未配置时使用上方 Speaker Name Text。")]
    [SerializeField] private TextMeshProUGUI leftSpeakerNameText;

    [Tooltip("右版式的说话人名称。")]
    [SerializeField] private TextMeshProUGUI rightSpeakerNameText;

    [Tooltip("左版式的对话正文。未配置时使用上方 Dialogue Text。")]
    [SerializeField] private TextMeshProUGUI leftDialogueText;

    [Tooltip("右版式的对话正文。")]
    [SerializeField] private TextMeshProUGUI rightDialogueText;

    [Tooltip("左版式的继续提示。未配置时使用下方 Continue Indicator。")]
    [SerializeField] private GameObject leftContinueIndicator;

    [Tooltip("右版式的继续提示。")]
    [SerializeField] private GameObject rightContinueIndicator;

    [Tooltip("左版式的跳过按钮。未配置时使用下方 Skip Button。")]
    [SerializeField] private Button leftSkipButton;

    [Tooltip("右版式的跳过按钮。")]
    [SerializeField] private Button rightSkipButton;
    
    [Tooltip("继续指示器")]
    public GameObject continueIndicator;
    
    [Tooltip("跳过按钮（可选）")]
    public Button skipButton;

    [Header("跳过剧情确认弹窗")]
    [Tooltip("跳过确认弹窗根节点，初始应设为隐藏。")]
    [SerializeField] private GameObject skipConfirmPopup;

    [Tooltip("确认跳过全部剧情的按钮。")]
    [SerializeField] private Button confirmSkipButton;

    [Tooltip("取消跳过并返回剧情的按钮。")]
    [SerializeField] private Button cancelSkipButton;
    
    [Header("设置")]
    [Tooltip("是否允许跳过对话")]
    public bool allowSkip = false;
    
    [Tooltip("打字机效果速度（字符/秒）")]
    public float typewriterSpeed = 30f;
    
    [Tooltip("背景遮罩颜色")]
    public Color maskColor = new Color(0, 0, 0, 0.7f);
    
    [Header("事件系统")]
    [Tooltip("对话开始事件")]
    [SerializeField] private DialogueStartEventSO dialogueStartEvent;
    
    [Tooltip("对话结束事件")]
    [SerializeField] private DialogueEndEventSO dialogueEndEvent;
    
    [Header("场景映射配置（用于从场景获取关卡编号）")]
    [Tooltip("场景名到关卡编号的映射表（用于TriggerNextWaveSpawnDialogue自动获取关卡）")]
    [SerializeField] private List<SceneLevelMapping> sceneMappings = new List<SceneLevelMapping>();

	public int CurrentLevelNumber => currentLevelNumber;

    public bool TryResolveLevelNumberFromScene(GameSceneSO scene, out int levelNumber)
    {
        levelNumber = -1;
        if (scene == null)
        {
            return false;
        }

        return TryResolveLevelNumberFromSceneName(scene.name, out levelNumber);
    }

    public bool TryResolveLevelNumberFromSceneName(string sceneName, out int levelNumber)
    {
        levelNumber = -1;
        int? mappedLevelNumber = FindLevelNumberBySceneName(sceneName);
        if (!mappedLevelNumber.HasValue)
        {
            return false;
        }

        levelNumber = mappedLevelNumber.Value;
        return true;
    }

	// 当前对话状态
	private DialogueSequence currentSequence;
    private int currentEntryIndex = 0;
    private bool isTyping = false;
    private bool isDialogueActive = false;
    private bool isForced = false; // 是否为强制弹出模式
    private bool isWaitingForSpecialNode = false; // 是否正在等待特殊节点完成
    private Coroutine typewriterCoroutine;
    private System.Action onDialogueComplete;
    private int currentLevelNumber = 0;
    private DialogueTriggerType currentTriggerType = DialogueTriggerType.LevelStart;
    private System.Action _cancelSpecialNode;
    private bool _shouldAbortSpecialNodeFlow;
    
    /// <summary>
    /// 按关卡记录 WaveSpawn 对话的触发次数
    /// Key: 关卡编号, Value: 触发次数（从1开始）
    /// </summary>
    private Dictionary<int, int> _waveSpawnTriggerCounts = new Dictionary<int, int>();
    private RectTransform _guideLayoutAvoidRect;
    private Vector2 _guideLayoutAvoidDefaultAnchoredPosition;
    private bool _guideLayoutAvoidDefaultCached;
    private const float GuideLayoutAvoidMargin = 24f;
    private const int UnskippableLevelCompleteLevel = 2;
    private bool _leftSkipButtonDefaultActive;
    private bool _rightSkipButtonDefaultActive;
    private bool _skipButtonDefaultActiveCached;
    private bool _isSkipConfirmationOpen;
    private DialogueLayoutSide _activeLayoutSide = DialogueLayoutSide.Left;

    private TextMeshProUGUI ActiveSpeakerNameText =>
        _activeLayoutSide == DialogueLayoutSide.Right ? rightSpeakerNameText : (leftSpeakerNameText != null ? leftSpeakerNameText : speakerNameText);

    private TextMeshProUGUI ActiveDialogueText =>
        _activeLayoutSide == DialogueLayoutSide.Right ? rightDialogueText : (leftDialogueText != null ? leftDialogueText : dialogueText);

    private GameObject ActiveContinueIndicator =>
        _activeLayoutSide == DialogueLayoutSide.Right ? rightContinueIndicator : (leftContinueIndicator != null ? leftContinueIndicator : continueIndicator);

    private Button ActiveSkipButton =>
        _activeLayoutSide == DialogueLayoutSide.Right ? rightSkipButton : (leftSkipButton != null ? leftSkipButton : skipButton);
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        // 初始隐藏对话面板
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // 设置背景遮罩颜色
        if (backgroundMask != null)
        {
            backgroundMask.color = maskColor;
        }

        HideSkipConfirmation();
    }
    
    private void Start()
    {
        CacheSkipButtonDefaultActive();
        ConfigureSkipButton(leftSkipButton != null ? leftSkipButton : skipButton);
        ConfigureSkipButton(rightSkipButton);

        if (confirmSkipButton != null)
        {
            confirmSkipButton.onClick.RemoveListener(ConfirmSkipDialogue);
            confirmSkipButton.onClick.AddListener(ConfirmSkipDialogue);
        }

        if (cancelSkipButton != null)
        {
            cancelSkipButton.onClick.RemoveListener(CancelSkipDialogue);
            cancelSkipButton.onClick.AddListener(CancelSkipDialogue);
        }
    }

    private void ConfigureSkipButton(Button button)
    {
        if (button == null)
            return;

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(ShowSkipConfirmation);
    }
    
    private void Update()
    {
        // 对话激活时处理输入
        if (isDialogueActive)
        {
            if (_isSkipConfirmationOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape))
                    CancelSkipDialogue();
                return;
            }

            bool shouldContinueByMouse = Input.GetMouseButtonDown(0) && !ShouldIgnoreMouseContinueThisFrame();

            // 点击或空格键继续
            if (shouldContinueByMouse || Input.GetKeyDown(KeyCode.Space))
            {
                OnContinueDialogue();
            }
            
            // ESC exits normal dialogue, but protected story sequences can lock it.
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (CanExitCurrentDialogue())
                {
                    ShowSkipConfirmation();
                }
            }
        }
    }

    private bool ShouldIgnoreMouseContinueThisFrame()
    {
        if (IsPointerOverSkipButton())
        {
            return true;
        }

        return GuideHighlightController.Instance != null &&
               GuideHighlightController.Instance.IsScreenPointOverHighlightedTarget(Input.mousePosition);
    }

    private bool IsPointerOverSkipButton()
    {
        Button currentSkipButton = ActiveSkipButton;
        if (currentSkipButton == null || !currentSkipButton.gameObject.activeInHierarchy || !currentSkipButton.interactable)
        {
            return false;
        }

        RectTransform skipButtonRect = currentSkipButton.transform as RectTransform;
        if (skipButtonRect == null)
        {
            return false;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            skipButtonRect,
            Input.mousePosition,
            GetEventCamera(skipButtonRect));
    }
    
    /// <summary>
    /// 显示对话
    /// </summary>
    /// <param name="levelNumber">关卡编号</param>
    /// <param name="triggerType">触发类型</param>
    /// <param name="waveNumber">波次编号（仅WaveSpawn类型有效）</param>
    /// <param name="onComplete">对话完成回调</param>
    /// <param name="isForced">是否为强制弹出（强制弹出时会中断玩家操作）</param>
    public void ShowDialogue(int levelNumber, DialogueTriggerType triggerType, int waveNumber = 0, System.Action onComplete = null, bool isForced = false)
    {
        // 防止在对话播放过程中重复调用
        if (isDialogueActive)
        {
            Debug.LogWarning($"[DialogueManager] 对话正在播放中，忽略重复的ShowDialogue调用");
            return;
        }

        Debug.Log($"[DialogueManager] 尝试显示对话：关卡={levelNumber}, 触发类型={triggerType}, 波次={waveNumber}");
        Debug.Log($"[DialogueManager] dialogueDataList 数量：{dialogueDataList?.Count ?? 0}");
        
        // 查找对应关卡的对话数据
        DialogueData dialogueData = dialogueDataList?.Find(d => d != null && d.levelNumber == levelNumber);
        if (dialogueData == null)
        {
            Debug.LogWarning($"[DialogueManager] 未找到关卡 {levelNumber} 的对话数据");
            if (dialogueDataList != null && dialogueDataList.Count > 0)
            {
                string levelNumbers = string.Join(", ", dialogueDataList.Where(d => d != null).Select(d => d.levelNumber.ToString()));
                Debug.LogWarning($"[DialogueManager] 当前dialogueDataList中的关卡编号：{levelNumbers}");
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] dialogueDataList为空或未初始化！请在Inspector中配置对话数据。");
            }
            onComplete?.Invoke();
            return;
        }
        
        Debug.Log($"[DialogueManager] 找到对话数据：{dialogueData.name}, 序列数量：{dialogueData.dialogueSequences?.Length ?? 0}");
        
        // 获取对话序列
        DialogueSequence sequence = dialogueData.GetDialogueSequence(triggerType, waveNumber);
        if (sequence == null || sequence.entries == null || sequence.entries.Length == 0)
        {
            Debug.LogWarning($"[DialogueManager] 关卡 {levelNumber} 的触发类型 {triggerType} 没有对话内容");
            if (dialogueData.dialogueSequences != null && dialogueData.dialogueSequences.Length > 0)
            {
                string triggerTypes = string.Join(", ", dialogueData.dialogueSequences.Where(s => s != null).Select(s => s.triggerType.ToString()));
                Debug.LogWarning($"[DialogueManager] 该对话数据中的序列类型：{triggerTypes}");
            }
            else
            {
                Debug.LogWarning($"[DialogueManager] 该对话数据的dialogueSequences为空！");
            }
            onComplete?.Invoke();
            return;
        }
        
        Debug.Log($"[DialogueManager] 找到对话序列，条目数量：{sequence.entries.Length}，开始显示对话");
        
        // 保存当前对话信息
        currentLevelNumber = levelNumber;
        currentTriggerType = triggerType;
        this.isForced = isForced;
        
        // 如果是 LevelStart 对话，重置该关卡的 WaveSpawn 计数器
        if (triggerType == DialogueTriggerType.LevelStart)
        {
            ResetWaveSpawnTriggerCount(levelNumber);
            Debug.Log($"[DialogueManager] 关卡 {levelNumber} 的 WaveSpawn 计数器已重置");
        }
        
        // 开始显示对话
        StartCoroutine(ShowDialogueSequence(sequence, onComplete));
    }
    
    /// <summary>
    /// 显示对话序列（协程）
    /// </summary>
    private IEnumerator ShowDialogueSequence(DialogueSequence sequence, System.Action onComplete)
    {
        currentSequence = sequence;
        currentEntryIndex = 0;
        onDialogueComplete = onComplete;
        isDialogueActive = true;
        isWaitingForSpecialNode = false;
        currentTriggerType = sequence.triggerType;
        ApplySkipButtonVisibilityForCurrentDialogue();
        // 触发对话开始事件
        if (dialogueStartEvent != null)
        {
            dialogueStartEvent.RaiseEvent(currentLevelNumber, currentTriggerType);
        }
        
        // 显示对话面板
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }
        
        // // 播放音效（可选）
        // if (AudioManager.Instance != null)
        // {
        //     AudioManager.Instance.PlaySFX("DialogueOpen");
        // }
        
        // 显示第一句对话
        yield return StartCoroutine(ShowCurrentEntryCoroutine());
        
        // 等待对话完成（通过OnContinueDialogue推进）
        // 注意：特殊节点（如NameInput）会在其协程内部自动推进，不需要在这里等待
        while (isDialogueActive)
        {
            yield return null;
        }

        // 结束对话
        FinishDialogue();

	}
    
    /// <summary>
    /// 显示当前对话条目（协程版本，支持特殊节点）
    /// </summary>
    private IEnumerator ShowCurrentEntryCoroutine()
    {
        if (currentSequence == null || currentEntryIndex >= currentSequence.entries.Length)
        {
            yield break;
        }
        
        DialogueEntry entry = currentSequence.entries[currentEntryIndex];
        
        // 根据节点类型处理
        switch (entry.nodeType)
        {
            case DialogueNodeType.Normal:
                // 普通对话节点
                ShowNormalDialogueEntry(entry);
                break;
                
            case DialogueNodeType.NameInput:
                // 起名弹窗节点
                yield return StartCoroutine(ShowNameInputNode(entry));
                break;
                
            case DialogueNodeType.CustomAction:
                // 自定义动作节点
                yield return StartCoroutine(ShowCustomActionNode(entry));
                break;
        }
    }
    
    /// <summary>
    /// 显示普通对话条目
    /// </summary>
    private void ShowNormalDialogueEntry(DialogueEntry entry)
    {
        ApplyDialogueLayout(entry);
        
        // 设置说话人名称
        TextMeshProUGUI currentSpeakerNameText = ActiveSpeakerNameText;
        if (currentSpeakerNameText != null)
        {
            currentSpeakerNameText.text = entry.speakerName;
        }
        
        // 隐藏继续指示器
        GameObject currentContinueIndicator = ActiveContinueIndicator;
        if (currentContinueIndicator != null)
        {
            currentContinueIndicator.SetActive(false);
        }
        
        // 显示对话文本（使用打字机效果）
        TextMeshProUGUI currentDialogueText = ActiveDialogueText;
        if (currentDialogueText != null)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            
            if (entry.useTypewriterEffect)
            {
                typewriterCoroutine = StartCoroutine(TypewriterEffect(entry.dialogueText, entry.typewriterSpeed));
            }
            else
            {
                currentDialogueText.text = entry.dialogueText;
                isTyping = false;
                ShowContinueIndicator();
            }
        }
    }

    private void ApplyDialogueLayout(DialogueEntry entry)
    {
        bool useRight = entry != null && entry.layoutSide == DialogueLayoutSide.Right;
        _activeLayoutSide = useRight ? DialogueLayoutSide.Right : DialogueLayoutSide.Left;

        if (leftLayoutRoot != null)
            leftLayoutRoot.SetActive(!useRight);
        if (rightLayoutRoot != null)
            rightLayoutRoot.SetActive(useRight);

        SetOptionalObjectActive(leftSpeakerNameText, !useRight);
        SetOptionalObjectActive(rightSpeakerNameText, useRight);
        SetOptionalObjectActive(leftDialogueText, !useRight);
        SetOptionalObjectActive(rightDialogueText, useRight);
        if (leftContinueIndicator != null)
            leftContinueIndicator.SetActive(false);
        if (rightContinueIndicator != null)
            rightContinueIndicator.SetActive(false);

        Image leftPortrait = leftSpeakerImage != null ? leftSpeakerImage : speakerImage;
        SetPortrait(leftPortrait, !useRight ? entry?.speakerImage : null);
        SetPortrait(rightSpeakerImage, useRight ? entry?.speakerImage : null);

        // textBoxBackground 继续兼容旧数据：配置后替换当前版式的前景框图片。
        if (entry != null && entry.textBoxBackground != null)
        {
            Image activeFront = useRight ? rightDialogueTextBGFront : leftDialogueTextBGFront;
            if (activeFront != null)
                activeFront.sprite = entry.textBoxBackground;
            else if (dialogueTextBG != null)
                dialogueTextBG.sprite = entry.textBoxBackground;
        }

        SetLayoutGraphicActive(leftDialogueTextBGBack, !useRight);
        SetLayoutGraphicActive(leftDialogueTextBGFront, !useRight);
        SetLayoutGraphicActive(rightDialogueTextBGBack, useRight);
        SetLayoutGraphicActive(rightDialogueTextBGFront, useRight);
        ApplySkipButtonVisibilityForCurrentDialogue();
    }

    private static void SetPortrait(Image target, Sprite portrait)
    {
        if (target == null)
            return;

        target.sprite = portrait;
        target.gameObject.SetActive(portrait != null);
    }

    private static void SetLayoutGraphicActive(Image target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }

    private static void SetOptionalObjectActive(Component target, bool active)
    {
        if (target != null)
            target.gameObject.SetActive(active);
    }
    
    /// <summary>
    /// 显示起名弹窗节点
    /// </summary>
    private IEnumerator ShowNameInputNode(DialogueEntry entry)
    {
        // 隐藏对话面板（可选，根据需求决定是否隐藏）
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // 标记正在等待特殊节点
        isWaitingForSpecialNode = true;
        
        // 显示起名弹窗
        bool nameInputCompleted = false;
        string playerName = "";
        
        if (NameInputDialog.Instance != null)
        {
            _cancelSpecialNode = () =>
            {
                if (NameInputDialog.Instance != null)
                {
                    NameInputDialog.Instance.Hide();
                }

                nameInputCompleted = true;
            };

            NameInputDialog.Instance.Show((name) =>
            {
                playerName = name;
                nameInputCompleted = true;
            });
        }
        else
        {
            Debug.LogError("[DialogueManager] NameInputDialog.Instance未找到，无法显示起名弹窗");
            nameInputCompleted = true; // 直接完成，避免卡死
        }
        
        // 等待起名弹窗完成
        while (!nameInputCompleted)
        {
            yield return null;
        }

        _cancelSpecialNode = null;
        if (_shouldAbortSpecialNodeFlow || !isDialogueActive || currentSequence == null)
        {
            _shouldAbortSpecialNodeFlow = false;
            isWaitingForSpecialNode = false;
            yield break;
        }
        
        // 恢复对话面板
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }
        
        // 标记特殊节点完成
        isWaitingForSpecialNode = false;
        
        Debug.Log($"[DialogueManager] 起名弹窗完成，玩家名字：{playerName}");
        
        // 自动继续到下一句对话（不通过OnContinueDialogue，直接推进索引）
        currentEntryIndex++;
        
        if (currentEntryIndex < currentSequence.entries.Length)
        {
            // 继续显示下一句对话
            yield return StartCoroutine(ShowCurrentEntryCoroutine());
        }
        else
        {
            // 对话结束
            EndDialogue();
        }
    }

    private IEnumerator ShowCustomActionNode(DialogueEntry entry)
    {
        if (DialogueCustomActionRouter.ShouldShowDialogueText(entry) && HasVisibleDialogueContent(entry))
        {
            if (dialoguePanel != null)
            {
                dialoguePanel.SetActive(true);
            }

            bool handledWithText = DialogueCustomActionRouter.TryExecute(entry, (_) => { });
            if (!handledWithText)
            {
                Debug.LogWarning($"[DialogueManager] 未识别的 CustomAction 节点：{entry.customActionId}");
            }

            isWaitingForSpecialNode = false;
            ShowNormalDialogueEntry(entry);
            yield break;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }

        isWaitingForSpecialNode = true;
        bool actionCompleted = false;

        _cancelSpecialNode = () =>
        {
            BoboBattleService.CloseCurrentAsCancelled();
            actionCompleted = true;
        };

        bool handled = DialogueCustomActionRouter.TryExecute(entry, (_) =>
        {
            actionCompleted = true;
        });

        if (!handled)
        {
            Debug.LogWarning($"[DialogueManager] 未识别的 CustomAction 节点：{entry.customActionId}");
            actionCompleted = true;
        }

        while (!actionCompleted)
        {
            yield return null;
        }

        _cancelSpecialNode = null;
        if (_shouldAbortSpecialNodeFlow || !isDialogueActive || currentSequence == null)
        {
            _shouldAbortSpecialNodeFlow = false;
            isWaitingForSpecialNode = false;
            yield break;
        }

        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(true);
        }

        isWaitingForSpecialNode = false;
        currentEntryIndex++;

        if (currentEntryIndex < currentSequence.entries.Length)
        {
            yield return StartCoroutine(ShowCurrentEntryCoroutine());
        }
        else
        {
            EndDialogue();
        }
    }

    private static bool HasVisibleDialogueContent(DialogueEntry entry)
    {
        return entry != null &&
               (!string.IsNullOrWhiteSpace(entry.dialogueText) ||
                !string.IsNullOrWhiteSpace(entry.speakerName) ||
                entry.speakerImage != null);
    }
    
    /// <summary>
    /// 打字机效果协程
    /// </summary>
    private IEnumerator TypewriterEffect(string text, float speed)
    {
        TextMeshProUGUI currentDialogueText = ActiveDialogueText;
        if (currentDialogueText == null)
            yield break;

        isTyping = true;
        currentDialogueText.text = "";
        
        float interval = 1f / speed;
        
        foreach (char c in text)
        {
            currentDialogueText.text += c;
            yield return new WaitForSeconds(interval);
        }
        
        isTyping = false;
        ShowContinueIndicator();
    }
    
    /// <summary>
    /// 显示继续指示器
    /// </summary>
    private void ShowContinueIndicator()
    {
        GameObject currentContinueIndicator = ActiveContinueIndicator;
        if (currentContinueIndicator != null)
        {
            currentContinueIndicator.SetActive(true);
            // 可以添加闪烁动画
        }
    }
    
    /// <summary>
    /// 继续对话（玩家点击或按空格）
    /// </summary>
    private void OnContinueDialogue()
    {
        // 如果正在等待特殊节点完成，忽略继续操作
        if (isWaitingForSpecialNode)
        {
            return;
        }
        
        // 如果正在打字，立即完成打字
        if (isTyping)
        {
            if (typewriterCoroutine != null)
            {
                StopCoroutine(typewriterCoroutine);
            }
            
            if (currentSequence != null && currentEntryIndex < currentSequence.entries.Length)
            {
                TextMeshProUGUI currentDialogueText = ActiveDialogueText;
                if (currentDialogueText != null)
                    currentDialogueText.text = currentSequence.entries[currentEntryIndex].dialogueText;
            }
            
            isTyping = false;
            ShowContinueIndicator();
            return;
        }
        
        // // 播放音效
        // if (AudioManager.Instance != null)
        // {
        //     AudioManager.Instance.PlaySFX("DialogueNext");
        // }
        
        // 显示下一句对话
        currentEntryIndex++;
        
        if (currentEntryIndex < currentSequence.entries.Length)
        {
            StartCoroutine(ShowCurrentEntryCoroutine());
        }
        else
        {
            // 对话结束
            EndDialogue();
        }
    }
    
    /// <summary>
    /// 跳过对话（保留用于兼容性，内部调用ExitDialogue）
    /// </summary>
    private void SkipDialogue()
    {
        ShowSkipConfirmation();
    }

    public void ShowSkipConfirmation()
    {
        if (!isDialogueActive || !CanExitCurrentDialogue())
            return;

        if (skipConfirmPopup == null || confirmSkipButton == null || cancelSkipButton == null)
        {
            Debug.LogWarning("[DialogueManager] 跳过确认弹窗引用不完整，已阻止直接跳过。请在 Inspector 中完成配置。");
            return;
        }

        _isSkipConfirmationOpen = true;
        skipConfirmPopup.SetActive(true);
        if (ActiveSkipButton != null)
            ActiveSkipButton.interactable = false;
    }


    private void ConfirmSkipDialogue()
    {
        if (!_isSkipConfirmationOpen)
            return;

        HideSkipConfirmation();
        ExitDialogue();
    }

    private void CancelSkipDialogue()
    {
        HideSkipConfirmation();
    }

    private void HideSkipConfirmation()
    {
        _isSkipConfirmationOpen = false;
        if (skipConfirmPopup != null)
            skipConfirmPopup.SetActive(false);
        if (ActiveSkipButton != null)
            ActiveSkipButton.interactable = true;
    }
    
    /// <summary>
    /// 直接退出对话（公开方法，供SkipButton和Esc键调用）
    /// </summary>
    public void ExitDialogue()
    {
        if (!isDialogueActive)
        {
            return;
        }

        if (!CanExitCurrentDialogue())
        {
            Debug.Log("[DialogueManager] Current dialogue is marked as unskippable; ExitDialogue ignored.");
            return;
        }
        
        HideSkipConfirmation();
        Debug.Log("[DialogueManager] 退出对话");
        
        // 停止打字机效果
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        
        // 如果正在显示起名弹窗，先关闭它
        if (isWaitingForSpecialNode && _cancelSpecialNode != null)
        {
            _shouldAbortSpecialNodeFlow = true;
            _cancelSpecialNode.Invoke();
        }

        RestoreGuideLayoutAvoidance();
        RestoreSkipButtonVisibility();

		// 重置状态,不能重置对话序列的触发类型，否则会导致事件系统无法正确识别当前对话类型
		isTyping = false;
        isDialogueActive = false;
        isWaitingForSpecialNode = false;
        currentSequence = null;
        currentEntryIndex = 0;
        isForced = false;
        _cancelSpecialNode = null;
        _shouldAbortSpecialNodeFlow = false;
 
		// // 播放音效
		// if (AudioManager.Instance != null)
		// {
		//     AudioManager.Instance.PlaySFX("DialogueClose");
		// }

	}
    
    /// <summary>
    /// 结束对话（内部方法，正常完成对话时调用，执行状态控制）
    /// </summary>
    private void EndDialogue()
    {
        HideSkipConfirmation();
        RestoreGuideLayoutAvoidance();
        RestoreSkipButtonVisibility();
        isDialogueActive = false;
        isWaitingForSpecialNode = false;
        currentSequence = null;
        currentEntryIndex = 0;
        isForced = false;
        _cancelSpecialNode = null;
        _shouldAbortSpecialNodeFlow = false;
        
        // // 播放音效
        // if (AudioManager.Instance != null)
        // {
        //     AudioManager.Instance.PlaySFX("DialogueClose");
        // }
    }
    
    /// <summary>
    /// 检查对话是否正在进行
    /// </summary>
    public bool IsDialogueActive()
    {
        return isDialogueActive;
    }

    public void ApplyGuideLayoutAvoidance(IReadOnlyList<Rect> avoidRects)
    {
        // 新版左右剧情对话框拥有各自固定布局，不再跟随教程高亮区域移动。
        // 保留公开入口以兼容 GuideHighlightController 的现有调用。
        RestoreGuideLayoutAvoidance();
    }

    public void ClearGuideLayoutAvoidance()
    {
        RestoreGuideLayoutAvoidance();
    }

    private bool TryGetGuideLayoutAvoidRect(out RectTransform layoutRect)
    {
        layoutRect = dialogueTextBG != null ? dialogueTextBG.rectTransform : null;
        if (layoutRect == null)
        {
            return false;
        }

        if (_guideLayoutAvoidRect != layoutRect)
        {
            _guideLayoutAvoidRect = layoutRect;
            _guideLayoutAvoidDefaultAnchoredPosition = layoutRect.anchoredPosition;
            _guideLayoutAvoidDefaultCached = true;
        }
        else if (!_guideLayoutAvoidDefaultCached)
        {
            _guideLayoutAvoidDefaultAnchoredPosition = layoutRect.anchoredPosition;
            _guideLayoutAvoidDefaultCached = true;
        }

        return true;
    }

    private float CalculateGuideLayoutAvoidShift(RectTransform layoutRect, IReadOnlyList<Rect> avoidRects)
    {
        if (avoidRects == null || avoidRects.Count == 0)
        {
            return 0f;
        }

        var originalPosition = layoutRect.anchoredPosition;
        if (originalPosition != _guideLayoutAvoidDefaultAnchoredPosition)
        {
            layoutRect.anchoredPosition = _guideLayoutAvoidDefaultAnchoredPosition;
            Canvas.ForceUpdateCanvases();
        }

        if (!TryGetScreenRect(layoutRect, out var layoutScreenRect))
        {
            return 0f;
        }

        float desiredBottom = layoutScreenRect.yMin;
        bool shouldMove = false;

        for (int i = 0; i < avoidRects.Count; i++)
        {
            var avoidRect = avoidRects[i];
            bool overlapX = layoutScreenRect.xMin < avoidRect.xMax && layoutScreenRect.xMax > avoidRect.xMin;
            bool overlapY = layoutScreenRect.yMin < avoidRect.yMax + GuideLayoutAvoidMargin &&
                            layoutScreenRect.yMax > avoidRect.yMin - GuideLayoutAvoidMargin;

            if (!overlapX || !overlapY)
            {
                continue;
            }

            desiredBottom = Mathf.Max(desiredBottom, avoidRect.yMax + GuideLayoutAvoidMargin);
            shouldMove = true;
        }

        if (!shouldMove)
        {
            return 0f;
        }

        var parentRect = layoutRect.parent as RectTransform;
        if (parentRect == null)
        {
            return desiredBottom - layoutScreenRect.yMin;
        }

        var eventCamera = GetEventCamera(parentRect);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, new Vector2(0f, layoutScreenRect.yMin), eventCamera, out var currentLocalBottom))
        {
            return desiredBottom - layoutScreenRect.yMin;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, new Vector2(0f, desiredBottom), eventCamera, out var desiredLocalBottom))
        {
            return desiredBottom - layoutScreenRect.yMin;
        }

        return desiredLocalBottom.y - currentLocalBottom.y;
    }

    private void RestoreGuideLayoutAvoidance()
    {
        if (_guideLayoutAvoidRect == null || !_guideLayoutAvoidDefaultCached)
        {
            return;
        }

        _guideLayoutAvoidRect.anchoredPosition = _guideLayoutAvoidDefaultAnchoredPosition;
    }

    private bool CanExitCurrentDialogue()
    {
        return !IsCurrentDialogueExitLocked();
    }

    private bool IsCurrentDialogueExitLocked()
    {
        return currentLevelNumber == UnskippableLevelCompleteLevel &&
               currentTriggerType == DialogueTriggerType.LevelComplete;
    }

    private void ApplySkipButtonVisibilityForCurrentDialogue()
    {
        CacheSkipButtonDefaultActive();
        Button leftButton = leftSkipButton != null ? leftSkipButton : skipButton;
        bool exitAllowed = !IsCurrentDialogueExitLocked();

        if (leftButton != null)
            leftButton.gameObject.SetActive(_activeLayoutSide == DialogueLayoutSide.Left && _leftSkipButtonDefaultActive && exitAllowed);
        if (rightSkipButton != null && rightSkipButton != leftButton)
            rightSkipButton.gameObject.SetActive(_activeLayoutSide == DialogueLayoutSide.Right && _rightSkipButtonDefaultActive && exitAllowed);
    }

    private void RestoreSkipButtonVisibility()
    {
        if (!_skipButtonDefaultActiveCached)
        {
            return;
        }

        Button leftButton = leftSkipButton != null ? leftSkipButton : skipButton;
        if (leftButton != null)
            leftButton.gameObject.SetActive(_leftSkipButtonDefaultActive);
        if (rightSkipButton != null && rightSkipButton != leftButton)
            rightSkipButton.gameObject.SetActive(_rightSkipButtonDefaultActive);
    }

    private void CacheSkipButtonDefaultActive()
    {
        if (_skipButtonDefaultActiveCached)
        {
            return;
        }

        Button leftButton = leftSkipButton != null ? leftSkipButton : skipButton;
        _leftSkipButtonDefaultActive = leftButton != null && leftButton.gameObject.activeSelf;
        _rightSkipButtonDefaultActive = rightSkipButton != null && rightSkipButton.gameObject.activeSelf;
        _skipButtonDefaultActiveCached = true;
    }

    private static bool TryGetScreenRect(RectTransform rectTransform, out Rect screenRect)
    {
        screenRect = default;
        if (rectTransform == null)
        {
            return false;
        }

        var corners = new Vector3[4];
        rectTransform.GetWorldCorners(corners);

        var eventCamera = GetEventCamera(rectTransform);
        var min = new Vector2(float.MaxValue, float.MaxValue);
        var max = new Vector2(float.MinValue, float.MinValue);

        for (int i = 0; i < corners.Length; i++)
        {
            var screenPoint = RectTransformUtility.WorldToScreenPoint(eventCamera, corners[i]);
            min = Vector2.Min(min, screenPoint);
            max = Vector2.Max(max, screenPoint);
        }

        if (max.x - min.x <= 0.5f || max.y - min.y <= 0.5f)
        {
            return false;
        }

        screenRect = Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        return true;
    }

    private static Camera GetEventCamera(Component component)
    {
        if (component == null)
        {
            return null;
        }

        var canvas = component.GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            return null;
        }

        var rootCanvas = canvas.rootCanvas;
        if (rootCanvas != null && rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            return rootCanvas.worldCamera;
        }

        return null;
    }

    // 结束对话序列，执行表现层管理和事件发布
	private void FinishDialogue()
	{
		HideSkipConfirmation();
		RestoreGuideLayoutAvoidance();
        RestoreSkipButtonVisibility();

		// 隐藏对话面板
		if (dialoguePanel != null)
		{
			dialoguePanel.SetActive(false);
		}

		if (dialogueEndEvent != null)
		{
			dialogueEndEvent.RaiseEvent(currentLevelNumber, currentTriggerType);
		}

		onDialogueComplete?.Invoke();
		onDialogueComplete = null;
	}

	/// <summary>
	/// 触发当前关卡的下一次 WaveSpawn 对话
	/// </summary>
	/// <param name="levelNumber">关卡编号（如果为-1，则使用当前关卡或从场景获取）</param>
	/// <param name="onComplete">对话完成回调</param>
	/// <param name="isForced">是否为强制弹出</param>
	/// <returns>是否成功触发对话</returns>
	public bool TriggerNextWaveSpawnDialogue(int levelNumber = -1, System.Action onComplete = null, bool isForced = false)
    {
        // 1. 确定关卡编号
        if (levelNumber < 0)
        {
            // 如果未指定，尝试使用当前关卡
            levelNumber = currentLevelNumber;
            
            // 如果当前关卡也未设置，尝试从场景获取
            if (levelNumber < 0)
            {
                levelNumber = GetCurrentLevelFromScene();
            }
            
            // 如果仍然无法确定，返回失败
            if (levelNumber < 0)
            {
                Debug.LogError("[DialogueManager] 无法确定当前关卡编号，请手动指定 levelNumber 参数或在Inspector中配置场景映射表");
                return false;
            }
        }
        
        // 2. 获取当前触发次数
        int triggerCount = GetWaveSpawnTriggerCount(levelNumber);
        
        // 3. 计算波次编号（第1次触发 = waveNumber 0）
        int waveNumber = triggerCount;
        
        // 4. 检查对话数据是否存在
        DialogueData dialogueData = dialogueDataList?.Find(d => d != null && d.levelNumber == levelNumber);
        if (dialogueData == null)
        {
            Debug.LogWarning($"[DialogueManager] 未找到关卡 {levelNumber} 的对话数据");
            return false;
        }
        
        // 检查该波次的对话是否存在
        DialogueSequence sequence = dialogueData.GetDialogueSequence(DialogueTriggerType.WaveSpawn, waveNumber);
        if (sequence == null || sequence.entries == null || sequence.entries.Length == 0)
        {
            Debug.LogWarning($"[DialogueManager] 关卡 {levelNumber} 的 WaveSpawn 对话（波次 {waveNumber}）不存在");
            return false;
        }
        
        // 5. 创建包装的回调，在对话结束后更新计数
        System.Action wrappedCallback = () =>
        {
            // 更新触发次数
            _waveSpawnTriggerCounts[levelNumber] = triggerCount + 1;
            Debug.Log($"[DialogueManager] 关卡 {levelNumber} 的 WaveSpawn 对话触发次数已更新为：{triggerCount + 1}");
            
            // 调用原始回调
            onComplete?.Invoke();
        };
        
        // 6. 触发对话
        ShowDialogue(levelNumber, DialogueTriggerType.WaveSpawn, waveNumber, wrappedCallback, isForced);
        
        Debug.Log($"[DialogueManager] 触发关卡 {levelNumber} 的第 {triggerCount + 1} 次 WaveSpawn 对话（波次 {waveNumber}）");
        return true;
    }
    
    /// <summary>
    /// 从场景获取当前关卡编号
    /// </summary>
    private int GetCurrentLevelFromScene()
    {
        // 尝试从 SceneManager 获取当前场景
        SceneManager sceneManager = FindObjectOfType<SceneManager>();
        if (sceneManager != null)
        {
            // 通过反射获取 currentScene 字段（因为它是 private）
            var currentSceneField = typeof(SceneManager).GetField("currentScene", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (currentSceneField != null)
            {
                var currentScene = currentSceneField.GetValue(sceneManager) as GameSceneSO;
                if (currentScene != null)
                {
                    // 获取场景名称（使用SO资源名称）
                    string sceneName = currentScene.name;
                    
                    // 使用映射表查找关卡编号
                    int? levelNumber = FindLevelNumberBySceneName(sceneName);
                    if (levelNumber.HasValue)
                    {
                        Debug.Log($"[DialogueManager] 从场景 {sceneName} 获取到关卡编号：{levelNumber.Value}");
                        return levelNumber.Value;
                    }
                    else
                    {
                        Debug.LogWarning($"[DialogueManager] 场景 {sceneName} 未在映射表中找到对应的关卡编号");
                    }
                }
            }
        }
        
        return -1; // 无法确定
    }
    
    /// <summary>
    /// 根据场景名查找对应的关卡编号
    /// </summary>
    private int? FindLevelNumberBySceneName(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            return null;
        }
        
        // 遍历映射表查找匹配的场景
        foreach (var mapping in sceneMappings)
        {
            if (mapping != null && mapping.Matches(sceneName))
            {
                return mapping.levelNumber;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// 获取指定关卡的 WaveSpawn 触发次数
    /// </summary>
    public int GetWaveSpawnTriggerCount(int levelNumber)
    {
        return _waveSpawnTriggerCounts.TryGetValue(levelNumber, out int count) ? count : 0;
    }
    
    /// <summary>
    /// 重置指定关卡的 WaveSpawn 触发次数
    /// </summary>
    public void ResetWaveSpawnTriggerCount(int levelNumber)
    {
        _waveSpawnTriggerCounts[levelNumber] = 0;
        Debug.Log($"[DialogueManager] 关卡 {levelNumber} 的 WaveSpawn 触发次数已重置");
    }
    
    /// <summary>
    /// 重置所有关卡的 WaveSpawn 触发次数
    /// </summary>
    public void ResetAllWaveSpawnTriggerCounts()
    {
        _waveSpawnTriggerCounts.Clear();
        Debug.Log("[DialogueManager] 所有关卡的 WaveSpawn 触发次数已重置");
    }

	public void PlaySequence(DialogueSequence sequence, System.Action onComplete = null, bool isForced = false)
	{
		if (sequence == null || sequence.entries == null || sequence.entries.Length == 0)
		{
			onComplete?.Invoke();
			return;
		}

		if (isDialogueActive)
		{
			Debug.LogWarning("[DialogueManager] 对话正在播放中，忽略PlaySequence调用");
			return;
		}

		this.isForced = isForced;
		StartCoroutine(ShowDialogueSequence(sequence, onComplete));
	}
}
