using System.Collections.Generic;
using UnityEngine;

public class Data
{
	public bool isHavingSceneData = false;
	public string sceneToSave;
	public Dictionary<string, bool> boolSaveData = new Dictionary<string, bool>();

	public void SaveGameScene(GameSceneSO savedScene)
	{
		sceneToSave = JsonUtility.ToJson(savedScene);
		Debug.Log(savedScene);
	}

	public GameSceneSO GetSavedScene()
	{
		var newScene = ScriptableObject.CreateInstance<GameSceneSO>();
		JsonUtility.FromJsonOverwrite(sceneToSave, newScene);

		return newScene;
	}

}
