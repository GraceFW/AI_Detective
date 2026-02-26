using UnityEngine;
using UnityEngine.EventSystems;

public class UIDraggableWindow : MonoBehaviour, IBeginDragHandler, IDragHandler
{
    [Header("Drag")]
    [SerializeField] private RectTransform target;

    private Vector2 _pointerOffset;
    private RectTransform _canvasRect;
    private Canvas _canvas;

    private void Awake()
    {
        if (target == null)
        {
            target = transform as RectTransform;
        }

        _canvas = GetComponentInParent<Canvas>();
        if (_canvas != null)
        {
            _canvasRect = _canvas.transform as RectTransform;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (target == null)
        {
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            target,
            eventData.position,
            eventData.pressEventCamera,
            out _pointerOffset
        );
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (target == null)
        {
            return;
        }

        if (_canvasRect == null)
        {
            target.position = eventData.position;
            return;
        }

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRect,
            eventData.position,
            eventData.pressEventCamera,
            out var localPointerPos
        );

        var newAnchoredPos = localPointerPos - _pointerOffset;
        target.anchoredPosition = newAnchoredPos;
    }
}
