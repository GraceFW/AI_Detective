using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClueDetailPopupUI : MonoBehaviour
{
    private const string GuideTargetKeyPrefix = "clueDetailPopup:";

    [SerializeField] private TextMeshProUGUI summaryText;
    [SerializeField] private Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseAndDestroy);
        }
    }

    private void OnDestroy()
    {
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseAndDestroy);
        }
    }

    public void Show(ClueData clue)
    {
        if (summaryText != null)
        {
            summaryText.text = clue != null ? clue.summary : string.Empty;
        }

        RegisterToGuide(clue);
        gameObject.SetActive(true);
    }

    public static string GetGuideTargetKey(string clueId)
    {
        return string.IsNullOrWhiteSpace(clueId) ? string.Empty : $"{GuideTargetKeyPrefix}{clueId}";
    }

    public void CloseAndDestroy()
    {
        Destroy(gameObject);
    }

    private void RegisterToGuide(ClueData clue)
    {
        string guideKey = GetGuideTargetKey(clue != null ? clue.id : null);
        if (string.IsNullOrEmpty(guideKey))
        {
            return;
        }

        var target = GetComponent<GuideTarget>();
        if (target == null)
        {
            target = gameObject.AddComponent<GuideTarget>();
        }

        target.Init(guideKey);
    }
}
