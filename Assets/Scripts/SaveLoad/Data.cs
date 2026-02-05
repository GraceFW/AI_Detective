using System.Collections.Generic;
using UnityEngine;

public class Data
{
	public bool isHavingSceneData = false;
	
	/// <summary>
	/// 保存的场景标识（当前使用 GameSceneSO 的资源名）
	/// </summary>
	public string savedSceneId;
	
	public Dictionary<string, bool> boolSaveData = new Dictionary<string, bool>();

	public void SaveGameScene(GameSceneSO savedScene)
	{
		if (savedScene == null)
		{
			Debug.LogWarning("[Data] SaveGameScene: savedScene is null");
			savedSceneId = null;
			return;
		}

		// 目前使用资源名作为场景标识；后续可替换为 GameSceneSO 内部的自定义 sceneId
		savedSceneId = savedScene.name;
		Debug.Log($"[Data] SaveGameScene: {savedSceneId}");
	}

	public GameSceneSO GetSavedScene()
	{
		if (string.IsNullOrEmpty(savedSceneId))
		{
			Debug.LogWarning("[Data] GetSavedScene: savedSceneId is null or empty");
			return null;
		}

		if (SceneDatabaseSO.Instance == null)
		{
			Debug.LogWarning("[Data] GetSavedScene: SceneDatabaseSO.Instance is null，无法根据标识恢复场景");
			return null;
		}

		var scene = SceneDatabaseSO.Instance.GetSceneByName(savedSceneId);
		if (scene == null)
		{
			Debug.LogWarning($"[Data] GetSavedScene: 在 SceneDatabase 中未找到名为 {savedSceneId} 的 GameSceneSO");
		}

		return scene;
	}

}
