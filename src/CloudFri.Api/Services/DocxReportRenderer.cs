using System.Collections;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace CloudFri.Api.Services;

/// <summary>
/// Word 模板渲染（自 EnterpriseMS 报告生成服务移植，保持同一套占位符约定）：
/// 标量 {{字段}} 全文替换；值是列表的字段作为「行循环标记」——模板表格中含 {{明细}} 的行
/// 会按列表逐行克隆，行内 {{字段}} 取当前行字典的值。替换时整段重建（保留首个 run 的样式，
/// 文本中的换行转 &lt;w:br/&gt;），因此占位符被拆散在多个 run 中也能正确替换。
/// </summary>
public static class DocxReportRenderer
{
    public static byte[] Render(byte[] templateBytes, Dictionary<string, object> fieldValues)
    {
        using var ms = new MemoryStream();
        ms.Write(templateBytes, 0, templateBytes.Length);
        ms.Position = 0;
        using (var doc = WordprocessingDocument.Open(ms, true))
        {
            var body = doc.MainDocumentPart?.Document?.Body
                       ?? throw new InvalidOperationException("模板内容为空");

            var scalars = fieldValues
                .Where(kv => !IsListValue(kv.Value))
                .ToDictionary(kv => kv.Key, kv => kv.Value?.ToString() ?? "");
            var lists = fieldValues
                .Where(kv => IsListValue(kv.Value))
                .ToDictionary(kv => kv.Key, kv => (IEnumerable)kv.Value);

            // 1) 表格：含列表标记的行做行循环；其余行做标量替换
            foreach (var table in body.Descendants<Table>().ToList())
            {
                foreach (var row in table.Elements<TableRow>().ToList())
                {
                    var rowText = string.Concat(row.Descendants<Paragraph>().Select(p => p.InnerText));
                    var marker = lists.Keys.FirstOrDefault(k => rowText.Contains("{{" + k + "}}"));
                    if (marker != null)
                    {
                        var clones = new List<TableRow>();
                        foreach (var item in lists[marker])
                        {
                            var itemDict = ToStringDict(item);
                            var clone = (TableRow)row.CloneNode(true);
                            FillRowScalars(clone, itemDict, scalars, marker);
                            clones.Add(clone);
                        }
                        foreach (var nr in clones) row.Parent!.InsertBefore(nr, row);
                        row.Remove();
                    }
                    else
                    {
                        FillRowScalars(row, null, scalars, null);
                    }
                }
            }

            // 2) 正文段落标量替换
            foreach (var para in body.Descendants<Paragraph>().ToList())
                FillParagraphScalars(para, scalars);

            // 3) VML 艺术字/形状：文本在 <v:textpath string="..."> 属性中，单独处理
            foreach (var textPath in body.Descendants<DocumentFormat.OpenXml.Vml.TextPath>().ToList())
            {
                var s = textPath.String?.Value;
                if (string.IsNullOrEmpty(s)) continue;
                var changed = false;
                foreach (var kv in scalars)
                    if (s.Contains("{{" + kv.Key + "}}")) { s = s.Replace("{{" + kv.Key + "}}", kv.Value); changed = true; }
                if (changed) textPath.String = s;
            }

            doc.MainDocumentPart!.Document.Save();
        }
        return ms.ToArray();
    }

    private static bool IsListValue(object? v) => v is IEnumerable && v is not string;

    private static Dictionary<string, string> ToStringDict(object item)
    {
        var d = new Dictionary<string, string>();
        if (item is IDictionary<string, object> d1)
            foreach (var kv in d1) d[kv.Key] = kv.Value?.ToString() ?? "";
        else if (item is IDictionary<string, string> d2)
            foreach (var kv in d2) d[kv.Key] = kv.Value ?? "";
        return d;
    }

    private static void FillRowScalars(TableRow row, Dictionary<string, string>? itemDict, Dictionary<string, string> scalars, string? markerKey)
    {
        foreach (var para in row.Descendants<Paragraph>().ToList())
        {
            var text = para.InnerText;
            var changed = false;
            // 清除循环标记（{{明细}} 等），避免残留
            if (markerKey != null && text.Contains("{{" + markerKey + "}}"))
            {
                text = text.Replace("{{" + markerKey + "}}", "");
                changed = true;
            }
            if (itemDict != null)
                foreach (var kv in itemDict)
                    if (text.Contains("{{" + kv.Key + "}}")) { text = text.Replace("{{" + kv.Key + "}}", kv.Value); changed = true; }
            foreach (var kv in scalars)
                if (text.Contains("{{" + kv.Key + "}}")) { text = text.Replace("{{" + kv.Key + "}}", kv.Value); changed = true; }
            if (changed) RebuildParagraph(para, text);
        }
    }

    private static void FillParagraphScalars(Paragraph para, Dictionary<string, string> scalars)
    {
        var text = para.InnerText;
        var changed = false;
        foreach (var kv in scalars)
            if (text.Contains("{{" + kv.Key + "}}")) { text = text.Replace("{{" + kv.Key + "}}", kv.Value); changed = true; }
        if (changed) RebuildParagraph(para, text);
    }

    private static void RebuildParagraph(Paragraph para, string text)
    {
        var runProps = para.Elements<Run>().FirstOrDefault()?.RunProperties?.CloneNode(true);
        para.Elements<Run>().ToList().ForEach(r => r.Remove());
        // 按换行拆分，换行处插入 <w:br/>，保留多行文本
        var lines = text.Split('\n');
        for (int i = 0; i < lines.Length; i++)
        {
            if (i > 0) para.Append(new Run(new Break()));
            var run = new Run();
            if (runProps != null) run.Append((RunProperties)runProps.CloneNode(true));
            run.Append(new Text(lines[i]) { Space = SpaceProcessingModeValues.Preserve });
            para.Append(run);
        }
    }
}
