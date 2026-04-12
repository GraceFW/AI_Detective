using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 小游戏模块的服务入口。
/// 它负责确保运行时有可用的宿主对象、弹窗节点和 EventSystem，
/// 对外只暴露 Open / Close 这类简单接口，方便剧情系统或其他模块直接调用。
/// </summary>
public class BoboBattleService : MonoBehaviour
{
    private static BoboBattleService instance;

    private BoboBattlePanel panel;

    /// <summary>
    /// 打开一场小游戏。
    /// </summary>
    public static bool Open(BoboBattleRequest request)
    {
        return EnsureInstance().OpenInternal(request);
    }

    /// <summary>
    /// 以“主动取消”的方式关闭当前小游戏。
    /// 给对话系统或其他外层流程在中断时使用。
    /// </summary>
    public static void CloseCurrentAsCancelled()
    {
        if (instance != null && instance.panel != null)
        {
            instance.panel.CloseAsCancelled();
        }
    }

    /// <summary>
    /// 保证服务是单例存在的，并且跨场景存活。
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
        // 标准的运行时单例防重逻辑。
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
        EnsurePanel();

        // 当前小游戏已打开时不重复弹同一个面板，避免多层 UI 叠加。
        if (panel != null && panel.IsVisible)
        {
            return false;
        }

        panel.Show(request ?? new BoboBattleRequest());
        return true;
    }

    /// <summary>
    /// 懒创建小游戏面板。
    /// 这样首次进入相关剧情节点时才会生成 UI，平时不占场景层级。
    /// </summary>
    private void EnsurePanel()
    {
        if (panel != null)
        {
            return;
        }

        Transform parent = ResolvePopupParent();
        GameObject panelObject = new GameObject("BoboBattlePanelRoot", typeof(RectTransform), typeof(CanvasGroup));
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.SetParent(parent, false);
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        CanvasGroup canvasGroup = panelObject.GetComponent<CanvasGroup>();
        panel = panelObject.AddComponent<BoboBattlePanel>();
        panel.Initialize(canvasGroup);
    }

    /// <summary>
    /// 优先复用现有项目里的 PopupCanvas。
    /// 如果项目里没有对应节点，再退化到任何可用 Canvas，最后才自己兜底创建。
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
    /// 确保当前场景中存在 EventSystem。
    /// 因为小游戏是运行时创建的弹窗，不能假定每个场景都已经配好了输入系统。
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
