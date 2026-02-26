using UnityEngine;

/// <summary>
/// 音效配置条目
/// </summary>
[System.Serializable]
public class SfxEntry
{
    [Tooltip("音效 ID")]
    public SfxId id;

    [Tooltip("音频片段")]
    public AudioClip clip;

    [Tooltip("音量 (0-1)")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Tooltip("是否循环播放")]
    public bool loop = false;

    [Tooltip("音调最小值")]
    [Range(0.5f, 2f)]
    public float pitchMin = 1f;

    [Tooltip("音调最大值")]
    [Range(0.5f, 2f)]
    public float pitchMax = 1f;

    [Tooltip("最大同时播放数量")]
    [Range(1, 16)]
    public int maxSimultaneous = 8;

    [Tooltip("冷却时间（秒），防止连点爆音")]
    [Range(0f, 1f)]
    public float cooldown = 0f;
}

