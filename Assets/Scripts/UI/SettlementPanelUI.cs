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

    [Header("问题配置")]
    [SerializeField] private List<SettlementQuestionConfig> questions = new List<SettlementQuestionConfig>();

    private readonly List<SettlementQuestionRowUI> _rows = new List<SettlementQuestionRowUI>();

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
            return;
        }

        Debug.Log($"[SettlementPanelUI] 提交成功：全部正确。题目数={result.Count}");
        OnSubmitted?.Invoke(result);
    }
}
