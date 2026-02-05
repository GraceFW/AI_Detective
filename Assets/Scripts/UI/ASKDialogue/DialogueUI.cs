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

    [Header("对话关键词点击收集（仅对话启用）")]
    [Tooltip("关键词数据库：在对话文本中把关键词替换成可点击链接")]
    [SerializeField] private CaseKeywordDatabase keywordDatabase;

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

    private Dictionary<string, string> _clickableTerms;

    private void Awake()
    {
        _dialogueController = GetComponent<DialogueController>();

        // 绑定点击事件：优先使用对话框点击区域；如果没有，则直接挂在对话文本上
        DialogueBoxClickHandler clickHandler = null;
        if (dialogueBoxButton != null)
        {
            clickHandler = dialogueBoxButton.GetComponent<DialogueBoxClickHandler>();
            if (clickHandler == null)
            {
                clickHandler = dialogueBoxButton.gameObject.AddComponent<DialogueBoxClickHandler>();
            }
        }
        else if (dialogueText != null)
        {
            clickHandler = dialogueText.GetComponent<DialogueBoxClickHandler>();
            if (clickHandler == null)
            {
                clickHandler = dialogueText.gameObject.AddComponent<DialogueBoxClickHandler>();
            }
        }

        if (clickHandler != null)
        {
            clickHandler.Init(dialogueText, _dialogueController);
        }

        if (prevButton != null)
        {
            // [SFX] 为对话翻页按钮添加音效组件
            if (prevButton.GetComponent<PlaySfxOnClick>() == null)
            {
                prevButton.gameObject.AddComponent<PlaySfxOnClick>();
            }
            prevButton.onClick.AddListener(OnPrevButtonClick);
        }

        if (nextButton != null)
        {
            // [SFX] 为对话翻页按钮添加音效组件
            if (nextButton.GetComponent<PlaySfxOnClick>() == null)
            {
                nextButton.gameObject.AddComponent<PlaySfxOnClick>();
            }
            nextButton.onClick.AddListener(OnNextButtonClick);
        }

        // 初始化UI状态
        ClearDialogue();

        _clickableTerms = BuildClickableTerms();
    }

    private void OnDestroy()
    {
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
    /// <param name="text">对话文本</param>
    /// <param name="hasOptions">是否有选项</param>
    /// <param name="useTypewriter">是否使用打字机效果（默认true，历史回溯时为false）</param>
    public void ShowDialogue(string text, bool hasOptions, bool useTypewriter = true)
    {
        if (dialogueText != null)
        {
            string processedText = InjectLinks(text);
            
            // 检查是否有打字机效果组件
            TypewriterEffect typewriterEffect = dialogueText.GetComponent<TypewriterEffect>();
            if (typewriterEffect != null && useTypewriter)
            {
                // 使用打字机效果显示文本
                typewriterEffect.SetText(processedText);
            }
            else
            {
                // 直接设置文本（兼容旧代码或历史回溯时）
                // 如果禁用了打字机效果但组件存在，需要停止打字机效果并直接显示全部文本
                if (typewriterEffect != null && !useTypewriter)
                {
                    // 停止任何正在进行的打字机效果
                    dialogueText.text = processedText;
                    dialogueText.maxVisibleCharacters = processedText.Length;  // 确保显示全部文本
                    dialogueText.ForceMeshUpdate();
                }
                else
                {
                    // 没有打字机效果组件，直接设置文本
                    dialogueText.text = processedText;
                }
            }
        }

        Debug.Log($"[DialogueUI] 显示对话: {text.Substring(0, Mathf.Min(20, text.Length))}... (打字机效果: {useTypewriter})");
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
                    // [SFX] 为对话选项按钮添加音效组件
                    var playSfx = buttonObj.GetComponent<PlaySfxOnClick>();
                    if (playSfx == null)
                    {
                        playSfx = buttonObj.AddComponent<PlaySfxOnClick>();
                    }
                    
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
            // 检查是否有打字机效果组件
            TypewriterEffect typewriterEffect = dialogueText.GetComponent<TypewriterEffect>();
            if (typewriterEffect != null)
            {
                typewriterEffect.Clear();
            }
            else
            {
                dialogueText.text = "";
            }
        }

        ClearOptions();
        UpdateNavigationButtons(false, false);
    }

    /// <summary>
    /// 构建“关键词 -> 线索ID”的映射
    /// </summary>
    private Dictionary<string, string> BuildClickableTerms()
    {
        var result = new Dictionary<string, string>();

        if (keywordDatabase == null || keywordDatabase.keywords == null)
        {
            return result;
        }

        foreach (var entry in keywordDatabase.keywords)
        {
            if (entry == null || string.IsNullOrEmpty(entry.term) || entry.revealsClue == null)
            {
                continue;
            }

            if (!result.ContainsKey(entry.term))
            {
                result.Add(entry.term, entry.revealsClue.id);
            }
        }

        return result;
    }

    /// <summary>
    /// 仅用于对话文本：把关键词替换为 TMP <link>，用于点击收集线索
    /// </summary>
    private string InjectLinks(string rawText)
    {
        if (string.IsNullOrEmpty(rawText))
        {
            return rawText;
        }

        if (_clickableTerms == null || _clickableTerms.Count == 0)
        {
            return rawText;
        }

        string result = rawText;

        foreach (var pair in _clickableTerms)
        {
            string word = pair.Key;
            string clueId = pair.Value;

            result = result.Replace(
                word,
                $"<link=\"{clueId}\"><color=#4AA3FF>{word}</color></link>"
            );
        }

        return result;
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

