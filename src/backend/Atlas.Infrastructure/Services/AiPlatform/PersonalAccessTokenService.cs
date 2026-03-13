using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Atlas.Application.AiPlatform.Abstractions;
using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;
using Atlas.Infrastructure.Repositories;

namespace Atlas.Infrastructure.Services.AiPlatform;

public sealed class PersonalAccessTokenService : IPersonalAccessTokenService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PersonalAccessTokenRepository _repository;
    private readonly IIdGeneratorAccessor _idGeneratorAccessor;

    public PersonalAccessTokenService(
        PersonalAccessTokenRepository repository,
        IIdGeneratorAccessor idGeneratorAccessor)
    {
        _repository = repository;
        _idGeneratorAccessor = idGeneratorAccessor;
    }

    public async Task<PagedResult<PersonalAccessTokenListItem>> GetPagedAsync(
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
        return new PagedResult<PersonalAccessTokenListItem>(
            items.Select(MapListItem).ToList(),
            total,
            pageIndex,
            pageSize);
    }

    public async Task<PersonalAccessTokenCreateResult> CreateAsync(
        TenantId tenantId,
        long createdByUserId,
        PersonalAccessTokenCreateRequest request,
        CancellationToken cancellationToken)
    {
        var rawToken = GenerateRawToken();
        var hash = ComputeSha256(rawToken);
        var scopes = NormalizeScopes(request.Scopes);
        var entity = new PersonalAccessToken(
            tenantId,
            request.Name.Trim(),
            tokenPrefix: rawToken[..Math.Min(16, rawToken.Length)],
            tokenHash: hash,
            scopesJson: SerializeScopes(scopes),
            createdByUserId,
            request.ExpiresAt,
            _idGeneratorAccessor.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        return new PersonalAccessTokenCreateResult(
            entity.Id,
            entity.Name,
            entity.TokenPrefix,
            rawToken,
            entity.ExpiresAt,
            scopes);
    }

    public async Task UpdateAsync(
        TenantId tenantId,
        long createdByUserId,
        long tokenId,
        PersonalAccessTokenUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindOwnedByIdAsync(tenantId, createdByUserId, tokenId, cancellationToken)
            ?? throw new BusinessException("PAT 不存在。", ErrorCodes.NotFound);
        if (entity.RevokedAt.HasValue)
        {
            throw new BusinessException("已撤销的 PAT 不允许修改。", ErrorCodes.ValidationError);
        }

        entity.Update(request.Name.Trim(), SerializeScopes(NormalizeScopes(request.Scopes)), request.ExpiresAt);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task RevokeAsync(
        TenantId tenantId,
        long createdByUserId,
        long tokenId,
        CancellationToken cancellationToken)
    {
        var entity = await _repository.FindOwnedByIdAsync(tenantId, createdByUserId, tokenId, cancellationToken)
            ?? throw new BusinessException("PAT 不存在。", ErrorCodes.NotFound);
        if (entity.RevokedAt.HasValue)
        {
            return;
        }

        entity.Revoke();
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task<PersonalAccessTokenValidateResult> ValidateAsync(
        TenantId tenantId,
        string rawToken,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return new PersonalAccessTokenValidateResult(false, 0, 0, [], "token 为空");
        }

        var hash = ComputeSha256(rawToken.Trim());
        var entity = await _repository.FindByHashAsync(tenantId, hash, cancellationToken);
        if (entity is null)
        {
            return new PersonalAccessTokenValidateResult(false, 0, 0, [], "token 不存在");
        }

        if (entity.RevokedAt.HasValue)
        {
            return new PersonalAccessTokenValidateResult(false, entity.Id, entity.CreatedByUserId, [], "token 已撤销");
        }

        if (entity.ExpiresAt.HasValue && entity.ExpiresAt.Value <= DateTimeOffset.UtcNow)
        {
            return new PersonalAccessTokenValidateResult(false, entity.Id, entity.CreatedByUserId, [], "token 已过期");
        }

        entity.MarkUsed();
        _ = _repository.UpdateAsync(entity, CancellationToken.None);
        var scopes = ParseScopes(entity.ScopesJson);
        return new PersonalAccessTokenValidateResult(true, entity.Id, entity.CreatedByUserId, scopes, string.Empty);
    }

    private static PersonalAccessTokenListItem MapListItem(PersonalAccessToken entity)
        => new(
            entity.Id,
            entity.Name,
            entity.TokenPrefix,
            ParseScopes(entity.ScopesJson),
            entity.CreatedByUserId,
            entity.ExpiresAt,
            entity.LastUsedAt,
            entity.RevokedAt,
            entity.CreatedAt);

    private static IReadOnlyList<string> NormalizeScopes(IReadOnlyList<string> scopes)
    {
        return scopes
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        var random = Convert.ToHexString(bytes).ToLowerInvariant();
        return $"pat_{random}";
    }

    private static string ComputeSha256(string value)
    {
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
