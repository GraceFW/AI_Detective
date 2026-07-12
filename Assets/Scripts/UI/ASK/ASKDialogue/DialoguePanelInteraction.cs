using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 为整块问讯对话面板提供点击推进和“仍有后续”提示。
/// </summary>
public sealed class DialoguePanelInteraction : MonoBehaviour, IPointerClickHandler
{
	private const string IndicatorName = "ContinueDialogueIndicator";

	private Action _onPanelClick;
	private CanvasGroup _indicatorCanvasGroup;
	private Coroutine _blinkCoroutine;
	private bool _continueVisible;

	public void Initialize(Action onPanelClick)
	{
		_onPanelClick = onPanelClick;

		// 没有 Graphic 的 RectTransform 不会参与 UI 射线检测。
		// 透明 Image 只负责让 DialogueBox 的全部矩形成为点击区域。
		Image raycastImage = GetComponent<Image>();
		if (raycastImage == null)
		{
			raycastImage = gameObject.AddComponent<Image>();
			raycastImage.color = Color.clear;
		}
		raycastImage.raycastTarget = true;

		EnsureIndicator();
		SetContinueVisible(false);
	}

	public void OnPointerClick(PointerEventData eventData)
	{
		if (eventData == null || eventData.button != PointerEventData.InputButton.Left)
			return;

		_onPanelClick?.Invoke();
	}

	public void SetContinueVisible(bool visible)
	{
		_continueVisible = visible;
		EnsureIndicator();
		if (_indicatorCanvasGroup == null)
			return;

		_indicatorCanvasGroup.gameObject.SetActive(visible);

		if (_blinkCoroutine != null)
		{
			StopCoroutine(_blinkCoroutine);
			_blinkCoroutine = null;
		}

		_indicatorCanvasGroup.alpha = visible ? 1f : 0f;
		if (visible && isActiveAndEnabled)
			_blinkCoroutine = StartCoroutine(BlinkIndicator());
	}

	private void EnsureIndicator()
	{
		if (_indicatorCanvasGroup != null)
			return;

		Transform existing = transform.Find(IndicatorName);
		GameObject indicatorObject = existing != null
			? existing.gameObject
			: new GameObject(IndicatorName, typeof(RectTransform));
		indicatorObject.layer = gameObject.layer;

		RectTransform rect = indicatorObject.GetComponent<RectTransform>();
		rect.SetParent(transform, false);
		rect.anchorMin = new Vector2(1f, 0f);
		rect.anchorMax = new Vector2(1f, 0f);
		rect.pivot = new Vector2(1f, 0f);
		rect.anchoredPosition = new Vector2(-18f, 12f);
		rect.sizeDelta = new Vector2(36f, 30f);

		TextMeshProUGUI icon = indicatorObject.GetComponent<TextMeshProUGUI>();
		if (icon == null)
			icon = indicatorObject.AddComponent<TextMeshProUGUI>();
		icon.text = "▼";
		icon.fontSize = 24f;
		icon.alignment = TextAlignmentOptions.Center;
		icon.color = Color.white;
		icon.raycastTarget = false;

		_indicatorCanvasGroup = indicatorObject.GetComponent<CanvasGroup>();
		if (_indicatorCanvasGroup == null)
			_indicatorCanvasGroup = indicatorObject.AddComponent<CanvasGroup>();
		_indicatorCanvasGroup.interactable = false;
		_indicatorCanvasGroup.blocksRaycasts = false;
	}

	private IEnumerator BlinkIndicator()
	{
		const float cycleDuration = 0.9f;
		while (true)
		{
			float phase = Mathf.PingPong(Time.unscaledTime * (2f / cycleDuration), 1f);
			_indicatorCanvasGroup.alpha = phase;
			yield return null;
		}
	}

	private void OnDisable()
	{
		if (_blinkCoroutine != null)
		{
			StopCoroutine(_blinkCoroutine);
			_blinkCoroutine = null;
		}
	}

	private void OnEnable()
	{
		if (_continueVisible && _indicatorCanvasGroup != null && _blinkCoroutine == null)
			_blinkCoroutine = StartCoroutine(BlinkIndicator());
	}
}
