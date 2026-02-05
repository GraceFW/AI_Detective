using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// 音效管理器（单例）
/// 负责播放所有 SFX 音效，管理 OneShot 池和 Loop 音效
/// </summary>
public class SfxManager : MonoBehaviour
{
    public static SfxManager Instance { get; private set; }

    [Header("配置")]
    [Tooltip("音效配置库")]
    [SerializeField] private SfxLibrary library;

    [Tooltip("AudioMixer 的 SFX 组")]
    [SerializeField] private AudioMixerGroup sfxMixerGroup;

    [Header("OneShot 池设置")]
    [Tooltip("OneShot 音频源池大小")]
    [Range(8, 32)]
    [SerializeField] private int oneShotPoolSize = 16;

    // OneShot 音频源池
    private Queue<AudioSource> _oneShotPool = new Queue<AudioSource>();
    private List<AudioSource> _activeOneShots = new List<AudioSource>();

    // Loop 音效管理：Dictionary<(SfxId, ownerKey), AudioSource>
    private Dictionary<(SfxId id, object ownerKey), AudioSource> _loopMap = new Dictionary<(SfxId, object), AudioSource>();

    // 冷却时间记录：Dictionary<SfxId, lastPlayTime>
    private Dictionary<SfxId, float> _lastPlayTime = new Dictionary<SfxId, float>();

    // 当前播放计数：Dictionary<SfxId, count>
    private Dictionary<SfxId, int> _currentPlayCount = new Dictionary<SfxId, int>();

