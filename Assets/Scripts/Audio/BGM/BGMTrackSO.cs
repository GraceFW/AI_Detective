using UnityEngine;

[CreateAssetMenu(menuName = "Audio/BGMTrack")]
public class BGMTrackSO : ScriptableObject
{
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
    public bool loop = true;
}
