using System.Collections;
using DG.Tweening;
using UnityEngine;

public class BGMManager : MonoBehaviour
{
    [Header("事件")]
    [SerializeField] private GameSceneEventSO sceneLoadedEvent;
    [SerializeField] private BGMTrackEventSO bgmRequestEvent;

    [Header("映射")]
    [SerializeField] private SceneBGMMapSO sceneBgmMap;

    [Header("音频")]
    [SerializeField] private AudioSource audioSource;
    [Range(0f, 1f)] [SerializeField] private float masterVolume = 1f;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration = 0.5f;

    private Coroutine _playRoutine;
    private BGMTrackSO _current;

    private void Awake()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.loop = true;
    }

    private void OnEnable()
    {
        if (sceneLoadedEvent != null)
        {
            sceneLoadedEvent.OnEventRaised += HandleSceneLoaded;
        }

        if (bgmRequestEvent != null)
        {
            bgmRequestEvent.OnEventRaised += HandleBgmRequested;
        }
    }

    private void OnDisable()
    {
        if (sceneLoadedEvent != null)
        {
            sceneLoadedEvent.OnEventRaised -= HandleSceneLoaded;
        }

        if (bgmRequestEvent != null)
        {
            bgmRequestEvent.OnEventRaised -= HandleBgmRequested;
        }
    }

    private void HandleSceneLoaded(GameSceneSO scene)
    {
        if (sceneBgmMap == null)
        {
            return;
        }

        if (sceneBgmMap.TryGetTrack(scene, out var track))
        {
            Play(track);
        }
    }

    private void HandleBgmRequested(BGMTrackSO track)
    {
        Play(track);
    }

    public void Play(BGMTrackSO track)
    {
        if (track == null || track.clip == null)
        {
            StopBgm();
            return;
        }

        if (_current != null && _current.clip == track.clip)
        {
            return;
        }

        _current = track;

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
        }

        _playRoutine = StartCoroutine(PlayRoutine(track));
    }

    public void StopBgm()
    {
        _current = null;

        if (_playRoutine != null)
        {
            StopCoroutine(_playRoutine);
            _playRoutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }

    private IEnumerator PlayRoutine(BGMTrackSO track)
    {
        if (audioSource == null)
        {
            yield break;
        }

        audioSource.DOKill();

        if (audioSource.isPlaying && fadeOutDuration > 0f)
        {
            yield return audioSource.DOFade(0f, fadeOutDuration).SetEase(Ease.Linear).WaitForCompletion();
        }

        audioSource.clip = track.clip;
        audioSource.loop = track.loop;
        audioSource.volume = 0f;
        audioSource.Play();

        var targetVolume = Mathf.Clamp01(track.volume * masterVolume);
        if (fadeInDuration > 0f)
        {
            yield return audioSource.DOFade(targetVolume, fadeInDuration).SetEase(Ease.Linear).WaitForCompletion();
        }
        else
        {
            audioSource.volume = targetVolume;
        }

        _playRoutine = null;
    }
}
