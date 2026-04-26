using System;
using UnityEngine;

/// <summary>
/// 战斗流程控制器。
/// 它负责把“玩家输入 -> AI 决策 -> 逐槽结算 -> 胜负判断 -> 结果广播”这条主流程串起来，
/// 但它本身不负责具体规则计算，也不直接操作任何 UI 控件。
/// </summary>
public class BattleController
{
    private readonly BattleAiPlanner aiPlanner;

    /// <summary>
    /// 对外暴露规则系统，主要是为了给 UI 做能量预校验和投影。
    /// </summary>
    public BattleRuleSystem RuleSystem { get; private set; }

    /// <summary>
    /// 当前真实战斗状态。
    /// </summary>
    public BattleModel Model { get; private set; }

    public BoboBattleAiMode AiMode
    {
        get { return aiPlanner != null ? aiPlanner.AiMode : BoboBattleAiMode.Normal; }
        set
        {
            if (aiPlanner != null)
            {
                aiPlanner.AiMode = value;
            }
        }
    }

    /// <summary>
    /// 状态变化广播。UI 收到的是克隆快照，而不是内部真实对象。
    /// </summary>
    public event Action<BattleModel> BattleStateChanged;

    /// <summary>
    /// 单回合完整结算后广播。
    /// </summary>
    public event Action<BattleRoundResult> RoundResolved;

    /// <summary>
    /// 整局战斗结束时广播。
    /// </summary>
    public event Action<BoboBattleSessionResult> BattleEnded;

    public BattleController(BattleRuleSystem ruleSystem, BattleAiPlanner aiPlanner)
    {
        RuleSystem = ruleSystem;
        this.aiPlanner = aiPlanner;
    }

    /// <summary>
    /// 初始化一场新战斗。
    /// 这一步会重置双方状态、回合数、方案缓存以及最近结算记录。
    /// </summary>
    public void StartNewBattle(string playerName, string aiName, int startingHp, int startingEnergy)
    {
        int normalizedEnergy = RuleSystem != null ? RuleSystem.ClampEnergy(startingEnergy) : Mathf.Clamp(startingEnergy, 0, BattleRuleSystem.MaxEnergy);

        Model = new BattleModel();
        Model.Player = new FighterState(playerName, startingHp, normalizedEnergy);
        Model.AI = new FighterState(aiName, startingHp, normalizedEnergy);
        Model.RoundIndex = 1;
        Model.IsFinished = false;
        Model.Winner = BattleWinner.None;
        Model.CurrentPlayerPlan.Clear();
        Model.CurrentAiPlan.Clear();
        Model.LastRoundInfos.Clear();
        RaiseBattleStateChanged();
    }

    /// <summary>
    /// 尝试提交玩家本回合方案，并立即完成这一回合的真实结算。
    /// 返回 false 时说明提交失败，例如能量不足、战斗未初始化或战斗已结束。
    /// </summary>
    public bool TrySubmitPlayerPlan(BattlePlan playerPlan, out BattleRoundResult roundResult, out string errorMessage)
    {
        roundResult = null;
        errorMessage = string.Empty;

        if (Model == null || Model.Player == null || Model.AI == null)
        {
            errorMessage = "战斗尚未初始化。";
            return false;
        }

        if (Model.IsFinished)
        {
            errorMessage = "当前战斗已经结束。";
            return false;
        }

        if (playerPlan == null)
        {
            errorMessage = "玩家行动不能为空。";
            return false;
        }

        int invalidSlotIndex;
        if (!RuleSystem.ValidatePlan(Model.Player.Energy, playerPlan.Slots, out invalidSlotIndex, out errorMessage))
        {
            return false;
        }

        // 先锁定玩家方案，再基于当前真实状态和玩家方案生成 AI 方案。
        int resolvingRoundIndex = Model.RoundIndex;
        Model.CurrentPlayerPlan = playerPlan.Clone();
        Model.CurrentAiPlan = aiPlanner.ChoosePlan(Model, Model.CurrentPlayerPlan);
        Model.LastRoundInfos.Clear();

        // 逐槽推进。每一槽的结果都会立即影响后续槽位的血量和能量。
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            ActionResolveInfo resolveInfo = RuleSystem.ResolveSlot(Model, i, Model.CurrentPlayerPlan[i], Model.CurrentAiPlan[i]);
            Model.Winner = DetermineWinner(Model);
            Model.IsFinished = Model.Winner != BattleWinner.None;
            resolveInfo.BattleEndedAfterSlot = Model.IsFinished;
            resolveInfo.WinnerAfterSlot = Model.Winner;
            Model.LastRoundInfos.Add(resolveInfo);

            if (Model.IsFinished)
            {
                break;
            }
        }

        // 组装一个适合 UI 和外部系统消费的回合结果对象。
        roundResult = new BattleRoundResult();
        roundResult.RoundIndex = resolvingRoundIndex;
        roundResult.PlayerPlan = Model.CurrentPlayerPlan.Clone();
        roundResult.AIPlan = Model.CurrentAiPlan.Clone();
        for (int i = 0; i < Model.LastRoundInfos.Count; i++)
        {
            if (Model.LastRoundInfos[i] != null)
            {
                roundResult.SlotInfos.Add(Model.LastRoundInfos[i].Clone());
            }
        }

        if (!Model.IsFinished)
        {
            // 只有整回合打完且无人死亡，才会推进到下一回合。
            Model.RoundIndex++;
        }

        roundResult.SnapshotAfterRound = Model.Clone();
        roundResult.IsBattleFinished = Model.IsFinished;
        roundResult.Winner = Model.Winner;

        RaiseBattleStateChanged();
        RoundResolved?.Invoke(roundResult);

        if (Model.IsFinished)
        {
            BattleEnded?.Invoke(BuildSessionResult(false));
        }

        return true;
    }

    /// <summary>
    /// 组装整局结束结果。
    /// UI 关闭面板、剧情系统接回主流程时，都会消费这个对象。
    /// </summary>
    public BoboBattleSessionResult BuildSessionResult(bool wasCancelled)
    {
        BoboBattleSessionResult result = new BoboBattleSessionResult();
        result.Winner = Model != null ? Model.Winner : BattleWinner.None;
        result.WasCancelled = wasCancelled;
        result.FinalModel = Model != null ? Model.Clone() : null;

        if (Model == null)
        {
            result.CompletedRounds = 0;
            return result;
        }

        int completedRounds = Model.RoundIndex - (Model.IsFinished ? 0 : 1);
        result.CompletedRounds = Mathf.Max(0, completedRounds);
        return result;
    }

    /// <summary>
    /// 根据当前 HP 判断是否分出胜负。
    /// </summary>
    private BattleWinner DetermineWinner(BattleModel battleModel)
    {
        bool playerDead = battleModel.Player.HP <= 0;
        bool aiDead = battleModel.AI.HP <= 0;

        if (playerDead && aiDead)
        {
            return BattleWinner.Draw;
        }

        if (aiDead)
        {
            return BattleWinner.Player;
        }

        if (playerDead)
        {
            return BattleWinner.AI;
        }

        return BattleWinner.None;
    }

    /// <summary>
    /// 对外广播当前状态的快照。
    /// 使用 Clone 可以避免 UI 或其他监听者意外篡改真实模型。
    /// </summary>
    private void RaiseBattleStateChanged()
    {
        BattleStateChanged?.Invoke(Model != null ? Model.Clone() : null);
    }
}
