using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 点击事件转发器
/// 将自身对象的点击事件转发给父对象或指定对象上的 IPointerClickHandler 组件
/// </summary>
public class ClickEventForwarder : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("转发目标。如果为空，则自动向父级查找 IPointerClickHandler")]
    [SerializeField] private GameObject forwardTarget;

    public void OnPointerClick(PointerEventData eventData)
    {
        GameObject target = forwardTarget != null ? forwardTarget : transform.parent?.gameObject;
        
        if (target == null)
        {
            Debug.LogWarning("[ClickEventForwarder] 找不到转发目标");
            return;
        }

        // 获取目标上的所有 IPointerClickHandler 组件并调用
        var handlers = target.GetComponents<IPointerClickHandler>();
        if (handlers != null && handlers.Length > 0)
        {
            foreach (var handler in handlers)
            {
                // 跳过自己，避免无限循环
                if ((object)handler != this)
                {
                    handler.OnPointerClick(eventData);
                }
            }
        }
        else
        {
            Debug.LogWarning($"[ClickEventForwarder] 目标 {target.name} 上没有找到 IPointerClickHandler 组件");
        }
    }
}

