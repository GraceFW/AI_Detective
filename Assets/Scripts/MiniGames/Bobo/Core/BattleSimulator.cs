/// <summary>
/// 战斗模拟器。
/// 它的职责是“在不污染真实战斗状态的前提下，完整跑一遍规则”。
/// AI 决策完全依赖它来预估不同方案的收益。
/// </summary>
public class BattleSimulator
{
    private readonly BattleRuleSystem ruleSystem;

    /// <summary>
    /// 模拟器不自己持有规则，直接复用真实战斗的 RuleSystem，
    /// 保证“AI 看到的世界”和“真实结算的世界”是同一套规则。
    /// </summary>
    public BattleSimulator(BattleRuleSystem ruleSystem)
    {
        this.ruleSystem = ruleSystem;
    }

    /// <summary>
    /// 基于输入模型和双方方案，返回一份完整的模拟结果。
    /// 这里会先 Clone 模型，再逐槽结算，因此不会影响真实战斗中的 HP / Energy。
    /// </summary>
    public SimResult Simulate(BattleModel sourceModel, BattlePlan playerPlan, BattlePlan aiPlan)
    {
        BattleModel simulatedModel = sourceModel.Clone();
        simulatedModel.CurrentPlayerPlan = playerPlan.Clone();
        simulatedModel.CurrentAiPlan = aiPlan.Clone();
        simulatedModel.LastRoundInfos.Clear();

        SimResult result = new SimResult();
        result.FinalModel = simulatedModel;

        // 按真实规则完全一致的顺序逐槽推进，直到三槽结束或有人提前死亡。
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            ActionResolveInfo resolveInfo = ruleSystem.ResolveSlot(simulatedModel, i, playerPlan[i], aiPlan[i]);
            result.SlotInfos.Add(resolveInfo);
            simulatedModel.LastRoundInfos.Add(resolveInfo);
            result.DamageToPlayer += resolveInfo.DamageToPlayer;
            result.DamageToAI += resolveInfo.DamageToAI;

            if (resolveInfo.PlayerUltimateWasted)
            {
                result.WastedUltimatesByPlayer++;
            }

            if (resolveInfo.AiUltimateWasted)
            {
                result.WastedUltimatesByAI++;
            }

            if (simulatedModel.Player.HP <= 0 || simulatedModel.AI.HP <= 0)
            {
                break;
            }
        }

        result.PlayerKilled = simulatedModel.Player.HP <= 0;
        result.AiKilled = simulatedModel.AI.HP <= 0;
        return result;
    }
}
