using System;
using System.Collections.Generic;
using UnityEngine;

public class ClueManager : MonoBehaviour
{
    public static ClueManager instance;

    [Header("Database")]
    [SerializeField] private ClueDatabaseSO clueDatabase;

    public event Action<ClueData> OnClueRevealed;

    private readonly HashSet<string> _revealedIds = new HashSet<string>(StringComparer.Ordinal);

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }
    }

    public bool IsRevealed(string clueId)
    {
        if (string.IsNullOrEmpty(clueId))
        {
            return false;
        }

        return _revealedIds.Contains(clueId);
    }

    public bool RevealClue(string clueId)
    {
        if (string.IsNullOrEmpty(clueId))
        {
            Debug.LogWarning("ClueManager.RevealClue called with empty clueId.");
            return false;
        }

        if (_revealedIds.Contains(clueId))
        {
            return false;
        }

        if (clueDatabase == null)
        {
            Debug.LogError("ClueManager: clueDatabase is not assigned.");
            return false;
        }

        if (!clueDatabase.TryGetClue(clueId, out var clue) || clue == null)
        {
            Debug.LogWarning($"ClueManager: clueId not found in database: {clueId}");
            return false;
        }

        _revealedIds.Add(clueId);
        clue.collected = true;

        OnClueRevealed?.Invoke(clue);
        Debug.Log($"[ClueManager] Revealed clue: {clue.id} / {clue.displayName}");
        return true;
    }
}
