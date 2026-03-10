using UnityEngine;

/// <summary>
/// 线索 link 处理器（业务层）
///
/// 职责：
/// - 解析 linkId -> clueId
/// - 点击时调用 ClueManager.RevealClue
///
/// 兼容两种 linkId：
/// 1) "clue_001"（当前项目）
/// 2) "clue:clue_001"（后期优化方向：更规范、便于扩展多类型 handler）
/// </summary>
public class ClueLinkHandler : MonoBehaviour, IInteractiveLinkHandler
{
	public bool CanHandle(string linkId)
	{
		// 当前版本所有 link 都是 clue，所以这里默认都能处理
		// 若后续要支持 新的类型（非目前的线索大类），请把这里改为严格前缀判断
		return !string.IsNullOrEmpty(linkId);
	}

	public void OnHoverEnter(string linkId, InteractiveLinkContext ctx)
	{
		// 预留：如果想 hover 出 tooltip，就在这里做（不建议写在 View 里）
		// 例：根据 clueId 去 ClueDatabaseSO 查 displayName/desc，然后显示 Tooltip
	}

	public void OnHoverExit(string linkId, InteractiveLinkContext ctx)
	{
		// 预留：隐藏 tooltip
	}

	public void OnClick(string linkId, InteractiveLinkContext ctx)
	{
		string clueId = NormalizeClueId(linkId);
		Debug.Log($"[TypewriterEffect] 点击线索链接: {clueId}");

		if (ClueManager.instance != null)
			ClueManager.instance.RevealClue(clueId);
		else
			Debug.LogWarning("[ClueLinkHandler] ClueManager.instance is null.");
	}

	// 定义一个私有方法，返回值为string类型，方法名NormalizeClueId，参数是string类型的linkId
	private string NormalizeClueId(string linkId)
	{
		// 定义一个常量字符串，值为"clue:"，常量一旦定义不能修改
		const string prefix = "clue:";

		// 判断linkId是否以prefix开头（Ordinal表示按字符编码逐字符比较，不区分文化/大小写）
		if (linkId.StartsWith(prefix, System.StringComparison.Ordinal))
			// 如果满足条件：截取prefix长度之后的子字符串并返回（去掉"clue:"前缀）
			return linkId.Substring(prefix.Length);

		// 如果不满足条件：直接返回原linkId
		return linkId;
	}
}