using UnityEngine;

[CreateAssetMenu(menuName = "Event/DialogueEndEventSO")]
/// <summary>
/// 对话结束事件脚本化对象
/// <para>泛型参数说明：</para>
/// <para>T1: int - 关卡编号</para>
/// <para>T2: DialogueTriggerType - 触发类型</para>
/// </summary>
public class DialogueEndEventSO : GameEventSO<int, DialogueTriggerType> {}

