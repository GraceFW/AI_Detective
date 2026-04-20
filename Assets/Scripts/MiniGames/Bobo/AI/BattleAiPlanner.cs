using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波波攒 AI 的决策器。
/// 它不是“拍脑袋随机”，也不是“永远最优解”。
/// 当前策略是：
/// 1. 根据当前能量枚举所有合法三槽方案
/// 2. 用轻量启发式先筛一轮候选
/// 3. 对候选方案做完整模拟
/// 4. 按伤害、存活、能量、大招浪费等维度评分
/// 5. 加入随机扰动后，从 Top N 里随机拿一个
/// </summary>
public class BattleAiPlanner
{
    /// <summary>
    /// AI 会尽量把候选池控制在这个数量级，避免无意义地模拟过多方案。
    /// </summary>
    private const int DesiredCandidateCount = 10;

    /// <summary>
    /// 最终只在前几名中随机选，保证“有脑子但不完美”。
    /// </summary>
    private const int TopPoolCount = 6;

    private readonly BattleRuleSystem ruleSystem;
    private readonly BattleSimulator simulator;

    public BattleAiPlanner(BattleRuleSystem ruleSystem, BattleSimulator simulator)
    {
        this.ruleSystem = ruleSystem;
        this.simulator = simulator;
    }

    /// <summary>
    /// 读取玩家已经锁定的方案后，为 AI 选出一套三槽动作。
    /// </summary>
    public BattlePlan ChoosePlan(BattleModel model, BattlePlan playerPlan)
    {
        List<BattlePlan> validPlans = GenerateValidPlans(model.AI.Energy);
        if (validPlans.Count == 0)
        {
            // 理论上不会走到这里，因为至少还能 Guard / Charge，
            // 但保留一个兜底，避免极端情况下 AI 无法返回方案。
            return BattlePlan.Create(ActionType.Charge, ActionType.Charge, ActionType.Charge);
        }

        // 先做一次启发式筛选，再进入完整模拟，降低总开销。
        List<CandidateScore> candidatePool = BuildCandidatePool(model, playerPlan, validPlans, DesiredCandidateCount);
        for (int i = 0; i < candidatePool.Count; i++)
        {
            CandidateScore candidate = candidatePool[i];
            SimResult simResult = simulator.Simulate(model, playerPlan, candidate.Plan);
            candidate.Score = ScorePlan(model, playerPlan, candidate.Plan, simResult);

            // 最终决策故意保留一定扰动，避免每次都走完全相同的最优路径。
            candidate.Score += Random.Range(-1.15f, 1.15f);
        }

        candidatePool.Sort(CompareCandidateDescending);
        int topPoolSize = Mathf.Min(TopPoolCount, candidatePool.Count);
        int pickIndex = Random.Range(0, topPoolSize);
        return candidatePool[pickIndex].Plan.Clone();
    }

    /// <summary>
    /// 根据起始能量枚举所有合法三槽方案。
    /// 这里不读对手动作，只保证“我这三步在能量规则下能走得通”。
    /// </summary>
    private List<BattlePlan> GenerateValidPlans(int startingEnergy)
    {
        List<BattlePlan> plans = new List<BattlePlan>();
        ActionType[] buffer = new ActionType[BattlePlan.SlotCount];
        GeneratePlanRecursive(0, startingEnergy, false, buffer, plans);
        return plans;
    }

    /// <summary>
    /// 递归构造完整方案树。
    /// 每深入一层，都会把当前槽位的动作效果投影到后续能量上。
    /// </summary>
    private void GeneratePlanRecursive(int slotIndex, int currentEnergy, bool hasUsedGuard, ActionType[] buffer, List<BattlePlan> plans)
    {
        if (slotIndex >= BattlePlan.SlotCount)
        {
            plans.Add(new BattlePlan(buffer));
            return;
        }

        List<ActionType> availableActions = GetAvailableActions(currentEnergy, hasUsedGuard);
        for (int i = 0; i < availableActions.Count; i++)
        {
            ActionType actionType = availableActions[i];
            buffer[slotIndex] = actionType;
            int nextEnergy = ruleSystem.ProjectEnergyAfterAction(currentEnergy, actionType);
            bool nextHasUsedGuard = hasUsedGuard || actionType == ActionType.Guard;
            GeneratePlanRecursive(slotIndex + 1, nextEnergy, nextHasUsedGuard, buffer, plans);
        }
    }

    /// <summary>
    /// 给定当前能量，返回这一槽理论上可以用的动作集合。
    /// </summary>
    private List<ActionType> GetAvailableActions(int currentEnergy, bool hasUsedGuard)
    {
        List<ActionType> actions = new List<ActionType>(4);
        actions.Add(ActionType.Charge);
        if (!hasUsedGuard)
        {
            actions.Add(ActionType.Guard);
        }

        if (ruleSystem.CanAffordAction(currentEnergy, ActionType.Attack))
        {
            actions.Add(ActionType.Attack);
        }

        if (ruleSystem.CanAffordAction(currentEnergy, ActionType.Ultimate))
        {
            actions.Add(ActionType.Ultimate);
        }

        return actions;
    }

