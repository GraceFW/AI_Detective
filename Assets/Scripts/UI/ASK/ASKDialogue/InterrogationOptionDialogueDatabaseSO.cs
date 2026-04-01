using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "InterrogationOptionDialogueDB", menuName = "Game/Interrogation Option Dialogue DB SO")]
public class InterrogationOptionDialogueDatabaseSO : ScriptableObject
{
	[System.Serializable]
	public class Entry
	{
		public int levelNumber;      // 0=不区分案件；否则必须匹配
		public string personId;
		public string nodeId;
		public string optionId;      // 对应 DialogueOption.optionId
		public DialogueSequence sequence;
		public bool isForced = true;
	}

	public List<Entry> entries = new List<Entry>();

	public bool TryGet(int levelNumber, string personId, string nodeId, string optionId, out Entry result)
	{
		foreach (var e in entries)
		{
			if (e == null) continue;
			if (e.levelNumber != 0 && e.levelNumber != levelNumber) continue;
			if (e.personId == personId && e.nodeId == nodeId && e.optionId == optionId)
			{
				result = e;
				return true;
			}
		}
		result = null;
		return false;
	}
}