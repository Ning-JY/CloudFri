using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/contracts")]
public class ContractsController : ControllerBase
{
    private readonly AppDbContext _db;
    public ContractsController(AppDbContext db) => _db = db;

    public record ContractReq(string No, string Name, string Type, string? PartyA,
        decimal? Amount, string? AmountNote, DateOnly? SignDate, string? Status);
    public record LinkReq(int ProjectId, bool IsPrimary);

    [HttpGet]
    public async Task<IActionResult> List(string? q = null, string? type = null)
    {
        var query = _db.Contracts.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(x => x.Name.Contains(q) || x.No.Contains(q) || (x.PartyA != null && x.PartyA.Contains(q)));
        if (!string.IsNullOrEmpty(type)) query = query.Where(x => x.Type == type);

        var rows = await query.OrderByDescending(x => x.SignDate).ThenByDescending(x => x.Id)
            .Select(c => new
            {
                c.Id, c.No, c.Name, c.Type, c.PartyA, c.Amount, c.AmountNote, c.SignDate, c.Status,
                ProjectCount = _db.ProjectContracts.Count(pc => pc.ContractId == c.Id),
                Projects = _db.ProjectContracts.Where(pc => pc.ContractId == c.Id)
                    .Join(_db.Projects, pc => pc.ProjectId, p => p.Id, (pc, p) => p.Name).ToList()
            }).ToListAsync();
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create(ContractReq req)
    {
        if (await _db.Contracts.AnyAsync(x => x.No == req.No))
            return BadRequest(new { message = "合同编号已存在" });
        var c = new Contract
        {
            No = req.No, Name = req.Name, Type = req.Type, PartyA = req.PartyA,
            Amount = req.Amount, AmountNote = req.AmountNote, SignDate = req.SignDate, Status = req.Status
        };
        _db.Contracts.Add(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ContractReq req)
    {
        var c = await _db.Contracts.FindAsync(id);
        if (c == null) return NotFound();
        c.No = req.No; c.Name = req.Name; c.Type = req.Type; c.PartyA = req.PartyA;
        c.Amount = req.Amount; c.AmountNote = req.AmountNote; c.SignDate = req.SignDate; c.Status = req.Status;
        await _db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var c = await _db.Contracts.FindAsync(id);
        if (c == null) return NotFound();
        _db.ProjectContracts.RemoveRange(_db.ProjectContracts.Where(x => x.ContractId == id));
        _db.Contracts.Remove(c);
        await _db.SaveChangesAsync();
        return Ok(new { c.Id });
    }

    [HttpPost("{id:int}/link")]
    public async Task<IActionResult> Link(int id, LinkReq req)
    {
        if (!await _db.Contracts.AnyAsync(x => x.Id == id)) return NotFound();
        if (!await _db.Projects.AnyAsync(x => x.Id == req.ProjectId)) return NotFound();
        if (await _db.ProjectContracts.AnyAsync(x => x.ContractId == id && x.ProjectId == req.ProjectId))
            return BadRequest(new { message = "已关联该项目" });
        _db.ProjectContracts.Add(new ProjectContract { ContractId = id, ProjectId = req.ProjectId, IsPrimary = req.IsPrimary });
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }

    [HttpDelete("{id:int}/link/{projectId:int}")]
    public async Task<IActionResult> Unlink(int id, int projectId)
    {
        var pc = await _db.ProjectContracts.FirstOrDefaultAsync(x => x.ContractId == id && x.ProjectId == projectId);
        if (pc == null) return NotFound();
        _db.ProjectContracts.Remove(pc);
        await _db.SaveChangesAsync();
        return Ok(new { ok = true });
    }
}

[Authorize]
[ApiController]
[Route("api/fees")]
public class FeesController : ControllerBase
{
    private readonly AppDbContext _db;
    public FeesController(AppDbContext db) => _db = db;

    /// <summary>收费台账：每项目的应收/已收/待收与最近收款。</summary>
    [HttpGet]
    public async Task<IActionResult> Overview()
    {
        var rows = await _db.Projects.OrderByDescending(x => x.AcceptedDate)
            .Select(p => new
            {
                p.Id, p.Code, p.Name, p.Client, p.Status, p.Fee,
                Received = _db.Receipts.Where(r => r.ProjectId == p.Id).Sum(r => (decimal?)r.Amount) ?? 0,
                LastReceiptDate = _db.Receipts.Where(r => r.ProjectId == p.Id).Max(r => (DateOnly?)r.Date),
                ReceiptCount = _db.Receipts.Count(r => r.ProjectId == p.Id)
            }).ToListAsync();
        var totals = new
        {
            FeeTotal = rows.Sum(x => x.Fee),
            ReceivedTotal = rows.Sum(x => x.Received),
            ProjectCount = rows.Count
        };
        return Ok(new { rows, totals });
    }
}
