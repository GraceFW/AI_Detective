using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Case/Keyword Database")]
public class CaseKeywordDatabase : ScriptableObject
{
    // 关键词 -> 线索 的映射列表：用于 UI/文本层决定哪些词可点击并揭示线索。
    public List<CaseKeywordEntry> keywords;
}

[System.Serializable]
public class CaseKeywordEntry
{
    [Tooltip("Text shown in UI that can be clicked")]
    // 在原始文本中要查找/替换的词。
    public string term;

    [Tooltip("Clue revealed when this term is clicked")]
    // 玩家点击该词后要揭示的目标线索。
    public ClueData revealsClue;
}

