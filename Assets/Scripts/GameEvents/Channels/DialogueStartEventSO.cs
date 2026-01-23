using UnityEngine;

[CreateAssetMenu(menuName = "Event/DialogueStartEventSO")]
/// <summary>
/// 对话开始事件脚本化对象
/// <para>泛型参数说明：</para>
/// <para>T1: int - 关卡编号</para>
/// <para>T2: DialogueTriggerType - 触发类型</para>
/// </summary>
public class DialogueStartEventSO : GameEventSO<int, DialogueTriggerType> {}

