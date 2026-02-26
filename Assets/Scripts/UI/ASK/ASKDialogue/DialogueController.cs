using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话控制器
/// 管理对话流程、对话历史、选项分支
/// </summary>
public class DialogueController : MonoBehaviour
{
    [Header("组件引用")]
    [SerializeField] private DialogueUI dialogueUI;
    [SerializeField] private DialogueHistory dialogueHistory;

    // 当前对话人物
    private PersonClueData _currentPerson;
    
    // 当前对话节点
    private DialogueNode _currentNode;
    
    // 当前对话所在的节点列表（用于查找下一个节点）
    private List<DialogueNode> _currentDialogueNodes;
    
    // 已使用的线索对话（用于singleUse检查）
    private HashSet<string> _usedClueDialogues = new HashSet<string>();

    /// <summary>
    /// 当前对话的人物
    /// </summary>
    public PersonClueData CurrentPerson => _currentPerson;

    /// <summary>
    /// 是否有当前对话人物
    /// </summary>
    public bool HasCurrentPerson => _currentPerson != null;

    /// <summary>
    /// 是否正在浏览历史
    /// </summary>
    public bool IsBrowsingHistory => dialogueHistory != null && dialogueHistory.IsBrowsingHistory;

    private void Awake()
    {
        if (dialogueUI == null)
        {
            dialogueUI = GetComponent<DialogueUI>();
        }

        if (dialogueHistory == null)
        {
            dialogueHistory = GetComponent<DialogueHistory>();
        }
    }

    /// <summary>
    /// 启动基础对话（传唤人物）
    /// </summary>
    public void StartBaseDialogue(PersonClueData person)
    {
        if (person == null)
        {
            Debug.LogError("[DialogueController] 传入的人物数据为空");
            return;
        }

        if (person.baseDialogues == null || person.baseDialogues.Count == 0)
        {
            Debug.LogError($"[DialogueController] {person.displayName} 没有基础对话数据");
            return;
        }

        _currentPerson = person;
        _currentDialogueNodes = person.baseDialogues;

        // 清空历史
        dialogueHistory.Clear();

        // 显示人物信息
        dialogueUI.ShowPerson(person.displayName, person.portrait);

        // 加载第一个对话节点
        LoadDialogueNode(person.baseDialogues[0]);

        Debug.Log($"[DialogueController] 启动与 {person.displayName} 的对话");
    }

    /// <summary>
    /// 显示线索触发的对话
    /// </summary>
    public void ShowClueDialogue(ClueData clue)
    {
        if (_currentPerson == null)
        {
            Debug.LogWarning("[DialogueController] 没有当前对话人物");
            return;
        }

        if (clue == null)
        {
            Debug.LogWarning("[DialogueController] 线索为空");
            return;
        }

        // 查找线索对话
        var clueDialogueEntry = FindClueDialogue(clue);

        if (clueDialogueEntry != null && clueDialogueEntry.dialogues != null && clueDialogueEntry.dialogues.Count > 0)
        {
            // 找到了对应的线索对话
            _currentDialogueNodes = clueDialogueEntry.dialogues;
            LoadDialogueNode(clueDialogueEntry.dialogues[0]);
            Debug.Log($"[DialogueController] 出示线索 {clue.displayName}，触发线索对话");
        }
        else
        {
            // 使用兜底对话
            if (_currentPerson.fallbackDialogues != null && _currentPerson.fallbackDialogues.Count > 0)
            {
                _currentDialogueNodes = _currentPerson.fallbackDialogues;
                LoadDialogueNode(_currentPerson.fallbackDialogues[0]);
                Debug.Log($"[DialogueController] 出示线索 {clue.displayName}，触发兜底对话");
            }
            else
            {
                Debug.LogWarning($"[DialogueController] {_currentPerson.displayName} 没有兜底对话");
            }
        }
    }

