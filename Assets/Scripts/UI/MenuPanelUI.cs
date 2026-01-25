using UnityEngine;
using UnityEngine.UI;

public class UIMenu : MonoBehaviour
{
	public Button startBtn;
	public Button exitBtn;
	[SerializeField] private GameSceneEventSO _loadSceneEvent;
	[SerializeField] private GameSceneSO _firstLevelScene;
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
	public void OnStartGameButtonClick()
	{
		Debug.Log("startBtn done!");
		// 发布加载请求，无需直接调用SceneManager
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
