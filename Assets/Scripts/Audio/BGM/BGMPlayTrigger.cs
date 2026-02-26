using UnityEngine;

public class BGMPlayTrigger : MonoBehaviour
{
    [SerializeField] private BGMTrackEventSO bgmRequestEvent;
    [SerializeField] private BGMTrackSO track;

    public void Play()
    {
        if (bgmRequestEvent != null)
        {
            bgmRequestEvent.RaiseEvent(track);
        }
    }
}