    /// <summary>
    /// 点击继续（下一段对话）
    /// </summary>
    public void NextDialogue()
    {
        // 如果在浏览历史，回到最新
        if (dialogueHistory.IsBrowsingHistory)
        {
            dialogueHistory.ResetToLatest();
            var latestEntry = dialogueHistory.GetCurrent();
            if (latestEntry != null)
            {
                // 回到最新时也不使用打字机效果，因为这是历史记录
                dialogueUI.ShowDialogue(latestEntry.text, latestEntry.options != null && latestEntry.options.Count > 0, useTypewriter: false);
                if (latestEntry.options != null && latestEntry.options.Count > 0)
                {
                    dialogueUI.ShowOptions(latestEntry.options);
                }
                else
                {
                    dialogueUI.ClearOptions();
                }
            }
            UpdateNavigationButtons();
            return;
        }

        // 如果当前有选项，不能直接继续
        if (_currentNode != null && _currentNode.options != null && _currentNode.options.Count > 0)
        {
            Debug.Log("[DialogueController] 当前有选项，请选择一个选项");
            return;
        }

        // 查找下一个节点
        if (_currentNode != null && !string.IsNullOrEmpty(_currentNode.nextNodeId))
        {
            // 先查找当前对话列表
            var nextNode = FindNodeById(_currentNode.nextNodeId, _currentDialogueNodes);
            
            // 如果找不到，再查找baseDialogues（线索对话的nextNodeId可能指向baseDialogues）
            if (nextNode == null && _currentPerson != null && _currentPerson.baseDialogues != null)
            {
                nextNode = FindNodeById(_currentNode.nextNodeId, _currentPerson.baseDialogues);
                // 如果节点在baseDialogues中找到，切换当前对话列表
                if (nextNode != null)
                {
                    _currentDialogueNodes = _currentPerson.baseDialogues;
                }
            }
            
            if (nextNode != null)
            {
                LoadDialogueNode(nextNode);
            }
            else
            {
                Debug.LogWarning($"[DialogueController] 找不到节点ID: {_currentNode.nextNodeId}");
                EndDialogue();
            }
        }
        else
        {
            // 对话结束
            EndDialogue();
        }
    }

    /// <summary>
    /// 选择选项
    /// </summary>
    public void SelectOption(int optionIndex)
    {
        // 如果正在浏览历史，不允许选择选项
        if (dialogueHistory.IsBrowsingHistory)
        {
            Debug.LogWarning("[DialogueController] 正在浏览历史，无法选择选项。请先回到最新对话。");
            return;
        }

        if (_currentNode == null || _currentNode.options == null || optionIndex < 0 || optionIndex >= _currentNode.options.Count)
        {
            Debug.LogError($"[DialogueController] 无效的选项索引: {optionIndex}");
            return;
        }

        var option = _currentNode.options[optionIndex];
        Debug.Log($"[DialogueController] 选择选项: {option.optionText}");

        // 清空选项显示
        dialogueUI.ClearOptions();

        // 跳转到选项对应的节点
        if (!string.IsNullOrEmpty(option.nextNodeId))
        {
            // 先查找当前对话列表
            var nextNode = FindNodeById(option.nextNodeId, _currentDialogueNodes);
            
            // 如果找不到，再查找baseDialogues（线索对话的选项可能指向baseDialogues）
            if (nextNode == null && _currentPerson != null && _currentPerson.baseDialogues != null)
            {
                nextNode = FindNodeById(option.nextNodeId, _currentPerson.baseDialogues);
                // 如果节点在baseDialogues中找到，切换当前对话列表
                if (nextNode != null)
                {
                    _currentDialogueNodes = _currentPerson.baseDialogues;
                }
            }
            
            if (nextNode != null)
            {
                LoadDialogueNode(nextNode);
            }
            else
            {
                Debug.LogWarning($"[DialogueController] 找不到选项对应的节点ID: {option.nextNodeId}");
                EndDialogue();
            }
        }
        else
        {
            EndDialogue();
        }
    }

