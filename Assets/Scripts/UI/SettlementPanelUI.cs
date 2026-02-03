using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 结算界面总面板
/// - 负责生成可下拉的问题列表（配合 ScrollRect）
/// - 每个问题行包含：问题文本 + 若干线索填充框
/// - 点击提交按钮后，校验是否填满，并输出结果（你可以在这里接入评分/结算逻辑）
/// </summary>
public class SettlementPanelUI : MonoBehaviour
{
    [Serializable]
    public class SettlementQuestionConfig
    {
        [Tooltip("问题文本")]
        public string question;

        [Tooltip("该问题的正确线索")]
        public ClueData correctClue;
    }

    [Serializable]
    public class SettlementAnswerResult
    {
        public string question;
        public ClueData selectedClue;
        public ClueData correctClue;
        public bool isCorrect;
    }

    [Header("Scroll 内容")]
    [Tooltip("ScrollRect/Viewport/Content，挂 VerticalLayoutGroup 的那个 Content")]
    [SerializeField] private Transform contentRoot;

    [Tooltip("如果 contentRoot 没配对，可在此绑定 ScrollRect，脚本会自动取其 content")]
    [SerializeField] private ScrollRect scrollRect;

    [Tooltip("问题行预制体")]
    [SerializeField] private SettlementQuestionRowUI rowPrefab;

    [Header("提交")]
    [SerializeField] private Button submitButton;

    [Header("弹窗预制体")]
    [Tooltip("结算成功面板预制体")]
    [SerializeField] private SettlementSuccessPanel successPanelPrefab;

    [Tooltip("错误提示窗口预制体")]
    [SerializeField] private SettlementErrorDialog errorDialogPrefab;

    [Tooltip("弹窗父对象（通常是 Canvas 或专门的弹窗容器）")]
    [SerializeField] private Transform popupParent;

    [Header("问题配置")]
    [SerializeField] private List<SettlementQuestionConfig> questions = new List<SettlementQuestionConfig>();

    [Header("对话配置")]
    [Tooltip("当前关卡编号（用于触发LevelComplete对话）")]
    [SerializeField] private int currentLevelNumber = 0;

    private readonly List<SettlementQuestionRowUI> _rows = new List<SettlementQuestionRowUI>();
    private SettlementSuccessPanel _currentSuccessPanel;
    private SettlementErrorDialog _currentErrorDialog;
    private List<SettlementAnswerResult> _pendingSuccessResults;

    /// <summary>
    /// 当提交成功（全部填满）时回调
    /// </summary>
    public event Action<List<SettlementAnswerResult>> OnSubmitted;

    private void Awake()
    {
        if (submitButton != null)
        {
            submitButton.onClick.AddListener(HandleSubmitClicked);
        }
    }

    private void Start()
    {
        Rebuild();
    }

    private void OnDestroy()
    {
        if (submitButton != null)
        {
            submitButton.onClick.RemoveListener(HandleSubmitClicked);
        }

        // 清理事件监听
        if (_currentSuccessPanel != null)
        {
            _currentSuccessPanel.OnNextLevelClicked -= HandleNextLevelClicked;
        }
    }

    /// <summary>
    /// 重新生成问题列表
    /// </summary>
    public void Rebuild()
    {
        if ((contentRoot == null || contentRoot.GetComponent<ScrollRect>() != null) && scrollRect == null)
        {
            scrollRect = GetComponentInChildren<ScrollRect>();
        }

        if ((contentRoot == null || contentRoot.GetComponent<ScrollRect>() != null) && scrollRect != null)
        {
            contentRoot = scrollRect.content;
        }

        for (int i = 0; i < _rows.Count; i++)
        {
            if (_rows[i] != null)
            {
                Destroy(_rows[i].gameObject);
            }
        }
        _rows.Clear();

        if (contentRoot == null || rowPrefab == null)
        {
            Debug.LogWarning("SettlementPanelUI: contentRoot 或 rowPrefab 未配置。");
            return;
        }

        foreach (var q in questions)
        {
            if (q == null)
            {
                continue;
            }

            var row = Instantiate(rowPrefab, contentRoot);
            row.Setup(q.question, q.correctClue);
            _rows.Add(row);
        }
    }

