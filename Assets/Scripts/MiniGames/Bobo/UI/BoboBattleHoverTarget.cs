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
    private Action<string, string, RectTransform> onShow;
    private Action<RectTransform> onMove;
    private Action onHide;
    private RectTransform cachedRectTransform;

    public void Configure(
        Func<string> titleProvider,
        Func<string> bodyProvider,
        Action<string, string, RectTransform> onShow,
        Action<RectTransform> onMove,
        Action onHide)
    {
        this.titleProvider = titleProvider;
        this.bodyProvider = bodyProvider;
        this.onShow = onShow;
        this.onMove = onMove;
        this.onHide = onHide;
        cachedRectTransform = transform as RectTransform;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        string title = titleProvider != null ? titleProvider() : string.Empty;
        string body = bodyProvider != null ? bodyProvider() : string.Empty;
        onShow?.Invoke(title, body, cachedRectTransform);
    }

    public void OnPointerMove(PointerEventData eventData)
    {
        onMove?.Invoke(cachedRectTransform);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        onHide?.Invoke();
    }
}
