using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 对话UI管理器
/// 负责更新UI显示和处理UI事件
/// </summary>
public class DialogueUI : MonoBehaviour
{
    [Header("UI组件")]
    [Tooltip("name对象 - 显示人物名字")]
    [SerializeField] private TextMeshProUGUI nameText;

    [Tooltip("person对象 - 显示人物头像")]
    [SerializeField] private Image portraitImage;

    [Tooltip("penlu对象 - 显示对话文本")]
    [SerializeField] private TextMeshProUGUI dialogueText;

    [Tooltip("choicecontainer - 选项容器")]
    [SerializeField] private Transform choiceContainer;

    [Tooltip("选项按钮预制体")]
    [SerializeField] private GameObject choiceButtonPrefab;

    [Header("导航按钮（可选）")]
    [Tooltip("上一条按钮")]
    [SerializeField] private Button prevButton;

    [Tooltip("下一条按钮")]
    [SerializeField] private Button nextButton;

    [Header("交互")]
    [Tooltip("对话框点击区域")]
    [SerializeField] private Button dialogueBoxButton;

    private DialogueController _dialogueController;
    private List<GameObject> _currentChoiceButtons = new List<GameObject>();

    private void Awake()
    {
        _dialogueController = GetComponent<DialogueController>();

        // 绑定按钮事件
        if (dialogueBoxButton != null)
        {
            dialogueBoxButton.onClick.AddListener(OnDialogueBoxClick);
        }

        if (prevButton != null)
        {
            prevButton.onClick.AddListener(OnPrevButtonClick);
        }

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(OnNextButtonClick);
        }

        // 初始化UI状态
        ClearDialogue();
    }

    private void OnDestroy()
    {
        if (dialogueBoxButton != null)
        {
            dialogueBoxButton.onClick.RemoveListener(OnDialogueBoxClick);
        }

        if (prevButton != null)
        {
            prevButton.onClick.RemoveListener(OnPrevButtonClick);
        }

        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(OnNextButtonClick);
        }
    }

    /// <summary>
    /// 显示人物信息（名字和头像）
    /// </summary>
    public void ShowPerson(string personName, Sprite portrait)
    {
        if (nameText != null)
        {
            nameText.text = personName;
        }

        if (portraitImage != null && portrait != null)
        {
            portraitImage.sprite = portrait;
            portraitImage.enabled = true;
        }

        Debug.Log($"[DialogueUI] 显示人物: {personName}");
    }

    /// <summary>
    /// 显示对话文本
    /// </summary>
    public void ShowDialogue(string text, bool hasOptions)
    {
        if (dialogueText != null)
        {
            dialogueText.text = text;
        }

        Debug.Log($"[DialogueUI] 显示对话: {text.Substring(0, Mathf.Min(20, text.Length))}...");
    }

    /// <summary>
    /// 显示选项
    /// </summary>
    /// <param name="options">选项列表</param>
    /// <param name="isHistoryView">是否在浏览历史（历史中的选项不可点击）</param>
    public void ShowOptions(List<DialogueOption> options, bool isHistoryView = false)
    {
        // 清空现有选项
        ClearOptions();

        if (options == null || options.Count == 0 || choiceContainer == null || choiceButtonPrefab == null)
        {
            return;
        }

        // 创建选项按钮
        for (int i = 0; i < options.Count; i++)
        {
            var option = options[i];
            var buttonObj = Instantiate(choiceButtonPrefab, choiceContainer);
            _currentChoiceButtons.Add(buttonObj);

            // 设置按钮文本
            var buttonText = buttonObj.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = option.optionText;
            }

            // 绑定点击事件
            var button = buttonObj.GetComponent<Button>();
            if (button != null)
            {
                // 如果在浏览历史，禁用按钮
                button.interactable = !isHistoryView;

                if (!isHistoryView)
                {
                    int optionIndex = i;  // 捕获索引
                    button.onClick.AddListener(() => OnOptionClick(optionIndex));
                }
            }

            buttonObj.SetActive(true);
        }

        Debug.Log($"[DialogueUI] 显示 {options.Count} 个选项 (历史浏览: {isHistoryView})");
    }

    /// <summary>
    /// 清空选项
    /// </summary>
    public void ClearOptions()
    {
        foreach (var button in _currentChoiceButtons)
        {
            if (button != null)
            {
                Destroy(button);
            }
        }
        _currentChoiceButtons.Clear();
    }

    /// <summary>
    /// 更新导航按钮状态
    /// </summary>
    public void UpdateNavigationButtons(bool canPrev, bool canNext)
    {
        if (prevButton != null)
        {
            prevButton.interactable = canPrev;
        }

        if (nextButton != null)
        {
            nextButton.interactable = canNext;
        }
    }

    /// <summary>
    /// 清空所有对话显示
    /// </summary>
    public void ClearDialogue()
    {
        if (nameText != null)
        {
            nameText.text = "";
        }

        if (portraitImage != null)
        {
            portraitImage.enabled = false;
        }

        if (dialogueText != null)
        {
            dialogueText.text = "";
        }

        ClearOptions();
        UpdateNavigationButtons(false, false);
    }

    /// <summary>
    /// 对话框点击事件
    /// </summary>
    private void OnDialogueBoxClick()
    {
        if (_dialogueController != null)
        {
            _dialogueController.NextDialogue();
        }
    }

    /// <summary>
    /// 选项点击事件
    /// </summary>
    private void OnOptionClick(int optionIndex)
    {
        if (_dialogueController != null)
        {
            _dialogueController.SelectOption(optionIndex);
        }
    }

    /// <summary>
    /// 上一条按钮点击事件
    /// </summary>
    private void OnPrevButtonClick()
    {
        if (_dialogueController != null)
        {
            _dialogueController.NavigateHistory(-1);
        }
    }

    /// <summary>
    /// 下一条按钮点击事件
    /// </summary>
    private void OnNextButtonClick()
    {
        if (_dialogueController != null)
        {
            _dialogueController.NavigateHistory(1);
        }
    }
}

