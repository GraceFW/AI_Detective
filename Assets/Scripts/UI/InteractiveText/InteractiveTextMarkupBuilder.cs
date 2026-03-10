using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 交互文本标注构建器（基于 CaseKeywordDatabase）
///
/// 作用：
/// 1. 输入纯文本和关键词数据库
/// 2. 自动扫描文本中的关键词
/// 3. 将命中的文本片段包装成 TMP 的 <link> 富文本标签
/// 4. 输出可直接给 TextMeshProUGUI / TypewriterEffect 使用的富文本字符串
///
/// 设计目标：
/// - 文案层保持纯文本，不需要手写 <link>
/// - 复用项目中已有的 CaseKeywordDatabase，避免再造一套 ClueData.keywords
/// - 长词优先，避免“血”先匹配导致“血迹”无法完整匹配
/// - 避免重叠匹配
/// - 尽量跳过原文本中已存在的 <link> 区域，避免重复嵌套
///
/// 使用示例：
/// string richText = InteractiveTextMarkupBuilder.Build(rawText, caseKeywordDatabase);
/// typewriter.SetText(richText);
/// </summary>
public static class InteractiveTextMarkupBuilder
{
	/// <summary>
	/// 构建富文本（使用默认配置）
	/// </summary>
	public static string Build(string rawText, CaseKeywordDatabase keywordDatabase)
	{
		return Build(rawText, keywordDatabase, BuildOptions.Default);
	}

	/// <summary>
	/// 构建富文本（可传自定义配置）
	/// </summary>
	public static string Build(string rawText, CaseKeywordDatabase keywordDatabase, BuildOptions options)
	{
		if (string.IsNullOrEmpty(rawText))
			return string.Empty;

		if (keywordDatabase == null)
		{
			Debug.LogWarning("[InteractiveTextMarkupBuilder] keywordDatabase 为空，返回原文本。");
			return rawText;
		}

		if (keywordDatabase.keywords == null || keywordDatabase.keywords.Count == 0)
			return rawText;

		// 1. 收集所有有效关键词条目
		List<KeywordEntry> entries = CollectKeywordEntries(keywordDatabase, options);
		if (entries.Count == 0)
			return rawText;

		// 2. 如果原文中已经有手写 <link>，则将这些区域保护起来，避免重复嵌套
		List<RangeInt> protectedRanges = options.IgnoreExistingLinks
			? FindProtectedLinkContentRanges(rawText)
			: new List<RangeInt>();

		// 3. 查找所有候选匹配
		List<MatchResult> allMatches = FindAllMatches(rawText, entries, protectedRanges, options);
		if (allMatches.Count == 0)
			return rawText;

		// 4. 长词优先 + 不重叠筛选
		List<MatchResult> acceptedMatches = SelectNonOverlappingMatches(allMatches, rawText.Length);
		if (acceptedMatches.Count == 0)
			return rawText;

		// 5. 按文本顺序排序，用于最终组装字符串
		acceptedMatches.Sort((a, b) => a.StartIndex.CompareTo(b.StartIndex));

		// 6. 生成富文本
		return BuildRichText(rawText, acceptedMatches);
	}

	#region Step 1: 收集关键词条目

	/// <summary>
	/// 从 CaseKeywordDatabase 收集所有有效关键词。
	///
	/// 规则：
	/// - term 不能为空
	/// - revealsClue 不能为空
	/// - revealsClue.id 不能为空
	/// - 默认去重：同一个 term + 同一个 clueId 只保留一份
	/// - 结果按“关键词长度降序”排序，确保长词优先
	/// </summary>
	private static List<KeywordEntry> CollectKeywordEntries(CaseKeywordDatabase keywordDatabase, BuildOptions options)
	{
		var result = new List<KeywordEntry>();
		var dedup = new HashSet<string>(StringComparer.Ordinal);

		foreach (var entry in keywordDatabase.keywords)
		{
			if (entry == null)
				continue;

			if (entry.revealsClue == null)
				continue;

			string clueId = entry.revealsClue.id;
			if (string.IsNullOrWhiteSpace(clueId))
				continue;

			if (string.IsNullOrWhiteSpace(entry.term))
				continue;

			string term = options.TrimKeywordWhitespace ? entry.term.Trim() : entry.term;
			if (string.IsNullOrEmpty(term))
				continue;

			// 去重键：term + clueId
			string dedupKey = $"{term}@@{clueId}";
			if (!dedup.Add(dedupKey))
				continue;

			result.Add(new KeywordEntry
			{
				Term = term,
				ClueId = clueId
			});
		}

		// 长词优先；长度相同则按字典序，保证排序稳定
		result.Sort((a, b) =>
		{
			int lenCompare = b.Term.Length.CompareTo(a.Term.Length);
			if (lenCompare != 0) return lenCompare;
			return string.CompareOrdinal(a.Term, b.Term);
		});

		return result;
	}

