using System.ComponentModel.DataAnnotations;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Identity;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Presentation.Shared.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.AppHost.Controllers;

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
    public async Task<ActionResult<ApiResponse<IReadOnlyList<AppHostOpenApiKeyDto>>>> List(
        [FromQuery] string? keyword = null,
        [FromQuery] int pageIndex = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantId = _tenantProvider.GetTenantId();
        var userId = _currentUserAccessor.GetCurrentUserOrThrow().UserId;
        var paged = await _service.GetPagedAsync(tenantId, userId, keyword, pageIndex, pageSize, cancellationToken);
        var items = paged.Items.Select(MapToDto).ToArray();
        return Ok(ApiResponse<IReadOnlyList<AppHostOpenApiKeyDto>>.Ok(items, HttpContext.TraceIdentifier));
    }

    [HttpPost]
    [Authorize(Policy = PermissionPolicies.PersonalAccessTokenCreate)]
    public async Task<ActionResult<ApiResponse<AppHostOpenApiKeyCreateResponse>>> Create(
        [FromBody] AppHostOpenApiKeyCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Alias))
        {
            return BadRequest(ApiResponse<AppHostOpenApiKeyCreateResponse>.Fail(
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

        var response = new AppHostOpenApiKeyCreateResponse(
            Key: result.PlainTextToken,
            Item: new AppHostOpenApiKeyDto(
                Id: result.Id.ToString(),
                Alias: result.Name,
                Prefix: result.TokenPrefix,
                Scopes: result.Scopes,
                CreatedAt: DateTimeOffset.UtcNow,
                LastUsedAt: null,
                ExpiresAt: result.ExpiresAt));

        return Ok(ApiResponse<AppHostOpenApiKeyCreateResponse>.Ok(response, HttpContext.TraceIdentifier));
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

    private static AppHostOpenApiKeyDto MapToDto(PersonalAccessTokenListItem item)
    {
        return new AppHostOpenApiKeyDto(
            Id: item.Id.ToString(),
            Alias: item.Name,
            Prefix: item.TokenPrefix,
            Scopes: item.Scopes,
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(item.CreatedAt, DateTimeKind.Utc)),
            LastUsedAt: item.LastUsedAt,
            ExpiresAt: item.ExpiresAt);
    }
}

public sealed record AppHostOpenApiKeyDto(
    string Id,
    string Alias,
    string Prefix,
    IReadOnlyList<string> Scopes,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastUsedAt,
    DateTimeOffset? ExpiresAt);

public sealed record AppHostOpenApiKeyCreateRequest(
    [Required, StringLength(64, MinimumLength = 1)] string Alias,
    IReadOnlyList<string>? Scopes,
    DateTimeOffset? ExpiresAt);

public sealed record AppHostOpenApiKeyCreateResponse(
    string Key,
    AppHostOpenApiKeyDto Item);
