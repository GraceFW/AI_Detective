using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 引导目标注册中心（全局唯一）
/// 作用：
/// 1. 管理所有“可被引导系统定位的UI”
/// 2. 提供 key → RectTransform 的映射
/// 3. 提供“UI注册完成”事件（解决动态UI问题）
/// </summary>
public class GuideTargetRegistry : MonoBehaviour
{
	public static GuideTargetRegistry Instance;

	/// <summary>
	/// key -> UI RectTransform 的映射表
	/// </summary>
	private Dictionary<string, RectTransform> targets = new();

	/// <summary>
	/// 当某个UI注册时触发（关键：GuideManager靠它等待动态UI）
	/// </summary>
	public event Action<string, RectTransform> OnTargetRegistered;

	private void Awake()
	{
		Instance = this;
	}

	/// <summary>
	/// 注册一个UI目标
	/// </summary>
	public void Register(string key, RectTransform target)
	{
		if (string.IsNullOrEmpty(key) || target == null)
		{
			Debug.LogWarning("[GuideTargetRegistry] 注册失败：key或target为空");
			return;
		}

		targets[key] = target;

		// 通知外部：这个UI已经准备好了
		OnTargetRegistered?.Invoke(key, target);
	}

	/// <summary>
	/// 获取UI目标（如果未创建会返回null）
	/// </summary>
	public RectTransform Get(string key)
	{
		if (string.IsNullOrEmpty(key))
			return null;

		targets.TryGetValue(key, out var t);
		return t;
	}

	/// <summary>
	/// 反注册（UI销毁时调用）
	/// </summary>
	public void Unregister(string key)
	{
		if (string.IsNullOrEmpty(key))
			return;

		if (targets.ContainsKey(key))
		{
			targets.Remove(key);
		}
	}
}