using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Game/Level Reward Clue Config")]
public class LevelRewardClueConfigSO : ScriptableObject
{
	public List<LevelClueRewardConfig> configs;
}