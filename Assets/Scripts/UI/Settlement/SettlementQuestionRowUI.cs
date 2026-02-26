using TMPro;
using UnityEngine;

/// <summary>
/// 结算界面：单行问题 UI
/// - 显示问题文本
/// - 仅包含 1 个线索填充框（SettlementClueDropSlot）
/// </summary>
public class SettlementQuestionRowUI : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI questionText;
    [SerializeField] private SettlementClueDropSlot slot;

    public string Question { get; private set; }
    public ClueData CorrectClue { get; private set; }

    public void Setup(string question, ClueData correctClue)
    {
        Question = question;
        CorrectClue = correctClue;

        if (questionText != null)
        {
            questionText.text = question;
        }
    }

    public bool IsComplete()
    {
        return slot != null && slot.CurrentClue != null;
    }

    public ClueData GetFilledClue()
    {
        return slot != null ? slot.CurrentClue : null;
    }

    public bool IsCorrect()
    {
        if (slot == null || slot.CurrentClue == null || CorrectClue == null)
        {
            return false;
        }

        return slot.CurrentClue.id == CorrectClue.id;
    }
}
