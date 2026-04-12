using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

[Serializable]
public class BattlePlan
{
    /// <summary>
    /// 固定三槽设计。
    /// 这里提成常量，是为了让规则、AI、UI 对槽位数量使用同一份定义。
    /// </summary>
    public const int SlotCount = 3;

    /// <summary>
    /// 当前回合的三槽动作。
    /// index 0/1/2 分别对应 UI 上从左到右或从上到下显示的 1/2/3 号槽位。
    /// </summary>
    [SerializeField] private ActionType[] slots = new ActionType[SlotCount];

    /// <summary>
    /// 创建一个空方案，默认全部为 None。
    /// </summary>
    public BattlePlan()
    {
        Clear();
    }

    /// <summary>
    /// 根据外部动作列表创建方案。
    /// 多余的动作会被截断，不足的部分保留为 None。
    /// </summary>
    public BattlePlan(IReadOnlyList<ActionType> sourceActions)
    {
        slots = new ActionType[SlotCount];
        Clear();

        if (sourceActions == null)
        {
            return;
        }

        int count = Mathf.Min(sourceActions.Count, SlotCount);
        for (int i = 0; i < count; i++)
        {
            slots[i] = sourceActions[i];
        }
    }

    /// <summary>
    /// 通过索引读写槽位动作，便于规则和 UI 用统一方式访问。
    /// </summary>
    public ActionType this[int index]
    {
        get { return slots[index]; }
        set { slots[index] = value; }
    }

    /// <summary>
    /// 只读暴露内部槽位数组，避免外部直接替换引用。
    /// </summary>
    public IReadOnlyList<ActionType> Slots
    {
        get { return slots; }
    }

    /// <summary>
    /// 清空方案，通常用于新回合开始或玩家修改前置槽位后重置后续槽位。
    /// </summary>
    public void Clear()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            slots[i] = ActionType.None;
        }
    }

    /// <summary>
    /// 是否仍有未选择的槽位。
    /// UI 提交前会先用它拦截不完整方案。
    /// </summary>
    public bool HasUnselectedSlot()
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == ActionType.None)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 判断方案中是否出现过指定动作。
    /// AI 在评分时会利用这个方法快速识别“是否包含大招/防御”等策略倾向。
    /// </summary>
    public bool Contains(ActionType actionType)
    {
        for (int i = 0; i < SlotCount; i++)
        {
            if (slots[i] == actionType)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 深拷贝一份方案，避免不同层之间共享同一个可写数组。
    /// </summary>
    public BattlePlan Clone()
    {
        return new BattlePlan(slots);
    }

    /// <summary>
    /// 快速创建三槽方案，便于测试或 fallback 场景使用。
    /// </summary>
    public static BattlePlan Create(ActionType slot0, ActionType slot1, ActionType slot2)
    {
        return new BattlePlan(new ActionType[] { slot0, slot1, slot2 });
    }

    /// <summary>
    /// 用于日志或调试面板显示。
    /// </summary>
    public override string ToString()
    {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < SlotCount; i++)
        {
            if (i > 0)
            {
                builder.Append(" / ");
            }

            builder.Append(slots[i].GetDisplayName());
        }

        return builder.ToString();
    }
}
