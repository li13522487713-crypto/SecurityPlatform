using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Atlas.Application.Options;
using Atlas.Core.Tenancy;
using Atlas.Infrastructure.Repositories;
using Atlas.Presentation.Shared.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Presentation.Shared.Security;

public sealed class OpenProjectAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "OpenProject";
    public const string TokenTypeClaim = "token_type";
    public const string TokenTypeValue = "open_project";
    public const string ProjectIdClaim = "project_id";
    public const string AppIdClaim = "app_id";

    private readonly OpenApiProjectRepository _repository;
    private readonly TenancyOptions _tenancyOptions;
    private readonly JwtOptions _jwtOptions;

    public OpenProjectAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        OpenApiProjectRepository repository,
        IOptions<TenancyOptions> tenancyOptions,
        IOptions<JwtOptions> jwtOptions)
        : base(options, logger, encoder)
    {
        _repository = repository;
        _tenancyOptions = tenancyOptions.Value;
        _jwtOptions = jwtOptions.Value;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            return AuthenticateResult.NoResult();
        }

        var header = authHeader.ToString();
        if (!header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return AuthenticateResult.NoResult();
        }

        if (!Request.Headers.TryGetValue(_tenancyOptions.HeaderName, out var tenantHeader)
            || !Guid.TryParse(tenantHeader.ToString(), out var tenantGuid))
        {
            return AuthenticateResult.Fail("开放应用请求缺少有效租户标识。");
        }

        var rawToken = header["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticateResult.NoResult();
        }

        ClaimsPrincipal principal;
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validation = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateIssuerSigningKey = true,
                ValidateLifetime = true,
                ValidIssuer = _jwtOptions.Issuer,
                ValidAudience = _jwtOptions.Audience,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey)),
                NameClaimType = ClaimTypes.NameIdentifier,
                ClockSkew = TimeSpan.FromMinutes(1)
            };
            principal = tokenHandler.ValidateToken(rawToken, validation, out _);
        }
        catch (Exception ex)
        {
            return AuthenticateResult.Fail($"开放应用令牌无效：{ex.Message}");
        }

        var tokenType = principal.FindFirstValue(TokenTypeClaim);
        if (!string.Equals(tokenType, TokenTypeValue, StringComparison.Ordinal))
        {
            return AuthenticateResult.NoResult();
        }

        var tenantClaim = principal.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantClaim, out var tenantClaimGuid) || tenantClaimGuid != tenantGuid)
        {
            return AuthenticateResult.Fail("开放应用令牌租户不匹配。");
        }

        var projectIdRaw = principal.FindFirstValue(ProjectIdClaim);
        if (!long.TryParse(projectIdRaw, out var projectId))
        {
            return AuthenticateResult.Fail("开放应用令牌缺少 project_id。");
        }

        var tenantId = new TenantId(tenantGuid);
        var project = await _repository.FindByIdAsync(tenantId, projectId, Context.RequestAborted);
        if (project is null || !project.IsActive)
        {
            return AuthenticateResult.Fail("开放应用不存在或已禁用。");
        }

        var appId = principal.FindFirstValue(AppIdClaim);
        if (string.IsNullOrWhiteSpace(appId) || !string.Equals(appId, project.AppId, StringComparison.Ordinal))
        {
            return AuthenticateResult.Fail("开放应用令牌 AppId 不匹配。");
        }

        if (project.ExpiresAt > DateTime.UnixEpoch && project.ExpiresAt <= DateTime.UtcNow)
        {
            return AuthenticateResult.Fail("开放应用已过期。");
        }

        var identity = new ClaimsIdentity(principal.Claims, SchemeName);
        var authPrincipal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(authPrincipal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
