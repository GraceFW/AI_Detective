using System;

[Serializable]
public class ActionResolveInfo
{
    /// <summary>
    /// 本条记录属于第几回合、第几个槽位。
    /// </summary>
    public int RoundIndex;
    public int SlotIndex;

    /// <summary>
    /// Selected 表示双方原始选择，Executed 表示经过能量校验后真正执行的动作。
    /// 例如玩家点了 Ultimate，但能量不足，Executed 就会退化为 None。
    /// </summary>
    public ActionType PlayerSelectedAction;
    public ActionType AiSelectedAction;
    public ActionType PlayerExecutedAction;
    public ActionType AiExecutedAction;

    /// <summary>
    /// 槽位结算前后的生命值快照。
    /// </summary>
    public int PlayerHPBefore;
    public int PlayerHPAfter;
    public int AiHPBefore;
    public int AiHPAfter;

    /// <summary>
    /// 槽位结算前后的能量快照。
    /// </summary>
    public int PlayerEnergyBefore;
    public int PlayerEnergyAfter;
    public int AiEnergyBefore;
    public int AiEnergyAfter;

    /// <summary>
    /// 方便 UI 和日志直接读取的净变化量。
    /// </summary>
    public int PlayerEnergyChange;
    public int AiEnergyChange;

    /// <summary>
    /// 本槽位造成的实际伤害。
    /// 已经包含格挡、取消、Buff 修改后的结果。
    /// </summary>
    public int DamageToPlayer;
    public int DamageToAI;

    /// <summary>
    /// 记录本槽位中出现的关键结算标记。
    /// </summary>
    public bool PlayerBlocked;
    public bool AiBlocked;
    public bool PlayerActionCancelled;
    public bool AiActionCancelled;
    public bool PlayerUltimateWasted;
    public bool AiUltimateWasted;
    public bool PlayerFailedByEnergy;
    public bool AiFailedByEnergy;
    public bool BattleEndedAfterSlot;
    public BattleWinner WinnerAfterSlot;

    public string Summary;

    /// <summary>
    /// 当前对象只包含值类型和字符串，MemberwiseClone 已足够。
    /// </summary>
    public ActionResolveInfo Clone()
    {
        return (ActionResolveInfo)MemberwiseClone();
    }
}
