// Assets/Editor/ClueExporter.cs
// Unity Editor 工具：将 Clues 目录下所有线索 SO 导出为 xlsx
// 每个案子（Case0/Case1/…）生成独立的 Sheet
// 菜单：Tools > 线索导出 > 导出线索表为 Excel (.xlsx)
//
// 无第三方依赖：使用 System.IO.Compression 手写 OOXML zip 包

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ClueExporter
{
    // ══════════════════════════════════════════
    //  列头定义（与 ClueRow 字段一一对应）
    // ══════════════════════════════════════════
    static readonly string[] Headers =
    {
        "编号",
        "显示名称",
        "线索类型",
        "可探测",
        "可收集",
        "已收集",
        "AttackKey",
        "已解锁Attack内容",
        "摘要",
        "详细内容 (detailText)",
        "富文本内容 (Detail_Mark)",
        "可传唤 (Person专属)",
        "有头像 (Person专属)",
        "时间帧数量 (Camera专属)",
        "资产路径"
    };

    static readonly int[] ColWidths =
    {
        8,   // 编号
        16,  // 显示名称
        10,  // 线索类型
        8,   // 可探测
        8,   // 可收集
        8,   // 已收集
        14,  // AttackKey
        12,  // 已解锁Attack内容
        42,  // 摘要
        62,  // detailText
        62,  // Detail_Mark
        12,  // 可传唤
        10,  // 有头像
        14,  // 时间帧数量
        52   // 资产路径
    };

    // ══════════════════════════════════════════
    //  菜单入口
    // ══════════════════════════════════════════
    [MenuItem("Tools/线索导出/导出线索表为 Excel (.xlsx)")]
    public static void ExportAllCases()
    {
        const string cluesRoot = "Assets/DataSO/Clues";
        if (!AssetDatabase.IsValidFolder(cluesRoot))
        {
            EditorUtility.DisplayDialog("错误", $"找不到线索目录：{cluesRoot}", "确定");
            return;
        }

        string defaultName = $"线索表_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        string savePath    = EditorUtility.SaveFilePanel("保存线索表", "", defaultName, "xlsx");
        if (string.IsNullOrEmpty(savePath)) return;

        try
        {
            // 按案件名分组收集
            var caseDict = CollectByCase(cluesRoot);

            if (caseDict.Count == 0)
            {
                EditorUtility.DisplayDialog("提示", "未找到任何线索 SO 资产。", "确定");
                return;
            }

            WriteXlsx(savePath, caseDict);

            int total = 0;
            foreach (var v in caseDict.Values) total += v.Count;

            EditorUtility.DisplayDialog("完成",
                $"导出成功！\n" +
                $"案件数：{caseDict.Count}\n" +
                $"线索总数：{total}\n\n" +
                $"保存至：\n{savePath}", "确定");

            EditorUtility.RevealInFinder(savePath);
        }
        catch (Exception ex)
        {
            EditorUtility.DisplayDialog("导出失败", ex.Message + "\n\n" + ex.StackTrace, "确定");
            Debug.LogException(ex);
        }
    }

    // ══════════════════════════════════════════
    //  收集数据，按案件名分组
    //  返回 SortedDictionary<caseName, List<ClueRow>>
    // ══════════════════════════════════════════
    static SortedDictionary<string, List<ClueRow>> CollectByCase(string cluesRoot)
    {
        var result = new SortedDictionary<string, List<ClueRow>>(StringComparer.Ordinal);

        string[] caseFolders = AssetDatabase.GetSubFolders(cluesRoot);
        Array.Sort(caseFolders, StringComparer.Ordinal);

        string[] typeFolders = { "Normal", "Person", "Camera" };

        foreach (string caseFolder in caseFolders)
        {
            string caseName = Path.GetFileName(caseFolder);
            var rows = new List<ClueRow>();

            foreach (string typeFolder in typeFolders)
            {
                string typePath = $"{caseFolder}/{typeFolder}";
                if (!AssetDatabase.IsValidFolder(typePath)) continue;

                string[] guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { typePath });
                foreach (string guid in guids)
                {
                    string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                    var clue = AssetDatabase.LoadAssetAtPath<ClueData>(assetPath);
                    if (clue == null) continue;

                    var row = new ClueRow
                    {
                        Id          = clue.id          ?? "",
                        DisplayName = clue.displayName ?? "",
                        ClueType    = typeFolder,
                        Detectable  = clue.detectable              ? "是" : "否",
                        Collectable = clue.collectable             ? "是" : "否",
                        Collected   = clue.collected               ? "是" : "否",
                        AttackKey   = clue.attackKey               ?? "",
                        AttackUnlocked = clue.isAttackContentUnlocked ? "是" : "否",
                        Summary     = clue.summary                 ?? "",
                        DetailText  = clue.detailText              ?? "",
                        DetailMark  = clue.Detail_Mark             ?? "",
                        AssetPath   = assetPath,
                    };

                    if (clue is PersonClueData person)
                    {
                        row.CanBeSummoned = person.canBeSummoned      ? "是" : "否";
                        row.HasPortrait   = person.portrait != null   ? "是" : "否";
                    }

                    if (clue is CameraClueData cam)
                        row.FrameCount = (cam.frames?.Count ?? 0).ToString();

                    rows.Add(row);
                }
            }

            // 案件内按类型顺序 → id 排序
            rows.Sort((a, b) =>
            {
                int t = TypeOrder(a.ClueType).CompareTo(TypeOrder(b.ClueType));
                if (t != 0) return t;
                return string.Compare(a.Id, b.Id, StringComparison.Ordinal);
            });

            result[caseName] = rows;
        }

        return result;
    }

    static int TypeOrder(string t) => t switch
    {
        "Normal" => 0, "Person" => 1, "Camera" => 2, _ => 9
    };

    // ══════════════════════════════════════════
    //  写 xlsx
    //  每个案件 = 一个 Sheet
    // ══════════════════════════════════════════
    static void WriteXlsx(string filePath, SortedDictionary<string, List<ClueRow>> caseDict)
    {
        const string COL_HEADER = "FF2F5496";  // 深蓝
        const string COL_ODD    = "FFEEF3FA";  // 浅蓝奇数行
        const string COL_BORDER = "FFAAAAAA";  // 边框灰

        var caseNames = new List<string>(caseDict.Keys);
        int sheetCount = caseNames.Count;

        // 全局 SharedStringTable（所有 sheet 共用）
        var sst = new SharedStringTable();

        // 预先注册列头
        foreach (string h in Headers) sst.Add(h);

        // 构建各 sheet 的单元格数据
        var allSheets = new List<List<List<int>>>();  // [sheet][row][col] = sstIndex

        foreach (string caseName in caseNames)
        {
            var rows    = caseDict[caseName];
            var sheetRows = new List<List<int>>();

            // ── 列头行 ──
            var hRow = new List<int>();
            foreach (string h in Headers) hRow.Add(sst.Add(h));
            sheetRows.Add(hRow);

            // ── 数据行 ──
            foreach (var r in rows)
            {
                var vals = new[]
                {
                    r.Id, r.DisplayName, r.ClueType,
                    r.Detectable, r.Collectable, r.Collected,
                    r.AttackKey, r.AttackUnlocked,
                    r.Summary, r.DetailText, r.DetailMark,
                    r.CanBeSummoned, r.HasPortrait, r.FrameCount,
                    r.AssetPath
                };
                var dRow = new List<int>();
                foreach (string v in vals) dRow.Add(sst.Add(v));
                sheetRows.Add(dRow);
            }

            allSheets.Add(sheetRows);
        }

        // ── 组装 zip ──
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            ZipWrite(zip, "[Content_Types].xml", BuildContentTypes(sheetCount));
            ZipWrite(zip, "_rels/.rels",          BuildRels());
            ZipWrite(zip, "xl/workbook.xml",       BuildWorkbook(caseNames));
            ZipWrite(zip, "xl/_rels/workbook.xml.rels", BuildWorkbookRels(sheetCount));
            ZipWrite(zip, "xl/styles.xml",         BuildStyles(COL_HEADER, COL_ODD, COL_BORDER));
            ZipWrite(zip, "xl/sharedStrings.xml",  sst.ToXml());

            for (int i = 0; i < sheetCount; i++)
                ZipWrite(zip, $"xl/worksheets/sheet{i + 1}.xml",
                         BuildSheetXml(allSheets[i], ColWidths));
        }

        File.WriteAllBytes(filePath, ms.ToArray());
    }

    // ══════════════════════════════════════════
    //  OOXML 各部件生成
    // ══════════════════════════════════════════

    static string BuildContentTypes(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<Types xmlns=""http://schemas.openxmlformats.org/package/2006/content-types"">");
        sb.AppendLine(@"  <Default Extension=""rels"" ContentType=""application/vnd.openxmlformats-package.relationships+xml""/>");
        sb.AppendLine(@"  <Default Extension=""xml""  ContentType=""application/xml""/>");
        sb.AppendLine(@"  <Override PartName=""/xl/workbook.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml""/>");
        for (int i = 1; i <= sheetCount; i++)
            sb.AppendLine($@"  <Override PartName=""/xl/worksheets/sheet{i}.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml""/>");
        sb.AppendLine(@"  <Override PartName=""/xl/styles.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml""/>");
        sb.AppendLine(@"  <Override PartName=""/xl/sharedStrings.xml"" ContentType=""application/vnd.openxmlformats-officedocument.spreadsheetml.sharedStrings+xml""/>");
        sb.AppendLine("</Types>");
        return sb.ToString();
    }

    static string BuildRels() =>
        @"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">
  <Relationship Id=""rId1"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument"" Target=""xl/workbook.xml""/>
