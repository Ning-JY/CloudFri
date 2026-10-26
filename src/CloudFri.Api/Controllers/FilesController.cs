using System.Text.Json;
using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using CloudFri.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects/{projectId:int}/files")]
public class FilesController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string _root;

    public FilesController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _root = Path.GetFullPath(cfg["Storage:Root"] ?? "storage");
    }

    private string SaveUpload(int projectId, string kind, IFormFile file)
    {
        var dir = Path.Combine(_root, "projects", projectId.ToString(), kind);
        Directory.CreateDirectory(dir);
        var ext = Path.GetExtension(file.FileName);
        var stored = Path.Combine(dir, Guid.NewGuid().ToString("N") + ext);
        using (var fs = System.IO.File.Create(stored)) file.CopyTo(fs);
        return stored;
    }

    private string CurrentUserName() =>
        User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? "";

    private static string ParseNote(int itemCount) =>
        $"工程信息为字符串级提取（工程树/大写金额/计价依据）；清单明细已逐行提取（编码/名称/单位/工程量，共 {itemCount} 条）；综合单价与合价的二进制字段尚未可靠提取，暂不显示。";

    /// <summary>取项目当前成果版本集合；没有则建 V1，并把旧文件（SetId 为空，整体版本功能上线前上传的）归入最早的版本集合。</summary>
    private async Task<DeliverableSet> EnsureCurrentSetAsync(int projectId)
    {
        var sets = await _db.DeliverableSets.Where(s => s.ProjectId == projectId).OrderBy(s => s.Version).ToListAsync();
        if (sets.Count == 0)
        {
            var s1 = new DeliverableSet { ProjectId = projectId, Version = 1, CreatedBy = CurrentUserName() };
            _db.DeliverableSets.Add(s1);
            await _db.SaveChangesAsync();
            sets.Add(s1);
        }
        var legacy = await _db.ProjectFiles.Where(f => f.ProjectId == projectId && f.SetId == null).ToListAsync();
        if (legacy.Count > 0)
        {
            foreach (var f in legacy) f.SetId = sets[0].Id;
            await _db.SaveChangesAsync();
        }
        // 当前版本：被「设为最终版」标记的集合优先，否则取版本号最大者
        return sets.FirstOrDefault(s => s.IsFinal) ?? sets[^1];
    }

    /// <summary>把某个历史版本设为最终版：该版本重新成为当前版本（承接后续上传），其余版本转历史。</summary>
    [HttpPost("sets/{setId:int}/set-final")]
    public async Task<IActionResult> SetFinal(int projectId, int setId)
    {
        var sets = await _db.DeliverableSets.Where(s => s.ProjectId == projectId).ToListAsync();
        var target = sets.FirstOrDefault(s => s.Id == setId);
        if (target == null) return NotFound();
        foreach (var s in sets) s.IsFinal = s.Id == setId;
        await _db.SaveChangesAsync();
        return Ok(new { target.Id, target.Version });
    }

    /// <summary>新增成果版本：当前版本（软件版/文档版/报告）整体转入历史，开启新版本承接后续上传。</summary>
    [HttpPost("new-version")]
    public async Task<IActionResult> NewVersion(int projectId)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId)) return NotFound();
        var cur = await EnsureCurrentSetAsync(projectId);
        var hasFiles = await _db.ProjectFiles.AnyAsync(f => f.ProjectId == projectId && f.SetId == cur.Id);
        if (!hasFiles) return Ok(new { cur.Version, Created = false });
        // 新版本成为当前版本：清除既有「最终版」标记
        var marked = await _db.DeliverableSets.Where(s => s.ProjectId == projectId && s.IsFinal).ToListAsync();
        foreach (var s in marked) s.IsFinal = false;
        var maxVersion = await _db.DeliverableSets.Where(s => s.ProjectId == projectId).MaxAsync(s => s.Version);
        var next = new DeliverableSet { ProjectId = projectId, Version = maxVersion + 1, CreatedBy = CurrentUserName() };
        _db.DeliverableSets.Add(next);
        await _db.SaveChangesAsync();
        return Ok(new { next.Version, Created = true });
    }

    /// <summary>上传成果文件（软件版/文档版/报告）：同分类版本号自动 +1，旧版转历史。GCFX 自动触发字符串级解析。</summary>
    [HttpPost("deliverables")]
    [RequestSizeLimit(300_000_000)]
    public async Task<IActionResult> UploadDeliverable(int projectId, [FromForm] string category, IFormFile file)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId)) return NotFound();
        if (file == null || file.Length == 0) return BadRequest(new { message = "文件为空" });
        if (category is not ("Software" or "Document" or "Report"))
            return BadRequest(new { message = "成果分类不正确" });

        var set = await EnsureCurrentSetAsync(projectId);
        var stored = SaveUpload(projectId, "deliverables", file);
        var maxVer = await _db.ProjectFiles
            .Where(f => f.ProjectId == projectId && f.Category == category && f.SetId == set.Id)
            .MaxAsync(f => (int?)f.Version) ?? 0;
        var pf = new ProjectFile
        {
            ProjectId = projectId, Category = category, FileName = file.FileName,
            StoredPath = stored, SizeBytes = file.Length, Version = maxVer + 1, SetId = set.Id,
            UploadedBy = CurrentUserName()
        };
        _db.ProjectFiles.Add(pf);
        await _db.SaveChangesAsync();

        GcfxParse? parse = null;
        if (Path.GetExtension(file.FileName).Equals(".gcfx", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(file.FileName).Equals(".sgcfx", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var r = GcfxService.Parse(stored);
                parse = new GcfxParse
                {
                    ProjectFileId = pf.Id, ProjectName = r.ProjectName, TotalAmount = r.TotalAmount,
                    AmountCn = r.AmountCn,
                    Basis = r.Basis, Region = r.Region,
                    UnitsJson = JsonSerializer.Serialize(r.Units),
                    ItemsJson = JsonSerializer.Serialize(r.Items),
                    Note = ParseNote(r.Items.Count)
                };
                _db.GcfxParses.Add(parse);
                await _db.SaveChangesAsync();
            }
            catch { /* 解析失败不阻塞上传 */ }
        }

        // 金额回填（规则见 ApplyAmountFillAsync）：仅软件版解析成功时尝试
        string? filledField = null;
        decimal? filledAmountWan = null;
        if (parse?.TotalAmount is > 0 && category == "Software")
            (filledField, filledAmountWan) = await ApplyAmountFillAsync(projectId, parse.TotalAmount.Value);
        return Ok(new { pf.Id, pf.Version, ParseId = parse?.Id, FilledField = filledField, FilledAmountWan = filledAmountWan });
    }

    /// <summary>
    /// 金额回填：GCFX 文件内只有一个文件总造价（无送审/审定区分字段），按业务类型回填到对应栏位，
    /// 仅在栏位为空时回填，绝不覆盖用户手填值。审核类成果文件即审定成果 → 审定金额；其余 → 送审栏（预算/控制价/建安）。
    /// </summary>
    private async Task<(string? Field, decimal? Wan)> ApplyAmountFillAsync(int projectId, decimal totalYuan)
    {
        try
        {
            var proj = await _db.Projects.FindAsync(projectId);
            if (proj == null) return (null, null);
            var wan = Math.Round(totalYuan / 10000m, 2);
            string? field = null;
            if (proj.BizType is "结算审核" or "竣工决算")
            {
                if (proj.ApprovedAmount == null) { proj.ApprovedAmount = wan; field = "审定金额"; }
            }
            else if (proj.SubmitAmount == null)
            {
                proj.SubmitAmount = wan;
                field = proj.BizType switch
                {
                    "预算编制" => "预算金额",
                    "清单控制价" => "控制价金额",
                    "跟踪审计" or "全过程咨询" => "建安造价",
                    _ => "送审金额"
                };
            }
            if (field == null) return (null, null);
            proj.UpdatedAt = DateTime.Now;
            await _db.SaveChangesAsync();
            return (field, wan);
        }
        catch { return (null, null); /* 回填失败不阻塞主流程 */ }
    }

    /// <summary>重新解析已上传的 GCFX/SGCFX（解析器升级后刷新旧解析，或用户重传后手动刷新）。</summary>
    [HttpPost("deliverables/{fileId:int}/reparse")]
    public async Task<IActionResult> Reparse(int projectId, int fileId)
    {
        var f = await _db.ProjectFiles.FirstOrDefaultAsync(x => x.Id == fileId && x.ProjectId == projectId);
        if (f == null || !System.IO.File.Exists(f.StoredPath)) return NotFound();
        var ext = Path.GetExtension(f.FileName);
        if (!ext.Equals(".gcfx", StringComparison.OrdinalIgnoreCase) && !ext.Equals(".sgcfx", StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { message = "只有 GCFX / SGCFX 文件可以解析" });
        GcfxService.GcfxResult r;
        try { r = GcfxService.Parse(f.StoredPath); }
        catch { return BadRequest(new { message = "解析失败：文件无法读取" }); }
        var parse = await _db.GcfxParses.FirstOrDefaultAsync(g => g.ProjectFileId == f.Id);
        if (parse == null) { parse = new GcfxParse { ProjectFileId = f.Id }; _db.GcfxParses.Add(parse); }
        parse.ProjectName = r.ProjectName;
        parse.TotalAmount = r.TotalAmount;
        parse.AmountCn = r.AmountCn;
        parse.Basis = r.Basis;
        parse.Region = r.Region;
        parse.UnitsJson = JsonSerializer.Serialize(r.Units);
        parse.ItemsJson = JsonSerializer.Serialize(r.Items);
        parse.Note = ParseNote(r.Items.Count);
        parse.ParsedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        string? filledField = null;
        decimal? filledAmountWan = null;
        if (r.TotalAmount is > 0 && f.Category == "Software")
            (filledField, filledAmountWan) = await ApplyAmountFillAsync(projectId, r.TotalAmount.Value);
        return Ok(new { parse.Id, parse.TotalAmount, FilledField = filledField, FilledAmountWan = filledAmountWan });
    }

    /// <summary>上传项目过程资料（按分类归档，只增不改版）。</summary>
    [HttpPost("docs")]
    [RequestSizeLimit(300_000_000)]
    public async Task<IActionResult> UploadDoc(int projectId, [FromForm] string category, IFormFile file)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId)) return NotFound();
        if (file == null || file.Length == 0) return BadRequest(new { message = "文件为空" });
        var stored = SaveUpload(projectId, "docs", file);
        var doc = new ProjectDoc
        {
            ProjectId = projectId, Category = category, FileName = file.FileName,
            StoredPath = stored, SizeBytes = file.Length, UploadedBy = CurrentUserName()
        };
        _db.ProjectDocs.Add(doc);
        await _db.SaveChangesAsync();
        return Ok(new { doc.Id });
    }

    [HttpGet("deliverables/{fileId:int}/download")]
    public async Task<IActionResult> DownloadDeliverable(int projectId, int fileId)
    {
        var f = await _db.ProjectFiles.FirstOrDefaultAsync(x => x.Id == fileId && x.ProjectId == projectId);
        if (f == null || !System.IO.File.Exists(f.StoredPath)) return NotFound();
        return PhysicalFile(f.StoredPath, "application/octet-stream", f.FileName);
    }

    [HttpGet("docs/{docId:int}/download")]
    public async Task<IActionResult> DownloadDoc(int projectId, int docId)
    {
        var d = await _db.ProjectDocs.FirstOrDefaultAsync(x => x.Id == docId && x.ProjectId == projectId);
        if (d == null || !System.IO.File.Exists(d.StoredPath)) return NotFound();
        return PhysicalFile(d.StoredPath, "application/octet-stream", d.FileName);
    }

    [HttpDelete("deliverables/{fileId:int}")]
    public async Task<IActionResult> DeleteDeliverable(int projectId, int fileId)
    {
        var f = await _db.ProjectFiles.FirstOrDefaultAsync(x => x.Id == fileId && x.ProjectId == projectId);
        if (f == null) return NotFound();
        _db.GcfxParses.RemoveRange(_db.GcfxParses.Where(g => g.ProjectFileId == f.Id));
        _db.ProjectFiles.Remove(f);
        await _db.SaveChangesAsync();
        TryDeleteFile(f.StoredPath);
        return Ok(new { f.Id });
    }

    [HttpDelete("docs/{docId:int}")]
    public async Task<IActionResult> DeleteDoc(int projectId, int docId)
    {
        var d = await _db.ProjectDocs.FirstOrDefaultAsync(x => x.Id == docId && x.ProjectId == projectId);
        if (d == null) return NotFound();
        _db.ProjectDocs.Remove(d);
        await _db.SaveChangesAsync();
        TryDeleteFile(d.StoredPath);
        return Ok(new { d.Id });
    }

    private static void TryDeleteFile(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
    }
}