    /// <summary>
    /// 历史浏览
    /// </summary>
    public void NavigateHistory(int direction)
    {
        if (direction < 0)
        {
            // 向前
            if (dialogueHistory.CanNavigateBack())
            {
                dialogueHistory.NavigateBack();
                ShowHistoryEntry();
            }
        }
        else if (direction > 0)
        {
            // 向后
            if (dialogueHistory.CanNavigateForward())
            {
                dialogueHistory.NavigateForward();
                ShowHistoryEntry();
            }
        }

        UpdateNavigationButtons();
    }

    /// <summary>
    /// 显示历史条目
    /// </summary>
    private void ShowHistoryEntry()
    {
        var entry = dialogueHistory.GetCurrent();
        if (entry != null)
        {
            // 历史回溯时不使用打字机效果，直接显示完整文本
            dialogueUI.ShowDialogue(entry.text, entry.options != null && entry.options.Count > 0, useTypewriter: false);
            
            if (entry.options != null && entry.options.Count > 0)
            {
                // 根据是否在浏览历史来决定选项是否可点击
                // 如果已经回到最新（IsBrowsingHistory = false），选项应该可用
                bool isInHistoryMode = dialogueHistory.IsBrowsingHistory;
                dialogueUI.ShowOptions(entry.options, isHistoryView: isInHistoryMode);
            }
            else
            {
                dialogueUI.ClearOptions();
            }
        }
    }

    /// <summary>
    /// 加载对话节点
    /// </summary>
    private void LoadDialogueNode(DialogueNode node)
    {
        if (node == null)
        {
            Debug.LogError("[DialogueController] 对话节点为空");
            return;
        }

        _currentNode = node;

        // 添加到历史（只在非浏览模式下）
        if (!dialogueHistory.IsBrowsingHistory)
        {
            dialogueHistory.AddEntry(node.text, node.options, node.nodeId);
        }

        // 显示对话
        bool hasOptions = node.options != null && node.options.Count > 0;
        dialogueUI.ShowDialogue(node.text, hasOptions);

        if (hasOptions)
        {
            dialogueUI.ShowOptions(node.options);
        }
        else
        {
            dialogueUI.ClearOptions();
        }

        UpdateNavigationButtons();
    }

    /// <summary>
    /// 更新导航按钮状态
    /// </summary>
    private void UpdateNavigationButtons()
    {
        bool canPrev = dialogueHistory.CanNavigateBack();
        bool canNext = dialogueHistory.CanNavigateForward();
        dialogueUI.UpdateNavigationButtons(canPrev, canNext);
    }

    /// <summary>
    /// 结束对话
    /// </summary>
    private void EndDialogue()
    {
        Debug.Log("[DialogueController] 对话结束");
        // 可以在这里添加对话结束的处理逻辑
    }

    /// <summary>
    /// 在节点列表中查找指定ID的节点
    /// </summary>
    private DialogueNode FindNodeById(string nodeId, List<DialogueNode> nodes)
    {
        if (string.IsNullOrEmpty(nodeId) || nodes == null)
            return null;

        foreach (var node in nodes)
        {
            if (node != null && node.nodeId == nodeId)
            {
                return node;
            }
        }

        return null;
    }

    /// <summary>
    /// 查找线索对话
    /// </summary>
    private ClueDialogueEntry FindClueDialogue(ClueData clue)
    {
        if (_currentPerson == null || _currentPerson.clueDialogues == null || clue == null)
            return null;

        foreach (var entry in _currentPerson.clueDialogues)
        {
            if (entry == null || entry.shownClue == null)
                continue;

            if (entry.shownClue == clue)
            {
                string key = $"{_currentPerson.id}_{clue.id}";

                // 检查单次使用
                if (entry.singleUse && _usedClueDialogues.Contains(key))
                {
                    Debug.Log($"[DialogueController] 线索对话 {clue.displayName} 已使用过（singleUse）");
                    continue;
                }

                // 标记为已使用
                if (entry.singleUse)
                {
                    _usedClueDialogues.Add(key);
                }

                return entry;
            }
        }

        return null;
    }
}

