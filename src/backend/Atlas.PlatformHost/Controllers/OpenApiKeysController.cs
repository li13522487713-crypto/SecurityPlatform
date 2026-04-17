using System.ComponentModel.DataAnnotations;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.PlatformHost.Controllers;

/// <summary>
/// Coze PRD API 管理（PRD 02-7.10）。
///
/// 复用 <see cref="IPersonalAccessTokenService"/>（PAT 服务），暴露给前端"API 管理"页：
///   GET    /api/v1/open/api-keys
///   POST   /api/v1/open/api-keys
///   DELETE /api/v1/open/api-keys/{keyId}
///
/// 字段映射（前端 protocol → 后端 PAT）：
///   alias → Name
///   prefix → TokenPrefix
///   scopes → Scopes
///   createdAt → CreatedAt
///   lastUsedAt → LastUsedAt
///   plainTextToken → key（仅在 create 响应中返回一次，落库只存 hash）
/// </summary>
[ApiController]
[Route("api/v1/open/api-keys")]
[Authorize]
public sealed class OpenApiKeysController : ControllerBase
{
    private static readonly IReadOnlyList<string> DefaultScopes = new[] { "agent:read", "workflow:run" };

    private readonly IPersonalAccessTokenService _service;
    private readonly ITenantProvider _tenantProvider;
    private readonly ICurrentUserAccessor _currentUserAccessor;

    public OpenApiKeysController(
        IPersonalAccessTokenService service,
        ITenantProvider tenantProvider,
        ICurrentUserAccessor currentUserAccessor)
    {
        _service = service;
        _tenantProvider = tenantProvider;
        _currentUserAccessor = currentUserAccessor;
    }

    [HttpGet]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenView)]
    public async Task<ActionResult<ApiResponse<IReadOnlyList<OpenApiKeyDto>>>> List(
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var paged = await _service.GetPagedAsync(tenantId, userId, keyword, pageIndex, pageSize, cancellationToken);
        var items = paged.Items.Select(MapToDto).ToArray();
        return Ok(ApiResponse<IReadOnlyList<OpenApiKeyDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenCreate)]
    public async Task<ActionResult<ApiResponse<OpenApiKeyCreateResponse>>> Create(
        [FromBody] OpenApiKeyCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Alias))
        {
            return BadRequest(ApiResponse<OpenApiKeyCreateResponse>.Fail(
                ErrorCodes.ValidationError,
                "AliasRequired",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var scopes = request.Scopes is { Count: > 0 } ? request.Scopes : DefaultScopes;

        var result = await _service.CreateAsync(
            tenantId,
            userId,
            new PersonalAccessTokenCreateRequest(request.Alias.Trim(), scopes, request.ExpiresAt),
            cancellationToken);

        var response = new OpenApiKeyCreateResponse(
            Key: result.PlainTextToken,
            Item: new OpenApiKeyDto(
                Id: result.Id.ToString(),
                Alias: result.Name,
                Prefix: result.TokenPrefix,
                Scopes: result.Scopes,
                CreatedAt: DateTimeOffset.UtcNow,
                LastUsedAt: null,
                ExpiresAt: result.ExpiresAt));

        return Ok(ApiResponse<OpenApiKeyCreateResponse>.Ok(response, HttpContext.TraceIdentifier));
    }

    [HttpDelete("{keyId}")]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenDelete)]
    public async Task<ActionResult<ApiResponse<object>>> Delete(
        string keyId,
        CancellationToken cancellationToken)
    {
        if (!long.TryParse(keyId, out var id))
        {
            return NotFound(ApiResponse<object>.Fail(
                ErrorCodes.NotFound,
                "ApiKeyNotFound",
                HttpContext.TraceIdentifier));
        }

        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        await _service.RevokeAsync(tenantId, userId, id, cancellationToken);
        return Ok(ApiResponse<object>.Ok(new { Success = true }, HttpContext.TraceIdentifier));
    }

    private static OpenApiKeyDto MapToDto(PersonalAccessTokenListItem item)
    {
        return new OpenApiKeyDto(
            Id: item.Id.ToString(),
            Alias: item.Name,
            Prefix: item.TokenPrefix,
            Scopes: item.Scopes,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc)),
            LastUsedAt: item.LastUsedAt,
            ExpiresAt: item.ExpiresAt);
    }
}

public sealed record OpenApiKeyDto(
    string Id,
    string Alias,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt);

public sealed record OpenApiKeyCreateRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Alias,
    IReadOnlyList<string>? Scopes,
    DateTimeOffset? ExpiresAt);

public sealed record OpenApiKeyCreateResponse(
    string Key,
    OpenApiKeyDto Item);
