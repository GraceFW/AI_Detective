// Assets/Editor/ChapterDialogueExporter.cs
// 导出章节对话表（DialogueData）为 xlsx
// 每个案件一张 Sheet，格式与文案表格中的章节对话表一致
// 菜单：Tools > 线索导出 > 导出章节对话表为 Excel (.xlsx)

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ChapterDialogueExporter
{
    // ── 列头 ──────────────────────────────────
    static readonly string[] Headers =
    {
        "序列编号",
        "对话序列 (触发类型)",
        "条目序号",
        "说话人",
        "对话内容",
        "节点类型",
        "打字机",
        "打字机速度",
        "自定义动作ID",
        "动作参数"
    };

    static readonly int[] ColWidths =
    {
        10,  // 序列编号
        24,  // 对话序列
        10,  // 条目序号
        14,  // 说话人
        70,  // 对话内容
        14,  // 节点类型
        10,  // 打字机
        12,  // 打字机速度
        20,  // 自定义动作ID
        20   // 动作参数
    };

    // ── 菜单入口 ──────────────────────────────
    [MenuItem("Tools/线索导出/导出章节对话表为 Excel (.xlsx)")]
    public static void Export()
    {
        const string dialogueRoot = "Assets/DataSO/DIalogueData";

        if (!AssetDatabase.IsValidFolder(dialogueRoot))
        {
            EditorUtility.DisplayDialog("错误", $"找不到对话目录：{dialogueRoot}", "确定");
            return;
        }

        string defaultName = $"章节对话表_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string savePath    = EditorUtility.SaveFilePanel("保存章节对话表", "", defaultName, "xlsx");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            var caseData = BuildCaseData(dialogueRoot);

            if (caseData.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到任何章节对话数据。", "确定");
                return;
            }

            WriteXlsx(savePath, caseData);

            int totalRows = 0;
            foreach (var v in caseData.Values)
                totalRows += v.Count;

            EditorUtility.DisplayDialog("完成",
                $"导出成功！\n案件数：{caseData.Count}\n对话行数：{totalRows}\n\n保存至：\n{savePath}", "确定");
            EditorUtility.RevealInFinder(savePath);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("导出失败", ex.Message + "\n\n" + ex.StackTrace, "确定");
            Debug.LogException(ex);
        }
    }

    // ── 数据结构 ──────────────────────────────
    class ChapterDialogueRow
    {
        public string SeqIndex      = "";
        public string TriggerType   = "";
        public string EntryIndex    = "";
        public string SpeakerName   = "";
        public string DialogueText  = "";
        public string NodeType      = "";
        public string UseTypewriter = "";
        public string TypewriterSpeed = "";
        public string CustomActionId = "";
        public string CustomActionArgument = "";
    }

    // ── 核心：收集数据 ────────────────────────
    static SortedDictionary<string, List<ChapterDialogueRow>> BuildCaseData(string dialogueRoot)
    {
        var result = new SortedDictionary<string, List<ChapterDialogueRow>>(StringComparer.Ordinal);

        // 查找所有 DialogueData_LevelX.asset 文件
        string[] guids = AssetDatabase.FindAssets("t:DialogueData", new[] { dialogueRoot });
        
        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            var dialogueData = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);
            if (dialogueData == null) continue;

            // 从文件名提取案件编号：DialogueData_Level0 → Case0
            int levelNumber = dialogueData.levelNumber;
            string caseName = $"Case{levelNumber}";

            var rows = new List<ChapterDialogueRow>();

            if (dialogueData.dialogueSequences != null)
            {
                int globalSeqIndex = 1; // 全局序列编号

                foreach (var sequence in dialogueData.dialogueSequences)
                {
                    if (sequence == null) continue;

                    string triggerDesc = GetTriggerTypeDescription(sequence.triggerType, sequence.waveNumber);

                    if (sequence.entries != null)
                    {
                        for (int entryIdx = 0; entryIdx < sequence.entries.Length; entryIdx++)
                        {
                            var entry = sequence.entries[entryIdx];
                            if (entry == null) continue;

                            var row = new ChapterDialogueRow
                            {
                                SeqIndex      = globalSeqIndex.ToString(),
                                TriggerType   = triggerDesc,
                                EntryIndex    = (entryIdx + 1).ToString(),
                                SpeakerName   = entry.speakerName ?? "",
                                DialogueText  = entry.dialogueText ?? "",
                                NodeType      = GetNodeTypeDescription(entry.nodeType),
                                UseTypewriter = entry.useTypewriterEffect ? "是" : "否",
                                TypewriterSpeed = entry.typewriterSpeed.ToString(),
                                CustomActionId = entry.customActionId ?? "",
                                CustomActionArgument = entry.customActionArgument ?? ""
                            };

                            rows.Add(row);
                        }
                    }

                    globalSeqIndex++;
                }
            }

            result[caseName] = rows;
        }

        return result;
    }

    static string GetTriggerTypeDescription(DialogueTriggerType triggerType, int waveNumber)
    {
        return triggerType switch
        {
            DialogueTriggerType.LevelStart => "关卡开始 (LevelStart)",
            DialogueTriggerType.WaveSpawn => $"波次生成 (WaveSpawn) - 波次{waveNumber}",
            DialogueTriggerType.LevelComplete => "关卡完成 (LevelComplete)",
            _ => "未知"
        };
    }

    static string GetNodeTypeDescription(DialogueNodeType nodeType)
    {
        return nodeType switch
        {
            DialogueNodeType.Normal => "普通对话",
            DialogueNodeType.NameInput => "起名弹窗",
            DialogueNodeType.CustomAction => "自定义动作",
            _ => "未知"
        };
    }

    // ══════════════════════════════════════════
    //  写 xlsx（每个案件一个 Sheet）
    // ══════════════════════════════════════════
    static void WriteXlsx(string filePath,
        SortedDictionary<string, List<ChapterDialogueRow>> caseData)
    {
        const string COL_HEADER  = "FF2F5496";
        const string COL_ODD     = "FFEEF3FA";
        const string COL_SECTION = "FFFFD966"; // 黄色段落标题行
        const string COL_BORDER  = "FFAAAAAA";

        var caseNames = new List<string>(caseData.Keys);
        int sheetCount = caseNames.Count;

        var sst = new ChapSharedStringTable();
        foreach (string h in Headers) sst.Add(h);

        var allSheetRows  = new List<List<List<int>>>();
        var allRowStyles  = new List<List<int>>();

        foreach (string caseName in caseNames)
        {
            var rows      = caseData[caseName];
            var sheetRows = new List<List<int>>();
            var rowStyles = new List<int>();

            // 列头
            var hRow = new List<int>();
            foreach (string h in Headers) hRow.Add(sst.Add(h));
            sheetRows.Add(hRow);
            rowStyles.Add(0);

            int dataRowIdx = 0;
            foreach (var r in rows)
            {
                var vals = new[]
                {
                    r.SeqIndex, r.TriggerType, r.EntryIndex,
                    r.SpeakerName, r.DialogueText, r.NodeType,
                    r.UseTypewriter, r.TypewriterSpeed,
                    r.CustomActionId, r.CustomActionArgument
                };
                var dRow = new List<int>();
                foreach (string v in vals) dRow.Add(sst.Add(v ?? ""));
                sheetRows.Add(dRow);

                // 判断行类型
                int style;
                bool isEmpty = string.IsNullOrEmpty(r.SpeakerName) && string.IsNullOrEmpty(r.DialogueText);
                if (isEmpty)
                    style = 3; // 空行
                else if (dataRowIdx % 2 == 0)
                    style = 1; // 奇数行
                else
                    style = 2; // 偶数行

                rowStyles.Add(style);
                dataRowIdx++;
            }

            allSheetRows.Add(sheetRows);
            allRowStyles.Add(rowStyles);
        }

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ChapZipWrite(zip, "[Content_Types].xml", ChapContentTypes(sheetCount));
            ChapZipWrite(zip, "_rels/.rels",          ChapRels());
            ChapZipWrite(zip, "xl/workbook.xml",       ChapWorkbook(caseNames));
            ChapZipWrite(zip, "xl/_rels/workbook.xml.rels", ChapWorkbookRels(sheetCount));
            ChapZipWrite(zip, "xl/styles.xml",
                ChapStyles(COL_HEADER, COL_ODD, COL_SECTION, COL_BORDER));
            ChapZipWrite(zip, "xl/sharedStrings.xml",  sst.ToXml());

            for (int i = 0; i < sheetCount; i++)
                ChapZipWrite(zip, $"xl/worksheets/sheet{i + 1}.xml",
                    ChapSheetXml(allSheetRows[i], allRowStyles[i], ColWidths));
        }
        File.WriteAllBytes(filePath, ms.ToArray());
    }

    // ══════════════════════════════════════════
    //  OOXML 各部件
    // ══════════════════════════════════════════
    static string ChapContentTypes(int n)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">");
        sb.AppendLine(@"  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>");
        sb.AppendLine(@"  <Default Extension=""xml"" ContentType=""application/xml""/>");
        sb.AppendLine(@"  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>");
        for (int i = 1; i <= n; i++)
            sb.AppendLine($@"  <Override PartName=""/xl/worksheets/sheet{i}.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>");
        sb.AppendLine(@"  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>");
        sb.AppendLine(@"  <Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>");
        sb.Append("</Types>");
        return sb.ToString();
    }

    static string ChapRels() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

    static string ChapWorkbook(List<string> names)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");
        sb.AppendLine("  <sheets>");
        for (int i = 0; i < names.Count; i++)
        {
            string n = ChapAttrEsc(names[i]);
            if (n.Length > 31) n = n.Substring(0, 31);
            sb.AppendLine($@"    <sheet name=""{n}"" sheetId=""{i + 1}"" r:id=""rId{i + 1}""/>");
        }
        sb.AppendLine("  </sheets>");
        sb.Append("</workbook>");
        return sb.ToString();
    }

    static string ChapWorkbookRels(int n)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">");
        for (int i = 1; i <= n; i++)
            sb.AppendLine($@"  <Relationship Id=""rId{i}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet{i}.xml""/>");
        sb.AppendLine($@"  <Relationship Id=""rId{n + 1}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>");
        sb.AppendLine($@"  <Relationship Id=""rId{n + 2}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>");
        sb.Append("</Relationships>");
        return sb.ToString();
    }

    // styles：0=默认 1=标题 2=奇数行 3=偶数行 4=空行
    static string ChapStyles(string cH, string cOdd, string cSec, string cBdr) =>
        $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font><sz val=""11""/><name val=""Microsoft YaHei""/></font>
    <font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Microsoft YaHei""/></font>
  </fonts>
  <fills count=""4"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{cH}""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{cOdd}""/></patternFill></fill>
  </fills>
  <borders count=""2"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left style=""thin""><color rgb=""{cBdr}""/></left>
      <right style=""thin""><color rgb=""{cBdr}""/></right>
      <top style=""thin""><color rgb=""{cBdr}""/></top>
      <bottom style=""thin""><color rgb=""{cBdr}""/></bottom>
    </border>
  </borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""4"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""3"" borderId=""1"" xfId=""0"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
  </cellXfs>
