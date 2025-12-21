using UnityEngine;
using UnityEngine.UI;

public class UIMenu : MonoBehaviour
{
	public Button startBtn;
	[SerializeField] private LoadSceneEventSO _loadSceneEvent;
	[SerializeField] private GameSceneSO _firstLevelScene;
	private void Start()
	{
		startBtn.onClick.AddListener(OnStartGameButtonClick);
	}
	public void OnStartGameButtonClick()
	{
		Debug.Log("startBtn done!");
		// 发布加载请求，无需直接调用SceneManager
		_loadSceneEvent.RaiseEvent(_firstLevelScene);
	}
}
