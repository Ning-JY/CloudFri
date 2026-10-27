using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/projects")]
public class ProjectsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ProjectsController(AppDbContext db) => _db = db;

    public record ProjectReq(
        string Name, string Client, string? ContactName, string? ContactPhone,
        string BizType, string? Scale, string Status, int? OwnerMemberId,
        DateOnly? AcceptedDate, DateOnly? DueDate, DateOnly? ReportDate, string? ReportNo,
        decimal? SubmitAmount, decimal? ApprovedAmount, decimal Fee, string? Description);

    public record TeamItem(int MemberId, string Role, decimal Share, string? Duty = null);

    [HttpGet]
    public async Task<IActionResult> List(string? q = null, string? status = null, string? bizType = null, int? ownerId = null, int? year = null)
    {
        var query = _db.Projects.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || x.Client.Contains(q) || x.Code.Contains(q));
        if (!string.IsNullOrEmpty(status)) query = query.Where(x => x.Status == status);
        if (!string.IsNullOrEmpty(bizType)) query = query.Where(x => x.BizType == bizType);
        if (ownerId != null) query = query.Where(x => x.OwnerMemberId == ownerId);
        if (year != null) query = query.Where(x => x.AcceptedDate.HasValue && x.AcceptedDate.Value.Year == year.Value);

        var rows = await query.OrderByDescending(x => x.AcceptedDate).ThenByDescending(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Code, x.Name, x.Client, x.BizType, x.Status, x.OwnerMemberId,
                x.AcceptedDate, x.SubmitAmount, x.ApprovedAmount, x.Fee, x.CreatedBy, x.CreatedAt,
                Received = _db.Receipts.Where(r => r.ProjectId == x.Id && r.IsReceived).Sum(r => (decimal?)r.Amount) ?? 0,
                OwnerName = _db.Members.Where(m => m.Id == x.OwnerMemberId).Select(m => m.Name).FirstOrDefault()
            }).ToListAsync();
        return Ok(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var p = await _db.Projects.FindAsync(id);
        if (p == null) return NotFound();

        var team = await (from pm in _db.ProjectMembers
                          join m in _db.Members on pm.MemberId equals m.Id
                          where pm.ProjectId == id
                          orderby pm.Share descending
                          select new { pm.Id, pm.MemberId, m.Name, m.Gender, m.AvatarPath, pm.Role, pm.Duty, pm.Share }).ToListAsync();

        // 参与人员为空时默认展示项目负责人：角色「项目负责人」、职责「整体项目管理」（在「编辑分工」里保存后落库）
        object teamOut = team;
        if (team.Count == 0 && p.OwnerMemberId is int ownerId)
        {
            var om = await _db.Members.Where(m => m.Id == ownerId).Select(m => new { m.Name, m.Gender, m.AvatarPath }).FirstOrDefaultAsync();
            if (om != null)
                teamOut = new[] { new { Id = 0, MemberId = ownerId, om.Name, om.Gender, om.AvatarPath, Role = "项目负责人", Duty = (string?)"整体项目管理", Share = 100m } };
        }

        var contracts = await (from pc in _db.ProjectContracts
                               join c in _db.Contracts on pc.ContractId equals c.Id
                               where pc.ProjectId == id
                               orderby pc.IsPrimary descending
                               select new
                               {
                                   c.Id, c.No, c.Name, c.Type, c.PartyA, c.Amount, c.AmountNote,
                                   c.SignDate, c.Status, pc.IsPrimary,
                                   Files = _db.ContractFiles.Where(f => f.ContractId == c.Id)
                                       .OrderByDescending(f => f.UploadedAt)
                                       .Select(f => new { f.Id, f.FileName, f.SizeBytes, f.UploadedAt }).ToList()
                               }).ToListAsync();

        var receipts = await (from r in _db.Receipts
                              where r.ProjectId == id
                              orderby r.Date
                              join c in _db.Contracts on r.ContractId equals c.Id into gj
                              from c in gj.DefaultIfEmpty()
                              select new
                              {
                                  r.Id, r.Date, r.Amount, r.Method, r.Handler, r.Note, r.ContractId, r.InvoiceNo, r.IsReceived, r.ReceivedDate,
                                  ContractNo = c != null ? c.No : null,
                                  ContractName = c != null ? c.Name : null
                              }).ToListAsync();

        var files = await _db.ProjectFiles.Where(f => f.ProjectId == id)
            .OrderBy(f => f.Category).ThenByDescending(f => f.Version)
            .Select(f => new { f.Id, f.Category, f.FileName, f.SizeBytes, f.Version, f.SetId, f.UploadedBy, f.UploadedAt }).ToListAsync();

        var sets = await _db.DeliverableSets.Where(s => s.ProjectId == id)
            .OrderBy(s => s.Version)
            .Select(s => new { s.Id, s.Version, s.IsFinal, s.CreatedAt }).ToListAsync();

        var docs = await _db.ProjectDocs.Where(d => d.ProjectId == id)
            .OrderBy(d => d.Category).ThenByDescending(d => d.UploadedAt)
            .Select(d => new { d.Id, d.Category, d.FileName, d.SizeBytes, d.UploadedBy, d.UploadedAt }).ToListAsync();

        var ownerName = p.OwnerMemberId == null ? null
            : await _db.Members.Where(m => m.Id == p.OwnerMemberId).Select(m => m.Name).FirstOrDefaultAsync();

        var gcfxFileIds = files.Select(f => f.Id).ToList();
        var gcfxDocIds = docs.Select(x => x.Id).ToList();
        var parses = await _db.GcfxParses.Where(g =>
                (g.ProjectFileId != null && gcfxFileIds.Contains(g.ProjectFileId.Value)) ||
                (g.ProjectDocId != null && gcfxDocIds.Contains(g.ProjectDocId.Value)))
            .Select(g => new { g.Id, g.ProjectFileId, g.ProjectDocId, g.ProjectName, g.TotalAmount, g.AmountCn, g.Basis, g.Region, g.UnitsJson, g.ItemsJson, g.Note, g.ParsedAt })
            .ToListAsync();

        return Ok(new
        {
            Project = new
            {
                p.Id, p.Code, p.Name, p.Client, p.ContactName, p.ContactPhone, p.BizType, p.Scale,
                p.Status, p.OwnerMemberId, OwnerName = ownerName, p.AcceptedDate, p.DueDate, p.ReportDate, p.ReportNo,
                p.SubmitAmount, p.ApprovedAmount, p.Fee, p.Description, p.CreatedAt, p.UpdatedAt, p.CreatedBy
            },
            Team = teamOut,
            Contracts = contracts,
            Receipts = receipts,
            ReceivedTotal = receipts.Where(r => r.IsReceived).Sum(r => r.Amount),
            InvoicedTotal = receipts.Sum(r => r.Amount),
            Files = files,
            DeliverableSets = sets,
            Docs = docs,
            GcfxParses = parses
        });
    }

    [HttpPost]
    public async Task<IActionResult> Create(ProjectReq req)
    {
        var year = (req.AcceptedDate?.Year ?? DateTime.Now.Year);
        var prefix = $"ZJ-{year}-";
        var count = await _db.Projects.CountAsync(x => x.Code.StartsWith(prefix));
        var p = new Project
        {
            Code = prefix + (count + 1).ToString("D3"),
            Name = req.Name, Client = req.Client, ContactName = req.ContactName, ContactPhone = req.ContactPhone,
            BizType = req.BizType, Scale = req.Scale, Status = req.Status, OwnerMemberId = req.OwnerMemberId,
            AcceptedDate = req.AcceptedDate, DueDate = req.DueDate, ReportDate = req.ReportDate, ReportNo = req.ReportNo,
            SubmitAmount = req.SubmitAmount, ApprovedAmount = req.ApprovedAmount, Fee = req.Fee, Description = req.Description,
            CreatedBy = User.DisplayName()
        };
        _db.Projects.Add(p);
        await _db.SaveChangesAsync();
        // 参与人员默认添加项目负责人：角色「项目负责人」、职责「整体项目管理」
        if (p.OwnerMemberId is int ownerId)
        {
            _db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, MemberId = ownerId, Role = "项目负责人", Duty = "整体项目管理", Share = 100 });
            await _db.SaveChangesAsync();
        }
        return Ok(new { p.Id, p.Code });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ProjectReq req)
    {
        var p = await _db.Projects.FindAsync(id);
        if (p == null) return NotFound();
        p.Name = req.Name; p.Client = req.Client; p.ContactName = req.ContactName; p.ContactPhone = req.ContactPhone;
        p.BizType = req.BizType; p.Scale = req.Scale; p.Status = req.Status; p.OwnerMemberId = req.OwnerMemberId;
        p.AcceptedDate = req.AcceptedDate; p.DueDate = req.DueDate; p.ReportDate = req.ReportDate; p.ReportNo = req.ReportNo;
        p.SubmitAmount = req.SubmitAmount; p.ApprovedAmount = req.ApprovedAmount; p.Fee = req.Fee;
        p.Description = req.Description; p.UpdatedAt = DateTime.Now;
        await _db.SaveChangesAsync();
        return Ok(new { p.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Projects.FindAsync(id);
        if (p == null) return NotFound();
        var files = await _db.ProjectFiles.Where(x => x.ProjectId == id).ToListAsync();
        var docs = await _db.ProjectDocs.Where(x => x.ProjectId == id).ToListAsync();
        var fileIds = files.Select(f => f.Id).ToList();
        var docIds = docs.Select(x => x.Id).ToList();
        _db.GcfxParses.RemoveRange(_db.GcfxParses.Where(g =>
            (g.ProjectFileId != null && fileIds.Contains(g.ProjectFileId.Value)) ||
            (g.ProjectDocId != null && docIds.Contains(g.ProjectDocId.Value))));
        _db.ProjectMembers.RemoveRange(_db.ProjectMembers.Where(x => x.ProjectId == id));
        _db.ProjectContracts.RemoveRange(_db.ProjectContracts.Where(x => x.ProjectId == id));
        _db.Receipts.RemoveRange(_db.Receipts.Where(x => x.ProjectId == id));
        _db.ProjectFiles.RemoveRange(files);
        _db.DeliverableSets.RemoveRange(_db.DeliverableSets.Where(x => x.ProjectId == id));
        _db.ProjectDocs.RemoveRange(docs);
        _db.Projects.Remove(p);
        await _db.SaveChangesAsync();
        foreach (var f in files) TryDeletePhysical(f.StoredPath);
        foreach (var d in docs) TryDeletePhysical(d.StoredPath);
        return Ok(new { p.Id });
    }

    private static void TryDeletePhysical(string path)
    {
        try { if (System.IO.File.Exists(path)) System.IO.File.Delete(path); } catch { }
    }

    /// <summary>整体替换项目团队（角色 + 职责 + 占比）。</summary>
    [HttpPut("{id:int}/team")]
    public async Task<IActionResult> SetTeam(int id, List<TeamItem> items)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == id)) return NotFound();
        _db.ProjectMembers.RemoveRange(_db.ProjectMembers.Where(x => x.ProjectId == id));
        foreach (var it in items)
            _db.ProjectMembers.Add(new ProjectMember { ProjectId = id, MemberId = it.MemberId, Role = it.Role, Duty = string.IsNullOrWhiteSpace(it.Duty) ? null : it.Duty.Trim(), Share = it.Share });
        await _db.SaveChangesAsync();
        return Ok(new { count = items.Count });
    }
}