	#endregion

	#region Step 2: 找到原文本中已有的 <link> 保护区间

	/// <summary>
	/// 找出文本中所有 <link ...>内容</link> 的“内容区间”，避免自动标注再次覆盖这些内容。
	/// </summary>
	private static List<RangeInt> FindProtectedLinkContentRanges(string text)
	{
		var ranges = new List<RangeInt>();

		const string openTagPrefix = "<link";
		const string closeTag = "</link>";

		int searchIndex = 0;

		while (searchIndex < text.Length)
		{
			int openStart = text.IndexOf(openTagPrefix, searchIndex, StringComparison.Ordinal);
			if (openStart < 0)
				break;

			int openEnd = text.IndexOf('>', openStart);
			if (openEnd < 0)
				break;

			int contentStart = openEnd + 1;

			int closeStart = text.IndexOf(closeTag, contentStart, StringComparison.Ordinal);
			if (closeStart < 0)
				break;

			int contentLength = closeStart - contentStart;
			if (contentLength > 0)
			{
				ranges.Add(new RangeInt(contentStart, contentLength));
			}

			searchIndex = closeStart + closeTag.Length;
		}

		return ranges;
	}

	#endregion

	#region Step 3: 查找所有候选匹配

	/// <summary>
	/// 在原文本中查找所有关键词的命中结果。
	/// </summary>
	private static List<MatchResult> FindAllMatches(
		string rawText,
		List<KeywordEntry> entries,
		List<RangeInt> protectedRanges,
		BuildOptions options)
	{
		var matches = new List<MatchResult>();

		foreach (var entry in entries)
		{
			if (string.IsNullOrEmpty(entry.Term))
				continue;

			int searchStart = 0;

			while (searchStart < rawText.Length)
			{
				int foundIndex = rawText.IndexOf(entry.Term, searchStart, options.ComparisonType);
				if (foundIndex < 0)
					break;

				int foundLength = entry.Term.Length;

				// 若命中区域落在已有 link 内容区间内，则跳过
				if (IsInsideAnyRange(foundIndex, foundLength, protectedRanges))
				{
					searchStart = foundIndex + foundLength;
					continue;
				}

				// 可选边界检查（中文项目一般不需要）
				if (options.RequireWordBoundary && !PassWordBoundaryCheck(rawText, foundIndex, foundLength))
				{
					searchStart = foundIndex + foundLength;
					continue;
				}

				matches.Add(new MatchResult
				{
					StartIndex = foundIndex,
					Length = foundLength,
					LinkId = options.LinkIdPrefix + entry.ClueId,
					Priority = foundLength
				});

				searchStart = foundIndex + foundLength;
			}
		}

		return matches;
	}

	/// <summary>
	/// 判断一个区间是否与任意保护区间有交集。
	/// </summary>
	private static bool IsInsideAnyRange(int start, int length, List<RangeInt> ranges)
	{
		int endExclusive = start + length;

		for (int i = 0; i < ranges.Count; i++)
		{
			int rangeStart = ranges[i].start;
			int rangeEndExclusive = ranges[i].start + ranges[i].length;

			bool overlap = start < rangeEndExclusive && endExclusive > rangeStart;
			if (overlap)
				return true;
		}

		return false;
	}

	/// <summary>
	/// 单词边界检查。
	///
	/// 用于英文/数字术语场景。
	/// 中文通常不需要开启。
	/// </summary>
	private static bool PassWordBoundaryCheck(string text, int start, int length)
	{
		int leftIndex = start - 1;
		int rightIndex = start + length;

		bool leftOk = leftIndex < 0 || !IsWordChar(text[leftIndex]);
		bool rightOk = rightIndex >= text.Length || !IsWordChar(text[rightIndex]);

		return leftOk && rightOk;
	}

