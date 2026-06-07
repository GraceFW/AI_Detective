using UnityEngine;
using UnityEngine.UI;

public class UIMenu : MonoBehaviour
{
	public Button startBtn;
	public Button exitBtn;
	[SerializeField] private GameSceneEventSO _loadSceneEvent;
	[SerializeField] private GameSceneSO _firstLevelScene;

	private Button[] _menuButtons;
	private LoadSaveButtonStateUI[] _conditionalButtons;
	private bool _menuInteractable;

	public bool IsInteractable => _menuInteractable;

	private void Awake()
	{
		_menuButtons = GetComponentsInChildren<Button>(true);
		_conditionalButtons = GetComponentsInChildren<LoadSaveButtonStateUI>(true);
		SetMenuInteractable(false);
	}

	private void Start()
	{
		Debug.Log("MenuPanel done!");
		startBtn.onClick.AddListener(OnStartGameButtonClick);
		if (exitBtn != null)
		{
			exitBtn.onClick.AddListener(OnExitButtonClick);
		}
		Debug.Log("btn added!");
	}

	public void SetMenuInteractable(bool enabled)
	{
		_menuInteractable = enabled;

		if (_menuButtons != null)
		{
			for (int i = 0; i < _menuButtons.Length; i++)
			{
				if (_menuButtons[i] != null)
				{
					_menuButtons[i].interactable = enabled;
				}
			}
		}

		if (enabled && _conditionalButtons != null)
		{
			for (int i = 0; i < _conditionalButtons.Length; i++)
			{
				_conditionalButtons[i]?.Refresh();
			}
		}
	}

	public void OnStartGameButtonClick()
	{
		Debug.Log("startBtn done!");
		_loadSceneEvent.RaiseEvent(_firstLevelScene);
	}

	public void OnExitButtonClick()
	{
		Debug.Log("exitBtn done!");
		Application.Quit();
		#if UNITY_EDITOR
		UnityEditor.EditorApplication.isPlaying = false;
		#endif
	}
}
