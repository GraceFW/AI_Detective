using UnityEngine;
using UnityEngine.Serialization;

public abstract class ClueData : ScriptableObject
{
    [Header("线索基本设置")]
    [Tooltip("线索唯一ID：用于 TMP 的 <link> id、数据库查找 key、存档 key 等。")]
	public string id;

    [Tooltip("线索显示名称：用于 UI 列表/标题等展示。")]
    public string displayName;

    [Tooltip("线索性质：是否可被 /Detect 探测")]
	public bool detectable;

	[Tooltip("线索性质：是否可被 /Detect 收集")]
	public bool collectable;
	
	[Tooltip("线索性质：是否已被收集/揭示。")]
	public bool collected; // 当前 Demo 在 ClueManager.RevealClue 中设置；后续可通过存档系统持久化。

    [Header("Attack相关")]
	[Tooltip("解锁新内容的秘钥")]
	public string attackKey;
	[Tooltip("是否已解锁加密内容")]
	public bool isAttackContentUnlocked;
	[Tooltip("是否直接展示已解锁的Attack内容,默认为是")]
	// 特殊情况：当玩家Detect了一个未收集的线索时，系统应该提示玩家“这都被你发现了！请收集它吧！”（或类似文案）
	// 而不是直接展示Attack解锁内容。此时可以将这个字段设置为false，等玩家收集线索后再展示
	public bool showAttackContentDirectly = true;
	[Tooltip("Attack命令解锁的新内容")]
	[TextArea(10, 30)]
	public string attackUnlockContent;

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

}