	/// <summary>
	/// 判断字符是否视为“单词字符”。
	/// </summary>
	private static bool IsWordChar(char c)
	{
		return char.IsLetterOrDigit(c) || c == '_';
	}

	#endregion

	#region Step 4: 长词优先 + 去重叠筛选

	/// <summary>
	/// 从所有候选命中中筛选出最终采用的匹配：
	/// - 长词优先
	/// - 不允许重叠
	/// - 若优先级相同，则靠前者优先
	/// </summary>
	private static List<MatchResult> SelectNonOverlappingMatches(List<MatchResult> allMatches, int textLength)
	{
		var accepted = new List<MatchResult>();
		bool[] occupied = new bool[textLength];

		allMatches.Sort((a, b) =>
		{
			int priorityCompare = b.Priority.CompareTo(a.Priority);
			if (priorityCompare != 0) return priorityCompare;

			int startCompare = a.StartIndex.CompareTo(b.StartIndex);
			if (startCompare != 0) return startCompare;

			return string.CompareOrdinal(a.LinkId, b.LinkId);
		});

		foreach (var match in allMatches)
		{
			if (match.StartIndex < 0 || match.StartIndex >= textLength)
				continue;

			int endExclusive = Mathf.Min(textLength, match.StartIndex + match.Length);

			bool conflict = false;
			for (int i = match.StartIndex; i < endExclusive; i++)
			{
				if (occupied[i])
				{
					conflict = true;
					break;
				}
			}

			if (conflict)
				continue;

			accepted.Add(match);

			for (int i = match.StartIndex; i < endExclusive; i++)
				occupied[i] = true;
		}

		return accepted;
	}

	#endregion

	#region Step 5: 组装最终富文本

	/// <summary>
	/// 将原文本和筛选后的匹配结果组装为最终 TMP 富文本。
	/// </summary>
	private static string BuildRichText(string rawText, List<MatchResult> acceptedMatches)
	{
		var sb = new StringBuilder(rawText.Length + acceptedMatches.Count * 32);

		int cursor = 0;

		for (int i = 0; i < acceptedMatches.Count; i++)
		{
			var match = acceptedMatches[i];

			// 先写普通文本
			if (match.StartIndex > cursor)
			{
				sb.Append(rawText, cursor, match.StartIndex - cursor);
			}

			// 再写 link
			sb.Append("<link=\"");
			sb.Append(match.LinkId);
			sb.Append("\">");
			sb.Append(rawText, match.StartIndex, match.Length);
			sb.Append("</link>");

			cursor = match.StartIndex + match.Length;
		}

		// 写剩余尾部
		if (cursor < rawText.Length)
		{
			sb.Append(rawText, cursor, rawText.Length - cursor);
		}

		return sb.ToString();
	}

	#endregion

	#region Internal Data Types

	/// <summary>
	/// 构建配置项。
	/// </summary>
	[Serializable]
	public struct BuildOptions
	{
		[Tooltip("生成的 linkId 前缀。默认 clue:，最终结果如 clue:clue_blood")]
		public string LinkIdPrefix;

		[Tooltip("是否跳过原文本中已存在的 <link> 内容，避免自动标注重复嵌套")]
		public bool IgnoreExistingLinks;

		[Tooltip("是否对关键词 term 做 Trim")]
		public bool TrimKeywordWhitespace;

		[Tooltip("是否要求单词边界。中文一般关闭，英文术语可开启")]
		public bool RequireWordBoundary;

		[Tooltip("字符串匹配方式。中文建议 Ordinal；英文不区分大小写可改为 OrdinalIgnoreCase")]
		public StringComparison ComparisonType;

		public static BuildOptions Default => new BuildOptions
		{
			LinkIdPrefix = "clue:",
			IgnoreExistingLinks = true,
			TrimKeywordWhitespace = true,
			RequireWordBoundary = false,
			ComparisonType = StringComparison.Ordinal
		};
	}

	/// <summary>
	/// 内部关键词条目。
	/// </summary>
	private struct KeywordEntry
	{
		public string Term;
		public string ClueId;
	}

	/// <summary>
	/// 内部匹配结果。
	/// </summary>
	private struct MatchResult
	{
		public int StartIndex;
		public int Length;
		public string LinkId;
		public int Priority;
	}

	#endregion
}