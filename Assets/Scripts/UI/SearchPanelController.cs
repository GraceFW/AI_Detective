using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 搜索面板控制器
/// 处理 /detect、/sniff 和 /attack 指令
/// </summary>
public class SearchPanelController : MonoBehaviour
{
    [Header("UI 组件")]
    [Tooltip("命令下拉框 (TMP_Dropdown)")]
    [SerializeField] private TMP_Dropdown commandDropdown;

    [Tooltip("搜索输入框 (TMP_InputField)")]
    [SerializeField] private TMP_InputField searchInput;

    [Tooltip("结果显示文本 (TextMeshProUGUI)")]
    [SerializeField] private TextMeshProUGUI resultText;

    [Tooltip("滚动视图 (ScrollRect) - 用于自动滚动到底部")]
    [SerializeField] private ScrollRect scrollRect;

    [Header("数据")]
    [Tooltip("线索数据库")]
    [SerializeField] private ClueDatabaseSO clueDatabase;

    [Tooltip("\"执行中...\" 显示时长")]
    [SerializeField] private float executingDuration = 0.8f;

    [Tooltip("滚动到底部的延迟时间（秒），用于等待文本更新")]
    [SerializeField] private float scrollDelay = 0.05f;

    private Coroutine _displayCoroutine;
    private StringBuilder _historyLog = new StringBuilder();
    private string _lastDisplayedText = string.Empty;  // 记录上次显示的文本，用于追加模式
    private bool _shouldClearOnNextUpdate = false;  // 标记下次更新是否应该清空（Detect 命令）
    private int _sniffUsageCount = 0;  // Sniff使用次数统计（场景级别，不存档）

    private enum CommandType
    {
        Detect = 0,
        Sniff = 1,
        Attack = 2
    }

    private void Start()
    {
        InitializeDropdown();

        if (searchInput != null)
        {
            searchInput.onSubmit.AddListener(OnSubmit);
        }

        if (resultText != null)
        {
            // 检查是否有打字机效果组件
            TypewriterEffect typewriterEffect = resultText.GetComponent<TypewriterEffect>();
            if (typewriterEffect != null)
            {
                typewriterEffect.Clear();
            }
            else
            {
                resultText.text = string.Empty;
            }
            _lastDisplayedText = string.Empty;
        }

        // 如果没有手动指定 ScrollRect，尝试自动查找
        if (scrollRect == null)
        {
            scrollRect = GetComponentInParent<ScrollRect>();
            if (scrollRect == null && resultText != null)
            {
                scrollRect = resultText.GetComponentInParent<ScrollRect>();
            }
        }
    }

    private void OnDestroy()
    {
        if (searchInput != null)
        {
            searchInput.onSubmit.RemoveListener(OnSubmit);
        }
    }

    private void InitializeDropdown()
    {
        if (commandDropdown == null)
        {
            return;
        }

        commandDropdown.ClearOptions();
        commandDropdown.AddOptions(new System.Collections.Generic.List<string>
        {
            "/Detect",
            "/Sniff",
            "/Attack"
        });
        commandDropdown.value = 0;
        commandDropdown.RefreshShownValue();
    }

    /// <summary>
    /// 当按下回车时调用
    /// </summary>
    private void OnSubmit(string inputText)
    {
        if (commandDropdown == null || clueDatabase == null)
        {
            Debug.LogError("SearchPanelController: commandDropdown 或 clueDatabase 未配置。");
            return;
        }

        var command = (CommandType)commandDropdown.value;
        var searchText = inputText?.Trim() ?? string.Empty;

        ExecuteCommand(command, searchText);

        // 清空输入框
        if (searchInput != null)
        {
            searchInput.text = string.Empty;
        }
    }

    /// <summary>
    /// 执行命令
    /// </summary>
    private void ExecuteCommand(CommandType command, string searchText)
    {
        if (_displayCoroutine != null)
        {
            StopCoroutine(_displayCoroutine);
        }

        _displayCoroutine = StartCoroutine(ExecuteCommandCoroutine(command, searchText));
    }

