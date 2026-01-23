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
    
    [Tooltip("继续指示器")]
    public GameObject continueIndicator;
    
    [Tooltip("跳过按钮（可选）")]
    public Button skipButton;
    
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
    
    /// <summary>
    /// 按关卡记录 WaveSpawn 对话的触发次数
    /// Key: 关卡编号, Value: 触发次数（从1开始）
    /// </summary>
    private Dictionary<int, int> _waveSpawnTriggerCounts = new Dictionary<int, int>();
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            
            // // DontDestroyOnLoad只能用于根GameObject
            // // 如果当前GameObject不是根对象，需要处理父对象
            // if (transform.parent != null)
            // {
            //     // 将父对象设为DontDestroyOnLoad（如果父对象是根对象）
            //     DontDestroyOnLoad(transform.root.gameObject);
            // }
            // else
            // {
            //     // 当前GameObject就是根对象，直接使用
            //     DontDestroyOnLoad(gameObject);
            // }
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
    }
    
    private void Start()
    {
        // 配置跳过按钮
        if (skipButton != null)
        {
            // 清除所有原有的监听器
            skipButton.onClick.RemoveAllListeners();
            // 注册新的退出对话回调
            skipButton.onClick.AddListener(ExitDialogue);
            Debug.Log("[DialogueManager] SkipButton已配置，点击将直接退出对话");
        }
        else
        {
            Debug.LogWarning("[DialogueManager] SkipButton未绑定，请在Inspector中绑定跳过按钮");
        }
    }
    
    private void Update()
    {
        // 对话激活时处理输入
        if (isDialogueActive)
        {
            // 点击或空格键继续
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Space))
            {
                OnContinueDialogue();
            }
            
            // ESC键退出对话（无需allowSkip限制）
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                ExitDialogue();
            }
        }
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
        
        // 隐藏对话面板
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // 触发对话结束事件
        if (dialogueEndEvent != null)
        {
            dialogueEndEvent.RaiseEvent(currentLevelNumber, currentTriggerType);
        }
        
        // 触发完成回调
        onDialogueComplete?.Invoke();
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
                // 自定义动作节点（预留）
                Debug.LogWarning("[DialogueManager] CustomAction节点类型尚未实现");
                break;
        }
    }
    
    /// <summary>
    /// 显示普通对话条目
    /// </summary>
    private void ShowNormalDialogueEntry(DialogueEntry entry)
    {
        // 设置说话人头像
        if (speakerImage != null)
        {
            if (entry.speakerImage != null)
            {
                speakerImage.sprite = entry.speakerImage;
                speakerImage.gameObject.SetActive(true);
            }
            else
            {
                speakerImage.gameObject.SetActive(false);
            }
        }
        
        // 设置说话人名称
        if (speakerNameText != null)
        {
            speakerNameText.text = entry.speakerName;
        }
        
        // 设置文本框背景（根据角色配置不同的文本框样式）
        if (dialogueTextBG != null)
        {
            if (entry.textBoxBackground != null)
            {
                dialogueTextBG.sprite = entry.textBoxBackground;
                dialogueTextBG.gameObject.SetActive(true);
            }
            else
            {
                // 如果没有配置，使用默认背景（保持当前设置）
                // dialogueTextBG.gameObject.SetActive(true); // 保持显示
            }
        }
        
        // 隐藏继续指示器
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(false);
        }
        
        // 显示对话文本（使用打字机效果）
        if (dialogueText != null)
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
                dialogueText.text = entry.dialogueText;
                isTyping = false;
                ShowContinueIndicator();
            }
        }
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
    
    /// <summary>
    /// 打字机效果协程
    /// </summary>
    private IEnumerator TypewriterEffect(string text, float speed)
    {
        isTyping = true;
        dialogueText.text = "";
        
        float interval = 1f / speed;
        
        foreach (char c in text)
        {
            dialogueText.text += c;
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
        if (continueIndicator != null)
        {
            continueIndicator.SetActive(true);
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
                dialogueText.text = currentSequence.entries[currentEntryIndex].dialogueText;
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
        ExitDialogue();
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
        
        Debug.Log("[DialogueManager] 退出对话");
        
        // 停止打字机效果
        if (typewriterCoroutine != null)
        {
            StopCoroutine(typewriterCoroutine);
            typewriterCoroutine = null;
        }
        
        // 如果正在显示起名弹窗，先关闭它
        if (isWaitingForSpecialNode && NameInputDialog.Instance != null)
        {
            NameInputDialog.Instance.Hide();
        }
        
        // 重置状态
        isTyping = false;
        isDialogueActive = false;
        isWaitingForSpecialNode = false;
        currentSequence = null;
        currentEntryIndex = 0;
        isForced = false;
        
        // 隐藏对话面板
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(false);
        }
        
        // // 播放音效
        // if (AudioManager.Instance != null)
        // {
        //     AudioManager.Instance.PlaySFX("DialogueClose");
        // }
        
        // 触发对话结束事件（即使未完成所有对话）
        if (dialogueEndEvent != null)
        {
            dialogueEndEvent.RaiseEvent(currentLevelNumber, currentTriggerType);
        }
        
        // 触发完成回调（即使未完成所有对话，也触发回调以继续游戏流程）
        onDialogueComplete?.Invoke();
        onDialogueComplete = null;
    }
    
    /// <summary>
    /// 结束对话（内部方法，正常完成对话时调用）
    /// </summary>
    private void EndDialogue()
    {
        isDialogueActive = false;
        isWaitingForSpecialNode = false;
        currentSequence = null;
        currentEntryIndex = 0;
        isForced = false;
        
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
}


