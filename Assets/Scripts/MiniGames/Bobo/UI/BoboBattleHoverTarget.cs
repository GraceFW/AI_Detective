using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 通用的波波攒悬浮目标。
/// 它本身不关心提示内容，只负责把悬浮事件转给外层配置的回调。
/// </summary>
[DisallowMultipleComponent]
public class BoboBattleHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerMoveHandler
{
    private Func<string> titleProvider;
    private Func<string> bodyProvider;
    private Action<string, string, PointerEventData> onShow;
    private Action<PointerEventData> onMove;
    private Action onHide;

    public void Configure(
        Func<string> titleProvider,
        Func<string> bodyProvider,
        Action<string, string, PointerEventData> onShow,
        Action<PointerEventData> onMove,
        Action onHide)
    {
        this.titleProvider = titleProvider;
        this.bodyProvider = bodyProvider;
        this.onShow = onShow;
        this.onMove = onMove;
        this.onHide = onHide;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        string title = titleProvider != null ? titleProvider() : string.Empty;
        string body = bodyProvider != null ? bodyProvider() : string.Empty;
        onShow?.Invoke(title, body, eventData);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        onMove?.Invoke(eventData);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHide?.Invoke();
    }
}
