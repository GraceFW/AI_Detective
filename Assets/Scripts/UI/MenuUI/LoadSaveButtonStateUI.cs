using UnityEngine;
using UnityEngine.UI;

public class LoadSaveButtonStateUI : MonoBehaviour
{
    [SerializeField] private Button targetButton;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }

    private void OnEnable()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (targetButton == null)
        {
            return;
        }

        targetButton.interactable = DataManager.instance != null && DataManager.instance.HasLoadableSceneSave();
    }
}