    private void HandleSubmitClicked()
    {
        // 1) 校验是否全部填满
        foreach (var row in _rows)
        {
            if (row == null)
            {
                continue;
            }

            if (!row.IsComplete())
            {
                Debug.Log("[SettlementPanelUI] 提交失败：还有问题未填满线索。");
                ShowErrorDialog("请完成所有问题", SettlementErrorDialog.ErrorType.Incomplete);
                return;
            }
        }

        // 2) 判题并汇总结果（你可以在这里接入评分/结算/跳转）
        var result = new List<SettlementAnswerResult>();
        var correctCount = 0;
        var allCorrect = true;
        foreach (var row in _rows)
        {
            if (row == null)
            {
                continue;
            }

            var selected = row.GetFilledClue();
            var isCorrect = row.IsCorrect();
            if (isCorrect)
            {
                correctCount++;
            }
            else
            {
                allCorrect = false;
            }

            result.Add(new SettlementAnswerResult
            {
                question = row.Question,
                selectedClue = selected,
                correctClue = row.CorrectClue,
                isCorrect = isCorrect
            });
        }

        if (!allCorrect)
        {
            Debug.Log($"[SettlementPanelUI] 提交失败：存在错误答案。题目数={result.Count}，正确数={correctCount}");
            ShowErrorDialog($"存在错误答案", SettlementErrorDialog.ErrorType.Incorrect);
            return;
        }

        Debug.Log($"[SettlementPanelUI] 提交成功：全部正确。题目数={result.Count}");
        OnSubmitted?.Invoke(result);
        
        // 保存结算结果，触发LevelComplete对话
        _pendingSuccessResults = result;
        TriggerLevelCompleteDialogue();
    }

    /// <summary>
    /// 显示成功面板
    /// </summary>
    private void ShowSuccessPanel(List<SettlementAnswerResult> results)
    {
        if (successPanelPrefab == null)
        {
            Debug.LogWarning("[SettlementPanelUI] successPanelPrefab 未配置，无法显示成功面板");
            return;
        }

        // 如果已有成功面板，先销毁
        if (_currentSuccessPanel != null)
        {
            Destroy(_currentSuccessPanel.gameObject);
        }

        if (popupParent == null)
        {
            // 尝试查找 Persistent 场景中的 Canvas
            Canvas persistentCanvas = GameObject.Find("PopupCanvas")?.GetComponent<Canvas>();
            if (persistentCanvas != null)
            {
                popupParent = persistentCanvas.transform;
            }
        }

        // 确定父对象
        Transform parent = popupParent != null ? popupParent : transform;

        // 实例化成功面板
        _currentSuccessPanel = Instantiate(successPanelPrefab, parent);
        
        // 监听下一关按钮事件
        _currentSuccessPanel.OnNextLevelClicked += HandleNextLevelClicked;

        // 显示面板
        _currentSuccessPanel.Show(results);
    }

    /// <summary>
    /// 显示错误对话框
    /// </summary>
    private void ShowErrorDialog(string message, SettlementErrorDialog.ErrorType type)
    {
        if (errorDialogPrefab == null)
        {
            Debug.LogWarning("[SettlementPanelUI] errorDialogPrefab 未配置，无法显示错误对话框");
            return;
        }

        // 如果已有错误对话框，先销毁
        if (_currentErrorDialog != null)
        {
            Destroy(_currentErrorDialog.gameObject);
        }

        if (popupParent == null)
        {
            // 尝试查找 Persistent 场景中的 Canvas
            Canvas persistentCanvas = GameObject.Find("PopupCanvas")?.GetComponent<Canvas>();
            if (persistentCanvas != null)
            {
                popupParent = persistentCanvas.transform;
            }
        }

        // 确定父对象
        Transform parent = popupParent != null ? popupParent : transform;

        // 实例化错误对话框
        _currentErrorDialog = Instantiate(errorDialogPrefab, parent);

        // 显示对话框
        _currentErrorDialog.Show(message, type);
    }

    /// <summary>
    /// 处理下一关按钮点击
    /// </summary>
    private void HandleNextLevelClicked()
    {
        Debug.Log("[SettlementPanelUI] 下一关按钮被点击");
        // 这里可以添加跳转到下一关的逻辑
        // 例如：SceneManager.LoadScene("NextLevel");
    }

    /// <summary>
    /// 触发LevelComplete对话
    /// </summary>
    private void TriggerLevelCompleteDialogue()
    {
        if (DialogueManager.Instance == null)
        {
            Debug.LogWarning("[SettlementPanelUI] DialogueManager未找到，直接显示成功面板");
            ShowSuccessPanel(_pendingSuccessResults);
            return;
        }

        // 触发LevelComplete对话，传入完成回调
        DialogueManager.Instance.ShowDialogue(
            levelNumber: currentLevelNumber,
            triggerType: DialogueTriggerType.LevelComplete,
            waveNumber: 0,
            onComplete: OnLevelCompleteDialogueFinished,
            isForced: true
        );
    }

    /// <summary>
    /// LevelComplete对话结束回调
    /// </summary>
    private void OnLevelCompleteDialogueFinished()
    {
        // 对话结束后显示成功面板
        if (_pendingSuccessResults != null)
        {
            ShowSuccessPanel(_pendingSuccessResults);
            _pendingSuccessResults = null;
        }
    }
}
