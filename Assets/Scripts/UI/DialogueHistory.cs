using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 对话历史管理器
/// 记录对话历史，支持前后浏览
/// </summary>
public class DialogueHistory : MonoBehaviour
{
    /// <summary>
    /// 对话历史条目
    /// </summary>
    [System.Serializable]
    public class DialogueHistoryEntry
    {
        public string text;
        public List<DialogueOption> options;
        public string currentNodeId;

        public DialogueHistoryEntry(string text, List<DialogueOption> options, string nodeId)
        {
            this.text = text;
            this.options = options != null ? new List<DialogueOption>(options) : new List<DialogueOption>();
            this.currentNodeId = nodeId;
        }
    }

    private List<DialogueHistoryEntry> _history = new List<DialogueHistoryEntry>();
    private int _currentIndex = -1;  // -1表示在最新位置

    /// <summary>
    /// 是否在浏览历史（不在最新位置）
    /// </summary>
    public bool IsBrowsingHistory => _currentIndex >= 0;

    /// <summary>
    /// 历史记录数量
    /// </summary>
    public int Count => _history.Count;

    /// <summary>
    /// 添加新的对话条目到历史
    /// </summary>
    public void AddEntry(string text, List<DialogueOption> options, string nodeId)
    {
        var entry = new DialogueHistoryEntry(text, options, nodeId);
        _history.Add(entry);
        
        // 添加新条目后，重置到最新位置
        _currentIndex = -1;

        Debug.Log($"[DialogueHistory] 添加历史记录，当前总数: {_history.Count}");
    }

    /// <summary>
    /// 获取当前显示的条目
    /// </summary>
    public DialogueHistoryEntry GetCurrent()
    {
        if (_history.Count == 0)
            return null;

        if (_currentIndex == -1)
        {
            // 返回最新的
            return _history[_history.Count - 1];
        }
        else
        {
            // 返回历史中的某一条
            return _history[_currentIndex];
        }
    }

    /// <summary>
    /// 能否向前浏览（查看更早的对话）
    /// </summary>
    public bool CanNavigateBack()
    {
        if (_history.Count <= 1)
            return false;

        if (_currentIndex == -1)
            return true;  // 在最新位置，可以往前

        return _currentIndex > 0;  // 在历史中，检查是否还能往前
    }

    /// <summary>
    /// 能否向后浏览（查看更新的对话）
    /// </summary>
    public bool CanNavigateForward()
    {
        if (_currentIndex == -1)
            return false;  // 已经在最新位置

        return _currentIndex < _history.Count - 1;
    }

    /// <summary>
    /// 向前浏览（查看更早的对话）
    /// </summary>
    public void NavigateBack()
    {
        if (!CanNavigateBack())
        {
            Debug.LogWarning("[DialogueHistory] 无法向前浏览");
            return;
        }

        if (_currentIndex == -1)
        {
            // 从最新位置开始浏览，跳到倒数第二条
            _currentIndex = _history.Count - 2;
        }
        else
        {
            // 继续往前
            _currentIndex--;
        }

        Debug.Log($"[DialogueHistory] 向前浏览，当前索引: {_currentIndex}");
    }

    /// <summary>
    /// 向后浏览（查看更新的对话）
    /// </summary>
    public void NavigateForward()
    {
        if (!CanNavigateForward())
        {
            Debug.LogWarning("[DialogueHistory] 无法向后浏览");
            return;
        }

        _currentIndex++;

        // 如果到达最后，回到最新位置
        if (_currentIndex >= _history.Count - 1)
        {
            _currentIndex = -1;
        }

        Debug.Log($"[DialogueHistory] 向后浏览，当前索引: {_currentIndex}");
    }

    /// <summary>
    /// 清空所有历史记录
    /// </summary>
    public void Clear()
    {
        _history.Clear();
        _currentIndex = -1;
        Debug.Log("[DialogueHistory] 清空历史记录");
    }

    /// <summary>
    /// 回到最新对话
    /// </summary>
    public void ResetToLatest()
    {
        _currentIndex = -1;
    }
}

