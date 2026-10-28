using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CloudFri.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/members")]
public class MembersController : ControllerBase
{
    private readonly AppDbContext _db;
    public MembersController(AppDbContext db) => _db = db;

    public record MemberReq(string Name, string Gender, string? Phone, int Sort, string? Department = null, string? Remark = null, string? AvatarPath = null);

    [HttpGet]
    public async Task<IActionResult> List(bool includeInactive = false)
    {
        var q = _db.Members.AsQueryable();
        if (!includeInactive) q = q.Where(x => x.IsActive);
        var rows = await q.OrderBy(x => x.Sort).ThenBy(x => x.Id)
            .Select(x => new { x.Id, x.Name, x.Gender, x.Phone, x.Department, x.Remark, x.AvatarPath, x.IsActive, x.Sort })
            .ToListAsync();
        return Ok(rows);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(MemberReq req)
    {
        var m = new Member { Name = req.Name, Gender = req.Gender, Phone = req.Phone, Sort = req.Sort, Department = req.Department, Remark = req.Remark, AvatarPath = string.IsNullOrWhiteSpace(req.AvatarPath) ? null : req.AvatarPath };
        _db.Members.Add(m);
        await _db.SaveChangesAsync();
        return Ok(new { m.Id });
    }

    [Authorize(Roles = "Admin")]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MemberReq req)
    {
        var m = await _db.Members.FindAsync(id);
        if (m == null) return NotFound();
        m.Name = req.Name; m.Gender = req.Gender; m.Phone = req.Phone; m.Sort = req.Sort;
        m.Department = req.Department; m.Remark = req.Remark;
        m.AvatarPath = string.IsNullOrWhiteSpace(req.AvatarPath) ? null : req.AvatarPath;
        await _db.SaveChangesAsync();
        return Ok(new { m.Id });
    }

    /// <summary>停用/启用切换（停用后可再启用，成员名单一直可见）。</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var m = await _db.Members.FindAsync(id);
        if (m == null) return NotFound();
        m.IsActive = !m.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { m.Id, m.IsActive });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var m = await _db.Members.FindAsync(id);
        if (m == null) return NotFound();
        m.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(new { m.Id });
    }
}

[Authorize]
[ApiController]
[Route("api/dicts")]
public class DictsController : ControllerBase
{
    private readonly AppDbContext _db;
    public DictsController(AppDbContext db) => _db = db;

    public record DictReq(string Group, string Name, int Sort);

    [HttpGet]
    public async Task<IActionResult> List(string? group = null)
    {
        var q = _db.DictItems.Where(x => x.IsActive);
        if (!string.IsNullOrEmpty(group)) q = q.Where(x => x.Group == group);
        var rows = await q.OrderBy(x => x.Group).ThenBy(x => x.Sort)
            .Select(x => new { x.Id, x.Group, x.Name, x.Sort }).ToListAsync();
        return Ok(rows);
    }

    [Authorize(Roles = "Admin")]
    [HttpPost]
    public async Task<IActionResult> Create(DictReq req)
    {
        var d = new DictItem { Group = req.Group, Name = req.Name, Sort = req.Sort };
        _db.DictItems.Add(d);
        await _db.SaveChangesAsync();
        return Ok(new { d.Id });
    }

    public record ReorderReq(string Group, int[] Ids);

    /// <summary>拖动排序：按传入的 ID 顺序重写该组的 Sort（1..n）。</summary>
    [Authorize(Roles = "Admin")]
    [HttpPost("reorder")]
    public async Task<IActionResult> Reorder(ReorderReq req)
    {
        var items = await _db.DictItems.Where(x => x.Group == req.Group && req.Ids.Contains(x.Id)).ToListAsync();
        for (int i = 0; i < req.Ids.Length; i++)
        {
            var d = items.FirstOrDefault(x => x.Id == req.Ids[i]);
            if (d != null) d.Sort = i + 1;
        }
        await _db.SaveChangesAsync();
        return Ok(new { count = items.Count });
    }

    [Authorize(Roles = "Admin")]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        var d = await _db.DictItems.FindAsync(id);
        if (d == null) return NotFound();
        d.IsActive = false;
        await _db.SaveChangesAsync();
        return Ok(new { d.Id });
    }
}

[Authorize(Roles = "Admin")]
[ApiController]
[Route("api/users")]
public class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly Microsoft.AspNetCore.Identity.PasswordHasher<User> _hasher = new();
    public UsersController(AppDbContext db) => _db = db;

    public record UserReq(string UserName, string Password, string DisplayName, string Role, int? MemberId);
    public record UserEditReq(string DisplayName, string Role, int? MemberId);
    public record ResetReq(string Password);

    [HttpGet]
    public async Task<IActionResult> List()
    {
        var rows = await _db.Users.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.UserName, x.DisplayName, x.Role, x.MemberId, x.IsActive, x.CreatedAt, x.LastLoginAt, x.LastLoginIp })
            .ToListAsync();
        return Ok(rows);
    }

    [HttpPost]
    public async Task<IActionResult> Create(UserReq req)
    {
        if (await _db.Users.AnyAsync(x => x.UserName == req.UserName))
            return BadRequest(new { message = "用户名已存在" });
        var u = new User { UserName = req.UserName, DisplayName = req.DisplayName, Role = req.Role, MemberId = req.MemberId };
        u.PasswordHash = _hasher.HashPassword(u, req.Password);
        _db.Users.Add(u);
        await _db.SaveChangesAsync();
        return Ok(new { u.Id });
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, UserEditReq req)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        if (req.Role != "Admin" && req.Role != "Member")
            return BadRequest(new { message = "角色不合法" });
        // 至少保留一名启用的管理员，避免把管理员全部降级后无人能进基础设置
        if (u.Role == "Admin" && req.Role != "Admin" && u.IsActive
            && !await _db.Users.AnyAsync(x => x.Id != id && x.Role == "Admin" && x.IsActive))
            return BadRequest(new { message = "至少保留一名管理员，不能降级最后一名管理员" });
        if (req.MemberId.HasValue && !await _db.Members.AnyAsync(x => x.Id == req.MemberId.Value))
            return BadRequest(new { message = "关联成员不存在" });
        u.DisplayName = req.DisplayName;
        u.Role = req.Role;
        u.MemberId = req.MemberId;
        await _db.SaveChangesAsync();
        return Ok(new { u.Id });
    }

    [HttpPost("{id:int}/reset-password")]
    public async Task<IActionResult> ResetPassword(int id, ResetReq req)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        u.PasswordHash = _hasher.HashPassword(u, req.Password);
        await _db.SaveChangesAsync();
        return Ok(new { u.Id });
    }

    [HttpPost("{id:int}/toggle-active")]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var u = await _db.Users.FindAsync(id);
        if (u == null) return NotFound();
        u.IsActive = !u.IsActive;
        await _db.SaveChangesAsync();
        return Ok(new { u.Id, u.IsActive });
    }
}
