using UnityEngine;

public abstract class ClueData : ScriptableObject
{
    public string id;
    public string displayName;

    [Header("线索简介")]
    [TextArea]
    public string Summary;

    [Header("搜索详细信息")]
    [TextArea(10, 30)]
    public string Detail;   // 三种线索共有


    public bool searchable;
    public bool collected;
}
