using System.Collections.Generic;
using UnityEngine;

public class ClueDetailViewTest : MonoBehaviour
{
    [Header("UI")]
    public ClickableTMPText clickableText;

    [Header("Data")]
    public NormalClueData testClue;
    public CaseKeywordDatabase keywordDatabase;

    void Start()
    {
        Dictionary<string, string> clickableTerms = BuildClickableTerms();
        var textToShow = string.IsNullOrWhiteSpace(testClue.Detail_Mark) ? testClue.detailText : testClue.Detail_Mark;
        clickableText.SetText(textToShow, clickableTerms);
    }

    Dictionary<string, string> BuildClickableTerms()
    {
        Dictionary<string, string> result = new Dictionary<string, string>();

        foreach (var entry in keywordDatabase.keywords)
        {
            if (entry.revealsClue != null)
            {
                result[entry.term] = entry.revealsClue.id;
            }
        }

        return result;
    }
}
