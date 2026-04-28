using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Abstractions;
using Atlas.Core.Exceptions;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Entities;
using AutoMapper;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class ExternalIdentityProviderCommandService : IExternalIdentityProviderCommandService
{
    private readonly IExternalIdentityProviderRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IIdGeneratorAccessor _idGenerator;
    private readonly ISecretProtector _dataProtection;
    private readonly TimeProvider _timeProvider;
    private readonly IMapper _mapper;

    public ExternalIdentityProviderCommandService(
        IExternalIdentityProviderRepository repository,
        ITenantProvider tenantProvider,
        IIdGeneratorAccessor idGenerator,
        ISecretProtector dataProtection,
        TimeProvider timeProvider,
        IMapper mapper)
    {
        _repository = repository;
        _tenantProvider = tenantProvider;
        _idGenerator = idGenerator;
        _dataProtection = dataProtection;
        _timeProvider = timeProvider;
        _mapper = mapper;
    }

    public async Task<ExternalIdentityProviderResponse> CreateAsync(ExternalIdentityProviderCreateRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var tenantId = _tenantProvider.GetTenantId();
        var existing = await _repository.GetByCodeAsync(tenantId, request.Code, cancellationToken).ConfigureAwait(false);
        if (existing is not null)
        {
            throw new BusinessException("CONNECTOR_PROVIDER_CODE_DUPLICATE", $"External identity provider with code '{request.Code}' already exists.");
        }

        var now = _timeProvider.GetUtcNow();
        var entity = new ExternalIdentityProvider(
            tenantId,
            _idGenerator.NextId(),
            request.ProviderType,
            request.Code,
            request.DisplayName,
            request.ProviderTenantId,
            request.AppId,
            _dataProtection.Encrypt(request.SecretJson),
            request.TrustedDomains,
            request.CallbackBaseUrl,
            request.AgentId,
            request.VisibilityScope,
            request.SyncCron,
            now);

        await _repository.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        return _mapper.Map<ExternalIdentityProviderResponse>(entity);
    }

    public async Task<ExternalIdentityProviderResponse> UpdateAsync(long id, ExternalIdentityProviderUpdateRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        entity.UpdateProfile(
            request.DisplayName,
            request.ProviderTenantId,
            request.AppId,
            request.TrustedDomains,
            request.CallbackBaseUrl,
            request.AgentId,
            request.VisibilityScope,
            request.SyncCron,
            now);
        await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return _mapper.Map<ExternalIdentityProviderResponse>(entity);
    }

    public async Task<ExternalIdentityProviderResponse> RotateSecretAsync(long id, ExternalIdentityProviderRotateSecretRequest request, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        entity.RotateSecret(_dataProtection.Encrypt(request.SecretJson), _timeProvider.GetUtcNow());
        await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return _mapper.Map<ExternalIdentityProviderResponse>(entity);
    }

    public async Task<ExternalIdentityProviderResponse> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken)
    {
        var entity = await LoadAsync(id, cancellationToken).ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow();
        if (enabled) entity.Enable(now); else entity.Disable(now);
        await _repository.UpdateAsync(entity, cancellationToken).ConfigureAwait(false);
        return _mapper.Map<ExternalIdentityProviderResponse>(entity);
    }

    public async Task DeleteAsync(long id, CancellationToken cancellationToken)
        => await _repository.DeleteAsync(_tenantProvider.GetTenantId(), id, cancellationToken).ConfigureAwait(false);

    private async Task<ExternalIdentityProvider> LoadAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(_tenantProvider.GetTenantId(), id, cancellationToken).ConfigureAwait(false);
        return entity ?? throw new BusinessException("CONNECTOR_PROVIDER_NOT_FOUND", $"External identity provider {id} not found.");
    }
}
