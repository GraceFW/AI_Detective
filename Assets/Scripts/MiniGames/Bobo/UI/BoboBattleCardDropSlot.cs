using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 玩家牌槽的拖拽落点。
/// 只负责接收当前正在拖拽的行动牌，并把“拖到了哪个槽位”通知给外层面板。
/// </summary>
[DisallowMultipleComponent]
public class BoboBattleCardDropSlot : MonoBehaviour, IDropHandler
{
    private int slotIndex;
    private Action<int, ActionType> onDropAction;
    private Action onReceiveDrop;

    public void Configure(int slotIndex, Action<int, ActionType> onDropAction, Action onReceiveDrop)
    {
        this.slotIndex = slotIndex;
        this.onDropAction = onDropAction;
        this.onReceiveDrop = onReceiveDrop;
    }

    public void OnDrop(PointerEventData eventData)
    {
        BoboBattleDragActionItem draggingItem = BoboBattleDragActionItem.CurrentDragging;
        if (draggingItem == null)
        {
            return;
        }

        onDropAction?.Invoke(slotIndex, draggingItem.ActionType);
        onReceiveDrop?.Invoke();
    }
}
