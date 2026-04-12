using UnityEngine;

/// <summary>
/// 施放动作时传递给 Buff 的上下文。
/// 常用于“出招时获得加成”“施法后叠层”等扩展。
/// </summary>
public class BuffCastContext
{
    public FighterState Owner;
    public FighterState Opponent;
    public ActionType ActionType;
    public int SlotIndex;
    public ActionResolveInfo ResolveInfo;

    public BuffCastContext(FighterState owner, FighterState opponent, ActionType actionType, int slotIndex, ActionResolveInfo resolveInfo)
    {
        Owner = owner;
        Opponent = opponent;
        ActionType = actionType;
        SlotIndex = slotIndex;
        ResolveInfo = resolveInfo;
    }
}

/// <summary>
/// 受击时传递给 Buff 的上下文。
/// Damage 是可修改的，因此它是当前 Buff 系统最直接的干预点。
/// </summary>
public class BuffHitContext
{
    public FighterState Owner;
    public FighterState Attacker;
    public ActionType IncomingAction;
    public bool IsBlocked;
    public ActionResolveInfo ResolveInfo;
    public int Damage;

    public BuffHitContext(FighterState owner, FighterState attacker, ActionType incomingAction, int damage, bool isBlocked, ActionResolveInfo resolveInfo)
    {
        Owner = owner;
        Attacker = attacker;
        IncomingAction = incomingAction;
        Damage = damage;
        IsBlocked = isBlocked;
        ResolveInfo = resolveInfo;
    }

    /// <summary>
    /// 调整即将承受的伤害。
    /// 例如反伤、减伤、护盾穿透等，都可以在这里进一步扩展。
    /// </summary>
    public void ModifyDamage(int delta)
    {
        Damage = Mathf.Max(0, Damage + delta);
    }
}

/// <summary>
/// 单个槽位全部结算结束后传递给 Buff 的上下文。
/// 常用于“回合后结算”“延迟伤害”“持续效果衰减”等逻辑。
/// </summary>
public class BuffResolveContext
{
    public FighterState Owner;
    public FighterState Opponent;
    public ActionResolveInfo ResolveInfo;

    public BuffResolveContext(FighterState owner, FighterState opponent, ActionResolveInfo resolveInfo)
    {
        Owner = owner;
        Opponent = opponent;
        ResolveInfo = resolveInfo;
    }
}

/// <summary>
/// Buff 抽象基类。
/// 当前版本只预留最小可扩展接口，不强行引入更重的效果系统。
/// </summary>
public abstract class BuffBase
{
    /// <summary>
    /// 当拥有者在当前槽位成功执行动作时触发。
    /// </summary>
    public virtual void OnCast(BuffCastContext context)
    {
    }

    /// <summary>
    /// 当拥有者即将受到伤害时触发。
    /// </summary>
    public virtual void OnBeHit(BuffHitContext context)
    {
    }

    /// <summary>
    /// 当前槽位结算结束后触发。
    /// </summary>
    public virtual void OnResolveEnd(BuffResolveContext context)
    {
    }

    /// <summary>
    /// Buff 需要支持克隆，因为模拟器会复制整套战斗状态。
    /// </summary>
    public abstract BuffBase Clone();
}
