using Atlas.Core.Models;
using Atlas.Infrastructure.Security;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Atlas.WebApi.Controllers;

/// <summary>
/// SSO / OIDC 登录入口：多 IdP 提供者查询与 OAuth2 授权发起
/// </summary>
[ApiController]
[Route("api/v1/auth/sso")]
public sealed class SsoController : ControllerBase
{
    private readonly OidcOptions _oidcOptions;

    public SsoController(IOptions<OidcOptions> oidcOptions)
    {
        _oidcOptions = oidcOptions.Value;
    }

    /// <summary>
    /// 获取所有已启用的 SSO/OIDC 提供者列表（供登录页展示第三方登录按钮）
    /// </summary>
    [HttpGet("providers")]
    [AllowAnonymous]
    public ActionResult<ApiResponse<object>> GetProviders()
    {
        if (!_oidcOptions.Enabled)
        {
            return Ok(ApiResponse<object>.Ok(Array.Empty<object>(), HttpContext.TraceIdentifier));
        }

        var providers = _oidcOptions.GetEffectiveProviders()
            .Select(p => new
            {
                p.ProviderId,
                p.DisplayName,
                p.IconUrl,
                LoginUrl = Url.Action(nameof(Login), new { providerId = p.ProviderId })
            })
            .ToArray();

        return Ok(ApiResponse<object>.Ok(providers, HttpContext.TraceIdentifier));
    }

    /// <summary>
    /// 发起指定 IdP 的 OIDC 登录跳转（浏览器直接访问此 URL 后会重定向到 IdP 授权页）
    /// </summary>
    /// <param name="providerId">提供者 ID（如 "github"、"default"）</param>
    /// <param name="returnUrl">登录成功后前端的回调 URL（可选）</param>
    [HttpGet("{providerId}/login")]
    [AllowAnonymous]
    public IActionResult Login(string providerId, [FromQuery] string? returnUrl = null)
    {
        if (!_oidcOptions.Enabled)
        {
            return BadRequest(ApiResponse<object>.Fail("FEATURE_DISABLED", "SSO 未启用", HttpContext.TraceIdentifier));
        }

        var providers = _oidcOptions.GetEffectiveProviders();
        var provider = providers.FirstOrDefault(p => string.Equals(p.ProviderId, providerId, StringComparison.OrdinalIgnoreCase));
        if (provider is null)
        {
            return NotFound(ApiResponse<object>.Fail("NOT_FOUND", $"提供者 '{providerId}' 不存在或未启用", HttpContext.TraceIdentifier));
        }

        // 使用 ASP.NET Core 的 Challenge 机制发起 OIDC 授权请求
        var callbackUrl = returnUrl ?? "/";
        var properties = new AuthenticationProperties
        {
            RedirectUri = callbackUrl,
            Items =
            {
                ["providerId"] = providerId
            }
        };
        return Challenge(properties, $"oidc_{providerId}");
    }

    /// <summary>
    /// 查询当前用户已绑定的 SSO 提供者（需登录，供"账号绑定"页面使用）
    /// </summary>
    [HttpGet("my-links")]
    [Authorize]
    public ActionResult<ApiResponse<object>> GetMyLinks()
    {
        // 从 JWT claims 中读取已绑定信息（简化实现：返回认证方案信息）
        var claims = User.Claims
            .Where(c => c.Type == "idp")
            .Select(c => c.Value)
            .ToArray();

        return Ok(ApiResponse<object>.Ok(new { LinkedProviders = claims }, HttpContext.TraceIdentifier));
    }
}
