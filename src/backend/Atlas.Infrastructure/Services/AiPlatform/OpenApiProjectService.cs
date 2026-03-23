using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.Abstractions;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Application.Options;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Options;
using Atlas.Infrastructure.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class OpenApiProjectService : IOpenApiProjectService
{
    private const string OpenProjectTokenType = "open_project";
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly OpenApiProjectRepository _repository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;
    private readonly JwtOptions _jwtOptions;
    private readonly AiPlatformOptions _aiPlatformOptions;

    public OpenApiProjectService(
        OpenApiProjectRepository repository,
        IPasswordHasher passwordHasher,
        IIdGeneratorAccessor idGeneratorAccessor,
        IOptions<JwtOptions> jwtOptions,
        IOptions<AiPlatformOptions> aiPlatformOptions)
    {
        _repository = repository;
        _passwordHasher = passwordHasher;
        _idGeneratorAccessor = idGeneratorAccessor;
        _jwtOptions = jwtOptions.Value;
        _aiPlatformOptions = aiPlatformOptions.Value;
    }

    public async Task<PagedResult<OpenApiProjectListItem>> GetPagedAsync(
        TenantId tenantId,
        long createdByUserId,
        string? keyword,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var (items, total) = await _repository.GetPagedByOwnerAsync(
            tenantId,
            createdByUserId,
            keyword,
            pageIndex,
            pageSize,
            cancellationToken);
        return new PagedResult<OpenApiProjectListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<OpenApiProjectCreateResult> CreateAsync(
        TenantId tenantId,
        long createdByUserId,
        OpenApiProjectCreateRequest request,
        CancellationToken cancellationToken)
    {
        var id = _idGeneratorAccessor.NextId();
        var appId = $"atlas_{id:x}";
        var appSecret = GenerateAppSecret();
        var secretHash = _passwordHasher.HashPassword(appSecret);
        var scopes = NormalizeScopes(request.Scopes);

        var entity = new OpenApiProject(
            tenantId,
            request.Name.Trim(),
            request.Description?.Trim(),
            appId,
            appSecret[..Math.Min(12, appSecret.Length)],
            secretHash,
            SerializeScopes(scopes),
            createdByUserId,
            request.ExpiresAt?.UtcDateTime,
            id);

        await _repository.AddAsync(entity, cancellationToken);
        return new OpenApiProjectCreateResult(
            entity.Id,
            entity.Name,
            entity.AppId,
            appSecret,
            entity.SecretPrefix,
            scopes,
            entity.ExpiresAt == DateTime.UnixEpoch ? null : entity.ExpiresAt);
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        OpenApiProjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindOwnedByIdAsync(tenantId, createdByUserId, projectId, cancellationToken)
            ?? throw new BusinessException("开放应用不存在。", ErrorCodes.NotFound);

        entity.Update(
            request.Name.Trim(),
            request.Description?.Trim(),
            SerializeScopes(NormalizeScopes(request.Scopes)),
            request.IsActive,
            request.ExpiresAt?.UtcDateTime);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<OpenApiProjectRotateSecretResult> RotateSecretAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindOwnedByIdAsync(tenantId, createdByUserId, projectId, cancellationToken)
            ?? throw new BusinessException("开放应用不存在。", ErrorCodes.NotFound);

        var appSecret = GenerateAppSecret();
        entity.RotateSecret(
            appSecret[..Math.Min(12, appSecret.Length)],
            _passwordHasher.HashPassword(appSecret));
        await _repository.UpdateAsync(entity, cancellationToken);
        return new OpenApiProjectRotateSecretResult(entity.Id, entity.AppId, appSecret, entity.SecretPrefix);
    }

    public async Task DeleteAsync(
        TenantId tenantId,
        long createdByUserId,
        long projectId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindOwnedByIdAsync(tenantId, createdByUserId, projectId, cancellationToken)
            ?? throw new BusinessException("开放应用不存在。", ErrorCodes.NotFound);
        entity.Update(entity.Name, entity.Description, entity.ScopesJson, isActive: false, entity.ExpiresAt);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<OpenApiProjectTokenExchangeResult> ExchangeTokenAsync(
        TenantId tenantId,
        OpenApiProjectTokenExchangeRequest request,
        CancellationToken cancellationToken)
    {
        var project = await _repository.FindByAppIdAsync(tenantId, request.AppId.Trim(), cancellationToken)
            ?? throw new BusinessException("AppId 或 AppSecret 无效。", ErrorCodes.Unauthorized);

        if (!project.IsActive)
        {
            throw new BusinessException("开放应用已禁用。", ErrorCodes.Forbidden);
        }

        var utcNow = DateTime.UtcNow;
        if (project.ExpiresAt > DateTime.UnixEpoch && project.ExpiresAt <= utcNow)
        {
            throw new BusinessException("开放应用已过期。", ErrorCodes.Forbidden);
        }

        if (!_passwordHasher.VerifyHashedPassword(project.SecretHash, request.AppSecret.Trim()))
        {
            throw new BusinessException("AppId 或 AppSecret 无效。", ErrorCodes.Unauthorized);
        }

        var scopes = ParseScopes(project.ScopesJson);
        var now = DateTimeOffset.UtcNow;
        var expiresAt = now.AddMinutes(
            Math.Clamp(_aiPlatformOptions.OpenApiProject.AccessTokenExpiresMinutes, 5, 24 * 60));

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, project.CreatedByUserId.ToString()),
            new(JwtRegisteredClaimNames.Sub, $"{OpenProjectTokenType}:{project.Id}"),
            new("tenant_id", tenantId.Value.ToString()),
            new("token_type", OpenProjectTokenType),
            new("project_id", project.Id.ToString()),
            new("app_id", project.AppId)
        };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);
        var accessToken = new JwtSecurityTokenHandler().WriteToken(token);

        project.MarkUsed();
        await _repository.UpdateAsync(project, cancellationToken);

        return new OpenApiProjectTokenExchangeResult(
            accessToken,
            "Bearer",
            expiresAt,
            project.Id,
            project.AppId,
            scopes);
    }

    private static OpenApiProjectListItem MapListItem(OpenApiProject item)
        => new(
            item.Id,
            item.Name,
            item.Description,
            item.AppId,
            item.SecretPrefix,
            ParseScopes(item.ScopesJson),
            item.IsActive,
            item.ExpiresAt == DateTime.UnixEpoch ? null : item.ExpiresAt,
            item.LastUsedAt == DateTime.UnixEpoch ? null : item.LastUsedAt,
            item.CreatedByUserId,
            item.CreatedAt,
            item.UpdatedAt);

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string> scopes)
    {
        var normalized = scopes
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (normalized.Length > 0)
        {
            return normalized;
        }

        return ["open:*"];
    }

    private static string SerializeScopes(IReadOnlyList<string> scopes)
    {
        return JsonSerializer.Serialize(scopes, JsonOptions);
    }

    private static IReadOnlyList<string> ParseScopes(string? scopesJson)
    {
        if (string.IsNullOrWhiteSpace(scopesJson))
        {
            return [];
        }

        try
        {
            return JsonSerializer.Deserialize<List<string>>(scopesJson, JsonOptions) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static string GenerateAppSecret()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return $"osk_{Convert.ToHexString(bytes).ToLowerInvariant()}";
    }
}