[Authorize]
[ApiController]
[Route("api/library")]
public class LibraryController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly string _root;

    public LibraryController(AppDbContext db, IConfiguration cfg)
    {
        _db = db;
        _root = Path.GetFullPath(cfg["Storage:Root"] ?? "storage");
    }

    [HttpGet]
    public async Task<IActionResult> List(string? category = null, string? q = null)
    {
        var query = _db.LibraryDocs.AsQueryable();
        if (!string.IsNullOrEmpty(category)) query = query.Where(x => x.Category == category);
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q));
        var rows = await query.OrderByDescending(x => x.UploadedAt)
            .Select(x => new { x.Id, x.Name, x.Category, x.Format, x.SizeBytes, x.UploadedBy, x.UploadedAt })
            .ToListAsync();
        return Ok(rows);
    }

    [HttpPost]
    [RequestSizeLimit(500_000_000)]
    public async Task<IActionResult> Upload([FromForm] string category, [FromForm] string? name, IFormFile file)
    {
        if (file == null || file.Length == 0) return BadRequest(new { message = "文件为空" });
        var dir = Path.Combine(_root, "library");
        Directory.CreateDirectory(dir);
        var stored = Path.Combine(dir, Guid.NewGuid().ToString("N") + Path.GetExtension(file.FileName));
        using (var fs = System.IO.File.Create(stored)) await file.CopyToAsync(fs);
        var doc = new LibraryDoc
        {
            Name = string.IsNullOrWhiteSpace(name) ? Path.GetFileNameWithoutExtension(file.FileName) : name,
            Category = category,
            Format = Path.GetExtension(file.FileName).TrimStart('.').ToUpperInvariant(),
            StoredPath = stored, SizeBytes = file.Length,
            UploadedBy = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value ?? ""
        };
        _db.LibraryDocs.Add(doc);
        await _db.SaveChangesAsync();
        return Ok(new { doc.Id });
    }

    [HttpGet("{id:int}/download")]
    public async Task<IActionResult> Download(int id)
    {
        var d = await _db.LibraryDocs.FindAsync(id);
        if (d == null || !System.IO.File.Exists(d.StoredPath)) return NotFound();
        var ext = Path.GetExtension(d.StoredPath);
        return PhysicalFile(d.StoredPath, ContentTypeFor(ext), d.Name + ext, enableRangeProcessing: true);
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var d = await _db.LibraryDocs.FindAsync(id);
        if (d == null) return NotFound();
        _db.LibraryDocs.Remove(d);
        await _db.SaveChangesAsync();
        try { if (System.IO.File.Exists(d.StoredPath)) System.IO.File.Delete(d.StoredPath); } catch { }
        return Ok(new { d.Id });
    }

    private static string ContentTypeFor(string ext) => ext.ToLowerInvariant() switch
    {
        ".pdf" => "application/pdf",
        ".png" => "image/png",
        ".jpg" or ".jpeg" => "image/jpeg",
        ".gif" => "image/gif",
        ".txt" => "text/plain",
        _ => "application/octet-stream"
    };
}
