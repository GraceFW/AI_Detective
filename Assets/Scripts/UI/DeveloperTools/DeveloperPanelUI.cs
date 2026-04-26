using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class DeveloperPanelUI : MonoBehaviour
{
    [Header("Root")]
    [SerializeField] private GameObject panelRoot;
    [SerializeField] private bool hideOnAwake = true;
    [SerializeField] private KeyCode toggleKey = KeyCode.BackQuote;
    [SerializeField] private bool forceTopmostOnShow = true;
    [SerializeField] private int topmostSortingOrder = 10000;

    [Header("Scene Loading")]
    [SerializeField] private GameSceneEventSO loadSceneEvent;
    [SerializeField] private SceneDatabaseSO sceneDatabase;
    [SerializeField] private List<GameSceneSO> sceneOptions = new List<GameSceneSO>();
    [SerializeField] private TMP_Dropdown sceneDropdown;
    [SerializeField] private Button loadSelectedSceneButton;
    [SerializeField] private Button refreshSceneListButton;
    [SerializeField] private int selectedSceneIndex;
    [SerializeField] private string persistentSceneName = "Persistent";
    [SerializeField] private bool useSafeSceneManagerPath = true;
    [SerializeField] private bool invokeLegacyLoadButtonOnConfirm;

    [Header("Scene Switch Confirmation")]
    [SerializeField] private GameObject sceneSwitchConfirmRoot;
    [SerializeField] private TextMeshProUGUI sceneSwitchConfirmText;
    [SerializeField] private Button confirmSceneSwitchButton;
    [SerializeField] private Button cancelSceneSwitchButton;
    [SerializeField] private string sceneSwitchConfirmTemplate = "Switch to scene: {0}?";

    [Header("Current Level Victory")]
    [SerializeField] private Button triggerVictoryButton;
    [SerializeField] private Button defeatBoboAiButton;
    [SerializeField] private Button defeatBoboPlayerButton;
    [SerializeField] private bool playLevelCompleteDialogue = true;
    [SerializeField] private int levelNumberOverride = -1;
    [SerializeField] private GameSceneSO nextSceneOverride;

    [Header("Status")]
    [SerializeField] private TextMeshProUGUI statusText;
    [SerializeField] private bool verboseLog = true;

    private readonly List<GameSceneSO> runtimeSceneOptions = new List<GameSceneSO>();
    private int previousSceneIndex;
    private int pendingSceneIndex = -1;
    private bool suppressSceneDropdownEvent;
    private CanvasGroup panelCanvasGroup;
    private bool hidePanelWithCanvasGroup;
    private Canvas panelCanvas;
    private GraphicRaycaster panelGraphicRaycaster;

    private void Awake()
    {
        EnsureEventSystem();

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        hidePanelWithCanvasGroup = panelRoot == gameObject;
        EnsurePanelInputSurface();
        BindButtons();
        RebuildSceneDropdown();
        HideSceneSwitchConfirm();

        if (hideOnAwake)
        {
            SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
        UnbindButtons();
    }

    private void Update()
    {
        if (toggleKey != KeyCode.None && Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }
    }

    public void TogglePanel()
    {
        SetPanelVisible(!IsPanelVisible());
    }

    public void ShowPanel()
    {
        SetPanelVisible(true);
    }

    public void HidePanel()
    {
        SetPanelVisible(false);
    }

    public void RebuildSceneDropdown()
    {
        runtimeSceneOptions.Clear();

        if (sceneOptions != null)
        {
            for (int i = 0; i < sceneOptions.Count; i++)
            {
                AddSceneOption(sceneOptions[i]);
            }
        }

        SceneDatabaseSO database = sceneDatabase != null ? sceneDatabase : SceneDatabaseSO.Instance;
        if (database != null && database.allScenes != null)
        {
            for (int i = 0; i < database.allScenes.Count; i++)
            {
                AddSceneOption(database.allScenes[i]);
            }
        }

        if (sceneDropdown != null)
        {
            sceneDropdown.ClearOptions();
            List<string> optionNames = new List<string>();
            for (int i = 0; i < runtimeSceneOptions.Count; i++)
            {
                GameSceneSO scene = runtimeSceneOptions[i];
                optionNames.Add(scene != null ? scene.name : "<Missing Scene>");
            }

            sceneDropdown.AddOptions(optionNames);
            selectedSceneIndex = Mathf.Clamp(selectedSceneIndex, 0, Mathf.Max(0, runtimeSceneOptions.Count - 1));
            previousSceneIndex = selectedSceneIndex;
            sceneDropdown.SetValueWithoutNotify(selectedSceneIndex);
            sceneDropdown.RefreshShownValue();
        }

        SetStatus(string.Format("Developer scene list ready. Count={0}", runtimeSceneOptions.Count));
    }

    public void LoadSelectedScene()
    {
        GameSceneSO selectedScene = GetSelectedScene();
        if (selectedScene == null)
        {
            SetStatus("No scene selected.");
            return;
        }

        LoadScene(selectedScene);
    }

    public void TriggerCurrentLevelVictory()
    {
        SettlementPanelUI settlementPanel = FindObjectOfType<SettlementPanelUI>(true);
        if (settlementPanel != null)
        {
            int levelNumber = ResolveCurrentLevelNumber();
            settlementPanel.DebugCompleteLevel(levelNumber);
            SetStatus(string.Format("Level victory triggered through SettlementPanelUI. Level={0}", levelNumber));
            return;
        }

        if (playLevelCompleteDialogue && DialogueManager.Instance != null)
        {
            int levelNumber = ResolveCurrentLevelNumber();
            DialogueManager.Instance.ShowDialogue(
                levelNumber,
                DialogueTriggerType.LevelComplete,
                0,
                () => SetStatus("LevelComplete dialogue finished. No settlement panel was found, so scene loading was not automated."),
                true);
            SetStatus(string.Format("Triggered LevelComplete dialogue for level {0}.", levelNumber));
            return;
        }

        SetStatus("Cannot trigger level victory. SettlementPanelUI and DialogueManager are both missing.");
    }

    public void DefeatBoboAi()
    {
        bool success = BoboBattleService.DebugDefeatCurrentAi();
        SetStatus(success ? "Bobo minigame AI defeated." : "No active Bobo minigame panel was found.");
    }

    public void DefeatBoboPlayer()
    {
        bool success = BoboBattleService.DebugDefeatCurrentPlayer();
        SetStatus(success ? "Bobo minigame player defeated." : "No active Bobo minigame panel was found.");
    }

    private void BindButtons()
    {
        if (loadSelectedSceneButton != null)
        {
            loadSelectedSceneButton.onClick.RemoveListener(LoadSelectedScene);
            loadSelectedSceneButton.onClick.AddListener(LoadSelectedScene);
        }

        if (refreshSceneListButton != null)
        {
            refreshSceneListButton.onClick.RemoveListener(RebuildSceneDropdown);
            refreshSceneListButton.onClick.AddListener(RebuildSceneDropdown);
        }

        if (triggerVictoryButton != null)
        {
            triggerVictoryButton.onClick.RemoveListener(TriggerCurrentLevelVictory);
            triggerVictoryButton.onClick.AddListener(TriggerCurrentLevelVictory);
        }

        if (defeatBoboAiButton != null)
        {
            defeatBoboAiButton.onClick.RemoveListener(DefeatBoboAi);
            defeatBoboAiButton.onClick.AddListener(DefeatBoboAi);
        }

        if (defeatBoboPlayerButton != null)
        {
            defeatBoboPlayerButton.onClick.RemoveListener(DefeatBoboPlayer);
            defeatBoboPlayerButton.onClick.AddListener(DefeatBoboPlayer);
        }

        if (sceneDropdown != null)
        {
            sceneDropdown.onValueChanged.RemoveListener(HandleSceneDropdownChanged);
            sceneDropdown.onValueChanged.AddListener(HandleSceneDropdownChanged);
        }

        if (confirmSceneSwitchButton != null)
        {
            confirmSceneSwitchButton.onClick.RemoveListener(ConfirmPendingSceneSwitch);
            confirmSceneSwitchButton.onClick.AddListener(ConfirmPendingSceneSwitch);
        }

        if (cancelSceneSwitchButton != null)
        {
            cancelSceneSwitchButton.onClick.RemoveListener(CancelPendingSceneSwitch);
            cancelSceneSwitchButton.onClick.AddListener(CancelPendingSceneSwitch);
        }
    }

    private void UnbindButtons()
    {
        if (loadSelectedSceneButton != null)
        {
            loadSelectedSceneButton.onClick.RemoveListener(LoadSelectedScene);
        }

        if (refreshSceneListButton != null)
        {
            refreshSceneListButton.onClick.RemoveListener(RebuildSceneDropdown);
        }

        if (triggerVictoryButton != null)
        {
            triggerVictoryButton.onClick.RemoveListener(TriggerCurrentLevelVictory);
        }

        if (defeatBoboAiButton != null)
        {
            defeatBoboAiButton.onClick.RemoveListener(DefeatBoboAi);
        }

        if (defeatBoboPlayerButton != null)
        {
            defeatBoboPlayerButton.onClick.RemoveListener(DefeatBoboPlayer);
        }

        if (sceneDropdown != null)
        {
            sceneDropdown.onValueChanged.RemoveListener(HandleSceneDropdownChanged);
        }

        if (confirmSceneSwitchButton != null)
        {
            confirmSceneSwitchButton.onClick.RemoveListener(ConfirmPendingSceneSwitch);
        }

        if (cancelSceneSwitchButton != null)
        {
            cancelSceneSwitchButton.onClick.RemoveListener(CancelPendingSceneSwitch);
        }
    }

    private void HandleSceneDropdownChanged(int value)
    {
        if (suppressSceneDropdownEvent)
        {
            return;
        }

        if (value == previousSceneIndex)
        {
            selectedSceneIndex = value;
            return;
        }

        if (value < 0 || value >= runtimeSceneOptions.Count)
        {
            RevertSceneDropdownSelection();
            return;
        }

        pendingSceneIndex = value;
        ShowSceneSwitchConfirm(runtimeSceneOptions[value]);
    }

    private void ConfirmPendingSceneSwitch()
    {
        if (pendingSceneIndex < 0 || pendingSceneIndex >= runtimeSceneOptions.Count)
        {
            CancelPendingSceneSwitch();
            return;
        }

        selectedSceneIndex = pendingSceneIndex;
        previousSceneIndex = pendingSceneIndex;
        pendingSceneIndex = -1;
        HideSceneSwitchConfirm();

        if (invokeLegacyLoadButtonOnConfirm && loadSelectedSceneButton != null)
        {
            loadSelectedSceneButton.onClick.Invoke();
        }
        else
        {
            LoadSelectedScene();
        }
    }

    private void CancelPendingSceneSwitch()
    {
        pendingSceneIndex = -1;
        HideSceneSwitchConfirm();
        RevertSceneDropdownSelection();
    }

    private void RevertSceneDropdownSelection()
    {
        selectedSceneIndex = previousSceneIndex;
        if (sceneDropdown == null)
        {
            return;
        }

        suppressSceneDropdownEvent = true;
        sceneDropdown.SetValueWithoutNotify(previousSceneIndex);
        sceneDropdown.RefreshShownValue();
        suppressSceneDropdownEvent = false;
    }

    private void ShowSceneSwitchConfirm(GameSceneSO targetScene)
    {
        EnsureSceneSwitchConfirmPopup();
        string sceneName = targetScene != null ? targetScene.name : "<Missing Scene>";
        if (sceneSwitchConfirmText != null)
        {
            sceneSwitchConfirmText.text = string.Format(sceneSwitchConfirmTemplate, sceneName);
        }

        if (sceneSwitchConfirmRoot != null)
        {
            sceneSwitchConfirmRoot.SetActive(true);
        }

        SetStatus("Scene switch confirmation requested: " + sceneName);
    }

    private void HideSceneSwitchConfirm()
    {
        if (sceneSwitchConfirmRoot != null)
        {
            sceneSwitchConfirmRoot.SetActive(false);
        }
    }

    private void EnsureSceneSwitchConfirmPopup()
    {
        if (sceneSwitchConfirmRoot != null &&
            sceneSwitchConfirmText != null &&
            confirmSceneSwitchButton != null &&
            cancelSceneSwitchButton != null)
        {
            return;
        }

        Transform parent = panelRoot != null ? panelRoot.transform : transform;
        GameObject root = new GameObject("SceneSwitchConfirmPopup", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(parent, false);

        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0.5f);
        rootRect.anchorMax = new Vector2(0.5f, 0.5f);
        rootRect.pivot = new Vector2(0.5f, 0.5f);
        rootRect.sizeDelta = new Vector2(520f, 220f);
        rootRect.anchoredPosition = Vector2.zero;

        Image background = root.GetComponent<Image>();
        background.color = new Color(0.04f, 0.04f, 0.04f, 0.94f);

        sceneSwitchConfirmRoot = root;
        sceneSwitchConfirmText = CreatePopupText(root.transform, "Message", new Vector2(0f, 44f), new Vector2(460f, 90f), 28f);
        confirmSceneSwitchButton = CreatePopupButton(root.transform, "ConfirmButton", "Confirm", new Vector2(-110f, -62f));
        cancelSceneSwitchButton = CreatePopupButton(root.transform, "CancelButton", "Cancel", new Vector2(110f, -62f));

        confirmSceneSwitchButton.onClick.RemoveListener(ConfirmPendingSceneSwitch);
        confirmSceneSwitchButton.onClick.AddListener(ConfirmPendingSceneSwitch);
        cancelSceneSwitchButton.onClick.RemoveListener(CancelPendingSceneSwitch);
        cancelSceneSwitchButton.onClick.AddListener(CancelPendingSceneSwitch);
    }

    private TextMeshProUGUI CreatePopupText(Transform parent, string objectName, Vector2 position, Vector2 size, float fontSize)
    {
        GameObject textObject = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);

        RectTransform rectTransform = textObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = size;
        rectTransform.anchoredPosition = position;

        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        return text;
    }

    private Button CreatePopupButton(Transform parent, string objectName, string label, Vector2 position)
    {
        GameObject buttonObject = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        buttonObject.transform.SetParent(parent, false);

        RectTransform rectTransform = buttonObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
        rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.sizeDelta = new Vector2(150f, 52f);
        rectTransform.anchoredPosition = position;

        Image image = buttonObject.GetComponent<Image>();
        image.color = new Color(0.20f, 0.56f, 0.36f, 1f);

        TextMeshProUGUI text = CreatePopupText(buttonObject.transform, "Text", Vector2.zero, new Vector2(140f, 44f), 24f);
        text.text = label;

        return buttonObject.GetComponent<Button>();
    }

    private void AddSceneOption(GameSceneSO scene)
    {
        if (scene == null || runtimeSceneOptions.Contains(scene))
        {
            return;
        }

        runtimeSceneOptions.Add(scene);
    }

    private GameSceneSO GetSelectedScene()
    {
        if (sceneDropdown != null)
        {
            selectedSceneIndex = sceneDropdown.value;
        }

        if (selectedSceneIndex < 0 || selectedSceneIndex >= runtimeSceneOptions.Count)
        {
            return null;
        }

        return runtimeSceneOptions[selectedSceneIndex];
    }

    private void LoadScene(GameSceneSO scene)
    {
        if (scene == null)
        {
            return;
        }

        SceneManager sceneManager = FindObjectOfType<SceneManager>();
        if (useSafeSceneManagerPath)
        {
            if (sceneManager != null)
            {
                if (!sceneManager.IsLoading && sceneManager.CurrentScene == scene)
                {
                    SetStatus("Scene is already loaded: " + scene.name);
                    return;
                }

                sceneManager.LoadScene(scene);
                SetStatus("Loading scene through SceneManager: " + scene.name);
                return;
            }

            DeveloperSceneLoadBridge.Begin(scene, persistentSceneName);
            SetStatus("Persistent scene is missing. Loading Persistent first, then: " + scene.name);
            return;
        }

        if (loadSceneEvent != null && loadSceneEvent.OnEventRaised != null)
        {
            loadSceneEvent.RaiseEvent(scene);
            SetStatus("Loading scene: " + scene.name);
            return;
        }

        if (sceneManager != null)
        {
            sceneManager.LoadScene(scene);
            SetStatus("Loading scene through SceneManager: " + scene.name);
            return;
        }

        DeveloperSceneLoadBridge.Begin(scene, persistentSceneName);
        SetStatus("Loading Persistent fallback before target scene: " + scene.name);
    }

    private int ResolveCurrentLevelNumber()
    {
        if (levelNumberOverride >= 0)
        {
            return levelNumberOverride;
        }

        GameSceneSO currentScene = ResolveCurrentScene();
        if (TryResolveLevelNumberFromScene(currentScene, out int currentSceneLevelNumber))
        {
            return currentSceneLevelNumber;
        }

        GameSceneSO selectedScene = GetSelectedScene();
        if (TryResolveLevelNumberFromScene(selectedScene, out int selectedSceneLevelNumber))
        {
            return selectedSceneLevelNumber;
        }

        if (DialogueManager.Instance != null && DialogueManager.Instance.CurrentLevelNumber >= 0)
        {
            Debug.LogWarning(
                "[DeveloperPanelUI] Falling back to DialogueManager.CurrentLevelNumber. " +
                "This may refer to the last played dialogue, not the current loaded scene.");
            return DialogueManager.Instance.CurrentLevelNumber;
        }

        return 0;
    }

    private bool TryResolveLevelNumberFromScene(GameSceneSO scene, out int levelNumber)
    {
        levelNumber = -1;
        if (scene == null)
        {
            return false;
        }

        if (DialogueManager.Instance != null &&
            DialogueManager.Instance.TryResolveLevelNumberFromScene(scene, out levelNumber))
        {
            return true;
        }

        SceneDialogueTrigger sceneDialogueTrigger = FindObjectOfType<SceneDialogueTrigger>();
        if (sceneDialogueTrigger != null &&
            sceneDialogueTrigger.TryResolveLevelNumber(scene, out levelNumber))
        {
            return true;
        }

        return false;
    }

    private GameSceneSO ResolveNextScene()
    {
        if (nextSceneOverride != null)
        {
            return nextSceneOverride;
        }

        GameSceneSO currentScene = ResolveCurrentScene();
        if (currentScene != null && currentScene.nextLevelScene != null)
        {
            return currentScene.nextLevelScene;
        }

        GameSceneSO selectedScene = GetSelectedScene();
        return selectedScene != null ? selectedScene.nextLevelScene : null;
    }

    private GameSceneSO ResolveCurrentScene()
    {
        SceneManager sceneManager = FindObjectOfType<SceneManager>();
        return sceneManager != null ? sceneManager.CurrentScene : null;
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }

        if (verboseLog)
        {
            Debug.Log("[DeveloperPanelUI] " + message);
        }
    }

    private bool IsPanelVisible()
    {
        if (panelRoot == null)
        {
            return false;
        }

        if (hidePanelWithCanvasGroup && panelCanvasGroup != null)
        {
            return panelCanvasGroup.alpha > 0.5f;
        }

        return panelRoot.activeSelf;
    }

    private void SetPanelVisible(bool visible)
    {
        if (panelRoot == null)
        {
            return;
        }

        if (visible)
        {
            PreparePanelForInteraction();
        }

        if (hidePanelWithCanvasGroup)
        {
            panelRoot.SetActive(true);
            panelCanvasGroup.alpha = visible ? 1f : 0f;
            panelCanvasGroup.interactable = visible;
            panelCanvasGroup.blocksRaycasts = visible;
            panelCanvasGroup.ignoreParentGroups = true;
            return;
        }

        panelRoot.SetActive(visible);
        if (visible)
        {
            PreparePanelForInteraction();
        }
    }

    private void EnsurePanelInputSurface()
    {
        if (panelRoot == null)
        {
            return;
        }

        panelCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            panelCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        panelCanvasGroup.ignoreParentGroups = true;

        panelCanvas = panelRoot.GetComponent<Canvas>();
        if (panelCanvas == null)
        {
            panelCanvas = panelRoot.AddComponent<Canvas>();
        }

        panelCanvas.overrideSorting = true;
        panelCanvas.sortingOrder = topmostSortingOrder;

        panelGraphicRaycaster = panelRoot.GetComponent<GraphicRaycaster>();
        if (panelGraphicRaycaster == null)
        {
            panelGraphicRaycaster = panelRoot.AddComponent<GraphicRaycaster>();
        }

        panelGraphicRaycaster.enabled = true;
        panelGraphicRaycaster.ignoreReversedGraphics = true;
    }

    private void PreparePanelForInteraction()
    {
        EnsureEventSystem();
        EnsurePanelInputSurface();

        if (forceTopmostOnShow && panelRoot.transform.parent != null)
        {
            panelRoot.transform.SetAsLastSibling();
        }

        if (panelCanvas != null)
        {
            panelCanvas.overrideSorting = true;
            panelCanvas.sortingOrder = topmostSortingOrder;
        }

        if (panelGraphicRaycaster != null)
        {
            panelGraphicRaycaster.enabled = true;
        }

        if (panelCanvasGroup != null)
        {
            panelCanvasGroup.alpha = 1f;
            panelCanvasGroup.interactable = true;
            panelCanvasGroup.blocksRaycasts = true;
            panelCanvasGroup.ignoreParentGroups = true;
        }
    }

    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null)
        {
            return;
        }

        GameObject eventSystemObject = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        DontDestroyOnLoad(eventSystemObject);
    }
}

