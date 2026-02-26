using System.Collections;
using UnityEngine;

/// <summary>
/// 限时文字提示组件：激活后显示，1秒后自动隐藏
/// </summary>
public class TimedTextTip : MonoBehaviour
{
    [Header("参数")]
    [Tooltip("显示时长（秒）")]
    [SerializeField] private float showDuration = 1f;

    private void OnEnable()
    {
        StartCoroutine(HideAfterDelay());
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(showDuration);
        gameObject.SetActive(false);
    }
}
