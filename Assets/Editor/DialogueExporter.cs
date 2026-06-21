// Assets/Editor/DialogueExporter.cs
// 导出 PersonClueData 中的 NPC 对话 + OptionDialogueDB 中主角插播对话
// 每个案件一张 Sheet，格式便于阅读
// 菜单：Tools > 线索导出 > 导出人物对话表为 Excel (.xlsx)

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class DialogueExporter
{
    // ── 列头 ──────────────────────────────────
    static readonly string[] Headers =
    {
        "人物编号", "人物名称", "触发条件", "节点ID",
        "说话人", "说话人角色", "对话内容",
        "选项ID", "选项文本", "跳转节点",
        "备注"
    };

    static readonly int[] ColWidths =
    {
        10,  // 人物编号
        14,  // 人物名称
        24,  // 触发条件
        10,  // 节点ID
        14,  // 说话人
        12,  // 说话人角色
        70,  // 对话内容
        14,  // 选项ID
        40,  // 选项文本
        10,  // 跳转节点
        30   // 备注
    };

    // ── 菜单入口 ──────────────────────────────
    [MenuItem("Tools/线索导出/导出人物对话表为 Excel (.xlsx)")]
    public static void Export()
    {
        const string cluesRoot    = "Assets/DataSO/Clues";
        const string dialogueRoot = "Assets/DataSO/DIalogueData";

        if (!AssetDatabase.IsValidFolder(cluesRoot))
        {
            EditorUtility.DisplayDialog("错误", $"找不到线索目录：{cluesRoot}", "确定");
            return;
        }

        string defaultName = $"人物对话表_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string savePath    = EditorUtility.SaveFilePanel("保存人物对话表", "", defaultName, "xlsx");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            var caseData = BuildCaseData(cluesRoot, dialogueRoot);

            if (caseData.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到任何人物线索。", "确定");
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
    class DialogueRow
    {
        public string PersonId      = "";
        public string PersonName    = "";
        public string TriggerDesc   = "";  // 触发条件描述
        public string NodeId        = "";
        public string Speaker       = "";
        public string SpeakerRole   = "";  // NPC / 主角 / 系统
        public string DialogueText  = "";
        public string OptionId      = "";
        public string OptionText    = "";
        public string NextNodeId    = "";
        public string Remark        = "";
    }

    // ── 核心：收集数据 ────────────────────────
    static SortedDictionary<string, List<DialogueRow>> BuildCaseData(
        string cluesRoot, string dialogueRoot)
    {
        var result = new SortedDictionary<string, List<DialogueRow>>(StringComparer.Ordinal);

        string[] caseFolders = AssetDatabase.GetSubFolders(cluesRoot);
        Array.Sort(caseFolders, StringComparer.Ordinal);

        foreach (string caseFolder in caseFolders)
        {
            string caseName = System.IO.Path.GetFileName(caseFolder);

            // 案件编号 0/1/2 → Level0/1/2
            int caseIdx = ExtractCaseIndex(caseName);

            // 加载对应的 OptionDialogueDB
            var optionDb = LoadOptionDb(dialogueRoot, caseIdx);

            // 找 Person 类线索
            string personPath = $"{caseFolder}/Person";
            if (!AssetDatabase.IsValidFolder(personPath)) continue;

            string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { personPath });
            var persons = new List<PersonClueData>();
            foreach (string guid in guids)
            {
                string ap = AssetDatabase.GUIDToAssetPath(guid);
                var p = AssetDatabase.LoadAssetAtPath<PersonClueData>(ap);
                if (p != null) persons.Add(p);
            }
            persons.Sort((a, b) => string.Compare(a.id, b.id, StringComparison.Ordinal));

            var rows = new List<DialogueRow>();

            foreach (var person in persons)
                AppendPersonRows(rows, person, optionDb, caseIdx);

            result[caseName] = rows;
        }

        return result;
    }

    static int ExtractCaseIndex(string caseName)
    {
        // "Case0" → 0, "Case1" → 1, ...
        for (int i = caseName.Length - 1; i >= 0; i--)
        {
            if (char.IsDigit(caseName[i]))
                return int.Parse(caseName[i].ToString());
        }
        return 0;
    }

    static InterrogationOptionDialogueDatabaseSO LoadOptionDb(string dialogueRoot, int caseIdx)
    {
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { dialogueRoot });
        foreach (string guid in guids)
        {
            string ap = AssetDatabase.GUIDToAssetPath(guid);
            if (!ap.Contains($"Level{caseIdx}")) continue;
            var db = AssetDatabase.LoadAssetAtPath<InterrogationOptionDialogueDatabaseSO>(ap);
            if (db != null) return db;
        }
        return null;
    }

    // ── 为一个人物生成对话行 ──────────────────
    static void AppendPersonRows(List<DialogueRow> rows,
        PersonClueData person,
        InterrogationOptionDialogueDatabaseSO optionDb,
        int caseIdx)
    {
        // ─ 1. baseDialogues（普通基础对话）
        if (person.baseDialogues != null && person.baseDialogues.Count > 0)
        {
            rows.Add(SectionHeader(person, "【基础对话】"));
            AppendDialogueNodes(rows, person, person.baseDialogues, "基础对话", optionDb, caseIdx);
        }

        // ─ 2. clueDialogues（出示线索触发的对话）
        if (person.clueDialogues != null && person.clueDialogues.Count > 0)
        {
            rows.Add(SectionHeader(person, "【线索触发对话】"));
            foreach (var entry in person.clueDialogues)
            {
                if (entry?.dialogues == null) continue;
                string clueName = entry.shownClue != null
                    ? $"{entry.shownClue.id} {entry.shownClue.displayName}"
                    : "（未指定线索）";
                string triggerDesc = $"出示线索：{clueName}";
                if (entry.singleUse) triggerDesc += " [仅触发一次]";
                AppendDialogueNodes(rows, person, entry.dialogues, triggerDesc, optionDb, caseIdx);
            }
        }

        // ─ 3. fallbackDialogues（兜底对话）
        if (person.fallbackDialogues != null && person.fallbackDialogues.Count > 0)
        {
            rows.Add(SectionHeader(person, "【兜底对话（未命中线索时）】"));
            AppendDialogueNodes(rows, person, person.fallbackDialogues, "兜底对话", optionDb, caseIdx);
        }

        // 每个人物后加空行分隔
        rows.Add(new DialogueRow());
    }

    static DialogueRow SectionHeader(PersonClueData person, string title) =>
        new DialogueRow
        {
            PersonId   = person.id ?? "",
            PersonName = person.displayName ?? "",
            Remark     = title
        };

    // ── 展开一组 DialogueNode 为行 ─────────────
    static void AppendDialogueNodes(
        List<DialogueRow> rows,
        PersonClueData person,
        List<DialogueNode> nodes,
        string triggerDesc,
        InterrogationOptionDialogueDatabaseSO optionDb,
        int caseIdx)
    {
        foreach (var node in nodes)
        {
            if (node == null) continue;

            // ─ NPC 台词行
            var npcRow = new DialogueRow
            {
                PersonId     = person.id   ?? "",
                PersonName   = person.displayName ?? "",
                TriggerDesc  = triggerDesc,
                NodeId       = node.nodeId ?? "",
                Speaker      = person.displayName ?? "",
                SpeakerRole  = "NPC",
                DialogueText = node.text ?? "",
                NextNodeId   = node.nextNodeId ?? "",
            };
            rows.Add(npcRow);

            // ─ 每个选项：先写选项行，再写主角台词（从 OptionDB 查）
            if (node.options != null)
            {
                foreach (var opt in node.options)
                {
                    if (opt == null) continue;

                    // 主角选项行
                    var optRow = new DialogueRow
                    {
                        PersonId    = person.id ?? "",
                        PersonName  = person.displayName ?? "",
                        TriggerDesc = triggerDesc,
                        NodeId      = node.nodeId ?? "",
                        Speaker     = "工藤新一",
                        SpeakerRole = "主角（选项）",
                        OptionId    = opt.optionId   ?? "",
                        OptionText  = opt.optionText ?? "",
                        NextNodeId  = opt.nextNodeId ?? "",
                    };
                    rows.Add(optRow);

                    // 查 OptionDB：主角插播台词
                    if (optionDb != null &&
                        optionDb.TryGet(caseIdx, person.id ?? "",
                                        node.nodeId ?? "", opt.optionId ?? "",
                                        out var dbEntry) &&
                        dbEntry?.sequence?.entries != null)
                    {
                        foreach (var e in dbEntry.sequence.entries)
                        {
                            if (e == null) continue;
                            rows.Add(new DialogueRow
                            {
                                PersonId     = person.id ?? "",
                                PersonName   = person.displayName ?? "",
                                TriggerDesc  = triggerDesc,
                                NodeId       = node.nodeId ?? "",
                                Speaker      = e.speakerName ?? "",
                                SpeakerRole  = "主角（插播）",
                                DialogueText = e.dialogueText ?? "",
                                Remark       = "来自 OptionDialogueDB",
                            });
                        }
                    }
                }
            }
        }
    }

    // ══════════════════════════════════════════
    //  写 xlsx（每个案件一个 Sheet）
    // ══════════════════════════════════════════
    static void WriteXlsx(string filePath,
        SortedDictionary<string, List<DialogueRow>> caseData)
    {
        const string COL_HEADER  = "FF2F5496";
        const string COL_ODD     = "FFEEF3FA";
        const string COL_SECTION = "FFFFD966"; // 黄色段落标题行
        const string COL_OPTION  = "FFE2EFDA"; // 浅绿主角选项
        const string COL_INSERT  = "FFDDEBF7"; // 浅蓝主角插播
        const string COL_BORDER  = "FFAAAAAA";

        var caseNames = new List<string>(caseData.Keys);
        int sheetCount = caseNames.Count;

        var sst = new DlgSharedStringTable();
        foreach (string h in Headers) sst.Add(h);

        // 行类型标记（用于颜色）
        // 0=普通NPC  1=段落标题  2=主角选项  3=主角插播  4=空行
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
            rowStyles.Add(0); // header style handled separately

            int dataRowIdx = 0;
            foreach (var r in rows)
            {
                var vals = new[]
                {
                    r.PersonId, r.PersonName, r.TriggerDesc, r.NodeId,
                    r.Speaker, r.SpeakerRole, r.DialogueText,
                    r.OptionId, r.OptionText, r.NextNodeId, r.Remark
                };
                var dRow = new List<int>();
                foreach (string v in vals) dRow.Add(sst.Add(v ?? ""));
                sheetRows.Add(dRow);

                // 判断行类型
                int style;
                bool isEmpty = string.IsNullOrEmpty(r.PersonId) && string.IsNullOrEmpty(r.DialogueText)
                               && string.IsNullOrEmpty(r.Speaker);
                if (isEmpty)
                    style = 4;
                else if (!string.IsNullOrEmpty(r.Remark) && r.Remark.StartsWith("【"))
                    style = 1; // 段落标题
                else if (r.SpeakerRole == "主角（选项）")
                    style = 2;
                else if (r.SpeakerRole == "主角（插播）")
                    style = 3;
                else
                    style = dataRowIdx % 2 == 0 ? 5 : 6; // 奇偶NPC行

                rowStyles.Add(style);
                dataRowIdx++;
            }

            allSheetRows.Add(sheetRows);
            allRowStyles.Add(rowStyles);
        }

        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            DlgZipWrite(zip, "[Content_Types].xml", DlgContentTypes(sheetCount));
            DlgZipWrite(zip, "_rels/.rels",          DlgRels());
            DlgZipWrite(zip, "xl/workbook.xml",       DlgWorkbook(caseNames));
            DlgZipWrite(zip, "xl/_rels/workbook.xml.rels", DlgWorkbookRels(sheetCount));
            DlgZipWrite(zip, "xl/styles.xml",
                DlgStyles(COL_HEADER, COL_ODD, COL_SECTION, COL_OPTION, COL_INSERT, COL_BORDER));
            DlgZipWrite(zip, "xl/sharedStrings.xml",  sst.ToXml());

            for (int i = 0; i < sheetCount; i++)
                DlgZipWrite(zip, $"xl/worksheets/sheet{i + 1}.xml",
                    DlgSheetXml(allSheetRows[i], allRowStyles[i], ColWidths));
        }
        File.WriteAllBytes(filePath, ms.ToArray());
    }

    // ══════════════════════════════════════════
    //  OOXML 各部件
    // ══════════════════════════════════════════
    static string DlgContentTypes(int n)
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

    static string DlgRels() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

    static string DlgWorkbook(List<string> names)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");
        sb.AppendLine("  <sheets>");
        for (int i = 0; i < names.Count; i++)
        {
            string n = DlgAttrEsc(names[i]);
            if (n.Length > 31) n = n.Substring(0, 31);
            sb.AppendLine($@"    <sheet name=""{n}"" sheetId=""{i + 1}"" r:id=""rId{i + 1}""/>");
        }
        sb.AppendLine("  </sheets>");
        sb.Append("</workbook>");
        return sb.ToString();
    }

    static string DlgWorkbookRels(int n)
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

    // styles：0=默认 1=标题 2=段落标题 3=主角选项 4=主角插播 5=NPC奇 6=NPC偶 7=空行
    static string DlgStyles(string cH, string cOdd, string cSec, string cOpt, string cIns, string cBdr) =>
        $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""3"">
    <font><sz val=""11""/><name val=""Microsoft YaHei""/></font>
    <font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Microsoft YaHei""/></font>
    <font><b/><sz val=""10""/><name val=""Microsoft YaHei""/></font>
  </fonts>
  <fills count=""7"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{cH}""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{cSec}""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{cOpt}""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{cIns}""/></patternFill></fill>
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
  <cellXfs count=""8"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""2"" fillId=""3"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""center"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""4"" borderId=""1"" xfId=""0"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""5"" borderId=""1"" xfId=""0"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""6"" borderId=""1"" xfId=""0"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0"" applyBorder=""1"" applyAlignment=""1""><alignment vertical=""top"" wrapText=""1""/></xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
  </cellXfs>
