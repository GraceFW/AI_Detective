using UnityEngine;

/// <summary>
/// 引导目标组件
/// 挂在任何“需要被引导系统定位”的UI上
/// </summary>
public class GuideTarget : MonoBehaviour
{
	/// <summary>
	/// 唯一标识（由外部决定）
	/// 例如：
	/// - clue_knife
	/// - ConfirmButton
	/// - AnalysisPanel
	/// </summary>
	public string key;

	/// <summary>
	/// 动态初始化（用于Prefab实例）
	/// </summary>
	public void Init(string k)
	{
		key = k;
		Register();
	}

	private void Start()
	{
		Register();
	}

	/// <summary>
	/// 向注册中心注册自己
	/// </summary>
	private void Register()
	{
		if (!string.IsNullOrEmpty(key))
		{
			GuideTargetRegistry.Instance.Register(key, transform as RectTransform);
		}
	}

	private void OnDestroy()
	{
		// UI销毁时从注册中心移除
		if (!string.IsNullOrEmpty(key))
		{
			GuideTargetRegistry.Instance.Unregister(key);
		}
	}
}