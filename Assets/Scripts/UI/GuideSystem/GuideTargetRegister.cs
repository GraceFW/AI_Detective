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
	public static bool HasInstance => Instance != null;

	/// <summary>
	/// key → 多个UI
	/// </summary>
	private Dictionary<string, List<RectTransform>> _targets = new();

	/// <summary>
	/// ⭐ 新增：当有UI注册时触发
	/// 参数：key, RectTransform
	/// </summary>
	public event Action<string, RectTransform> OnTargetRegistered;

	/// <summary>
	/// ⭐ 新增：当UI移除时触发
	/// </summary>
	public event Action<string, RectTransform> OnTargetUnregistered;


	public bool IsReady { get; private set; }

	private void Awake()
	{
		if (Instance != null && Instance != this)
		{
			Destroy(gameObject);
			return;
		}

		Instance = this;
		IsReady = true;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			IsReady = false;
			Instance = null;
		}
	}

	// =========================
	// 注册
	// =========================
	public void Register(string key, RectTransform rect)
	{
		if (string.IsNullOrEmpty(key) || rect == null)
		{
			Debug.LogWarning("[GuideTargetRegistry] 注册失败：key或rect为空");
			return;
		}

		if (!_targets.TryGetValue(key, out var list))
		{
			list = new List<RectTransform>();
			_targets[key] = list;
		}

		list.RemoveAll(item => item == null);

		if (!list.Contains(rect))
		{
			list.Add(rect);

			Debug.Log($"[GuideTargetRegistry] 注册: {key} (count={list.Count})");

			// ⭐ 触发事件（核心）
			OnTargetRegistered?.Invoke(key, rect);
		}
	}

	// =========================
	// 注销
	// =========================
	public void Unregister(string key, RectTransform rect)
	{
		if (string.IsNullOrEmpty(key) || rect == null)
		{
			return;
		}

		if (_targets.TryGetValue(key, out var list))
		{
			if (list.Remove(rect))
			{
				Debug.Log($"[GuideTargetRegistry] 注销: {key} (剩余={list.Count})");

				// ⭐ 触发事件
				OnTargetUnregistered?.Invoke(key, rect);
			}

			if (list.Count == 0)
			{
				_targets.Remove(key);
			}
		}
	}

	// =========================
	// 查询
	// =========================

	public RectTransform Get(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}

		if (_targets.TryGetValue(key, out var list))
		{
			list.RemoveAll(item => item == null);
			if (list.Count == 0)
			{
				_targets.Remove(key);
				return null;
			}

			return list[0];
		}
		return null;
	}

	public List<RectTransform> GetAll(string key)
	{
		if (string.IsNullOrEmpty(key))
		{
			return null;
		}

		if (_targets.TryGetValue(key, out var list))
		{
			list.RemoveAll(item => item == null);
			if (list.Count == 0)
			{
				_targets.Remove(key);
				return null;
			}

			return new List<RectTransform>(list);
		}
		return null;
	}

	public bool Contains(string key)
	{
		return _targets.ContainsKey(key);
	}
}