</Relationships>";

    static string BuildWorkbook(List<string> caseNames)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<workbook xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""");
        sb.AppendLine(@"          xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");
        sb.AppendLine("  <sheets>");
        for (int i = 0; i < caseNames.Count; i++)
        {
            // Sheet 名称做 XML 转义并截短（Excel 最长 31 字符）
            string sheetName = XmlAttrEscape(caseNames[i]);
            if (sheetName.Length > 31) sheetName = sheetName.Substring(0, 31);
            sb.AppendLine($@"    <sheet name=""{sheetName}"" sheetId=""{i + 1}"" r:id=""rId{i + 1}""/>");
        }
        sb.AppendLine("  </sheets>");
        sb.AppendLine("</workbook>");
        return sb.ToString();
    }

    static string BuildWorkbookRels(int sheetCount)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<Relationships xmlns=""http://schemas.openxmlformats.org/package/2006/relationships"">");
        for (int i = 1; i <= sheetCount; i++)
            sb.AppendLine($@"  <Relationship Id=""rId{i}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet"" Target=""worksheets/sheet{i}.xml""/>");
        sb.AppendLine($@"  <Relationship Id=""rId{sheetCount + 1}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles"" Target=""styles.xml""/>");
        sb.AppendLine($@"  <Relationship Id=""rId{sheetCount + 2}"" Type=""http://schemas.openxmlformats.org/officeDocument/2006/relationships/sharedStrings"" Target=""sharedStrings.xml""/>");
        sb.AppendLine("</Relationships>");
        return sb.ToString();
    }

    static string BuildStyles(string colHeader, string colOdd, string colBorder) =>
        $@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
  <fonts count=""2"">
    <font><sz val=""11""/><name val=""Microsoft YaHei""/></font>
    <font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Microsoft YaHei""/></font>
  </fonts>
  <fills count=""4"">
    <fill><patternFill patternType=""none""/></fill>
    <fill><patternFill patternType=""gray125""/></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{colHeader}""/></patternFill></fill>
    <fill><patternFill patternType=""solid""><fgColor rgb=""{colOdd}""/></patternFill></fill>
  </fills>
  <borders count=""2"">
    <border><left/><right/><top/><bottom/><diagonal/></border>
    <border>
      <left   style=""thin""><color rgb=""{colBorder}""/></left>
      <right  style=""thin""><color rgb=""{colBorder}""/></right>
      <top    style=""thin""><color rgb=""{colBorder}""/></top>
      <bottom style=""thin""><color rgb=""{colBorder}""/></bottom>
    </border>
  </borders>
  <cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
  <cellXfs count=""4"">
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
    <xf numFmtId=""0"" fontId=""1"" fillId=""2"" borderId=""1"" xfId=""0""
        applyFont=""1"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1"">
      <alignment horizontal=""center"" vertical=""center"" wrapText=""1""/>
    </xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""3"" borderId=""1"" xfId=""0""
        applyFill=""1"" applyBorder=""1"" applyAlignment=""1"">
      <alignment vertical=""top"" wrapText=""1""/>
    </xf>
    <xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""1"" xfId=""0""
        applyBorder=""1"" applyAlignment=""1"">
      <alignment vertical=""top"" wrapText=""1""/>
    </xf>
  </cellXfs>
</styleSheet>";

    static string BuildSheetXml(List<List<int>> rows, int[] colWidths)
    {
        var sb = new StringBuilder();
        sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
        sb.AppendLine(@"<worksheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main""");
        sb.AppendLine(@"           xmlns:r=""http://schemas.openxmlformats.org/officeDocument/2006/relationships"">");

        // sheetViews：冻结首行
        sb.AppendLine(@"  <sheetViews>
    <sheetView tabSelected=""1"" workbookViewId=""0"">
      <pane ySplit=""1"" topLeftCell=""A2"" activePane=""bottomLeft"" state=""frozen""/>
    </sheetView>
  </sheetViews>");

        // 列宽
        sb.AppendLine("  <cols>");
        for (int i = 0; i < colWidths.Length; i++)
            sb.AppendLine($@"    <col min=""{i + 1}"" max=""{i + 1}"" width=""{colWidths[i]}"" customWidth=""1""/>");
        sb.AppendLine("  </cols>");

        // 数据
        sb.AppendLine("  <sheetData>");
        for (int rowIdx = 0; rowIdx < rows.Count; rowIdx++)
        {
            int excelRow = rowIdx + 1;
            // styleId：1=标题，2=奇数数据，3=偶数数据
            int styleId  = rowIdx == 0 ? 1 : (rowIdx % 2 == 1 ? 2 : 3);
            double ht    = rowIdx == 0 ? 30 : 90;
            sb.AppendLine($@"    <row r=""{excelRow}"" customHeight=""1"" ht=""{ht}"">");

            var cols = rows[rowIdx];
            for (int colIdx = 0; colIdx < cols.Count; colIdx++)
            {
                string cellRef = ColLetter(colIdx + 1) + excelRow;
                int    sstIdx  = cols[colIdx];
                sb.AppendLine($@"      <c r=""{cellRef}"" t=""s"" s=""{styleId}""><v>{sstIdx}</v></c>");
            }
            sb.AppendLine("    </row>");
        }
        sb.AppendLine("  </sheetData>");
        sb.AppendLine("</worksheet>");
        return sb.ToString();
    }

    // ══════════════════════════════════════════
    //  工具方法
    // ══════════════════════════════════════════

    static void ZipWrite(ZipArchive zip, string entryName, string content)
    {
        var entry = zip.CreateEntry(entryName, System.IO.Compression.CompressionLevel.Optimal);
        using var stream = entry.Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    static string ColLetter(int col)
    {
        string r = "";
        while (col > 0) { col--; r = (char)('A' + col % 26) + r; col /= 26; }
        return r;
    }

    static string XmlAttrEscape(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&apos;");

    // ══════════════════════════════════════════
    //  SharedStringTable
    // ══════════════════════════════════════════
    class SharedStringTable
    {
        readonly List<string>        _strings = new List<string>();
        readonly Dictionary<string, int> _map = new Dictionary<string, int>();

        public int Add(string s)
        {
            s = s ?? "";
            if (_map.TryGetValue(s, out int idx)) return idx;
            idx = _strings.Count;
            _strings.Add(s);
            _map[s] = idx;
            return idx;
        }

        public string ToXml()
        {
            var sb = new StringBuilder();
            sb.AppendLine(@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>");
            sb.AppendLine($@"<sst xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"" count=""{_strings.Count}"" uniqueCount=""{_strings.Count}"">");
            foreach (string s in _strings)
            {
                // xml:space="preserve" 保留空格；换行用 <t> 内的实际 &#10; 存储
                string escaped = s.Replace("&", "&amp;")
                                  .Replace("<", "&lt;")
                                  .Replace(">", "&gt;")
                                  .Replace("\r\n", "&#10;")
                                  .Replace("\r",   "&#10;")
                                  .Replace("\n",   "&#10;");
                sb.AppendLine($"  <si><t xml:space=\"preserve\">{escaped}</t></si>");
            }
            sb.AppendLine("</sst>");
            return sb.ToString();
        }
    }

    // ══════════════════════════════════════════
    //  数据结构
    // ══════════════════════════════════════════
    class ClueRow
    {
        public string Id             = "";
        public string DisplayName    = "";
        public string ClueType       = "";
        public string Detectable     = "";
        public string Collectable    = "";
        public string Collected      = "";
        public string AttackKey      = "";
        public string AttackUnlocked = "";
        public string Summary        = "";
        public string DetailText     = "";
        public string DetailMark     = "";
        public string CanBeSummoned  = "";
        public string HasPortrait    = "";
        public string FrameCount     = "";
        public string AssetPath      = "";
    }
}
