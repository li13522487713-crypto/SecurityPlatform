using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Encodings.Web;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Presentation.Shared.Tenancy;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Atlas.Presentation.Shared.Security;

public sealed class PatAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
    public const string SchemeName = "Pat";
    private readonly IPersonalAccessTokenService _tokenService;
    private readonly TenancyOptions _tenancyOptions;

    public PatAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IPersonalAccessTokenService tokenService,
        IOptions<TenancyOptions> tenancyOptions)
        : base(options, logger, encoder)
    {
        _tokenService = tokenService;
        _tenancyOptions = tenancyOptions.Value;
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
            return AuthenticateResult.Fail("PAT 请求缺少有效租户标识。");
        }

        var rawToken = header["Bearer ".Length..].Trim();
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return AuthenticateResult.NoResult();
        }

        var result = await _tokenService.ValidateAsync(new Atlas.Core.Tenancy.TenantId(tenantGuid), rawToken, Context.RequestAborted);
        if (!result.Success)
        {
            return AuthenticateResult.Fail(result.FailureReason);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, result.UserId.ToString()),
            new(JwtRegisteredClaimNames.Sub, $"pat:{result.TokenId}"),
            new("tenant_id", tenantGuid.ToString()),
            new(ClaimTypes.Role, "PatUser")
        };
        claims.AddRange(result.Scopes.Select(scope => new Claim("scope", scope)));

        var identity = new ClaimsIdentity(claims, SchemeName);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, SchemeName);
        return AuthenticateResult.Success(ticket);
    }
}
