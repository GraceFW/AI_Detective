using UnityEngine;
using UnityEngine.UI;

public class LoadSaveButtonStateUI : MonoBehaviour
{
    [SerializeField] private Button targetButton;

    private UIMenu _menuPanel;

    private void Awake()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        _menuPanel = GetComponentInParent<UIMenu>();
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

        bool menuUnlocked = _menuPanel == null || _menuPanel.IsInteractable;
        bool hasSave = DataManager.instance != null && DataManager.instance.HasLoadableSceneSave();
        targetButton.interactable = menuUnlocked && hasSave;
    }
}
