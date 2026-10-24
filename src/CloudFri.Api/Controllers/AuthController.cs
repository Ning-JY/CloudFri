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

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginReq req)
    {
        var user = await _db.Users.FirstOrDefaultAsync(x => x.UserName == req.UserName && x.IsActive);
        if (user == null) return Unauthorized(new { message = "用户名或密码不正确" });
        var ok = _hasher.VerifyHashedPassword(user, user.PasswordHash, req.Password)
            is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded;
        if (!ok) return Unauthorized(new { message = "用户名或密码不正确" });

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
        var id = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        var user = await _db.Users.FindAsync(id);
        if (user == null) return Unauthorized();
        return Ok(new { user.Id, user.UserName, user.DisplayName, user.Role, user.MemberId });
    }

    [Authorize]
    [HttpPost("change-password")]
    public async Task<IActionResult> ChangePassword(ChangePwdReq req)
    {
        var id = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
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
