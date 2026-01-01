using UnityEngine;
using UnityEngine.Serialization;

public abstract class ClueData : ScriptableObject
{
    public string id;
    public string displayName;

    [Header("线索简介")]
    [TextArea]
    [FormerlySerializedAs("Summary")]
    public string summary;

    [Header("搜索详细信息")]
    [TextArea(10, 30)]
    [FormerlySerializedAs("Detail")]
    public string detailText;   // 三种线索共有

    [Header("富文本详细信息")]
    [TextArea(10, 30)]
    public string Detail_Mark;


    public bool searchable;
    public bool collected;
}
