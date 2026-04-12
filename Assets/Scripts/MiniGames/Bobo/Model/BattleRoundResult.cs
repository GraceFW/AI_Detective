using System;
using System.Collections.Generic;

[Serializable]
public class BattleRoundResult
{
    /// <summary>
    /// 本次返回对应的是哪一个回合。
    /// </summary>
    public int RoundIndex;

    /// <summary>
    /// 这一回合双方最终锁定的方案。
    /// </summary>
    public BattlePlan PlayerPlan;
    public BattlePlan AIPlan;

    /// <summary>
    /// 逐槽位的结算详情，顺序与实际播放顺序一致。
    /// </summary>
    public List<ActionResolveInfo> SlotInfos = new List<ActionResolveInfo>();

    /// <summary>
    /// 本回合全部结算完成后的战斗快照。
    /// UI 在回合动画结束后会基于它刷新到最新状态。
    /// </summary>
    public BattleModel SnapshotAfterRound;

    /// <summary>
    /// 本回合打完后是否已经结束整局战斗。
    /// </summary>
    public bool IsBattleFinished;
    public BattleWinner Winner;
}
