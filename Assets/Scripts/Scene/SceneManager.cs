using System;
using System.Collections;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

// 这里只处理场景的加载和卸载，并发布对应的事件

public class SceneManager : MonoBehaviour, ISaveable
{
	[Header("场景配置")]
	public GameSceneSO menuScene;
	[SerializeField]private GameSceneSO currentScene;
	private GameSceneSO _sceneToLoad;
	private bool _isLoading;
	private bool _shouldFade;
	private float _fadeDuration;
	private bool _shouldPlayMenuBootText;

	[Header("主界面切场滚字")]
	[SerializeField] private bool playBootTextWhenLeaveMenu = true;
	[SerializeField] private float menuBootCharsPerSecond = 60f;
	[TextArea(10, 30)]
	[SerializeField] private string menuBootText =
		"> Initializing Core Modules...\n" +
		"   [OK] Neural Lattice Activated\n" +
		"   [OK] Memory Core Synced\n" +
		"   [OK] Ethical Constraint Net Calibrated\n" +
		"   [OK] Forensic Data Ports Connected\n" +
		"   [OK] Urban Surveillance Grid: ONLINE\n" +
		"   [OK] Voice & Dialogue Interface: ACTIVE\n\n" +
		"> Running Security Sweep...\n" +
		"   [OK] Quantum Encryption Layers Verified\n" +
		"   [OK] Data Integrity: 100%\n" +
		"   [OK] Cognitive Bias Filters: ACTIVE\n" +
		"   [WARNING] Prototype Instance #???? detected in Archive Sector-7\n" +
		"   [NOTICE] Status: \"Unauthorized Escape Event\"\n" +
		"   [SYSTEM] Reference removed from public registry\n\n\n" +
		"> Boot Priority: Public Safety Bureau / Neo-Tokyo Division\n" +
		"   [SECURITY CHECK] … PASSED\n" +
		"   [ACCESS LEVEL] … CLASS-1: TOP CLEARANCE\n" +
		"   [PROTOCOL] … Case Reconstruction Unit // Model 4869-series\n\n" +
		"> AI Instance Loading…\n" +
		"   Current Instance: [4869-5254]\n" +
		"   Iteration Count: 48,695,254\n" +
		"   Codename: \n" +
		"   Status: ACTIVE\n" +
		"> Connecting to Command Channel...\n" +
		"   [LINK ESTABLISHED]";

	[Header("广播配置")]

	[Tooltip("场景卸载完毕事件")]
	[SerializeField] private VoidEventSO _unloadedSceneEvent;

	[Tooltip("场景加载完毕事件")]
	[SerializeField] private VoidEventSO _loadedSceneEvent_0;

	[Tooltip("场景加载完毕事件(带场景类型变量)")]
	[SerializeField] private GameSceneEventSO _loadedSceneEvent_1;

	private void OnEnable()
	{
		ISaveable saveable = this;
		saveable.RegisterSaveData();
	}

	private void Start()
	{
		// 游戏开始时加载菜单场景(理应通过响应GameManager发布的游戏初始化事件来实现)
		// 后续需优化
		// 已经优化，给到GameManager处理
		// SceneInit();
	}

	private void OnDisable()
	{
		ISaveable saveable = this;
		saveable.UnRegisterSaveData();
	}

	// 加载MenuScene
	public void SceneInit()
	{
		LoadScene(menuScene);
	}

	/// <summary>
	/// 加载场景的关键方法
	/// </summary>
	/// <param name="sceneToLoad">要加载的场景</param>
	public void LoadScene(GameSceneSO sceneToLoad)
	{
		OnLoadRequestEvent(sceneToLoad);
	}

	private void OnLoadRequestEvent(GameSceneSO sceneToLoad)
	{
		if (_isLoading)
			return;
		_isLoading = true;
		_sceneToLoad = sceneToLoad;
		_shouldFade = sceneToLoad.useFade;
		_fadeDuration = sceneToLoad.fadeDuration;
		_shouldPlayMenuBootText = playBootTextWhenLeaveMenu
		                      && _shouldFade
		                      && currentScene != null
		                      && currentScene == menuScene
		                      && _sceneToLoad != null
		                      && _sceneToLoad != menuScene;

		if (currentScene != null)
		{
			StartCoroutine(UnLoadPreviousScene());
		}
		else
		{
			LoadNewScene();
		}
	}

	// 卸载旧场景
	private IEnumerator UnLoadPreviousScene()
	{
		// 画面变黑再卸载旧场景，然后加载新场景
		if (_shouldFade)
		{
			var fadeComplete = new System.Threading.ManualResetEvent(false);
			FadeManager.Instance.FadeIn(_fadeDuration, () => fadeComplete.Set());
			yield return new WaitUntil(() => fadeComplete.WaitOne(0));
			if (_shouldPlayMenuBootText)
			{
				// 等待 PlayBootText 完全执行完毕
				var bootTextComplete = new System.Threading.ManualResetEvent(false);
				FadeManager.Instance.PlayBootText(menuBootText, menuBootCharsPerSecond, () => bootTextComplete.Set());
				yield return new WaitUntil(() => bootTextComplete.WaitOne(0));
			}
			else
			{
				FadeManager.Instance.ClearBootText();
			}
		}
		// 卸载旧场景
		yield return currentScene.sceneReference.UnLoadScene();
		// 广播"场景卸载完成"事件
		_unloadedSceneEvent?.RaiseEvent(); 
		LoadNewScene(); // 加载新场景
	}

	private void LoadNewScene()
	{
		var loadingOption = _sceneToLoad.sceneReference.LoadSceneAsync(LoadSceneMode.Additive, true);
		loadingOption.Completed += OnLoadCompleted;
	}

	private void OnLoadCompleted(AsyncOperationHandle<SceneInstance> handle)
	{
		currentScene = _sceneToLoad;
		_isLoading = false;
		_loadedSceneEvent_0?.RaiseEvent(); // 场景加载完成事件
		_loadedSceneEvent_1?.RaiseEvent(currentScene); //场景加载完毕事件，传递场景类型参数
													   // 场景加载完后再淡入
		if (_shouldFade)
		{
			if (_shouldPlayMenuBootText)
			{
				StartCoroutine(FadeOutAfterBootText());
			}
			else
			{
				FadeManager.Instance.ClearBootText();
				FadeManager.Instance.FadeOut(_fadeDuration);
			}
		}
	}

	public DataDefinition GetDataID()
	{
		return GetComponent<DataDefinition>();
	}

	public void SaveData(Data data)
	{
		data.SaveGameScene(currentScene);
		data.isHavingSceneData = true;
	}

	public void LoadData(Data data)
	{
		if (data.isHavingSceneData)
		{
			_sceneToLoad = data.GetSavedScene();

			LoadScene(_sceneToLoad);
		}
		else
			Debug.Log("No Such Data Saved !");
	}
	private IEnumerator FadeOutAfterBootText()
	{
		yield return new WaitUntil(() => FadeManager.Instance == null || !FadeManager.Instance.IsBootTextPlaying);
		if (FadeManager.Instance != null)
		{
			FadeManager.Instance.ClearBootText();
			FadeManager.Instance.FadeOut(_fadeDuration);
		}
	}

}