    private void Awake()
    {
        // 单例模式
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializePool();
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    /// <summary>
    /// 初始化 OneShot 音频源池
    /// </summary>
    private void InitializePool()
    {
        if (library == null)
        {
            Debug.LogError("[SfxManager] library 未配置，请在 Inspector 中绑定 SfxLibrary");
            return;
        }

        // 创建 OneShot 池
        for (int i = 0; i < oneShotPoolSize; i++)
        {
            var audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.outputAudioMixerGroup = sfxMixerGroup;
            _oneShotPool.Enqueue(audioSource);
        }

        Debug.Log($"[SfxManager] 已初始化 OneShot 池，大小: {oneShotPoolSize}");
    }

    /// <summary>
    /// 播放一次性音效
    /// </summary>
    /// <param name="id">音效 ID</param>
    public void Play(SfxId id)
    {
        if (library == null)
        {
            Debug.LogWarning("[SfxManager] library 未配置");
            return;
        }

        var entry = library.Get(id);
        if (entry == null || entry.clip == null)
        {
            return;
        }

        // 检查冷却时间
        if (entry.cooldown > 0f)
        {
            if (_lastPlayTime.TryGetValue(id, out var lastTime))
            {
                if (Time.time - lastTime < entry.cooldown)
                {
                    // 冷却中，拒绝播放
                    return;
                }
            }
        }

        // 检查并发限制
        int currentCount = _currentPlayCount.GetValueOrDefault(id, 0);
        if (currentCount >= entry.maxSimultaneous)
        {
            // 超过并发限制，拒绝播放
            return;
        }

        // 更新播放计数
        _currentPlayCount[id] = currentCount + 1;

        // 更新冷却时间
        _lastPlayTime[id] = Time.time;

        // 从池中获取 AudioSource
        AudioSource audioSource = null;
        if (_oneShotPool.Count > 0)
        {
            audioSource = _oneShotPool.Dequeue();
        }
        else
        {
            // 池已空，创建新的（理论上不应该发生）
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.outputAudioMixerGroup = sfxMixerGroup;
            Debug.LogWarning("[SfxManager] OneShot 池已空，创建新的 AudioSource");
        }

        // 配置并播放
        audioSource.clip = entry.clip;
        audioSource.volume = entry.volume;
        audioSource.loop = false;
        audioSource.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
        audioSource.outputAudioMixerGroup = sfxMixerGroup;

        _activeOneShots.Add(audioSource);
        audioSource.Play();

        // 协程：播放完成后回收
        StartCoroutine(ReturnToPoolWhenFinished(audioSource, id));
    }

    /// <summary>
    /// 播放循环音效
    /// </summary>
    /// <param name="id">音效 ID</param>
    /// <param name="ownerKey">拥有者标识（用于管理多个循环音效）</param>
    public void PlayLoop(SfxId id, object ownerKey)
    {
        if (library == null)
        {
            Debug.LogWarning("[SfxManager] library 未配置");
            return;
        }

        var entry = library.Get(id);
        if (entry == null || entry.clip == null)
        {
            return;
        }

        if (!entry.loop)
        {
            Debug.LogWarning($"[SfxManager] 音效 {id} 未配置为循环，请使用 Play() 方法");
            return;
        }

        var key = (id, ownerKey);

        // 如果已存在该 ownerKey 的循环音效，先停止
        if (_loopMap.TryGetValue(key, out var existingSource))
        {
            if (existingSource != null && existingSource.isPlaying)
            {
                // 已存在，不重复播放
                return;
            }
            else
            {
                // 清理无效引用
                _loopMap.Remove(key);
            }
        }

        // 检查并发限制
        int currentCount = _currentPlayCount.GetValueOrDefault(id, 0);
        if (currentCount >= entry.maxSimultaneous)
        {
            // 超过并发限制，拒绝播放
            return;
        }

        // 更新播放计数
        _currentPlayCount[id] = currentCount + 1;

        // 创建新的 AudioSource（循环音效使用独立 AudioSource）
        var audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.clip = entry.clip;
        audioSource.volume = entry.volume;
        audioSource.pitch = Random.Range(entry.pitchMin, entry.pitchMax);
        audioSource.outputAudioMixerGroup = sfxMixerGroup;

        _loopMap[key] = audioSource;
        audioSource.Play();
    }

    /// <summary>
    /// 停止循环音效
    /// </summary>
    /// <param name="id">音效 ID</param>
    /// <param name="ownerKey">拥有者标识</param>
    public void StopLoop(SfxId id, object ownerKey)
    {
        var key = (id, ownerKey);

        if (_loopMap.TryGetValue(key, out var audioSource))
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                Destroy(audioSource);

                // 更新播放计数
                if (_currentPlayCount.TryGetValue(id, out var count))
                {
                    _currentPlayCount[id] = Mathf.Max(0, count - 1);
                }
            }

            _loopMap.Remove(key);
        }
    }

    /// <summary>
    /// 停止指定 ownerKey 的所有循环音效
    /// </summary>
    /// <param name="ownerKey">拥有者标识</param>
    public void StopAllLoops(object ownerKey)
    {
        var keysToRemove = new List<(SfxId, object)>();

        foreach (var kvp in _loopMap)
        {
            if (kvp.Key.ownerKey == ownerKey)
            {
                var audioSource = kvp.Value;
                if (audioSource != null)
                {
                    audioSource.Stop();
                    Destroy(audioSource);

                    // 更新播放计数
                    var id = kvp.Key.id;
                    if (_currentPlayCount.TryGetValue(id, out var count))
                    {
                        _currentPlayCount[id] = Mathf.Max(0, count - 1);
                    }
                }

                keysToRemove.Add(kvp.Key);
            }
        }

        foreach (var key in keysToRemove)
        {
            _loopMap.Remove(key);
        }
    }

    /// <summary>
    /// 协程：播放完成后回收 AudioSource
    /// </summary>
    private System.Collections.IEnumerator ReturnToPoolWhenFinished(AudioSource audioSource, SfxId id)
    {
        if (audioSource == null || audioSource.clip == null)
        {
            yield break;
        }

        // 等待播放完成
        yield return new WaitWhile(() => audioSource != null && audioSource.isPlaying);

        // 回收
        if (audioSource != null)
        {
            audioSource.clip = null;
            _activeOneShots.Remove(audioSource);
            _oneShotPool.Enqueue(audioSource);

            // 更新播放计数
            if (_currentPlayCount.TryGetValue(id, out var count))
            {
                _currentPlayCount[id] = Mathf.Max(0, count - 1);
            }
        }
    }

    private void OnDestroy()
    {
        // 清理所有循环音效
        foreach (var audioSource in _loopMap.Values)
        {
            if (audioSource != null)
            {
                audioSource.Stop();
                Destroy(audioSource);
            }
        }
        _loopMap.Clear();
    }
}

