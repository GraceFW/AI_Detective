using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 游戏流程管理器脚本：调用游戏初始化等等
/// </summary>
public class GameManager : MonoBehaviour, ISaveable
{
    [Header("Bool保存数据")]
    // 也许有优化的方法？目前想到的方法就放这里。
    public bool isFirstPlaythrough;
    public bool isCatchKaitoKuroba;
	[Header("广播配置")]
	// 游戏初始化广播（目前场景管理器监听此事件）
	[SerializeField] private VoidEventSO _gameInitEvent;

	private void OnEnable()
	{
		ISaveable saveable = this;
		saveable.RegisterSaveData();
	}

	void Start()
	{
		_gameInitEvent.RaiseEvent();
	}

	private void OnDisable()
	{
		ISaveable saveable = this;
		saveable.UnRegisterSaveData();
	}

	public DataDefinition GetDataID()
	{
		return GetComponent<DataDefinition>();
	}

	public void LoadData(Data data)
	{
		if (data.boolSaveData.ContainsKey(GetDataID().ID + "Playthrough"))
		{
			this.isFirstPlaythrough = data.boolSaveData[GetDataID().ID + "Playthrough"];
			this.isCatchKaitoKuroba = data.boolSaveData[GetDataID().ID + "KaitoKuroba"];
		}
	}

	public void SaveData(Data data)
	{
		if (data.boolSaveData.ContainsKey(GetDataID().ID + "Playthrough"))
		{
			data.boolSaveData[GetDataID().ID + "Playthrough"] = this.isFirstPlaythrough;
			data.boolSaveData[GetDataID().ID + "KaitoKuroba"] = this.isCatchKaitoKuroba;
		}
		else
		{
			data.boolSaveData.Add(GetDataID().ID + "Playthrough", this.isFirstPlaythrough);
			data.boolSaveData.Add(GetDataID().ID + "KaitoKuroba", this.isCatchKaitoKuroba);
		}
	}



}
