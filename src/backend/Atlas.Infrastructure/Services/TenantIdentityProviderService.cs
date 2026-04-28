using Atlas.Application.Identity.Abstractions;
using Atlas.Application.Identity.Models;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.Identity.Entities;
using Atlas.Infrastructure.Repositories;
using Atlas.Infrastructure.Services.LowCode;

namespace Atlas.Infrastructure.Services;

public sealed class TenantIdentityProviderService : ITenantIdentityProviderService
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        TenantIdentityProvider.TypeOidc, TenantIdentityProvider.TypeSaml
    };

    private readonly TenantIdentityProviderRepository _repository;
    private readonly LowCodeCredentialProtector _protector;
    private readonly IIdGeneratorAccessor _idGenerator;

    public TenantIdentityProviderService(
        TenantIdentityProviderRepository repository,
        LowCodeCredentialProtector protector,
        IIdGeneratorAccessor idGenerator)
    {
        _repository = repository;
        _protector = protector;
        _idGenerator = idGenerator;
    }

    public async Task<IReadOnlyList<TenantIdentityProviderDto>> ListAsync(TenantId tenantId, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListByTenantAsync(tenantId, cancellationToken);
        return entities.Select(ToDto).ToArray();
    }

    public async Task<TenantIdentityProviderDto?> GetByCodeAsync(TenantId tenantId, string code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        var entity = await _repository.FindByCodeAsync(tenantId, code, cancellationToken);
        return entity is null ? null : ToDto(entity);
    }

    public async Task<TenantIdentityProviderDto> CreateAsync(TenantId tenantId, long createdBy, TenantIdentityProviderCreateRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code)) throw new BusinessException(ErrorCodes.ValidationError, "CodeRequired");
        if (!AllowedTypes.Contains(request.IdpType)) throw new BusinessException(ErrorCodes.ValidationError, "IdpTypeInvalid");
        var existing = await _repository.FindByCodeAsync(tenantId, request.Code, cancellationToken);
        if (existing is not null) throw new BusinessException(ErrorCodes.Conflict, "TenantIdpCodeDuplicate");
        var encrypted = string.IsNullOrEmpty(request.SecretJson) ? string.Empty : _protector.Encrypt(request.SecretJson);
        var entity = new TenantIdentityProvider(
            tenantId, request.Code, request.DisplayName, request.IdpType, request.Enabled,
            request.ConfigJson, encrypted, createdBy, _idGenerator.NextId());
        await _repository.AddAsync(entity, cancellationToken);
        return ToDto(entity);
    }

    public async Task UpdateAsync(TenantId tenantId, long id, long updatedBy, TenantIdentityProviderUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken)
            ?? throw new BusinessException(ErrorCodes.NotFound, "TenantIdpNotFound");
        var encrypted = string.IsNullOrEmpty(request.SecretJson) ? null : _protector.Encrypt(request.SecretJson!);
        entity.Update(request.DisplayName, request.Enabled, request.ConfigJson, encrypted, updatedBy);
        await _repository.UpdateAsync(entity, cancellationToken);
    }

    public async Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.FindByIdAsync(tenantId, id, cancellationToken);
        if (entity is null) return;
        await _repository.DeleteAsync(entity, cancellationToken);
    }

    internal static TenantIdentityProviderDto ToDto(TenantIdentityProvider entity)
    {
        return new TenantIdentityProviderDto(
            Id: entity.Id.ToString(),
            Code: entity.Code,
            DisplayName: entity.DisplayName,
            IdpType: entity.IdpType,
            Enabled: entity.Enabled,
            ConfigJson: entity.ConfigJson,
            HasSecret: !string.IsNullOrEmpty(entity.SecretJson),
            CreatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.CreatedAt, DateTimeKind.Utc)),
            UpdatedAt: new DateTimeOffset(DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc)));
    }
}
