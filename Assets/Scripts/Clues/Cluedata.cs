using UnityEngine;
using UnityEngine.Serialization;

public abstract class ClueData : ScriptableObject
{
    // 线索唯一ID：用于 TMP 的 <link> id、数据库查找 key、存档 key 等。
    public string id;

    // 线索显示名称：用于 UI 列表/标题等展示。
    public string displayName;

    [Header("线索简介")]
    [TextArea]
    [FormerlySerializedAs("Summary")]
    // 线索简介：用于弹窗/简略信息展示。
    public string summary;

    [Header("搜索详细信息")]
    [TextArea(10, 30)]
    [FormerlySerializedAs("Detail")]
    // 线索纯文本详情：建议用于搜索/匹配/推理逻辑，避免富文本标签污染。
    public string detailText;   // 三种线索共有

    [Header("富文本详细信息")]
    [TextArea(10, 30)]
    // 线索富文本详情：用于 TMP 显示（可包含 <b>、<color> 等富文本标签）。
    // 推荐显示策略：Detail_Mark 非空则用它，否则回退到 detailText。
    public string Detail_Mark;


    // 是否可被搜索系统纳入（如全文检索）。
    public bool searchable;

    // 是否已被收集/揭示。
    // 当前 Demo 在 ClueManager.RevealClue 中设置；后续可通过存档系统持久化。
    public bool collected;
}
