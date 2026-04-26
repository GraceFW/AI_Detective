using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chooses the AI three-slot plan.
/// Normal mode keeps the previous "smart but imperfect" behavior; special story
/// mode uses the same simulator to find a guaranteed counter after reading the
/// player's locked plan.
/// </summary>
public class BattleAiPlanner
{
    /// <summary>
    /// Normal AI only simulates a trimmed candidate set, which keeps it readable
    /// and avoids making every regular battle feel solved.
    /// </summary>
    private const int DesiredCandidateCount = 10;
    private const int TopPoolCount = 6;

    private readonly BattleRuleSystem ruleSystem;
    private readonly BattleSimulator simulator;

    public BoboBattleAiMode AiMode { get; set; } = BoboBattleAiMode.Normal;

    public BattleAiPlanner(BattleRuleSystem ruleSystem, BattleSimulator simulator)
    {
        this.ruleSystem = ruleSystem;
        this.simulator = simulator;
    }

    public BattlePlan ChoosePlan(BattleModel model, BattlePlan playerPlan)
    {
        List<BattlePlan> validPlans = GenerateValidPlans(model.AI.Energy);
        if (validPlans.Count == 0)
        {
            return BattlePlan.Create(ActionType.Charge, ActionType.Charge, ActionType.Charge);
        }

        if (AiMode == BoboBattleAiMode.GuaranteedCounter)
        {
            return ChooseGuaranteedCounterPlan(model, playerPlan, validPlans);
        }

        List<CandidateScore> candidatePool = BuildCandidatePool(model, playerPlan, validPlans, DesiredCandidateCount);
        for (int i = 0; i < candidatePool.Count; i++)
        {
            CandidateScore candidate = candidatePool[i];
            SimResult simResult = simulator.Simulate(model, playerPlan, candidate.Plan);
            candidate.Score = ScorePlan(model, playerPlan, candidate.Plan, simResult);

            // Keep normal AI readable but imperfect.
            candidate.Score += Random.Range(-1.15f, 1.15f);
        }

        candidatePool.Sort(CompareCandidateDescending);
        int topPoolSize = Mathf.Min(TopPoolCount, candidatePool.Count);
        int pickIndex = Random.Range(0, topPoolSize);
        return candidatePool[pickIndex].Plan.Clone();
    }

    private BattlePlan ChooseGuaranteedCounterPlan(BattleModel model, BattlePlan playerPlan, List<BattlePlan> validPlans)
    {
        CandidateScore bestKill = null;
        CandidateScore bestSurvival = null;
        CandidateScore bestFallback = null;

        // Special story mode is allowed to read the player's full plan. It does
        // not write any state because BattleSimulator clones the source model.
        for (int i = 0; i < validPlans.Count; i++)
        {
            BattlePlan plan = validPlans[i];
            SimResult simResult = simulator.Simulate(model, playerPlan, plan);
            CandidateScore candidate = new CandidateScore(plan.Clone());
            candidate.Score = ScorePlan(model, playerPlan, plan, simResult);

            if (simResult.PlayerKilled && !simResult.AiKilled)
            {
                bestKill = PickHigherScore(bestKill, candidate);
            }

            if (!simResult.AiKilled)
            {
                bestSurvival = PickHigherScore(bestSurvival, candidate);
            }

            bestFallback = PickHigherScore(bestFallback, candidate);
        }

        if (bestKill != null)
        {
            return bestKill.Plan.Clone();
        }

        if (bestSurvival != null)
        {
            Debug.LogWarning("[BattleAiPlanner] GuaranteedCounter could not find a killing line; using the best non-losing line instead.");
            return bestSurvival.Plan.Clone();
        }

        Debug.LogWarning("[BattleAiPlanner] GuaranteedCounter could not find a safe line; using the highest scored fallback line.");
        return bestFallback != null
            ? bestFallback.Plan.Clone()
            : BattlePlan.Create(ActionType.Charge, ActionType.Charge, ActionType.Charge);
    }

    private List<BattlePlan> GenerateValidPlans(int startingEnergy)
    {
        List<BattlePlan> plans = new List<BattlePlan>();
        ActionType[] buffer = new ActionType[BattlePlan.SlotCount];
        GeneratePlanRecursive(0, startingEnergy, false, buffer, plans);
        return plans;
    }

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

    private CandidateScore PickHigherScore(CandidateScore currentBest, CandidateScore candidate)
    {
        if (candidate == null)
        {
            return currentBest;
        }

        if (currentBest == null || candidate.Score > currentBest.Score)
        {
            return candidate;
        }

        return currentBest;
    }

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

    private static int CompareCandidateDescending(CandidateScore x, CandidateScore y)
    {
        return y.Score.CompareTo(x.Score);
    }

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
