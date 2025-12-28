using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json;
using System.IO;

[DefaultExecutionOrder(-100)]
public class DataManager : MonoBehaviour
{

	private List<ISaveable> saveableList = new List<ISaveable>();
	public static DataManager instance;
	private Data saveData;
	private string jsonFolder;

	private void Awake()
	{
		// 使用单例，创建与存档相关的唯一Data数据结构，以达到只需要序列化一个数据结构的能力
		saveData = new Data();
		if (instance == null)
		{
			instance = this;
		}
		else
		{
			Destroy(this.gameObject);
		}
		// 一个Unity内置的保存路径，具体在哪可以参考Unity官方技术文档
		jsonFolder = Application.persistentDataPath + "/SaveData/";
		// 游戏一开始时就读取一下保存的数据，相当于从硬盘拿到内存
		ReadSaveData();
	}

	private void Update()
	{
		// 按下L加载保存的数据，测试用
		if (Keyboard.current.lKey.wasPressedThisFrame)
			Load();
	}

	// 自动保存，实现加载场景后自动保存游戏数据(只有加载的是非菜单场景才保存)
	public void AutoSave(GameSceneSO scene)
	{
		if (scene.sceneType == SceneType.Level)
		{
			Save();
		}
	}

	public void Save()
	{
		foreach (var saverable in saveableList)
		{
			saverable.SaveData(saveData);
		}

		var resultPath = jsonFolder + "data.sav";
		var jsonData = JsonConvert.SerializeObject(saveData);

		if (!File.Exists(resultPath))
		{
			Directory.CreateDirectory(jsonFolder);
		}
		File.WriteAllText(resultPath, jsonData);
	}

	public void Load()
	{
		foreach (var saverable in saveableList)
		{
			saverable.LoadData(saveData);
		}
	}

	private void ReadSaveData()
	{
		var resultPath = jsonFolder + "data.sav";

		if (File.Exists(resultPath))
		{
			var stringData = File.ReadAllText(resultPath);
			var jsonData = JsonConvert.DeserializeObject<Data>(stringData);
			saveData = jsonData;
		}
	}

	public void RegisterSaveData(ISaveable saveable)
	{
		if (!saveableList.Contains(saveable))
		{
			saveableList.Add(saveable);
		}
	}

	public void UnRegisterSaveData(ISaveable saveable)
	{
		saveableList.Remove(saveable);
	}
}
