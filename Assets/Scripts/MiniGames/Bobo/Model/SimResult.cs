using System;
using System.Collections.Generic;

[Serializable]
public class SimResult
{
    /// <summary>
    /// 模拟完成后的最终战斗状态。
    /// 这是对克隆模型操作后的结果，不会回写真实战斗。
    /// </summary>
    public BattleModel FinalModel;

    /// <summary>
    /// 模拟期间逐槽得到的结算记录。
    /// AI 如果需要进一步做解释型决策，也可以消费这些明细。
    /// </summary>
    public List<ActionResolveInfo> SlotInfos = new List<ActionResolveInfo>();

    /// <summary>
    /// AI 评分时直接使用的聚合指标。
    /// </summary>
    public int DamageToPlayer;
    public int DamageToAI;
    public int WastedUltimatesByPlayer;
    public int WastedUltimatesByAI;
    public bool PlayerKilled;
    public bool AiKilled;
}
