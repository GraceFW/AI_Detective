using UnityEngine;
using UnityEngine.UI;

public class UIPanelSwitcher : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private RectTransform askPanel;
    [SerializeField] private RectTransform searchPanel;
    [SerializeField] private RectTransform recordPanel;

    [Header("Buttons")]
    [SerializeField] private Button leftButton;
    [SerializeField] private Button rightButton;

    [Header("Initial")]
    [SerializeField] private PanelCase initialCase = PanelCase.Case2;

    private PanelCase _currentCase;

    private enum PanelCase
    {
        Case1 = 0,
        Case2 = 1,
        Case3 = 2
    }

    private void Awake()
    {
        _currentCase = initialCase;
    }

    private void Start()
    {
        if (leftButton != null) leftButton.onClick.AddListener(OnLeftClick);
        if (rightButton != null) rightButton.onClick.AddListener(OnRightClick);

        ApplyCase(_currentCase);
        RefreshButtonInteractable();
    }

    private void OnDestroy()
    {
        if (leftButton != null) leftButton.onClick.RemoveListener(OnLeftClick);
        if (rightButton != null) rightButton.onClick.RemoveListener(OnRightClick);
    }

    private void OnLeftClick()
    {
        if (_currentCase == PanelCase.Case1) return;

        _currentCase = _currentCase - 1;
        ApplyCase(_currentCase);
        RefreshButtonInteractable();
    }

    private void OnRightClick()
    {
        if (_currentCase == PanelCase.Case3) return;

        _currentCase = _currentCase + 1;
        ApplyCase(_currentCase);
        RefreshButtonInteractable();
    }

    private void RefreshButtonInteractable()
    {
        if (leftButton != null) leftButton.interactable = _currentCase != PanelCase.Case1;
        if (rightButton != null) rightButton.interactable = _currentCase != PanelCase.Case3;
    }

    private void ApplyCase(PanelCase panelCase)
    {
        if (askPanel == null || searchPanel == null || recordPanel == null)
        {
            Debug.LogError("UIPanelSwitcher: Panels are not assigned.");
            return;
        }

        // 注意：这里使用 anchoredPosition（UI 推荐）。
        // 你的坐标描述是以 1920 为单位的横向位移，符合 anchoredPosition 的用法。
        switch (panelCase)
        {
            case PanelCase.Case1:
                // ASK 0,0; SEARCH 1920,-1920; RECORD 3840,-3840
                askPanel.anchoredPosition = new Vector2(0f, 0f);
                searchPanel.anchoredPosition = new Vector2(1920f, -1920f);
                recordPanel.anchoredPosition = new Vector2(3840f, -3840f);
                break;

            case PanelCase.Case2:
                // ASK -1920,1920; SEARCH 0,0; RECORD 1920,-1920
                askPanel.anchoredPosition = new Vector2(-1920f, 1920f);
                searchPanel.anchoredPosition = new Vector2(0f, 0f);
                recordPanel.anchoredPosition = new Vector2(1920f, -1920f);
                break;

            case PanelCase.Case3:
                // ASK -3840,3840; SEARCH -1920,1920; RECORD 0,0
                askPanel.anchoredPosition = new Vector2(-3840f, 3840f);
                searchPanel.anchoredPosition = new Vector2(-1920f, 1920f);
                recordPanel.anchoredPosition = new Vector2(0f, 0f);
                break;
        }
    }
}
