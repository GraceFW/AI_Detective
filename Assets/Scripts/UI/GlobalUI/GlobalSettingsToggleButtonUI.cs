using UnityEngine;
using UnityEngine.UI;

public class GlobalSettingsToggleButtonUI : MonoBehaviour
{
    [SerializeField] private Button toggleButton;
    [SerializeField] private MainMenuSettingsPanelUI settingsPanel;
    [SerializeField] private bool useGlobalVisibilityRules;
    [SerializeField] private bool hideInMenuScene = true;
    [SerializeField] private bool hideDuringGuide = true;
    [SerializeField] private bool hideDuringDialogue = true;
    [SerializeField] private float visibilityRefreshInterval = 0.1f;

    private bool _listenerBound;
    private bool _initialButtonInteractable;
    private bool _capturedInitialInteractable;
    private CanvasGroup _canvasGroup;
    private SceneManager _sceneManager;
    private GuideManager[] _guideManagers;
    private float _nextVisibilityRefreshTime;
    private bool _isVisible = true;

    private void Awake()
    {
        ResolveButton();
        ResolveCanvasGroup();
        BindButton();
        RefreshVisibility(true);
    }

    private void OnEnable()
    {
        ResolveButton();
        ResolveCanvasGroup();
        BindButton();
        RefreshVisibility(true);
    }

    private void Start()
    {
        ResolveButton();
        ResolveCanvasGroup();
        BindButton();
        RefreshVisibility(true);
    }

    private void Update()
    {
        if (!useGlobalVisibilityRules || Time.unscaledTime < _nextVisibilityRefreshTime)
        {
            return;
        }

        _nextVisibilityRefreshTime = Time.unscaledTime + visibilityRefreshInterval;
        RefreshVisibility(false);
    }

    private void OnDisable()
    {
        UnbindButton();
    }

    public void ToggleSettingsPanel()
    {
        MainMenuSettingsPanelUI panel = ResolveSettingsPanel();
        if (panel == null)
        {
            Debug.LogWarning("[GlobalSettingsToggleButtonUI] Settings panel is not assigned.");
            return;
        }

        panel.Toggle();
    }

    public void OpenSettingsPanel()
    {
        MainMenuSettingsPanelUI panel = ResolveSettingsPanel();
        if (panel == null)
        {
            Debug.LogWarning("[GlobalSettingsToggleButtonUI] Settings panel is not assigned.");
            return;
        }

        panel.Open();
    }

    private void ResolveButton()
    {
        if (toggleButton == null)
        {
            toggleButton = GetComponent<Button>();
        }

        if (toggleButton != null && !_capturedInitialInteractable)
        {
            _initialButtonInteractable = toggleButton.interactable;
            _capturedInitialInteractable = true;
        }
    }

    private void ResolveCanvasGroup()
    {
        if (_canvasGroup == null)
        {
            _canvasGroup = GetComponent<CanvasGroup>();
        }

        if (_canvasGroup == null)
        {
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void BindButton()
    {
        if (_listenerBound || toggleButton == null)
        {
            return;
        }

        toggleButton.onClick.AddListener(ToggleSettingsPanel);
        _listenerBound = true;
    }

    private void UnbindButton()
    {
        if (!_listenerBound)
        {
            return;
        }

        if (toggleButton != null)
        {
            toggleButton.onClick.RemoveListener(ToggleSettingsPanel);
        }

        _listenerBound = false;
    }

    private MainMenuSettingsPanelUI ResolveSettingsPanel()
    {
        if (settingsPanel != null)
        {
            return settingsPanel;
        }

        MainMenuSettingsPanelUI[] panels = Resources.FindObjectsOfTypeAll<MainMenuSettingsPanelUI>();
        for (int i = 0; i < panels.Length; i++)
        {
            MainMenuSettingsPanelUI panel = panels[i];
            if (panel != null && panel.gameObject.scene.IsValid())
            {
                settingsPanel = panel;
                return settingsPanel;
            }
        }

        return null;
    }

    private void RefreshVisibility(bool force)
    {
        if (!useGlobalVisibilityRules)
        {
            SetVisible(true, false, force);
            return;
        }

        bool isGuideRunning = hideDuringGuide && IsGuideRunning();
        bool isDialogueRunning = hideDuringDialogue && IsDialogueRunning();
        bool shouldHide = (hideInMenuScene && IsInMenuScene()) || isGuideRunning || isDialogueRunning;
        SetVisible(!shouldHide, isGuideRunning || isDialogueRunning, force);
    }

    private void SetVisible(bool visible, bool closePanelWhenHiding, bool force)
    {
        if (!force && _isVisible == visible)
        {
            return;
        }

        _isVisible = visible;

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        if (toggleButton != null)
        {
            toggleButton.interactable = visible && (!_capturedInitialInteractable || _initialButtonInteractable);
        }

        if (!visible && closePanelWhenHiding)
        {
            MainMenuSettingsPanelUI panel = ResolveSettingsPanel();
            if (panel != null && panel.IsOpen)
            {
                panel.Close();
            }
        }
    }

    private bool IsInMenuScene()
    {
        SceneManager sceneManager = ResolveSceneManager();
        if (sceneManager == null)
        {
            return false;
        }

        GameSceneSO currentScene = sceneManager.CurrentScene;
        if (currentScene == null)
        {
            return true;
        }

        if (sceneManager.menuScene != null && currentScene == sceneManager.menuScene)
        {
            return true;
        }

        return currentScene.sceneType == SceneType.Menu;
    }

    private SceneManager ResolveSceneManager()
    {
        if (_sceneManager == null)
        {
            _sceneManager = FindObjectOfType<SceneManager>();
        }

        return _sceneManager;
    }

    private bool IsGuideRunning()
    {
        GuideManager[] managers = ResolveGuideManagers();
        for (int i = 0; i < managers.Length; i++)
        {
            GuideManager manager = managers[i];
            if (manager != null && manager.isActiveAndEnabled && manager.IsGuiding)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsDialogueRunning()
    {
        return DialogueManager.Instance != null && DialogueManager.Instance.IsDialogueActive();
    }

    private GuideManager[] ResolveGuideManagers()
    {
        if (_guideManagers == null || HasMissingGuideManager(_guideManagers))
        {
            _guideManagers = Resources.FindObjectsOfTypeAll<GuideManager>();
        }

        return _guideManagers;
    }

    private bool HasMissingGuideManager(GuideManager[] managers)
    {
        if (managers == null || managers.Length == 0)
        {
            return true;
        }

        for (int i = 0; i < managers.Length; i++)
        {
            if (managers[i] == null)
            {
                return true;
            }
        }

        return false;
    }
}