[Authorize]
[ApiController]
[Route("api/projects/{projectId:int}/receipts")]
public class ReceiptsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ReceiptsController(AppDbContext db) => _db = db;

    public record ReceiptReq(DateOnly Date, decimal Amount, string? Method, string? Handler, string? Note, int? ContractId = null, string? InvoiceNo = null);

    /// <summary>新增开票：登记一张发票（Date=开票日期），此时 IsReceived=false，钱还没到账。</summary>
    [HttpPost]
    public async Task<IActionResult> Create(int projectId, ReceiptReq req)
    {
        if (!await _db.Projects.AnyAsync(x => x.Id == projectId)) return NotFound();
        if (req.ContractId is int cid && !await _db.Contracts.AnyAsync(x => x.Id == cid))
            return BadRequest(new { message = "所选合同不存在" });
        var r = new Receipt { ProjectId = projectId, ContractId = req.ContractId, Date = req.Date, Amount = req.Amount, Method = req.Method, Handler = req.Handler, Note = req.Note, InvoiceNo = req.InvoiceNo, IsReceived = false };
        _db.Receipts.Add(r);
        await _db.SaveChangesAsync();
        return Ok(new { r.Id });
    }

    public record ConfirmReq(DateOnly? ReceivedDate);

    /// <summary>确认收款：把一张已开票记录置为已到账（仿 EnterpriseMS 的 Received 动作）。</summary>
    [HttpPost("{id:int}/confirm")]
    public async Task<IActionResult> Confirm(int projectId, int id, ConfirmReq req)
    {
        var r = await _db.Receipts.FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId);
        if (r == null) return NotFound();
        r.IsReceived = true;
        r.ReceivedDate = req.ReceivedDate ?? DateOnly.FromDateTime(DateTime.Now);
        await _db.SaveChangesAsync();
        return Ok(new { r.Id, r.IsReceived, r.ReceivedDate });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int projectId, int id)
    {
        var r = await _db.Receipts.FirstOrDefaultAsync(x => x.Id == id && x.ProjectId == projectId);
        if (r == null) return NotFound();
        _db.Receipts.Remove(r);
        await _db.SaveChangesAsync();
        return Ok(new { r.Id });
    }
}
