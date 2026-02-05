using System;
using System.Collections.Generic;
using UnityEngine;

public class ClueManager : MonoBehaviour
{
    // 简单的运行时单例（用于 Demo/测试）。正式项目可替换为更健壮的服务管理方式。
    public static ClueManager instance;

    [Header("Database")]
    // 绑定一个包含所有 ClueData 资源的 ClueDatabaseSO。
    [SerializeField] private ClueDatabaseSO clueDatabase;

    // 当线索首次被揭示/收集时触发。
    public event Action<ClueData> OnClueRevealed;

    private readonly HashSet<string> _revealedIds = new HashSet<string>(StringComparer.Ordinal);

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            InitializeClues();
        }
        else
        {
            Destroy(this.gameObject);
            return;
        }
    }

    /// <summary>
    /// 初始化线索数据库：将所有线索的 collected 状态重置为 false
    /// </summary>
    private void InitializeClues()
    {
        if (clueDatabase == null)
        {
            Debug.LogWarning("[ClueManager] clueDatabase 未配置，无法初始化线索状态");
            return;
        }

        if (clueDatabase.clues == null)
        {
            Debug.LogWarning("[ClueManager] clueDatabase.clues 为空");
            return;
        }

        int resetCount = 0;
        foreach (var clue in clueDatabase.clues)
        {
            if (clue != null)
            {
                clue.collected = false;
                resetCount++;
            }
        }

        Debug.Log($"[ClueManager] 已初始化 {resetCount} 个线索的 collected 状态为 false");
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
        // 幂等：若已揭示或参数/数据无效则返回 false。
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

        // [SFX] 播放新线索提示音（仅在真正新增时播放）
        if (SfxManager.Instance != null)
        {
            SfxManager.Instance.Play(SfxId.NewClue);
        }

        // 通知 UI / 叙事系统等监听者。
        OnClueRevealed?.Invoke(clue);
        Debug.Log($"[ClueManager] Revealed clue: {clue.id} / {clue.displayName}");
        return true;
    }
}
