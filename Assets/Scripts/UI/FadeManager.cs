using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using TMPro;

public class FadeManager : MonoBehaviour
{
	public static FadeManager Instance; // 全局单例（优化点：通过DI注入）
	[SerializeField] private Image _fadeImage;
	[SerializeField] private TMP_Text _bootText;
	[SerializeField] private ScrollRect _bootScrollRect;

	private Coroutine _bootCoroutine;
	private bool _isBootTextPlaying;

	private void Awake()
	{
		if (Instance == null) Instance = this;
		else Destroy(gameObject);
		_fadeImage.color = Color.clear; // 初始透明
		if (_bootText != null)
		{
			_bootText.text = string.Empty;
			_bootText.maxVisibleCharacters = 0;
			if (_bootText.gameObject.activeSelf)
				_bootText.gameObject.SetActive(false);
		}
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

	public void PlayBootText(string fullText, float charsPerSecond, Action onComplete = null)
	{
		if (_bootText == null)
		{
			onComplete?.Invoke();
			return;
		}

		if (_bootCoroutine != null)
			StopCoroutine(_bootCoroutine);
		_bootCoroutine = StartCoroutine(PlayBootTextCoroutine(fullText, charsPerSecond, onComplete));
	}

	public void ClearBootText()
	{
		if (_bootText == null)
			return;

		if (_bootCoroutine != null)
		{
			StopCoroutine(_bootCoroutine);
			_bootCoroutine = null;
		}
		_isBootTextPlaying = false;
		_bootText.maxVisibleCharacters = 0;
		_bootText.text = string.Empty;
		_bootText.gameObject.SetActive(false);
	}

	public bool IsBootTextPlaying => _isBootTextPlaying;

	private IEnumerator PlayBootTextCoroutine(string fullText, float charsPerSecond, Action onComplete)
	{
		_isBootTextPlaying = true;
		_bootText.gameObject.SetActive(true);
		var text = fullText ?? string.Empty;
		_bootText.text = string.Empty;
		_bootText.maxVisibleCharacters = int.MaxValue;

		var totalCharacters = text.Length;
		var interval = charsPerSecond <= 0f ? 0f : 1f / charsPerSecond;
		var sb = new StringBuilder(totalCharacters);

		for (var i = 0; i < totalCharacters; i++)
		{
			sb.Append(text[i]);
			_bootText.text = sb.ToString();
			ScrollToBottom();

			if (interval > 0f)
				yield return new WaitForSeconds(interval);
			else
				yield return null;
		}
		ScrollToBottom();

		_bootCoroutine = null;
		_isBootTextPlaying = false;
		onComplete?.Invoke();
	}

	private void ScrollToBottom()
	{
		if (_bootScrollRect == null)
			return;

		var target = _bootScrollRect.content != null ? _bootScrollRect.content : _bootText.rectTransform;
		if (target == null)
			return;

		Canvas.ForceUpdateCanvases();
		LayoutRebuilder.ForceRebuildLayoutImmediate(target);
		Canvas.ForceUpdateCanvases();
		_bootScrollRect.verticalNormalizedPosition = 0f;
	}
}