    private IEnumerator ExecuteCommandCoroutine(CommandType command, string searchText)
    {
        // 只有 Detect 指令时清空文本窗当前显示的内容
        if (command == CommandType.Detect)
        {
            _historyLog.Clear();
            _lastDisplayedText = string.Empty;
            _shouldClearOnNextUpdate = true;
            UpdateResultText();
        }

        string commandStr = command switch
        {
            CommandType.Detect => "/detect",
            CommandType.Sniff => "/sniff",
            CommandType.Attack => "/attack",
            _ => "/detect"
        };
        var inputLine = $"> {commandStr} {searchText}\n";

        // 添加输入行到历史
        _historyLog.Append(inputLine);

        // 显示 "执行中..."
        var executingLine = "执行中...\n";
        _historyLog.Append(executingLine);
        UpdateResultText();
        // 不再手动调用 ScrollToBottom，打字机效果会自己处理滚动

        yield return new WaitForSeconds(executingDuration);

        // 执行命令逻辑
        string resultLine;

        if (string.IsNullOrEmpty(searchText))
        {
            // 没有输入文本
            resultLine = "[结果]：未获得数据探针。\n\n";
        }
        else
        {
            var clue = clueDatabase.SearchByDisplayName(searchText);
            
            resultLine = command switch
            {
                CommandType.Detect => ExecuteDetect(clue),
                CommandType.Sniff => ExecuteSniff(clue),
                CommandType.Attack => ExecuteAttack(clue),
                _ => "[结果]：未知命令。\n\n"
            };
        }

        _historyLog.Append(resultLine);
        UpdateResultText();
        // 不再手动调用 ScrollToBottom，打字机效果会自己处理滚动

        _displayCoroutine = null;
    }

    /// <summary>
    /// 执行 /detect 命令
    /// </summary>
    private string ExecuteDetect(ClueData clue)
    {
        if (clue == null)
        {
            return "[结果]：检定为低关联性信息。\n\n";
        }

        // 检查是否已收集（使用 clue.collected 字段）
        bool isRevealed = clue.collected;
        Debug.Log(clue.displayName + "线索收集状态：" + isRevealed);
        Debug.Log(clue.id + "线索ID：" + clue.id);
        Debug.Log(clue.searchable + "线索可搜索：" + clue.searchable);
        Debug.Log(clue.collectable + "线索可收集：" + clue.collectable);
        if (isRevealed && ClueManager.instance.IsRevealed(clue.id))
        {
            // 已被收集的线索
            if (clue.searchable)
            {
                // 如果searchable，显示detailText（不再收集）
                var detail = string.IsNullOrWhiteSpace(clue.detailText) ? clue.summary : clue.detailText;
                return $"[结果]：\n{detail}\n\n";
            }
            else
            {
                return "[结果]：该线索已存在于档案中。\n\n";
            }
        }
        else
        {
            // 未收集的线索
            bool shouldShowDetail = clue.searchable;
            bool shouldCollect = clue.collectable;
            
            if (shouldShowDetail && shouldCollect)
            {
                // searchable且collectable：显示detailText + 收集 + 提示文本
                var detail = string.IsNullOrWhiteSpace(clue.detailText) ? clue.summary : clue.detailText;
                
                if (ClueManager.instance != null)
                {
                    Debug.Log("RevealClue: " + clue.id);
                    ClueManager.instance.RevealClue(clue.id);
                }
                else
                {
                    Debug.LogWarning("SearchPanelController: ClueManager.instance 为空。");
                }
                
                return $"[结果]：\n{detail}\n[结果]：采集到关联线索。\n\n";
            }
            else if (shouldShowDetail && !shouldCollect)
            {
                // 只searchable不collectable：显示detailText（不收集）
                var detail = string.IsNullOrWhiteSpace(clue.detailText) ? clue.summary : clue.detailText;
                return $"[结果]：\n{detail}\n\n";
            }
            else if (!shouldShowDetail && shouldCollect)
            {
                // collectable但不searchable：收集 + 提示文本（不显示detailText）
                if (ClueManager.instance != null)
                {
                    ClueManager.instance.RevealClue(clue.id);
                }
                else
                {
                    Debug.LogWarning("SearchPanelController: ClueManager.instance 为空。");
                }
                
                return "[结果]：采集到关联线索。\n\n";
            }
            else
            {
                // 两个都不满足：既不显示文字也不收集
                return "[结果]：检定为低关联性信息。\n\n";
            }
        }
    }

