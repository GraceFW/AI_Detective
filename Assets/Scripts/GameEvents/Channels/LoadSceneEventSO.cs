using UnityEngine;

[CreateAssetMenu(menuName = "Event/LoadSceneEventSO")]
/// <summary>
/// 场景加载事件脚本化对象
/// <para>泛型参数说明：</para>
/// <para>T1: GameSceneSO - 要加载的场景的SO引用</para>
/// </summary>
public class LoadSceneEventSO : GameEventSO<GameSceneSO> {}
