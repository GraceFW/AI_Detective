using System;
using UnityEngine;

public static class DialogueCustomActionRouter
{
    public const string BoboBattleActionId = "BoboBattle";
    public const string BoboStoryFlowActionId = "BoboStoryFlow";
    public const string SwitchBgmActionId = "SwitchBGM";

    public static bool TryExecute(DialogueEntry entry, Action<BoboBattleSessionResult> onComplete)
    {
        if (entry == null)
        {
            return false;
        }

        string actionId = string.IsNullOrWhiteSpace(entry.customActionId)
            ? string.Empty
            : entry.customActionId.Trim();

        if (IsStoryFlowAction(actionId))
        {
            ExecuteStoryFlow(entry, onComplete);
            return true;
        }

        if (IsBgmAction(actionId))
        {
            ExecuteBgmSwitch(entry, onComplete);
            return true;
        }

        if (!IsBattleAction(actionId))
        {
            return false;
        }

        ExecuteSingleBattle(entry, onComplete);
        return true;
    }

    public static bool ShouldShowDialogueText(DialogueEntry entry)
    {
        if (entry == null)
        {
            return false;
        }

        string actionId = string.IsNullOrWhiteSpace(entry.customActionId)
            ? string.Empty
            : entry.customActionId.Trim();

        return IsBgmAction(actionId);
    }

    private static bool IsBattleAction(string actionId)
    {
        return string.Equals(actionId, BoboBattleActionId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionId, "\u6ce2\u6ce2\u6512", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsStoryFlowAction(string actionId)
    {
        return string.Equals(actionId, BoboStoryFlowActionId, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBgmAction(string actionId)
    {
        return string.Equals(actionId, SwitchBgmActionId, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionId, "BGM", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionId, "PlayBGM", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(actionId, "切换BGM", StringComparison.OrdinalIgnoreCase);
    }

    private static void ExecuteBgmSwitch(DialogueEntry entry, Action<BoboBattleSessionResult> onComplete)
    {
        if (BGMManager.Instance == null)
        {
            Debug.LogWarning("[DialogueCustomActionRouter] SwitchBGM requested, but no BGMManager exists in the scene.");
            onComplete?.Invoke(CreateCancelledResult());
            return;
        }

        BGMManager.Instance.Play(entry.customBgmTrack);
        onComplete?.Invoke(new BoboBattleSessionResult
        {
            Winner = BattleWinner.None,
            WasCancelled = false,
            CompletedRounds = 0,
            FinalModel = null
        });
    }

    private static void ExecuteSingleBattle(DialogueEntry entry, Action<BoboBattleSessionResult> onComplete)
    {
        BoboBattleRequest request = new BoboBattleRequest();
        request.Title = string.IsNullOrWhiteSpace(entry.dialogueText) ? "\u6ce2\u6ce2\u6512\u5bf9\u6297\u6f14\u7ec3" : entry.dialogueText;
        request.PlayerName = ResolvePlayerName();
        request.AiName = "\u955c\u50cfAI";
        request.SourceTag = entry.customActionArgument;
        request.AutoCompleteOnBattleEnd = true;
        request.OnCompleted = onComplete;

        bool opened = BoboBattleService.Open(request);
        if (!opened)
        {
            Debug.LogWarning("[DialogueCustomActionRouter] BoboBattle panel is unavailable. The custom action was skipped.");
            onComplete?.Invoke(CreateCancelledResult());
        }
    }

    private static void ExecuteStoryFlow(DialogueEntry entry, Action<BoboBattleSessionResult> onComplete)
    {
        BoboStoryFlowController controller = UnityEngine.Object.FindObjectOfType<BoboStoryFlowController>();
        if (controller == null)
        {
            Debug.LogWarning("[DialogueCustomActionRouter] BoboStoryFlow requested, but no BoboStoryFlowController exists in the scene.");
            onComplete?.Invoke(CreateCancelledResult());
            return;
        }

        controller.ScheduleStartAfterCurrentDialogue(entry.customActionArgument);

        // The flow is intentionally scheduled, not opened here, so the current
        // dialogue sequence can finish without re-entering DialogueManager.
        onComplete?.Invoke(new BoboBattleSessionResult
        {
            Winner = BattleWinner.None,
            WasCancelled = false,
            CompletedRounds = 0,
            FinalModel = null
        });
    }

    private static string ResolvePlayerName()
    {
        if (NameInputDialog.Instance != null)
        {
            return NameInputDialog.Instance.GetActualPlayerName();
        }

        return "\u73a9\u5bb6";
    }

    private static BoboBattleSessionResult CreateCancelledResult()
    {
        return new BoboBattleSessionResult
        {
            Winner = BattleWinner.None,
            WasCancelled = true,
            CompletedRounds = 0,
            FinalModel = null
        };
    }
}