</styleSheet>";

    static int RowStyleToXfIndex(int style, bool isHeaderRow) =>
        isHeaderRow ? 1 : style switch
        {
            1 => 2,  // 奇数行 → 浅蓝
            2 => 3,  // 偶数行 → 白色
            3 => 3,  // 空行 → 白色
            _ => 3
        };

    static string ChapSheetXml(List<List<int>> rows, List<int> rowStyles, int[] widths)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");
        sb.AppendLine(@"  <sheetViews><sheetView tabSelected=""1"" workbookViewId=""0""><pane ySplit=""1"" topLeftCell=""A2"" activePane=""bottomLeft"" state=""frozen""/></sheetView></sheetViews>");
        sb.AppendLine("  <cols>");
        for (int i = 0; i < widths.Length; i++)
            sb.AppendLine($@"    <col min=""{i+1}"" max=""{i+1}"" width=""{widths[i]}"" customWidth=""1""/>");
        sb.AppendLine("  </cols>");
        sb.AppendLine("  <sheetData>");

        for (int ri = 0; ri < rows.Count; ri++)
        {
            bool isHeader = ri == 0;
            int  style    = rowStyles[ri];
            int  xfIdx    = RowStyleToXfIndex(style, isHeader);
            double ht     = isHeader ? 28 : 75;
            int excelRow  = ri + 1;

            sb.AppendLine($@"    <row r=""{excelRow}"" customHeight=""1"" ht=""{ht}"">");
            var cols = rows[ri];
            for (int ci = 0; ci < cols.Count; ci++)
            {
                string cellRef = ChapColLetter(ci + 1) + excelRow;
                sb.AppendLine($@"      <c r=""{cellRef}"" t=""s"" s=""{xfIdx}""><v>{cols[ci]}</v></c>");
            }
            sb.AppendLine("    </row>");
        }

        sb.AppendLine("  </sheetData>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    // ── 工具 ──────────────────────────────────
    static void ChapZipWrite(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
        using var s = entry.Open();
        var b = Encoding.UTF8.GetBytes(content);
        s.Write(b, 0, b.Length);
    }

    static string ChapColLetter(int col)
    {
        string r = "";
        while (col > 0) { col--; r = (char)('A' + col % 26) + r; col /= 26; }
        return r;
    }

    static string ChapAttrEsc(string s) =>
        s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;")
         .Replace("\"","&quot;").Replace("'","&apos;");

    // ── SharedStringTable ─────────────────────
    class ChapSharedStringTable
    {
        readonly List<string>        _list = new List<string>();
        readonly Dictionary<string,int> _map = new Dictionary<string,int>();

        public int Add(string s)
        {
            s = s ?? "";
            if (_map.TryGetValue(s, out int idx)) return idx;
            idx = _list.Count;
            _list.Add(s); _map[s] = idx;
            return idx;
        }

        public string ToXml()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.AppendLine($@"<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{_list.Count}"" uniqueCount=""{_list.Count}"">");
            foreach (string s in _list)
            {
                string esc = s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;")
                              .Replace("\r\n","&#10;").Replace("\r","&#10;").Replace("\n","&#10;");
                sb.AppendLine($"  <si><t xml:space=\"preserve\">{esc}</t></si>");
            }
            sb.Append("</sst>");
            return sb.ToString();
        }
    }
}