</styleSheet>";

    // styleId 映射：0=默认 1=标题行 2=段落标题 3=主角选项 4=主角插播 5=NPC奇 6=NPC偶 7=空行
    static int RowStyleToXfIndex(int style, bool isHeaderRow) =>
        isHeaderRow ? 1 : style switch
        {
            1 => 2,  // 段落标题 → 黄色加粗
            2 => 3,  // 主角选项 → 浅绿
            3 => 4,  // 主角插播 → 浅蓝
            4 => 7,  // 空行     → 无边框
            5 => 5,  // NPC奇    → 浅蓝奇
            6 => 6,  // NPC偶    → 白色
            _ => 6
        };

    static string DlgSheetXml(List<List<int>> rows, List<int> rowStyles, int[] widths)
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
            double ht     = isHeader ? 28 : (style == 4 ? 10 : (style == 1 ? 22 : 75));
            int excelRow  = ri + 1;

            sb.AppendLine($@"    <row r=""{excelRow}"" customHeight=""1"" ht=""{ht}"">");
            var cols = rows[ri];
            for (int ci = 0; ci < cols.Count; ci++)
            {
                string cellRef = DlgColLetter(ci + 1) + excelRow;
                sb.AppendLine($@"      <c r=""{cellRef}"" t=""s"" s=""{xfIdx}""><v>{cols[ci]}</v></c>");
            }
            sb.AppendLine("    </row>");
        }

        sb.AppendLine("  </sheetData>");
        sb.Append("</worksheet>");
        return sb.ToString();
    }

    // ── 工具 ──────────────────────────────────
    static void DlgZipWrite(ZipArchive zip, string name, string content)
    {
        var entry = zip.CreateEntry(name, System.IO.Compression.CompressionLevel.Optimal);
        using var s = entry.Open();
        var b = Encoding.UTF8.GetBytes(content);
        s.Write(b, 0, b.Length);
    }

    static string DlgColLetter(int col)
    {
        string r = "";
        while (col > 0) { col--; r = (char)('A' + col % 26) + r; col /= 26; }
        return r;
    }

    static string DlgAttrEsc(string s) =>
        s.Replace("&","&amp;").Replace("<","&lt;").Replace(">","&gt;")
         .Replace("\"","&quot;").Replace("'","&apos;");

    // ── SharedStringTable ─────────────────────
    class DlgSharedStringTable
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
