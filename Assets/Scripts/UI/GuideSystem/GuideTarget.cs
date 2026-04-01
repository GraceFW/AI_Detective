using System.Collections;
using UnityEngine;

/// <summary>
/// 引导目标组件（所有可被高亮的UI必须挂这个）
/// </summary>
public class GuideTarget : MonoBehaviour
{
	public string key;

	private RectTransform _rect;
	private bool _isRegistered = false;
	private Coroutine _registerCoroutine;

	private void Awake()
	{
		_rect = transform as RectTransform;
	}

	/// <summary>
	/// 初始化（动态UI用）
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
