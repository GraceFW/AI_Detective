using UnityEngine;
using UnityEngine.UI;

public class ScrollDebug : MonoBehaviour
{
    public ScrollRect scroll;

    void LateUpdate()
    {
        if (scroll != null)
        {
            Debug.Log($"normPos={scroll.verticalNormalizedPosition}, contentPos={scroll.content.anchoredPosition}");
        }
    }
}