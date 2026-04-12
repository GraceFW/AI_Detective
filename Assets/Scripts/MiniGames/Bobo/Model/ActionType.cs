using UnityEngine;

/// <summary>
/// 单个槽位可执行的动作类型。
/// 这里的枚举同时被规则层、AI 层和 UI 层复用，是整个小游戏的“动作词典”。
/// </summary>
public enum ActionType
{
    None = 0,
    Charge = 1,
    Guard = 2,
    Attack = 3,
    Ultimate = 4
}

/// <summary>
/// 一局战斗结束后的胜负归属。
/// None 表示对局仍在继续，Draw 表示双方在同一槽位或同一回合内同时被击倒。
/// </summary>
public enum BattleWinner
{
    None = 0,
    Player = 1,
    AI = 2,
    Draw = 3
}

/// <summary>
/// 为 ActionType 提供一组便捷扩展。
/// 这些方法不保存状态，只负责把“动作枚举”转换成规则和表现层更容易使用的形式。
/// </summary>
public static class ActionTypeExtensions
{
    /// <summary>
    /// 返回动作在 UI 中展示的完整名称。
    /// </summary>
    public static string GetDisplayName(this ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Charge:
                return "攒气";
            case ActionType.Guard:
                return "防御";
            case ActionType.Attack:
                return "进攻";
            case ActionType.Ultimate:
                return "大招";
            default:
                return "未选择";
        }
    }

    /// <summary>
    /// 返回更短的动作简称。
    /// 当前版本暂未大规模使用，通常用于紧凑 UI 或日志摘要。
    /// </summary>
    public static string GetShortName(this ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Charge:
                return "攒";
            case ActionType.Guard:
                return "防";
            case ActionType.Attack:
                return "攻";
            case ActionType.Ultimate:
                return "大";
            default:
                return "-";
        }
    }

    /// <summary>
    /// 是否属于进攻类动作。
    /// 规则层会用这个标记判断“对撞”“被防御”“强弱压制”等逻辑。
    /// </summary>
    public static bool IsOffensive(this ActionType actionType)
    {
        return actionType == ActionType.Attack || actionType == ActionType.Ultimate;
    }

    /// <summary>
    /// 返回动作的基础伤害值。
    /// 真正结算时还可能被 Guard、Buff 或对撞规则修改，所以这里只是基础值。
    /// </summary>
    public static int GetBaseDamage(this ActionType actionType)
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
    /// 为 UI 提供动作对应的主题色。
    /// 这样按钮、标签、状态高亮都可以和动作语义保持一致。
    /// </summary>
    public static Color GetThemeColor(this ActionType actionType)
    {
        switch (actionType)
        {
            case ActionType.Charge:
                return new Color(0.24f, 0.57f, 0.86f);
            case ActionType.Guard:
                return new Color(0.19f, 0.70f, 0.48f);
            case ActionType.Attack:
                return new Color(0.89f, 0.33f, 0.28f);
            case ActionType.Ultimate:
                return new Color(0.84f, 0.58f, 0.16f);
            default:
                return new Color(0.36f, 0.39f, 0.46f);
        }
    }
}
