using CloudFri.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CloudFri.Api.Controllers;

/// <summary>
/// 报告生成（自 EnterpriseMS「报告生成」页移植）：Excel 导入、字段映射、筛选都在前端完成，
/// 这里只做两件事——列出内置 Word 模板、用模板把前端提交的标量与明细行渲染成 docx 下载。
/// 模板文件随 API 一起部署在 Templates/ 目录（与 EM 的 wwwroot/templates 同一份文件）。
/// </summary>
[Authorize]
[ApiController]
[Route("api/reportgen")]
public class ReportGenController : ControllerBase
{
    // 内置模板清单：id 对应 Templates/ 下的文件名（不带扩展名）
    private static readonly (string Id, string Name, string Category, string File)[] Templates =
    {
        ("cost-audit", "造价审核报告", "造价", "cost-audit-report.docx"),
    };

    private readonly string _templateDir =
        Path.Combine(AppContext.BaseDirectory, "Templates");

    [HttpGet("templates")]
    public IActionResult TemplatesList()
    {
        var list = Templates
            .Where(t => System.IO.File.Exists(Path.Combine(_templateDir, t.File)))
            .Select(t => new { id = t.Id, name = t.Name, category = t.Category });
        return Ok(list);
    }

    public class ExcelColumnDto
    {
        public string ColumnName { get; set; } = "";
        public string FieldName { get; set; } = "";
    }

    public class GenerateReq
    {
        public string TemplateId { get; set; } = "";
        public Dictionary<string, string>? SupplementaryFields { get; set; }
        public List<ExcelColumnDto>? ExcelColumns { get; set; }
        public List<Dictionary<string, string>>? ExcelRows { get; set; }
    }

    [HttpPost("generate")]
    public IActionResult Generate([FromBody] GenerateReq req)
    {
        var tpl = Templates.FirstOrDefault(t => t.Id == req.TemplateId);
        if (tpl.Id == null) return BadRequest(new { message = "模板不存在" });
        var path = Path.Combine(_templateDir, tpl.File);
        if (!System.IO.File.Exists(path)) return BadRequest(new { message = "模板文件不存在" });

        // 与 EM BuildFieldValues 同一口径：标量直填；Excel 行按 ColumnName→FieldName 聚合成列表字段「明细」
        var fieldValues = new Dictionary<string, object>();
        if (req.SupplementaryFields != null)
            foreach (var kv in req.SupplementaryFields) fieldValues[kv.Key] = kv.Value;

        if (req.ExcelRows is { Count: > 0 } && req.ExcelColumns is { Count: > 0 })
        {
            var listRows = new List<Dictionary<string, object>>();
            foreach (var row in req.ExcelRows)
            {
                var dict = new Dictionary<string, object>();
                foreach (var col in req.ExcelColumns)
                    if (row.TryGetValue(col.ColumnName, out var val))
                        dict[col.FieldName] = val;
                listRows.Add(dict);
            }
            fieldValues["明细"] = listRows;
        }

        try
        {
            var bytes = DocxReportRenderer.Render(System.IO.File.ReadAllBytes(path), fieldValues);
            return File(bytes,
                "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                tpl.Name + ".docx");
        }
        catch (Exception ex)
        {
            return BadRequest(new { message = "生成失败：" + ex.Message });
        }
    }
}
