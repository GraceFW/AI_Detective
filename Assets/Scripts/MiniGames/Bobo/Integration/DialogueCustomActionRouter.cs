using System;
using UnityEngine;

/// <summary>
/// 对话系统中的 CustomAction 路由器。
/// 当前它只负责识别并启动 BoboBattle，但未来也可以继续在这里挂别的剧情模块。
/// </summary>
public static class DialogueCustomActionRouter
{
    /// <summary>
    /// 在 DialogueEntry.customActionId 中约定使用的动作名。
    /// </summary>
    public const string BoboBattleActionId = "BoboBattle";

    /// <summary>
    /// 尝试把一个对话节点解释成小游戏入口。
    /// 返回 false 表示这个节点不归当前路由器处理。
    /// </summary>
    public static bool TryExecute(DialogueEntry entry, Action<BoboBattleSessionResult> onComplete)
    {
        if (entry == null)
        {
            return false;
        }

        // 支持英文 ID 和中文别名，方便策划在资源中配置时更灵活。
        string actionId = string.IsNullOrWhiteSpace(entry.customActionId) ? string.Empty : entry.customActionId.Trim();
        if (!string.Equals(actionId, BoboBattleActionId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(actionId, "波波攒", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        string playerName = "玩家";
        if (NameInputDialog.Instance != null)
        {
            playerName = NameInputDialog.Instance.GetActualPlayerName();
        }

        // 由对话节点组装一次完整的小游戏请求。
        BoboBattleRequest request = new BoboBattleRequest();
        request.Title = string.IsNullOrWhiteSpace(entry.dialogueText) ? "波波攒对抗演练" : entry.dialogueText;
        request.PlayerName = playerName;
        request.AiName = "镜像AI";
        request.SourceTag = entry.customActionArgument;
        request.OnCompleted = onComplete;

        bool opened = BoboBattleService.Open(request);
        if (!opened)
        {
            Debug.LogWarning("[DialogueCustomActionRouter] 波波攒面板当前不可打开，已直接跳过该节点。");
            onComplete?.Invoke(new BoboBattleSessionResult
            {
                Winner = BattleWinner.None,
                WasCancelled = true,
                CompletedRounds = 0,
                FinalModel = null
            });
        }

        return true;
    }
}
