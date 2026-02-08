using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Newtonsoft.Json;
using System.IO;

[DefaultExecutionOrder(-100)]
public class DataManager : MonoBehaviour
{
	// 保存列表，存放实现了该接口的类。或者说任何需要保存的类都要在这里注册一下
	private List<ISaveable> saveableList = new List<ISaveable>();
	// 单例
	public static DataManager instance;
	// 存档数据结构，我在思考是不是多几个Data就是多存档了？
	private Data saveData;
	// 存档文件在资源管理器中的目录
	private string jsonFolder;
	[Tooltip("是否是测试模式")]
	public bool isTest;

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
		if (Keyboard.current[Key.L].wasPressedThisFrame && isTest)
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
		// 调用ISaveable 接口的每一个SaveData 实现
		foreach (var saverable in saveableList)
		{
			saverable.SaveData(saveData);
		}
		// 保存行为输出的结果的路径：resultPath
		var resultPath = jsonFolder + "data.sav"; // .sav是一个随便写的的后缀

		// 使用NewtonSoft.Json 包中的API，把Data 类型的数据序列化
		var jsonData = JsonConvert.SerializeObject(saveData);

		// 把序列化的数据写入磁盘
		if (!File.Exists(resultPath))
		{
			Directory.CreateDirectory(jsonFolder);
		}
		File.WriteAllText(resultPath, jsonData);
	}

	public void Load()
	{
		// 调用ISaveable 接口的每一个LoadData 实现
		foreach (var saverable in saveableList)
		{
			saverable.LoadData(saveData);
		}
	}

	/// <summary>
	/// 从磁盘读取保存的数据
	/// </summary>
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

	/// <summary>
	/// 数据管理器单例对外暴露的重要方法，调用者用这个方法来使其进入保存列表，让管理器能够对其进行管理
	/// </summary>
	/// <param name="saveable">存档能力接口</param>
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
