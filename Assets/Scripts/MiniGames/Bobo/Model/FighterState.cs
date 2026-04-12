using System;
using System.Collections.Generic;

[Serializable]
public class FighterState
{
    /// <summary>
    /// 当前战斗中展示给 UI 的名字。
    /// 它不一定等于全局角色配置里的名字，允许由外部请求动态传入。
    /// </summary>
    public string DisplayName;

    /// <summary>
    /// 生命值。小于等于 0 时视为被击败。
    /// </summary>
    public int HP;

    /// <summary>
    /// 能量值。Attack 消耗 1，Ultimate 消耗 3，Charge 可回复 1。
    /// </summary>
    public int Energy;

    /// <summary>
    /// 当前挂载的 Buff 列表。
    /// 虽然本版本还没有具体 Buff 实现，但规则层已经会在关键时机回调它们。
    /// </summary>
    public List<BuffBase> Buffs = new List<BuffBase>();

    public FighterState()
    {
    }

    public FighterState(string displayName, int hp, int energy)
    {
        DisplayName = displayName;
        HP = hp;
        Energy = energy;
    }

    /// <summary>
    /// 语义化判断，便于上层调用时表达“是否还存活”。
    /// </summary>
    public bool IsAlive
    {
        get { return HP > 0; }
    }

    /// <summary>
    /// 深拷贝当前战斗者状态。
    /// 这里会连同 Buff 一起克隆，保证模拟器和真实战斗互不污染。
    /// </summary>
    public FighterState Clone()
    {
        FighterState clone = new FighterState(DisplayName, HP, Energy);

        if (Buffs == null)
        {
            return clone;
        }

        for (int i = 0; i < Buffs.Count; i++)
        {
            BuffBase buff = Buffs[i];
            if (buff != null)
            {
                clone.Buffs.Add(buff.Clone());
            }
        }

        return clone;
    }
}
