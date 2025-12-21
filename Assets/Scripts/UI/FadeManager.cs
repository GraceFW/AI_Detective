using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class FadeManager : MonoBehaviour
{
	public static FadeManager Instance; // 全局单例（优化点：通过DI注入）
	[SerializeField] private Image _fadeImage;

	private void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
		_fadeImage.color = Color.clear; // 初始透明
	}

	/// <summary>
	/// 淡入（变黑）
	/// </summary>
	/// <param name="duration">渐变时长</param>
	/// <param name="onComplete">渐变完成回调</param>
	public void FadeIn(float duration, Action onComplete = null)
	{
		_fadeImage.DOFade(1f, duration)
			.SetEase(Ease.Linear)
			.OnComplete(() => onComplete?.Invoke());
	}

	/// <summary>
	/// 淡出（变透明）
	/// </summary>
	public void FadeOut(float duration, Action onComplete = null)
	{
		_fadeImage.DOFade(0f, duration)
			.SetEase(Ease.Linear)
			.OnComplete(() => onComplete?.Invoke());
	}
}