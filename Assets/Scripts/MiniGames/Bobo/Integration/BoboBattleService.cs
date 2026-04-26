using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 波波攒小游戏模块的服务入口。
/// 负责：
/// 1. 确保存在可用的宿主 Canvas / Popup 层；
/// 2. 懒加载小游戏预制件；
/// 3. 对外暴露统一的 Open / Close 接口。
/// 这样剧情系统、调试入口、按钮入口都不需要关心 UI 是怎么实例化的。
/// </summary>
public class BoboBattleService : MonoBehaviour
{
    private const string PanelPrefabResourcePath = "BoboBattle/BoboBattlePanel";

    private static BoboBattleService instance;

    private BoboBattlePanel panel;

    /// <summary>
    /// 打开一场小游戏。
    /// </summary>
    public static bool Open(BoboBattleRequest request)
    {
        return EnsureInstance().OpenInternal(request);
    }

    public static bool IsCurrentBattleOpen()
    {
        return instance != null && instance.panel != null && instance.panel.IsVisible;
    }

    /// <summary>
    /// 以“主动取消”的方式关闭当前小游戏。
    /// 给对话系统或其他外层流程在中断时使用。
    /// </summary>
    public static void CloseCurrentAsCancelled()
    {
        TryCloseCurrentAsCancelled(false);
    }

    public static bool TryCloseCurrentAsCancelled(bool force = false)
    {
        if (instance == null || instance.panel == null)
        {
            return false;
        }

        return instance.panel.TryCloseAsCancelled(force);
    }

    public static bool ForceHideCurrentWithoutCallback()
    {
        if (instance == null || instance.panel == null)
        {
            return false;
        }

        instance.panel.ForceHideWithoutCallback();
        return true;
    }

    public static bool DebugDefeatCurrentAi()
    {
        if (instance == null || instance.panel == null || !instance.panel.IsVisible)
        {
            return false;
        }

        return instance.panel.DebugCompleteAsPlayerWin();
    }

    public static bool DebugDefeatCurrentPlayer()
    {
        if (instance == null || instance.panel == null || !instance.panel.IsVisible)
        {
            return false;
        }

        return instance.panel.DebugCompleteAsAiWin();
    }

    /// <summary>
    /// 保证服务以单例形式存在，并跨场景存活。
    /// </summary>
    private static BoboBattleService EnsureInstance()
    {
        if (instance != null)
        {
            return instance;
        }

        instance = FindObjectOfType<BoboBattleService>();
        if (instance != null)
        {
            return instance;
        }

        GameObject serviceObject = new GameObject("BoboBattleService");
        instance = serviceObject.AddComponent<BoboBattleService>();
        DontDestroyOnLoad(serviceObject);
        return instance;
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            return;
        }

        if (instance != this)
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// 打开面板前统一完成依赖准备。
    /// </summary>
    private bool OpenInternal(BoboBattleRequest request)
    {
        EnsureEventSystem();
        if (!EnsurePanel())
        {
            return false;
        }

        if (panel != null && panel.IsVisible)
        {
            return false;
        }

        panel.Show(request ?? new BoboBattleRequest());
        return true;
    }

    /// <summary>
    /// 懒加载小游戏面板预制件。
    /// 约定资源路径为：
    /// Assets/Resources/BoboBattle/BoboBattlePanel.prefab
    /// </summary>
    private bool EnsurePanel()
    {
        if (panel != null)
        {
            return true;
        }

        Transform parent = ResolvePopupParent();
        BoboBattlePanel panelPrefab = Resources.Load<BoboBattlePanel>(PanelPrefabResourcePath);
        if (panelPrefab == null)
        {
            Debug.LogError("[BoboBattleService] 未找到波波攒面板预制件，请确认路径为 Assets/Resources/BoboBattle/BoboBattlePanel.prefab。");
            return false;
        }

        panel = Instantiate(panelPrefab, parent, false);
        panel.name = "BoboBattlePanel";

        RectTransform panelRect = panel.transform as RectTransform;
        if (panelRect != null)
        {
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            panelRect.localScale = Vector3.one;
            panelRect.localRotation = Quaternion.identity;
        }

        CanvasGroup canvasGroup = panel.GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
        }

        if (!panel.Initialize(canvasGroup))
        {
            Debug.LogError("[BoboBattleService] 波波攒面板初始化失败，请检查预制件引用是否完整。", panel);
            return false;
        }

        return true;
    }

    /// <summary>
    /// 优先复用项目已有的 PopupCanvas。
    /// 如果没有，再退化到任意 Canvas，最后才自己创建兜底 Canvas。
    /// </summary>
    private Transform ResolvePopupParent()
    {
        GameObject popupCanvas = GameObject.Find("PopupCanvas");
        if (popupCanvas != null)
        {
            return popupCanvas.transform;
        }

        Canvas existingCanvas = FindObjectOfType<Canvas>();
        if (existingCanvas != null)
        {
            return existingCanvas.transform;
        }

        GameObject fallbackCanvas = new GameObject("PopupCanvas_Fallback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(fallbackCanvas);

        Canvas canvas = fallbackCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 3000;

        CanvasScaler scaler = fallbackCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        RectTransform rectTransform = fallbackCanvas.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        return fallbackCanvas.transform;
    }

    /// <summary>
    /// 确保当前场景存在 EventSystem。
    /// 因为小游戏是运行时弹出，不能假设每个场景都预先配好了输入系统。
    /// </summary>
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