    /// <summary>
    /// 执行 /sniff 命令
    /// </summary>
    private string ExecuteSniff(ClueData clue)
    {
        if (clue == null)
        {
            return "[结果]：未获得数据探针。\n\n";
        }

        // 只对searchable的线索有效
        if (!clue.searchable)
        {
            return "[结果]：未关联到高置信度结果。\n\n";
        }

        // 增加Sniff使用次数统计
        _sniffUsageCount++;

        // 检查是否有Detail_Mark
        if (!string.IsNullOrWhiteSpace(clue.Detail_Mark))
        {
            // 有Detail_Mark：显示Detail_Mark
            return $"[结果]：\n{clue.Detail_Mark}\n\n";
        }
        else
        {
            // 没有Detail_Mark：输出提示文本
            return "[结果]：该线索暂无标记文本。\n\n";
        }
    }

    /// <summary>
    /// 执行 /attack 命令
    /// </summary>
    private string ExecuteAttack(ClueData clue)
    {
        return "[结果]：此版本暂时不支持该功能。\n\n";
    }

    /// <summary>
    /// 获取Sniff使用次数（场景级别，不存档）
    /// </summary>
    public int GetSniffUsageCount()
    {
        return _sniffUsageCount;
    }

    private void UpdateResultText()
    {
        if (resultText != null)
        {
            string newText = _historyLog.ToString();
            
            // 检查是否有打字机效果组件
            TypewriterEffect typewriterEffect = resultText.GetComponent<TypewriterEffect>();
            if (typewriterEffect != null)
            {
                // 使用打字机效果
                if (_shouldClearOnNextUpdate || string.IsNullOrEmpty(_lastDisplayedText))
                {
                    // 清空或首次显示：使用 SetText
                    typewriterEffect.SetText(newText);
                    _shouldClearOnNextUpdate = false;
                }
                else
                {
                    // 追加模式：计算新增文本
                    if (newText.Length > _lastDisplayedText.Length && newText.StartsWith(_lastDisplayedText))
                    {
                        string addedText = newText.Substring(_lastDisplayedText.Length);
                        typewriterEffect.AppendText(addedText);
                    }
                    else
                    {
                        // 如果文本不匹配（可能是被外部修改），使用 SetText
                        typewriterEffect.SetText(newText);
                    }
                }
            }
            else
            {
                // 没有打字机效果：直接设置文本（兼容旧代码）
                resultText.text = newText;
                resultText.ForceMeshUpdate();
            }
            
            // 更新最后显示的文本
            _lastDisplayedText = newText;
        }
    }

    /// <summary>
    /// 滚动到底部，显示最新内容
    /// </summary>
    private void ScrollToBottom()
    {
        if (scrollRect == null)
        {
            return;
        }

        // 使用协程延迟滚动，确保文本已更新
        StartCoroutine(ScrollToBottomCoroutine());
    }

    private IEnumerator ScrollToBottomCoroutine()
    {
        // 等待一帧，确保 TextMeshPro 已更新 preferredHeight
        yield return null;

        // 再等待一小段时间，确保布局更新完成
        if (scrollDelay > 0f)
        {
            yield return new WaitForSeconds(scrollDelay);
        }

        // 强制更新 Canvas
        Canvas.ForceUpdateCanvases();

        // 滚动到底部（verticalNormalizedPosition = 0 表示底部）
        if (scrollRect != null)
        {
            scrollRect.verticalNormalizedPosition = 0f;
        }
    }

    /// <summary>
    /// 清空历史记录
    /// </summary>
    public void ClearHistory()
    {
        _historyLog.Clear();
        _lastDisplayedText = string.Empty;
        _shouldClearOnNextUpdate = true;
        
        // 如果有打字机效果，使用 Clear 方法
        if (resultText != null)
        {
            TypewriterEffect typewriterEffect = resultText.GetComponent<TypewriterEffect>();
            if (typewriterEffect != null)
            {
                typewriterEffect.Clear();
            }
            else
            {
                resultText.text = string.Empty;
            }
        }
        
        ScrollToBottom();
    }

    /// <summary>
    /// 外部调用：设置搜索输入框的文本（用于拖拽功能）
    /// </summary>
    public void SetSearchText(string text)
    {
        if (searchInput != null)
        {
            searchInput.text = text;
            searchInput.ActivateInputField();
        }
    }

    /// <summary>
    /// 获取搜索输入框的 RectTransform（用于拖拽检测）
    /// </summary>
    public RectTransform GetSearchInputRect()
    {
        return searchInput != null ? searchInput.GetComponent<RectTransform>() : null;
    }
}

