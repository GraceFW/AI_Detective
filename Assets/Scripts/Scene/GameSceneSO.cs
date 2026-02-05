using UnityEngine;
using UnityEngine.AddressableAssets;

[CreateAssetMenu(menuName = "GameScene/GameSceneSO")]
public class GameSceneSO : ScriptableObject 
{
	public SceneType sceneType;
	public AssetReference sceneReference;
	[Tooltip("下一关场景")]
	public GameSceneSO nextLevelScene;
	// 场景加载/卸载时是否使用淡入淡出
	public bool useFade = true;
	// 渐变时长
	public float fadeDuration = 1f;
}
