using System.Text.Json;
using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/forms")]
public class FormsController : ControllerBase
{
    private readonly AppDbContext _db;
    public FormsController(AppDbContext db) => _db = db;

    public record RecordReq(int TemplateId, Dictionary<string, JsonElement> Data);
    public record RecordUpdateReq(Dictionary<string, JsonElement> Data);

    [HttpGet("templates")]
    public async Task<IActionResult> Templates()
    {
        var rows = await _db.FormTemplates.Where(x => x.IsActive).OrderBy(x => x.Sort)
            .Select(x => new { x.Id, x.Key, x.Name, x.Code, x.Desc, x.SchemaJson })
            .ToListAsync();
        return Ok(rows);
    }

    [HttpGet("records")]
    public async Task<IActionResult> Records(int? templateId = null, string? q = null)
    {
        var query = _db.FormRecords.AsQueryable();
        if (templateId != null) query = query.Where(x => x.TemplateId == templateId);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.No.Contains(q) || (x.FilledByName != null && x.FilledByName.Contains(q)));
        var rows = await query.OrderByDescending(x => x.Id)
            .Select(r => new
            {
                r.Id, r.No, r.TemplateId,
                TemplateName = _db.FormTemplates.Where(t => t.Id == r.TemplateId).Select(t => t.Name).FirstOrDefault(),
                TemplateKey = _db.FormTemplates.Where(t => t.Id == r.TemplateId).Select(t => t.Key).FirstOrDefault(),
                r.DataJson, r.FilledByName, r.CreatedAt, r.UpdatedAt
            }).ToListAsync();
        return Ok(rows);
    }

    [HttpPost("records")]
    public async Task<IActionResult> Create(RecordReq req)
    {
        var tpl = await _db.FormTemplates.FindAsync(req.TemplateId);
        if (tpl == null) return BadRequest(new { message = "模板不存在" });
        var year = DateTime.Now.Year;
        var prefix = $"{tpl.Code}-{year}-";
        var count = await _db.FormRecords.CountAsync(x => x.No.StartsWith(prefix));
        var byKey = ReadByKey(tpl.SchemaJson);
        string? filledBy = null;
        if (byKey != null && req.Data.TryGetValue(byKey, out var v) && v.ValueKind == JsonValueKind.String)
            filledBy = v.GetString();
        var rec = new FormRecord
        {
            TemplateId = tpl.Id,
            No = prefix + (count + 1).ToString("D3"),
            DataJson = JsonSerializer.Serialize(req.Data),
            FilledByName = filledBy,
            CreatedByUserId = int.TryParse(User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out var uid) ? uid : null
        };
        _db.FormRecords.Add(rec);
        await _db.SaveChangesAsync();
        return Ok(new { rec.Id, rec.No });
    }

    [HttpPut("records/{id:int}")]
    public async Task<IActionResult> Update(int id, RecordUpdateReq req)
    {
        var rec = await _db.FormRecords.FindAsync(id);
        if (rec == null) return NotFound();
        rec.DataJson = JsonSerializer.Serialize(req.Data);
        rec.UpdatedAt = DateTime.Now;
        var tpl = await _db.FormTemplates.FindAsync(rec.TemplateId);
        if (tpl != null)
        {
            var byKey = ReadByKey(tpl.SchemaJson);
            if (byKey != null && req.Data.TryGetValue(byKey, out var v) && v.ValueKind == JsonValueKind.String)
                rec.FilledByName = v.GetString();
        }
        await _db.SaveChangesAsync();
        return Ok(new { rec.Id });
    }

    [HttpDelete("records/{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var rec = await _db.FormRecords.FindAsync(id);
        if (rec == null) return NotFound();
        _db.FormRecords.Remove(rec);
        await _db.SaveChangesAsync();
        return Ok(new { rec.Id });
    }

    private static string? ReadByKey(string schemaJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(schemaJson);
            return doc.RootElement.TryGetProperty("byKey", out var bk) ? bk.GetString() : null;
        }
        catch { return null; }
    }
}

[Authorize]
[ApiController]
[Route("api/stats")]
public class StatsController : ControllerBase
{
    private readonly AppDbContext _db;
    public StatsController(AppDbContext db) => _db = db;

    /// <summary>工作台与统计页聚合数据。</summary>
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var projects = await _db.Projects
            .Select(p => new { p.Id, p.Code, p.Name, p.Client, p.BizType, p.Status, p.Fee, p.SubmitAmount, p.ApprovedAmount, p.AcceptedDate, p.OwnerMemberId })
            .ToListAsync();
        var receipts = await _db.Receipts.Select(r => new { r.ProjectId, r.Amount, r.Date }).ToListAsync();
        var members = await _db.Members.Where(m => m.IsActive).Select(m => new { m.Id, m.Name }).ToListAsync();

        var receivedByProject = receipts.GroupBy(r => r.ProjectId).ToDictionary(g => g.Key, g => g.Sum(x => x.Amount));
        var feeTotal = projects.Sum(p => p.Fee);
        var receivedTotal = receipts.Sum(r => r.Amount);

        var byStatus = projects.GroupBy(p => p.Status).Select(g => new { Status = g.Key, Count = g.Count() }).ToList();
        var byType = projects.GroupBy(p => p.BizType).Select(g => new { Type = g.Key, Count = g.Count(), Fee = g.Sum(x => x.Fee) }).ToList();

        var now = DateTime.Now;
        var monthly = Enumerable.Range(0, 6).Select(i =>
        {
            var d = new DateTime(now.Year, now.Month, 1).AddMonths(-(5 - i));
            return new
            {
                Month = d.ToString("yyyy-MM"),
                NewProjects = projects.Count(p => p.AcceptedDate != null && p.AcceptedDate.Value.Year == d.Year && p.AcceptedDate.Value.Month == d.Month),
                Received = receipts.Where(r => r.Date.Year == d.Year && r.Date.Month == d.Month).Sum(r => r.Amount)
            };
        }).ToList();

        var byMember = members.Select(m => new
        {
            m.Id, m.Name,
            ProjectCount = projects.Count(p => p.OwnerMemberId == m.Id),
            Fee = projects.Where(p => p.OwnerMemberId == m.Id).Sum(p => p.Fee)
        }).OrderByDescending(x => x.ProjectCount).ToList();

        var recent = projects.OrderByDescending(p => p.AcceptedDate).Take(8)
            .Select(p => new { p.Id, p.Code, p.Name, p.BizType, p.Status, p.AcceptedDate }).ToList();

        var pending = projects.Where(p => p.Status is "编制中" or "审核中")
            .OrderBy(p => p.AcceptedDate).Take(8)
            .Select(p => new { p.Id, p.Code, p.Name, p.Status, p.AcceptedDate }).ToList();

        return Ok(new
        {
            Kpi = new
            {
                ProjectTotal = projects.Count,
                InProgress = projects.Count(p => p.Status is "编制中" or "审核中"),
                Reported = projects.Count(p => p.Status == "已出报告"),
                FeeTotal = feeTotal,
                ReceivedTotal = receivedTotal,
                Unreceived = feeTotal - receivedTotal
            },
            ByStatus = byStatus,
            ByType = byType,
            Monthly = monthly,
            ByMember = byMember,
            Recent = recent,
            Pending = pending,
            ReceivedByProject = receivedByProject
        });
    }
}
