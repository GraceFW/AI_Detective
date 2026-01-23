using UnityEngine;
using UnityEngine.UI;

public class DialogueTriggerButton : MonoBehaviour
{
    [SerializeField] private Button triggerButton;
    
    private void Start()
    {
        if (triggerButton != null)
        {
            triggerButton.onClick.AddListener(OnTriggerButtonClicked);
        }
    }
    
    private void OnTriggerButtonClicked()
    {
        // 触发下一波WaveSpawn对话
        bool success = DialogueManager.Instance.TriggerNextWaveSpawnDialogue(
            onComplete: () => {
                Debug.Log("对话结束，可以继续游戏");
            },
            isForced: true
        );
        
        if (!success)
        {
            Debug.LogWarning("触发对话失败，可能是该波次对话不存在或对话正在播放中");
        }
    }
}