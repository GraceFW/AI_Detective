using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Clue/Clue Database")]
public class ClueDatabaseSO : ScriptableObject
{
    public List<ClueData> clues = new List<ClueData>();

    private Dictionary<string, ClueData> _byId;
    private Dictionary<string, ClueData> _byDisplayName;

    private void OnEnable()
    {
        _byId = null;
        _byDisplayName = null;
    }

    public bool TryGetClue(string clueId, out ClueData clue)
    {
        clue = null;

        if (string.IsNullOrEmpty(clueId))
        {
            return false;
        }

        EnsureIndex();
        return _byId.TryGetValue(clueId, out clue);
    }

    /// <summary>
    /// 根据 displayName 精确匹配查找线索
    /// </summary>
    public bool TryGetClueByDisplayName(string displayName, out ClueData clue)
    {
        clue = null;

        if (string.IsNullOrEmpty(displayName))
        {
            return false;
        }

        EnsureIndex();
        return _byDisplayName.TryGetValue(displayName, out clue);
    }

    /// <summary>
    /// 根据 displayName 模糊搜索（包含匹配）
    /// </summary>
    public ClueData SearchByDisplayName(string searchText)
    {
        if (string.IsNullOrEmpty(searchText))
        {
            return null;
        }

        var trimmed = searchText.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return null;
        }

        // 优先精确匹配
        if (TryGetClueByDisplayName(trimmed, out var exactMatch))
        {
            return exactMatch;
        }

        // 模糊匹配（包含搜索文本）
        // foreach (var clue in clues)
        // {
        //     if (clue == null || string.IsNullOrEmpty(clue.displayName))
        //     {
        //         continue;
        //     }

        //     if (clue.displayName.Contains(trimmed) || trimmed.Contains(clue.displayName))
        //     {
        //         return clue;
        //     }
        // }

        return null;
    }

    private void EnsureIndex()
    {
        if (_byId != null && _byDisplayName != null)
        {
            return;
        }

        _byId = new Dictionary<string, ClueData>(StringComparer.Ordinal);
        _byDisplayName = new Dictionary<string, ClueData>(StringComparer.OrdinalIgnoreCase);

        foreach (var clue in clues)
        {
            if (clue == null)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(clue.id) && !_byId.ContainsKey(clue.id))
            {
                _byId.Add(clue.id, clue);
            }

            if (!string.IsNullOrEmpty(clue.displayName) && !_byDisplayName.ContainsKey(clue.displayName))
            {
                _byDisplayName.Add(clue.displayName, clue);
            }
        }
    }
}