public class DeveloperSceneLoadBridge : MonoBehaviour
{
    private const float ManagerWaitTimeout = 8f;
    private const float SceneIdleWaitTimeout = 15f;

    private GameSceneSO targetScene;
    private string persistentSceneName;

    public static void Begin(GameSceneSO targetScene, string persistentSceneName)
    {
        if (targetScene == null)
        {
            Debug.LogWarning("[DeveloperSceneLoadBridge] Target scene is null.");
            return;
        }

        GameObject bridgeObject = new GameObject("DeveloperSceneLoadBridge");
        DontDestroyOnLoad(bridgeObject);

        DeveloperSceneLoadBridge bridge = bridgeObject.AddComponent<DeveloperSceneLoadBridge>();
        bridge.targetScene = targetScene;
        bridge.persistentSceneName = string.IsNullOrWhiteSpace(persistentSceneName) ? "Persistent" : persistentSceneName;
        bridge.StartCoroutine(bridge.Run());
    }

    private IEnumerator Run()
    {
        UnityEngine.SceneManagement.Scene persistentScene = UnityEngine.SceneManagement.SceneManager.GetSceneByName(persistentSceneName);
        if (!persistentScene.IsValid() || !persistentScene.isLoaded)
        {
            AsyncOperation loadPersistent = UnityEngine.SceneManagement.SceneManager.LoadSceneAsync(
                persistentSceneName,
                UnityEngine.SceneManagement.LoadSceneMode.Single);

            if (loadPersistent == null)
            {
                Debug.LogError("[DeveloperSceneLoadBridge] Failed to start loading Persistent scene: " + persistentSceneName);
                Destroy(gameObject);
                yield break;
            }

            yield return loadPersistent;
        }

        yield return null;
        yield return null;

        SceneManager sceneManager = null;
        float managerDeadline = Time.realtimeSinceStartup + ManagerWaitTimeout;
        while (sceneManager == null && Time.realtimeSinceStartup < managerDeadline)
        {
            sceneManager = FindObjectOfType<SceneManager>();
            if (sceneManager == null)
            {
                yield return null;
            }
        }

        if (sceneManager == null)
        {
            Debug.LogError("[DeveloperSceneLoadBridge] Persistent loaded, but SceneManager was not found.");
            Destroy(gameObject);
            yield break;
        }

        float idleDeadline = Time.realtimeSinceStartup + SceneIdleWaitTimeout;
        while (sceneManager.IsLoading && Time.realtimeSinceStartup < idleDeadline)
        {
            yield return null;
        }

        if (sceneManager.IsLoading)
        {
            Debug.LogError("[DeveloperSceneLoadBridge] SceneManager is still loading. Target scene request skipped: " + targetScene.name);
            Destroy(gameObject);
            yield break;
        }

        if (sceneManager.CurrentScene == targetScene)
        {
            Debug.Log("[DeveloperSceneLoadBridge] Target scene is already loaded: " + targetScene.name);
            Destroy(gameObject);
            yield break;
        }

        sceneManager.LoadScene(targetScene);
        Debug.Log("[DeveloperSceneLoadBridge] Loading target scene after Persistent bootstrap: " + targetScene.name);
        Destroy(gameObject);
    }
}
