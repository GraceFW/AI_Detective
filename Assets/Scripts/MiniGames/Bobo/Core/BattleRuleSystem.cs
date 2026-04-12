using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 波波攒的唯一规则入口。
/// 任何和“动作是否合法”“这一槽怎么结算”“伤害怎么算”相关的逻辑都应该集中放在这里，
/// 避免规则散落在 Controller、AI 或 UI 里，后续改玩法时更容易维护。
/// </summary>
public class BattleRuleSystem
{
    /// <summary>
    /// 预检查一套三槽方案是否可执行。
    /// 这里不会改真实状态，只会沿着槽位顺序预测能量变化并检查动作是否可用。
    /// </summary>
    public bool ValidatePlan(int startingEnergy, IReadOnlyList<ActionType> actions, out int invalidSlotIndex, out string errorMessage)
    {
        invalidSlotIndex = -1;
        errorMessage = string.Empty;

        if (actions == null || actions.Count < BattlePlan.SlotCount)
        {
            errorMessage = "行动槽数量不足。";
            return false;
        }

        int predictedEnergy = startingEnergy;

        for (int i = 0; i < BattlePlan.SlotCount; i++)
        {
            ActionType actionType = actions[i];
            if (actionType == ActionType.None)
            {
                invalidSlotIndex = i;
                errorMessage = string.Format("第{0}槽尚未选择行动。", i + 1);
                return false;
            }

            if (!CanAffordAction(predictedEnergy, actionType))
            {
                invalidSlotIndex = i;
                errorMessage = string.Format("第{0}槽能量不足，无法使用{1}。", i + 1, actionType.GetDisplayName());
                return false;
            }

            predictedEnergy = ProjectEnergyAfterAction(predictedEnergy, actionType);
        }

        return true;
    }

    /// <summary>
    /// 判断当前能量是否足以执行某个动作。
    /// Charge 和 Guard 不消耗能量，因此始终可执行。
    /// </summary>
    public bool CanAffordAction(int currentEnergy, ActionType actionType)
    {
        return currentEnergy >= GetEnergyCost(actionType);
    }