    /// <summary>
    /// 从所有合法方案中筛出一个规模合适、分布相对均匀的候选池。
    /// 这样做的目标是减少模拟次数，同时避免候选过于同质化。
    /// </summary>
    private List<CandidateScore> BuildCandidatePool(BattleModel model, BattlePlan playerPlan, List<BattlePlan> validPlans, int desiredCount)
    {
        List<CandidateScore> seededCandidates = new List<CandidateScore>();
        for (int i = 0; i < validPlans.Count; i++)
        {
            CandidateScore candidate = new CandidateScore(validPlans[i].Clone());
            candidate.Score = SeedPlanScore(model, playerPlan, candidate.Plan);
            candidate.Score += Random.Range(-0.15f, 0.15f);
            seededCandidates.Add(candidate);
        }

        seededCandidates.Sort(CompareCandidateDescending);
        if (seededCandidates.Count <= desiredCount)
        {
            return seededCandidates;
        }

        List<CandidateScore> selected = new List<CandidateScore>();
        int frontCount = Mathf.Max(4, desiredCount / 2);
        for (int i = 0; i < seededCandidates.Count && selected.Count < frontCount; i++)
        {
            selected.Add(seededCandidates[i]);
        }

        int stride = Mathf.Max(1, seededCandidates.Count / desiredCount);
        for (int i = stride / 2; i < seededCandidates.Count && selected.Count < desiredCount; i += stride)
        {
            CandidateScore candidate = seededCandidates[i];
            if (!ContainsPlan(selected, candidate.Plan))
            {
                selected.Add(candidate);
            }
        }

        for (int i = 0; i < seededCandidates.Count && selected.Count < desiredCount; i++)
        {
            CandidateScore candidate = seededCandidates[i];
            if (!ContainsPlan(selected, candidate.Plan))
            {
                selected.Add(candidate);
            }
        }

        return selected;
    }

    /// <summary>
    /// 第一层启发式打分。
    /// 它不跑完整模拟，只根据动作对位关系和当前局势给出一个“值得不值得深入看”的粗分。
    /// </summary>
    private float SeedPlanScore(BattleModel model, BattlePlan playerPlan, BattlePlan aiPlan)
    {
        float score = 0f;

        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            ActionType playerAction = playerPlan[i];
            ActionType aiAction = aiPlan[i];

            if (playerAction.IsOffensive() && aiAction == ActionType.Guard)
            {
                score += 1.8f;
            }

            if (playerAction == ActionType.Charge && aiAction == ActionType.Attack)
            {
                score += 1.2f;
            }

            if (playerAction == ActionType.Charge && aiAction == ActionType.Ultimate)
            {
                score += 2.0f;
            }

            if (playerAction == ActionType.Ultimate && aiAction == ActionType.Ultimate)
            {
                score += 1.0f;
            }

            if (playerAction == ActionType.Attack && aiAction == ActionType.Attack)
            {
                score += 0.4f;
            }
        }

        if (model.Player.HP <= 1 && (aiPlan.Contains(ActionType.Attack) || aiPlan.Contains(ActionType.Ultimate)))
        {
            score += 1.2f;
        }

        if (model.AI.HP <= 1 && aiPlan.Contains(ActionType.Guard))
        {
            score += 0.9f;
        }

        if (model.AI.Energy >= 3 && aiPlan.Contains(ActionType.Ultimate))
        {
            score += 0.65f;
        }

        return score;
    }

    /// <summary>
    /// 完整模拟后的正式评分。
    /// 这里直接体现了当前版本 AI 的价值取向：
    /// 更看重击杀和净赚伤害，其次是保命和剩余能量，同时惩罚空放大招。
    /// </summary>
    private float ScorePlan(BattleModel model, BattlePlan playerPlan, BattlePlan aiPlan, SimResult simResult)
    {
        float score = 0f;
        score += simResult.DamageToPlayer * 4.0f;
        score -= simResult.DamageToAI * 4.8f;
        score += simResult.FinalModel.AI.Energy * 1.15f;
        score += simResult.FinalModel.AI.HP * 0.85f;
        score -= simResult.WastedUltimatesByAI * 3.0f;

        if (simResult.PlayerKilled)
        {
            score += 18.0f;
        }

        if (simResult.AiKilled)
        {
            score -= 20.0f;
        }

        if (playerPlan.Contains(ActionType.Ultimate) && aiPlan.Contains(ActionType.Guard))
        {
            score += 0.75f;
        }

        if (aiPlan.Contains(ActionType.Ultimate) && simResult.WastedUltimatesByAI == 0)
        {
            score += 0.65f;
        }

        return score;
    }

    /// <summary>
    /// 检查候选集中是否已经存在相同方案，避免重复模拟。
    /// </summary>
    private bool ContainsPlan(List<CandidateScore> candidates, BattlePlan plan)
    {
        for (int i = 0; i < candidates.Count; i++)
        {
            if (PlansEqual(candidates[i].Plan, plan))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 比较两个 BattlePlan 是否完全相同。
    /// </summary>
    private bool PlansEqual(BattlePlan left, BattlePlan right)
    {
        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            if (left[i] != right[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// 分数高的排前面。
    /// </summary>
    private static int CompareCandidateDescending(CandidateScore x, CandidateScore y)
    {
        return y.Score.CompareTo(x.Score);
    }

    /// <summary>
    /// AI 内部使用的候选项结构。
    /// </summary>
    private class CandidateScore
    {
        public BattlePlan Plan;
        public float Score;

        public CandidateScore(BattlePlan plan)
        {
            Plan = plan;
        }
    }
}
