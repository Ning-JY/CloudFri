using System.Security.Claims;

namespace CloudFri.Api;

/// <summary>从 JWT Claims 取当前登录者信息的统一入口（避免各控制器各写一套）。</summary>
public static class ClaimsExt
{
    /// <summary>显示名（displayName Claim），缺省回退到登录名。</summary>
    public static string DisplayName(this ClaimsPrincipal user)
        => user.FindFirst("displayName")?.Value ?? user.Identity?.Name ?? "";
}
