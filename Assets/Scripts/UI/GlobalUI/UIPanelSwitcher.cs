using UnityEngine;
using UnityEngine.UI;

public class UIPanelSwitcher : MonoBehaviour
{
    [Header("Panels")]
    [SerializeField] private RectTransform askPanel;
    [SerializeField] private RectTransform searchPanel;
    [SerializeField] private RectTransform recordPanel;
    [SerializeField] private RectTransform resultPanel;

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
        Case3 = 2,
        Case4 = 3
    }

    private void Awake()
    {
        _currentCase = initialCase;
    }

    private void Start()
    {
        if (leftButton != null)
        {
            // [SFX] 为左右切换按钮添加音效组件
            if (leftButton.GetComponent<PlaySfxOnClick>() == null)
            {
                leftButton.gameObject.AddComponent<PlaySfxOnClick>();
            }
            leftButton.onClick.AddListener(OnLeftClick);
        }
        
        if (rightButton != null)
        {
            // [SFX] 为左右切换按钮添加音效组件
            if (rightButton.GetComponent<PlaySfxOnClick>() == null)
            {
                rightButton.gameObject.AddComponent<PlaySfxOnClick>();
            }
            rightButton.onClick.AddListener(OnRightClick);
        }

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
        if (_currentCase == PanelCase.Case4) return;

        _currentCase = _currentCase + 1;
        ApplyCase(_currentCase);
        RefreshButtonInteractable();
    }

    private void RefreshButtonInteractable()
    {
        if (leftButton != null) leftButton.interactable = _currentCase != PanelCase.Case1;
        if (rightButton != null) rightButton.interactable = _currentCase != PanelCase.Case4;
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
                // ASK 0,0; SEARCH 1920,-1920; RECORD 3840,-3840; RESULT 5760 -5760
                askPanel.anchoredPosition = new Vector2(0f, 0f);
                searchPanel.anchoredPosition = new Vector2(1920f, -1920f);
                recordPanel.anchoredPosition = new Vector2(3840f, -3840f);
                resultPanel.anchoredPosition = new Vector2(5760f, -5760f);
                break;

            case PanelCase.Case2:
                // ASK -1920,1920; SEARCH 0,0; RECORD 1920,-1920, RESULT 3840, -3840
                askPanel.anchoredPosition = new Vector2(-1920f, 1920f);
                searchPanel.anchoredPosition = new Vector2(0f, 0f);
                recordPanel.anchoredPosition = new Vector2(1920f, -1920f);
                resultPanel.anchoredPosition = new Vector2(3840f, -3840f);
                break;

            case PanelCase.Case3:
                // ASK -3840,3840; SEARCH -1920,1920; RECORD 0,0, RESULT 1920 -1920
                askPanel.anchoredPosition = new Vector2(-3840f, 3840f);
                searchPanel.anchoredPosition = new Vector2(-1920f, 1920f);
                recordPanel.anchoredPosition = new Vector2(0f, 0f);
                resultPanel.anchoredPosition = new Vector2(1920f, -1920f);
                break;

            case PanelCase.Case4:
                // ASK LEFT RESULT CENTER
                askPanel.anchoredPosition = new Vector2(-5760f, 5760f);
                searchPanel.anchoredPosition = new Vector2(-3840f, 3840f);
                recordPanel.anchoredPosition = new Vector2(-1920f, 1920f);
                resultPanel.anchoredPosition = new Vector2(0f, -0f);
                break;
        }
    }
}
