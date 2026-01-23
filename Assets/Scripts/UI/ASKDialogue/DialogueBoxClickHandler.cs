using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

public class DialogueBoxClickHandler : MonoBehaviour, IPointerClickHandler
{
    [SerializeField] private TextMeshProUGUI dialogueText;
    [SerializeField] private DialogueController dialogueController;

    private Camera GetEventCamera(PointerEventData eventData)
    {
        if (eventData != null && eventData.pressEventCamera != null)
        {
            return eventData.pressEventCamera;
        }

        if (dialogueText == null)
        {
            return null;
        }

        var canvas = dialogueText.canvas;
        if (canvas == null)
        {
            return null;
        }

        if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    public void Init(TextMeshProUGUI text, DialogueController controller)
    {
        dialogueText = text;
        dialogueController = controller;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dialogueText == null || dialogueController == null)
        {
            return;
        }

        dialogueText.ForceMeshUpdate();

        var eventCamera = GetEventCamera(eventData);

        int linkIndex = TMP_TextUtilities.FindIntersectingLink(
            dialogueText,
            eventData.position,
            eventCamera
        );

        if (linkIndex != -1)
        {
            TMP_LinkInfo linkInfo = dialogueText.textInfo.linkInfo[linkIndex];
            string clueId = linkInfo.GetLinkID();

            Debug.Log($"[DialogueBoxClickHandler] 点击线索链接: {clueId}");

            if (ClueManager.instance != null)
            {
                ClueManager.instance.RevealClue(clueId);
            }

            return;
        }

        Debug.Log($"[DialogueBoxClickHandler] 未命中链接。linkCount={dialogueText.textInfo.linkCount}");
        Debug.Log("[DialogueBoxClickHandler] 点击对话框空白区域，进入下一句");
        dialogueController.NextDialogue();
    }
}
