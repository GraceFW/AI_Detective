using System.Collections;
using UnityEngine;

/// <summary>
/// 引导目标组件。
/// 所有希望被 guide 通过 key 找到的 UI，都应该最终在运行时拥有一个 GuideTarget。
///
/// 这个组件的职责只有两件事：
/// 1. 保存“这个 UI 在引导系统里的身份 key”。
/// 2. 把自己的 RectTransform 生命周期同步到 GuideTargetRegistry。
///
/// 业务 UI 不应该绕过它直接 Register/Unregister，
/// 因为动态对象的启用、禁用、销毁都需要这里统一兜底。
/// </summary>
public class GuideTarget : MonoBehaviour
{
	public string key;

	// 这里只接受 UI 目标，因此内部默认要求自身是 RectTransform。
	private RectTransform _rect;
	private bool _isRegistered = false;
	private Coroutine _registerCoroutine;

	private void Awake()
	{
		_rect = transform as RectTransform;
	}

	/// <summary>
	/// 动态 UI 的初始化入口。
	/// 常见场景：
	/// - 运行时生成的 clue item
	/// - 弹出的详情页 popup
	/// - 弹窗上的关闭按钮
	///
	/// 如果 key 发生变化，会先安全注销旧 key，再重新注册新 key。
	/// </summary>
	public void Init(string k)
	{
		if (string.IsNullOrEmpty(k))
		{
			Debug.LogWarning("[GuideTarget] Init key为空");
			return;
		}

		if (_isRegistered && !string.IsNullOrEmpty(key) && key != k)
		{
			UnregisterSafe(key, _rect);
		}

		key = k;
		RestartRegistration();
	}

	private void OnEnable()
	{
		if (!string.IsNullOrEmpty(key))
		{
			RestartRegistration();
		}
	}

	private void OnDisable()
	{
		StopPendingRegistration();
		UnregisterSafe(key, _rect);
	}

	private void OnDestroy()
	{
		StopPendingRegistration();
		UnregisterSafe(key, _rect);
	}

	private void RestartRegistration()
	{
		// Registry 可能比当前对象初始化得更晚，因此这里总是走协程延迟注册，
		// 而不是在 Init/OnEnable 时直接假设 Registry 已经准备好。
		if (!isActiveAndEnabled)
		{
			return;
		}

		StopPendingRegistration();
		_registerCoroutine = StartCoroutine(DelayedRegister());
	}

	private void StopPendingRegistration()
	{
		if (_registerCoroutine != null)
		{
			StopCoroutine(_registerCoroutine);
			_registerCoroutine = null;
		}
	}

	private IEnumerator DelayedRegister()
	{
		yield return new WaitUntil(() => GuideTargetRegistry.HasInstance
									 && GuideTargetRegistry.Instance.IsReady);

		Register();
		_registerCoroutine = null;
	}

	private void Register()
	{
		if (string.IsNullOrEmpty(key))
		{
			Debug.LogError("[GuideTarget] key为空，无法注册");
			return;
		}

		if (GuideTargetRegistry.Instance == null)
		{
			Debug.LogError("[GuideTarget] GuideTargetRegistry.Instance 为空！");
			return;
		}

		var rect = transform as RectTransform;
		if (rect == null)
		{
			Debug.LogError("[GuideTarget] RectTransform不存在！");
			return;
		}

		GuideTargetRegistry.Instance.Register(key, rect);
		_isRegistered = true;
	}

	private void UnregisterSafe(string targetKey, RectTransform rect)
	{
		// 注销逻辑允许在对象销毁过程中静默失败，
		// 这样即使 Registry 已先被销毁，也不会在退出场景时刷无意义错误。
		if (!_isRegistered || string.IsNullOrEmpty(targetKey) || rect == null)
		{
			return;
		}

		if (GuideTargetRegistry.HasInstance && GuideTargetRegistry.Instance.IsReady)
		{
			GuideTargetRegistry.Instance.Unregister(targetKey, rect);
		}

		_isRegistered = false;
	}
}
