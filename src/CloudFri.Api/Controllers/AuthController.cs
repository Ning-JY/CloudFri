using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CloudFri.Api.Data;
using CloudFri.Api.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace CloudFri.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IConfiguration _cfg;
    private readonly PasswordHasher<User> _hasher = new();

    public AuthController(AppDbContext db, IConfiguration cfg) { _db = db; _cfg = cfg; }

    public record LoginReq(string UserName, string Password);
    public record ChangePwdReq(string OldPassword, string NewPassword);
    public record ProfileReq(string DisplayName, string? Phone, string? Remark, string? AvatarPath);

    // JwtBearer 默认会把 JWT 的 sub 映射为 NameIdentifier，FindFirstValue("sub") 取不到会抛错（改密码 500 的根因）
    private int CurrentUserId() =>
        int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginReq req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserName == req.UserName && x.IsActive);
        if (user == null) return Unauthorized(new { message = "用户名或密码不正确" });
        var ok = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (!ok) return Unauthorized(new { message = "用户名或密码不正确" });

        // 记录最后登录时间与来源 IP（优先反向代理 X-Forwarded-For 的首段）
        user.LastLoginAt = DateTime.Now;
        var xff = Request.Headers["X-Forwarded-For"].FirstOrDefault();
        user.LastLoginIp = !string.IsNullOrWhiteSpace(xff) ? xff.Split(',')[0].Trim() : HttpContext.Connection.RemoteIpAddress?.ToString();
        await _db.SaveChangesAsync();

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_cfg["Jwt:Key"] ?? "dev-only-key-change-in-production-0123456789abcdef"));
        var token = new JwtSecurityToken(
            issuer: _cfg["Jwt:Issuer"] ?? "CloudFri",
            audience: _cfg["Jwt:Audience"] ?? "CloudFri",
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName),
                new Claim(ClaimTypes.Role, user.Role),
                new Claim("displayName", user.DisplayName)
            },
            expires: DateTime.Now.AddDays(7),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));
        return Ok(new
        {
            token = new JwtSecurityTokenHandler().WriteToken(token),
            user = new { user.Id, user.UserName, user.DisplayName, user.Role, user.MemberId }
        });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me()
    {
        var id = CurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Unauthorized();
        return Ok(new { user.Id, user.UserName, user.DisplayName, user.Role, user.MemberId });
    }

    /// <summary>个人中心详情：账号信息 + 关联成员信息（未关联成员时 Member 为 null）。</summary>
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        var id = CurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Unauthorized();
        object? member = null;
        if (user.MemberId is int mid)
        {
            var m = await _db.Members.FindAsync(mid);
            if (m != null)
                member = new { m.Id, m.Name, m.Gender, m.Phone, m.Department, m.Remark, m.AvatarPath, m.IsActive };
        }
        return Ok(new
        {
            user.Id, user.UserName, user.DisplayName, user.Role, user.MemberId,
            user.CreatedAt, user.LastLoginAt, user.LastLoginIp,
            Member = member
        });
    }

    /// <summary>个人中心自助编辑：显示名 + 关联成员的电话/备注/头像（姓名/性别/部门为组织信息，只读）。</summary>
    [Authorize]
    [HttpPut("profile")]
    public async Task<IActionResult> UpdateProfile(ProfileReq req)
    {
        var id = CurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Unauthorized();
        var dn = req.DisplayName?.Trim();
        if (string.IsNullOrWhiteSpace(dn)) return BadRequest(new { message = "显示名不能为空" });
        user.DisplayName = dn;
        if (user.MemberId is int mid)
        {
            var m = await _db.Members.FindAsync(mid);
            if (m != null)
            {
                m.Phone = string.IsNullOrWhiteSpace(req.Phone) ? null : req.Phone.Trim();
                m.Remark = string.IsNullOrWhiteSpace(req.Remark) ? null : req.Remark.Trim();
                m.AvatarPath = string.IsNullOrWhiteSpace(req.AvatarPath) ? null : req.AvatarPath.Trim();
            }
        }
        await _db.SaveChangesAsync();
        return await Profile();
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePwdReq req)
    {
        var id = CurrentUserId();
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Unauthorized();
        var ok = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.OldPassword)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (!ok) return BadRequest(new { message = "原密码不正确" });
        if (string.IsNullOrWhiteSpace(req.NewPassword) || req.NewPassword.Length < 6)
            return BadRequest(new { message = "新密码至少 6 位" });
        user.PasswordHash = _hasher.HashPassword(user, req.NewPassword);
        await _db.SaveChangesAsync();
        return Ok(new { message = "密码已修改" });
    }
}
