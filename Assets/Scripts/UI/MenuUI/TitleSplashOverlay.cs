using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.UI;

/// <summary>
/// 标题界面开场遮罩：
/// 1) 开头纯黑停留 initialBlackHoldDuration 秒
/// 2) 立刻播放第一段视频；视频结束后显示默认标题图
/// 3) 再次点击任意位置后播放第二段视频；视频结束后显示“标题选择图”
/// </summary>
public class TitleSplashOverlay : MonoBehaviour
{
    [Header("引用")]
    [Tooltip("遮罩根节点（用于阻挡点击）。播放视频/显示图片期间保持激活")]
    [SerializeField] private GameObject overlayRoot;

    [Tooltip("纯黑遮罩（可选，用于开头黑屏）")]
    [SerializeField] private CanvasGroup blackGroup;

    [Tooltip("用于显示视频画面的 RawImage")]
    [SerializeField] private RawImage videoImage;

    [Tooltip("用于播放视频的 VideoPlayer")]
    [SerializeField] private VideoPlayer videoPlayer;

    [Tooltip("用于播放音效的 AudioSource（一次性音效将通过 PlayOneShot 播放）")]
    [SerializeField] private AudioSource sfxSource;

    [Tooltip("第一段开头动效视频")]
    [SerializeField] private VideoClip introVideo;

    [Tooltip("第二段场景切换视频")]
    [SerializeField] private VideoClip transitionVideo;

    [Tooltip("第一段视频对应的一次性音效")]
    [SerializeField] private AudioClip introSfx;

    [Tooltip("第二段视频对应的一次性音效")]
    [SerializeField] private AudioClip transitionSfx;

    [Tooltip("第一段视频结束后显示的默认标题图片")]
    [SerializeField] private GameObject defaultTitleImage;

    [Tooltip("第二段视频结束后显示的标题选择图片")]
    [SerializeField] private GameObject selectTitleImage;

    [Tooltip("标题菜单面板；第二段视频结束并显示 SelectTitleImage 后解锁其按钮")]
    [SerializeField] private UIMenu menuPanel;

    [Header("参数")]
    [Tooltip("开头纯黑停留时间（秒）：完全黑屏停留结束后才开始进入渐变流程")]
    [SerializeField] private float initialBlackHoldDuration = 4f;

    private enum State
    {
        HoldingBlack,
        PlayingIntroVideo,
        WaitingForSecondInput,
        PlayingTransitionVideo,
        Finished
    }

    private State _state;
    private Coroutine _sequenceCoroutine;
    private bool _videoFinished;

    private void Awake()
    {
        if (sfxSource == null)
        {
            sfxSource = GetComponent<AudioSource>();
        }

        if (blackGroup == null)
        {
            // 自动寻找“纯黑层”的 CanvasGroup
            var groups = GetComponentsInChildren<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                var g = groups[i];
                if (g != null)
                {
                    blackGroup = g;
                    break;
                }
            }
        }

        if (overlayRoot == null)
        {
            overlayRoot = gameObject;
        }

        if (menuPanel == null)
        {
            menuPanel = FindObjectOfType<UIMenu>();
        }

        if (blackGroup != null)
        {
            blackGroup.alpha = 1f;
            blackGroup.blocksRaycasts = true;
            blackGroup.interactable = true;
        }

        if (videoImage != null)
        {
            videoImage.gameObject.SetActive(false);
        }

        if (defaultTitleImage != null)
        {
            defaultTitleImage.SetActive(false);
        }

        if (selectTitleImage != null)
        {
            selectTitleImage.SetActive(false);
        }

        _state = State.HoldingBlack;

        if (_sequenceCoroutine != null)
        {
            StopCoroutine(_sequenceCoroutine);
        }
        _sequenceCoroutine = StartCoroutine(BeginSequence());
    }

    private IEnumerator BeginSequence()
    {
        if (initialBlackHoldDuration > 0f)
        {
            yield return new WaitForSeconds(initialBlackHoldDuration);
        }

        yield return PlayVideo(introVideo, introSfx, State.PlayingIntroVideo);

        ShowDefaultTitleImage();
        _state = State.WaitingForSecondInput;
    }

    private void Update()
    {
        if (_state != State.WaitingForSecondInput)
        {
            return;
        }

        if (Input.anyKeyDown || Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            _state = State.PlayingTransitionVideo;
            StartCoroutine(PlayTransitionAndShowSelect());
        }
    }

    private IEnumerator PlayTransitionAndShowSelect()
    {
        HideAllImages();
        yield return PlayVideo(transitionVideo, transitionSfx, State.PlayingTransitionVideo);
        ShowSelectTitleImage();
        _state = State.Finished;
    }

    private IEnumerator PlayVideo(VideoClip clip, AudioClip sfxClip, State playingState)
    {
        if (videoPlayer == null || videoImage == null || clip == null)
        {
            yield break;
        }

        _state = playingState;

        if (blackGroup != null)
        {
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = true;
            blackGroup.interactable = true;
        }

        videoImage.gameObject.SetActive(true);
        videoImage.raycastTarget = false;

        videoPlayer.Stop();
        videoPlayer.clip = clip;
        videoPlayer.isLooping = false;
        videoPlayer.waitForFirstFrame = true;

        _videoFinished = false;
        videoPlayer.loopPointReached += HandleVideoFinished;

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        if (sfxSource != null && sfxClip != null)
        {
            sfxSource.PlayOneShot(sfxClip);
        }

        videoPlayer.Play();

        while (!_videoFinished)
        {
            yield return null;
        }

        videoPlayer.loopPointReached -= HandleVideoFinished;

        videoImage.gameObject.SetActive(false);
    }

    private void HandleVideoFinished(VideoPlayer source)
    {
        _videoFinished = true;
    }

    private void HideAllImages()
    {
        if (defaultTitleImage != null)
        {
            defaultTitleImage.SetActive(false);
        }

        if (selectTitleImage != null)
        {
            selectTitleImage.SetActive(false);
        }
    }

    private void ShowDefaultTitleImage()
    {
        HideAllImages();

        if (defaultTitleImage != null)
        {
            defaultTitleImage.SetActive(true);
        }

        if (blackGroup != null)
        {
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = true;
            blackGroup.interactable = true;
        }
    }

    private void ShowSelectTitleImage()
    {
        HideAllImages();

        if (selectTitleImage != null)
        {
            selectTitleImage.SetActive(true);
        }

        if (blackGroup != null)
        {
            blackGroup.alpha = 0f;
            blackGroup.blocksRaycasts = false;
            blackGroup.interactable = false;
        }

        if (overlayRoot != null && overlayRoot != gameObject)
        {
            overlayRoot.SetActive(false);
        }
        else
        {
            gameObject.SetActive(false);
        }

        menuPanel?.SetMenuInteractable(true);
    }
}
