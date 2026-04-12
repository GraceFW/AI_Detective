using System;
using System.Collections.Generic;

[Serializable]
public class BattleModel
{
    /// <summary>
    /// 玩家侧当前实时状态。
    /// </summary>
    public FighterState Player;

    /// <summary>
    /// AI 侧当前实时状态。
    /// </summary>
    public FighterState AI;

    /// <summary>
    /// 从 1 开始计数的回合索引。
    /// 回合结束且未分出胜负时才会自增。
    /// </summary>
    public int RoundIndex = 1;

    /// <summary>
    /// 当前对局是否已经结束。
    /// </summary>
    public bool IsFinished;

    /// <summary>
    /// 结束后的胜负结果；未结束时保持 None。
    /// </summary>
    public BattleWinner Winner = BattleWinner.None;

    /// <summary>
    /// 玩家本回合已经锁定的三槽方案。
    /// </summary>
    public BattlePlan CurrentPlayerPlan = new BattlePlan();

    /// <summary>
    /// AI 本回合已经选出的三槽方案。
    /// </summary>
    public BattlePlan CurrentAiPlan = new BattlePlan();

    /// <summary>
    /// 最近一次结算得到的逐槽信息。
    /// UI 动画播放、日志和调试都可以依赖这份数据。
    /// </summary>
    public List<ActionResolveInfo> LastRoundInfos = new List<ActionResolveInfo>();

    /// <summary>
    /// 深拷贝整场战斗快照。
    /// Controller 对外抛事件时会尽量传递副本，避免 UI 意外修改真实状态。
    /// </summary>
    public BattleModel Clone()
    {
        BattleModel clone = new BattleModel();
        clone.Player = Player != null ? Player.Clone() : null;
        clone.AI = AI != null ? AI.Clone() : null;
        clone.RoundIndex = RoundIndex;
        clone.IsFinished = IsFinished;
        clone.Winner = Winner;
        clone.CurrentPlayerPlan = CurrentPlayerPlan != null ? CurrentPlayerPlan.Clone() : new BattlePlan();
        clone.CurrentAiPlan = CurrentAiPlan != null ? CurrentAiPlan.Clone() : new BattlePlan();

        if (LastRoundInfos != null)
        {
            for (int i = 0; i < LastRoundInfos.Count; i++)
            {
                if (LastRoundInfos[i] != null)
                {
                    clone.LastRoundInfos.Add(LastRoundInfos[i].Clone());
                }
            }
        }

        return clone;
    }
}
