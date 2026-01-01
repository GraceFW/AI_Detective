using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ClueListItemUI : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private Button button;

    private ClueData _clue;

    public string ClueId { get; private set; }

    public event Action<ClueData> OnClicked;

    private void Awake()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (button != null)
        {
            button.onClick.AddListener(HandleButtonClicked);
        }
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleButtonClicked);
        }
    }

    public void Bind(ClueData clue)
    {
        _clue = clue;

        if (clue == null)
        {
            ClueId = null;
            if (nameText != null) nameText.text = string.Empty;
            return;
        }

        ClueId = clue.id;
        if (nameText != null) nameText.text = clue.displayName;
    }

    private void HandleButtonClicked()
    {
        OnClicked?.Invoke(_clue);
    }
}
