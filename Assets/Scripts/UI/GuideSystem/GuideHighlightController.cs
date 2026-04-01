using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 高亮控制器
/// 负责创建/销毁高亮遮罩
/// </summary>
public class GuideHighlightController : MonoBehaviour
{
	public static GuideHighlightController Instance;

	/// <summary>
	/// 当前所有高亮对象
	/// </summary>
	private List<GameObject> highlights = new();

	private void Awake()
	{
		ClaimInstance();
	}

	public void EnsureInitialized()
	{
		ClaimInstance();
	}

	private void ClaimInstance()
	{
		if (Instance != null && Instance != this)
		{
			enabled = false;
			return;
		}

		Instance = this;
		enabled = true;
	}

	private void OnDestroy()
	{
		if (Instance == this)
		{
			Instance = null;
		}
	}

	/// <summary>
	/// 高亮多个UI
	/// </summary>
	public void HighlightMultiple(List<RectTransform> targets)
	{
		ClearHighlight();

		foreach (var t in targets)
		{
			var go = CreateHighlight(t);
			highlights.Add(go);
		}
	}

	/// <summary>
	/// 创建单个高亮（简单版本）
	/// </summary>
	private GameObject CreateHighlight(RectTransform target)
	{
		GameObject go = new GameObject("Highlight");
		go.transform.SetParent(target, false);

		var img = go.AddComponent<Image>();
		img.color = new Color(1, 1, 0, 0.3f);

		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = rt.offsetMax = Vector2.zero;

		return go;
	}

	/// <summary>
	/// 清除所有高亮
	/// </summary>
	public void ClearHighlight()
	{
		foreach (var h in highlights)
		{
			if (h != null)
				Destroy(h);
		}
		highlights.Clear();
	}
}
