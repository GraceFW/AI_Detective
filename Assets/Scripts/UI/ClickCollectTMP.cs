using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

[RequireComponent(typeof(TextMeshProUGUI))]
public class ClickableTMPText : MonoBehaviour, IPointerClickHandler
{
    private TextMeshProUGUI tmpText;

    // 当前文本中允许点击的词 → 线索ID
    private Dictionary<string, string> clickableTerms;

    void Awake()
    {
        tmpText = GetComponent<TextMeshProUGUI>();
    }

    /// <summary>
    /// 设置文本并注入可点击词
    /// </summary>
    public void SetText(string rawText, Dictionary<string, string> terms)
    {
        clickableTerms = terms;
        tmpText.text = InjectLinks(rawText, clickableTerms);
    }

    /// <summary>
    /// 将指定词语替换为 TMP link
    /// </summary>
    private string InjectLinks(string text, Dictionary<string, string> terms)
    {
        string result = text;

        foreach (var pair in terms)
        {
            string word = pair.Key;
            string clueId = pair.Value;

            // 简单直接替换
            result = result.Replace(
                word,
                $"<link=\"{clueId}\"><color=#4AA3FF>{word}</color></link>"
            );
        }

        return result;
    }

    /// <summary>
    /// 处理点击
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        int linkIndex = TMP_TextUtilities.FindIntersectingLink(
            tmpText,
            eventData.position,
            eventData.pressEventCamera
        );

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = tmpText.textInfo.linkInfo[linkIndex];
            string clueId = linkInfo.GetLinkID();

            Debug.Log($"[Clickable Text] Clicked clue id: {clueId}");
        }
    }
}
