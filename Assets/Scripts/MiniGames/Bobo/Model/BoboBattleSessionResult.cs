using System;

[Serializable]
public class BoboBattleSessionResult
{
    /// <summary>
    /// 整局战斗的最终胜负。
    /// 如果是中途取消，通常保持 None。
    /// </summary>
    public BattleWinner Winner;

    /// <summary>
    /// 是否是通过主动关闭面板等方式结束，而不是正常打完。
    /// </summary>
    public bool WasCancelled;

    /// <summary>
    /// 已完整结算完成的回合数。
    /// </summary>
    public int CompletedRounds;

    /// <summary>
    /// 结束时的最终快照，外部流程可以据此决定奖励、分支或日志。
    /// </summary>
    public BattleModel FinalModel;
}
