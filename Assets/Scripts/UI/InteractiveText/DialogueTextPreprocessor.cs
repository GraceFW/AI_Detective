using UnityEngine;

/// <summary>
/// 对话文本预处理入口。
///
/// 职责：
/// 1. 接收原始纯文本
/// 2. 调用自动标注构建器，将关键词替换为 TMP link 富文本
/// 3. 返回最终可直接显示的富文本
///
/// 说明：
/// - 当前仅负责 CaseKeywordDatabase -> <link> 的自动构建
/// - 未来如果要增加：
///   1) 玩家名占位符替换
///   2) 术语高亮
///   3) NPC 名字标注
///   4) 特殊颜色标签
///   都可以继续在这里扩展
/// </summary>
public static class DialogueTextPreprocessor
{
	/// <summary>
	/// 处理原始文本，返回最终用于显示的富文本。
	/// </summary>
	public static string Process(string rawText, CaseKeywordDatabase keywordDatabase)
	{
		if (string.IsNullOrEmpty(rawText))
			return string.Empty;

		return InteractiveTextMarkupBuilder.Build(rawText, keywordDatabase);
	}
}