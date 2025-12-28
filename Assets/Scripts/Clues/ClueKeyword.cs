using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Case/Keyword Database")]
public class CaseKeywordDatabase : ScriptableObject
{
    public List<CaseKeywordEntry> keywords;
}

[System.Serializable]
public class CaseKeywordEntry
{
    [Tooltip("Text shown in UI that can be clicked")]
    public string term;

    [Tooltip("Clue revealed when this term is clicked")]
    public ClueData revealsClue;
}

