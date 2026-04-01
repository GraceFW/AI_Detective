using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(menuName = "Game/Guide Config")]
public class GuideConfigSO : ScriptableObject
{
	public int guideID;
	public List<GuideStep> steps;
}