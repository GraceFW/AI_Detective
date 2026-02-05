using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 按钮点击播放音效组件
/// 自动在 Button.onClick 时播放指定音效
/// </summary>
[RequireComponent(typeof(Button))]
public class PlaySfxOnClick : MonoBehaviour
{
    [Tooltip("要播放的音效 ID（默认 Confirm）")]
    [SerializeField] private SfxId sfxId = SfxId.Confirm;

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();
        if (_button == null)
        {
            Debug.LogWarning("[PlaySfxOnClick] 未找到 Button 组件");
            return;
        }

        // 绑定点击事件
        _button.onClick.AddListener(OnButtonClick);
    }

    private void OnDestroy()
    {
        if (_button != null)
        {
            _button.onClick.RemoveListener(OnButtonClick);
        }
    }

    /// <summary>
    /// 按钮点击事件处理
    /// </summary>
    private void OnButtonClick()
    {
        // [SFX] 播放确认音效
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.Play(sfxId);
        }
    }
}

