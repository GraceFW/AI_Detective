using System.Collections;
using UnityEngine;

/// <summary>
/// 标题界面开场遮罩：
/// 1) 先显示一张全屏图片（本物体上的 CanvasGroup）
/// 2) 玩家按任意键/鼠标点击后，图片渐隐
/// 3) 渐隐完成后显示标题界面（titleRoot）
/// </summary>
public class TitleSplashOverlay : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("遮罩图的 CanvasGroup（建议就挂在本物体上）")]
    [SerializeField] private CanvasGroup splashGroup;

    [Tooltip("纯黑遮罩的 CanvasGroup（用于从黑到图、从图到黑的过渡）")]
    [SerializeField] private CanvasGroup blackGroup;

    [Tooltip("标题界面根节点（包含开始按钮等）。遮罩显示期间会隐藏它")]
    [SerializeField] private GameObject titleRoot;

    [Header("参数")]
    [Tooltip("开头纯黑停留时间（秒）：完全黑屏停留结束后才开始进入渐变流程")]
    [SerializeField] private float initialBlackHoldDuration = 3f;

    [Tooltip("启动时：从纯黑渐变显示图片（秒）")]
    [SerializeField] private float fadeInFromBlackDuration = 0.6f;

    [Tooltip("按键后：图片渐变回纯黑（秒）")]
    [SerializeField] private float fadeOutToBlackDuration = 0.6f;

    [Tooltip("图片结束后：标题从纯黑渐变出现（秒）")]
    [SerializeField] private float titleFadeInFromBlackDuration = 0.6f;

    [Tooltip("是否允许点击/按键跳过渐隐（false=必须走渐隐）")]
    [SerializeField] private bool skipFadeOnSecondPress = false;

    private bool _started;
    private bool _fading;

    private enum State
    {
        FadingInFromBlack,
        WaitingForInput,
        FadingOutToBlack,
        TitleFadingIn,
        Finished
    }

    private State _state;

    private void Awake()
    {
        if (splashGroup == null)
        {
            splashGroup = GetComponent<CanvasGroup>();
        }

        if (blackGroup == null)
        {
            // 自动寻找“纯黑层”的 CanvasGroup：必须与 splashGroup 不同
            var groups = GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g != null && g != splashGroup)
                {
                    blackGroup = g;
                    break;
                }
            }
        }

        // 初始：遮罩可见并阻挡点击，标题界面隐藏
        if (splashGroup != null)
        {
            splashGroup.alpha = 1f;
            splashGroup.blocksRaycasts = false;
            splashGroup.interactable = false;
        }

        if (blackGroup != null)
        {
            blackGroup.alpha = 1f;
            blackGroup.blocksRaycasts = true;
            blackGroup.interactable = true;
        }

        if (titleRoot != null)
        {
            titleRoot.SetActive(false);
        }

        _started = false;
        _fading = false;

        _state = State.FadingInFromBlack;
        StartCoroutine(BeginSequence());
    }

    private IEnumerator BeginSequence()
    {
        // 先保持完全黑屏一段时间
        if (initialBlackHoldDuration > 0f)
        {
            yield return new WaitForSeconds(initialBlackHoldDuration);
        }

        // 再从纯黑淡出，显示图片
        yield return FadeBlackTo(0f, fadeInFromBlackDuration, State.WaitingForInput);
    }

    private void Update()
    {
        if (_fading)
        {
            // 可选：二次按键直接跳过
            if (skipFadeOnSecondPress && (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
            {
                Finish();
            }
            return;
        }

        if (_started)
        {
            return;
        }

        if (_state != State.WaitingForInput)
        {
            return;
        }

        // “任意按键/鼠标点击”
        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            _started = true;
            StartCoroutine(FadeOutSplashToBlackAndShowTitle());
        }
    }

    private IEnumerator FadeBlackTo(float targetAlpha, float duration, State nextState)
    {
        _fading = true;

        if (blackGroup == null)
        {
            _state = nextState;
            _fading = false;
            yield break;
        }

        if (duration <= 0f)
        {
            blackGroup.alpha = targetAlpha;
            _state = nextState;
            _fading = false;
            yield break;
        }

        float startAlpha = blackGroup.alpha;
        float t = 0f;

        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            blackGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, p);
            yield return null;
        }

        blackGroup.alpha = targetAlpha;
        _state = nextState;
        _fading = false;
    }

    private IEnumerator FadeOutSplashToBlackAndShowTitle()
    {
        _state = State.FadingOutToBlack;

        // 先回到纯黑
        yield return FadeBlackTo(1f, fadeOutToBlackDuration, State.TitleFadingIn);

        // 到纯黑后，切换显示对象
        if (splashGroup != null)
        {
            splashGroup.alpha = 0f;
            splashGroup.blocksRaycasts = false;
            splashGroup.interactable = false;
        }

        if (titleRoot != null)
        {
            titleRoot.SetActive(true);
        }

        // 再从纯黑淡出，显示标题
        yield return FadeBlackTo(0f, titleFadeInFromBlackDuration, State.Finished);

        // 淡出完毕，允许点击标题界面
        if (blackGroup != null)
        {
            blackGroup.blocksRaycasts = false;
            blackGroup.interactable = false;
        }

        gameObject.SetActive(false);
        _started = false;
        _state = State.Finished;
    }

    private IEnumerator FadeOutAndShowTitle()
    {
        _fading = true;

        if (fadeOutToBlackDuration <= 0f || splashGroup == null)
        {
            Finish();
            yield break;
        }

        float startAlpha = splashGroup.alpha;
        float t = 0f;

        while (t < fadeOutToBlackDuration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / fadeOutToBlackDuration);
            splashGroup.alpha = Mathf.Lerp(startAlpha, 0f, p);
            yield return null;
        }

        Finish();
    }

    private void Finish()
    {
        if (titleRoot != null)
        {
            titleRoot.SetActive(true);
        }

        if (splashGroup != null)
        {
            splashGroup.alpha = 0f;
            splashGroup.blocksRaycasts = false;
            splashGroup.interactable = false;
        }

        if (blackGroup != null)
        {
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = false;
            blackGroup.interactable = false;
        }

        // 直接隐藏遮罩（也可以 Destroy）
        gameObject.SetActive(false);
        _fading = false;
    }
}