    /// <summary>
    /// 读取动作的能量消耗。
    /// 如果后面要做数值配置化，这里会是一个很自然的抽离点。
    /// </summary>
    public int GetEnergyCost(ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Attack:
                return 1;
            case ActionType.Ultimate:
                return 3;
            default:
                return 0;
        }
    }

    /// <summary>
    /// 预测执行某动作后，战斗者的能量会变成多少。
    /// 它用于 UI 提前校验后续槽位，也用于 AI 枚举合法方案。
    /// </summary>
    public int ProjectEnergyAfterAction(int currentEnergy, ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Charge:
                return currentEnergy + 1;
            case ActionType.Attack:
                return currentEnergy - 1;
            case ActionType.Ultimate:
                return currentEnergy - 3;
            default:
                return currentEnergy;
        }
    }

    /// <summary>
    /// 结算单个槽位，是本小游戏最核心的方法。
    /// 执行顺序大致分为：
    /// 1. 记录前状态
    /// 2. 根据能量判定动作是否真正执行
    /// 3. 扣除动作消耗
    /// 4. 触发 OnCast Buff
    /// 5. 处理 Charge 获得能量
    /// 6. 处理进攻/防御/对撞
    /// 7. 触发 OnResolveEnd Buff
    /// 8. 回填前后状态与摘要
    /// </summary>
    public ActionResolveInfo ResolveSlot(BattleModel model, int slotIndex, ActionType playerSelectedAction, ActionType aiSelectedAction)
    {
        FighterState player = model.Player;
        FighterState ai = model.AI;

        // 先把这一槽开始前的快照记录下来，方便 UI 和日志回放。
        ActionResolveInfo info = new ActionResolveInfo();
        info.RoundIndex = model.RoundIndex;
        info.SlotIndex = slotIndex;
        info.PlayerSelectedAction = playerSelectedAction;
        info.AiSelectedAction = aiSelectedAction;
        info.PlayerHPBefore = player.HP;
        info.PlayerEnergyBefore = player.Energy;
        info.AiHPBefore = ai.HP;
        info.AiEnergyBefore = ai.Energy;

        // Selected 是“玩家/AI 选择了什么”，Executed 是“最终能不能真的放出来”。
        info.PlayerExecutedAction = GetExecutableAction(player, playerSelectedAction);
        info.AiExecutedAction = GetExecutableAction(ai, aiSelectedAction);
        info.PlayerFailedByEnergy = info.PlayerExecutedAction == ActionType.None && playerSelectedAction != ActionType.None;
        info.AiFailedByEnergy = info.AiExecutedAction == ActionType.None && aiSelectedAction != ActionType.None;

        // 先扣消耗，再处理动作效果，确保行为顺序稳定一致。
        SpendActionEnergy(player, info.PlayerExecutedAction);
        SpendActionEnergy(ai, info.AiExecutedAction);

        TriggerOnCast(player, ai, info.PlayerExecutedAction, slotIndex, info);
        TriggerOnCast(ai, player, info.AiExecutedAction, slotIndex, info);

        // Charge 不依赖对手动作，所以在战斗判定前直接结算能量收益。
        ApplyChargeGain(player, info.PlayerExecutedAction);
        ApplyChargeGain(ai, info.AiExecutedAction);

        // 真正的攻防和伤害在这里完成。
        ResolveCombat(player, ai, info.PlayerExecutedAction, info.AiExecutedAction, info);

        TriggerResolveEnd(player, ai, info);
        TriggerResolveEnd(ai, player, info);

        // 保险处理，避免极端情况下出现负血或负能量落到外层。
        player.HP = Mathf.Max(0, player.HP);
        ai.HP = Mathf.Max(0, ai.HP);
        player.Energy = Mathf.Max(0, player.Energy);
        ai.Energy = Mathf.Max(0, ai.Energy);

        info.PlayerHPAfter = player.HP;
        info.AiHPAfter = ai.HP;
        info.PlayerEnergyAfter = player.Energy;
        info.AiEnergyAfter = ai.Energy;
        info.PlayerEnergyChange = info.PlayerEnergyAfter - info.PlayerEnergyBefore;
        info.AiEnergyChange = info.AiEnergyAfter - info.AiEnergyBefore;
        info.Summary = BuildSummary(info);
        return info;
    }

    /// <summary>
    /// 根据战斗者当前能量，判断所选动作能否真正落地。
    /// 注意这里只判断“能不能执行”，并不修改任何状态。
    /// </summary>
    private ActionType GetExecutableAction(FighterState fighterState, ActionType selectedAction)
    {
        if (selectedAction == ActionType.None)
        {
            return ActionType.None;
        }

        if (!CanAffordAction(fighterState.Energy, selectedAction))
        {
            return ActionType.None;
        }

        return selectedAction;
    }

    /// <summary>
    /// 扣除动作消耗。
    /// 如果传入的是 None，消耗为 0，因此不会产生副作用。
    /// </summary>
    private void SpendActionEnergy(FighterState fighterState, ActionType actionType)
    {
        fighterState.Energy -= GetEnergyCost(actionType);
    }

    /// <summary>
    /// 结算 Charge 带来的 +1 能量。
    /// </summary>
    private void ApplyChargeGain(FighterState fighterState, ActionType actionType)
    {
        if (actionType == ActionType.Charge)
        {
            fighterState.Energy += 1;
        }
    }

    /// <summary>
    /// 处理两个动作之间的攻防关系。
    /// 这里体现了波波攒的几条核心规则：
    /// 1. 攻 vs 攻 抵消
    /// 2. 大 vs 大 抵消
    /// 3. 攻 vs 大 按大招结算，弱侧被压制
    /// 4. 其余情况由防御或单边进攻继续处理
    /// </summary>
    private void ResolveCombat(FighterState player, FighterState ai, ActionType playerAction, ActionType aiAction, ActionResolveInfo info)
    {
        bool playerOffensive = playerAction.IsOffensive();
        bool aiOffensive = aiAction.IsOffensive();

        if (playerOffensive && aiOffensive)
        {
            if (playerAction == aiAction)
            {
                info.PlayerActionCancelled = true;
                info.AiActionCancelled = true;
                return;
            }

            if (playerAction == ActionType.Ultimate && aiAction == ActionType.Attack)
            {
                info.AiActionCancelled = true;
                ApplyIncomingAction(player, ai, playerAction, false, info, true);
                return;
            }

            if (playerAction == ActionType.Attack && aiAction == ActionType.Ultimate)
            {
                info.PlayerActionCancelled = true;
                ApplyIncomingAction(ai, player, aiAction, false, info, false);
                return;
            }
        }

        if (playerOffensive)
        {
            ApplyIncomingAction(player, ai, playerAction, aiAction == ActionType.Guard, info, true);
        }

        if (aiOffensive)
        {
            ApplyIncomingAction(ai, player, aiAction, playerAction == ActionType.Guard, info, false);
        }
    }

    /// <summary>
    /// 处理一次“某一方进攻动作命中对手”的流程。
    /// 这里会统一经过 Buff、格挡和伤害写回，因此所有攻击都走同一条路径。
    /// </summary>
    private void ApplyIncomingAction(FighterState attacker, FighterState defender, ActionType attackAction, bool isBlocked, ActionResolveInfo info, bool isPlayerAttacker)
    {
        int damage = isBlocked ? 0 : attackAction.GetBaseDamage();
        BuffHitContext hitContext = new BuffHitContext(defender, attacker, attackAction, damage, isBlocked, info);
        TriggerOnBeHit(defender, hitContext);
        damage = Mathf.Max(0, hitContext.Damage);

        if (damage > 0)
        {
            defender.HP -= damage;
        }

        if (isPlayerAttacker)
        {
            info.DamageToAI = damage;
            info.AiBlocked = isBlocked;
            info.PlayerUltimateWasted = isBlocked && attackAction == ActionType.Ultimate;
        }
        else
        {
            info.DamageToPlayer = damage;
            info.PlayerBlocked = isBlocked;
            info.AiUltimateWasted = isBlocked && attackAction == ActionType.Ultimate;
        }
    }

    /// <summary>
    /// 触发拥有者的 OnCast Buff。
    /// </summary>
    private void TriggerOnCast(FighterState owner, FighterState opponent, ActionType actionType, int slotIndex, ActionResolveInfo resolveInfo)
    {
        if (owner == null || owner.Buffs == null || actionType == ActionType.None)
        {
            return;
        }

        BuffCastContext context = new BuffCastContext(owner, opponent, actionType, slotIndex, resolveInfo);
        for (int i = 0; i < owner.Buffs.Count; i++)
        {
            BuffBase buff = owner.Buffs[i];
            if (buff != null)
            {
                buff.OnCast(context);
            }
        }
    }

    /// <summary>
    /// 触发受击方的 OnBeHit Buff。
    /// </summary>
    private void TriggerOnBeHit(FighterState owner, BuffHitContext context)
    {
        if (owner == null || owner.Buffs == null)
        {
            return;
        }

        for (int i = 0; i < owner.Buffs.Count; i++)
        {
            BuffBase buff = owner.Buffs[i];
            if (buff != null)
            {
                buff.OnBeHit(context);
            }
        }
    }

    /// <summary>
    /// 触发槽位结算结束时的 Buff。
    /// </summary>
    private void TriggerResolveEnd(FighterState owner, FighterState opponent, ActionResolveInfo resolveInfo)
    {
        if (owner == null || owner.Buffs == null)
        {
            return;
        }

        BuffResolveContext context = new BuffResolveContext(owner, opponent, resolveInfo);
        for (int i = 0; i < owner.Buffs.Count; i++)
        {
            BuffBase buff = owner.Buffs[i];
            if (buff != null)
            {
                buff.OnResolveEnd(context);
            }
        }
    }

    /// <summary>
    /// 把结构化结算信息转成一条更适合直接显示给玩家看的摘要文案。
    /// 这是表现辅助逻辑，不影响规则结果本身。
    /// </summary>
    private string BuildSummary(ActionResolveInfo info)
    {
        List<string> parts = new List<string>();

        if (info.PlayerFailedByEnergy)
        {
            parts.Add("玩家动作因能量不足失效");
        }

        if (info.AiFailedByEnergy)
        {
            parts.Add("AI动作因能量不足失效");
        }

        bool sameOffenseCancelled = info.PlayerActionCancelled &&
                                    info.AiActionCancelled &&
                                    info.PlayerExecutedAction.IsOffensive() &&
                                    info.PlayerExecutedAction == info.AiExecutedAction;

        if (sameOffenseCancelled)
        {
            parts.Add("双方同类攻击对撞后相互抵消");
        }
        else
        {
            if (info.PlayerActionCancelled)
            {
                parts.Add("玩家的进攻被更强的动作压制");
            }

            if (info.AiActionCancelled)
            {
                parts.Add("AI的进攻被更强的动作压制");
            }

            if (info.DamageToAI > 0)
            {
                parts.Add(string.Format("AI受到{0}点伤害", info.DamageToAI));
            }
            else if (info.AiBlocked)
            {
                parts.Add("AI成功格挡了来袭伤害");
            }

            if (info.DamageToPlayer > 0)
            {
                parts.Add(string.Format("玩家受到{0}点伤害", info.DamageToPlayer));
            }
            else if (info.PlayerBlocked)
            {
                parts.Add("玩家成功格挡了来袭伤害");
            }
        }

        if (info.PlayerUltimateWasted)
        {
            parts.Add("玩家大招被完整挡下");
        }

        if (info.AiUltimateWasted)
        {
            parts.Add("AI大招被完整挡下");
        }

        if (info.PlayerEnergyChange > 0)
        {
            parts.Add(string.Format("玩家能量+{0}", info.PlayerEnergyChange));
        }
        else if (info.PlayerEnergyChange < 0)
        {
            parts.Add(string.Format("玩家能量{0}", info.PlayerEnergyChange));
        }

        if (info.AiEnergyChange > 0)
        {
            parts.Add(string.Format("AI能量+{0}", info.AiEnergyChange));
        }
        else if (info.AiEnergyChange < 0)
        {
            parts.Add(string.Format("AI能量{0}", info.AiEnergyChange));
        }

        if (parts.Count == 0)
        {
            parts.Add("双方都没有打出有效伤害");
        }

        return string.Join("，", parts.ToArray()) + "。";
    }
}
