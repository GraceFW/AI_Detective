using UnityEngine;
using UnityEngine.UI;

public class ReturnMainMenuButtonUI : MonoBehaviour
{
    [SerializeField] private Button returnButton;
    [SerializeField] private MainMenuSettingsPanelUI settingsPanel;
    [SerializeField] private bool closeSettingsPanelOnReturn = true;

    private SceneManager _sceneManager;

    private void Awake()
    {
        if (returnButton == null)
        {
            returnButton = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        if (returnButton != null)
        {
            returnButton.onClick.AddListener(ReturnToMainMenu);
        }

        RefreshInteractable();
    }

    private void OnDisable()
    {
        if (returnButton != null)
        {
            returnButton.onClick.RemoveListener(ReturnToMainMenu);
        }
    }

    public void ReturnToMainMenu()
    {
        SceneManager sceneManager = ResolveSceneManager();
        if (sceneManager == null || sceneManager.menuScene == null)
        {
            Debug.LogWarning("[ReturnMainMenuButtonUI] SceneManager or menuScene is missing.");
            RefreshInteractable();
            return;
        }

        if (sceneManager.IsLoading || sceneManager.CurrentScene == sceneManager.menuScene)
        {
            RefreshInteractable();
            return;
        }

        if (closeSettingsPanelOnReturn)
        {
            MainMenuSettingsPanelUI panel = ResolveSettingsPanel();
            if (panel != null)
            {
                panel.Close();
            }
        }

        sceneManager.LoadScene(sceneManager.menuScene);
        RefreshInteractable();
    }

    public void RefreshInteractable()
    {
        if (returnButton == null)
        {
            return;
        }

        SceneManager sceneManager = ResolveSceneManager();
        returnButton.interactable = sceneManager != null
            && sceneManager.menuScene != null
            && !sceneManager.IsLoading
            && sceneManager.CurrentScene != sceneManager.menuScene;
    }

    private SceneManager ResolveSceneManager()
    {
        if (_sceneManager != null)
        {
            return _sceneManager;
        }

        _sceneManager = FindObjectOfType<SceneManager>();
        return _sceneManager;
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
}
