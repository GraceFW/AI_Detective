using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClueDetailPopupUI : MonoBehaviour
{
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

        gameObject.SetActive(true);
    }

    public void CloseAndDestroy()
    {
        Destroy(gameObject);
    }
}
