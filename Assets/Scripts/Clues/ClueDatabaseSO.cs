using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Clue/Clue Database")]
public class ClueDatabaseSO : ScriptableObject
{
    public List<ClueData> clues = new List<ClueData>();

    private Dictionary<string, ClueData> _byId;

    private void OnEnable()
    {
        _byId = null;
    }

    public bool TryGetClue(string clueId, out ClueData clue)
    {
        clue = null;

        if (string.IsNullOrEmpty(clueId))
        {
            return false;
        }

        EnsureIndex();
        return _byId.TryGetValue(clueId, out clue);
    }

    private void EnsureIndex()
    {
        if (_byId != null)
        {
            return;
        }

        _byId = new Dictionary<string, ClueData>(StringComparer.Ordinal);

        foreach (var clue in clues)
        {
            if (clue == null || string.IsNullOrEmpty(clue.id))
            {
                continue;
            }

            if (!_byId.ContainsKey(clue.id))
            {
                _byId.Add(clue.id, clue);
            }
        }
    }
}
