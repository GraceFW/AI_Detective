using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Clue/Person Clue")]
public class PersonClueData : ClueData
{
    [Header("Person Visual")]
    public Sprite portrait;

    [Header("Base Dialogue")]
    [Tooltip("Dialogue shown when talking to this person normally")]
    public List<DialogueNode> baseDialogues;

    [Header("Clue-Triggered Dialogues")]
    [Tooltip("Dialogue triggered when showing other clues to this person")]
    public List<ClueDialogueEntry> clueDialogues;
}


[System.Serializable]
public class DialogueNode
{
    [Tooltip("Unique ID inside this person")]
    public string nodeId;

    [TextArea(3, 10)]
    public string text;

    [Header("Flow Control")]
    [Tooltip("Next node if there are no options (click to continue)")]
    public string nextNodeId;

    [Tooltip("Optional dialogue choices")]
    public List<DialogueOption> options;
}

[System.Serializable]
public class DialogueOption
{
    public string optionText;

    [Tooltip("Next dialogue node id")]
    public string nextNodeId;
}

[System.Serializable]
public class ClueDialogueEntry
{
    [Tooltip("The clue shown to this person")]
    public ClueData shownClue;

    [Tooltip("Dialogue nodes triggered by showing this clue")]
    public List<DialogueNode> dialogues;

    [Tooltip("Can this dialogue be triggered only once")]
    public bool singleUse = true;
}
