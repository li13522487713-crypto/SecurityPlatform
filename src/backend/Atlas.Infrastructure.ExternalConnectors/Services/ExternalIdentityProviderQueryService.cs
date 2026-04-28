using Atlas.Application.ExternalConnectors.Abstractions;
using Atlas.Application.ExternalConnectors.Models;
using Atlas.Application.ExternalConnectors.Repositories;
using Atlas.Core.Tenancy;
using Atlas.Domain.ExternalConnectors.Enums;
using AutoMapper;

namespace Atlas.Infrastructure.ExternalConnectors.Services;

public sealed class ExternalIdentityProviderQueryService : IExternalIdentityProviderQueryService
{
    private readonly IExternalIdentityProviderRepository _repository;
    private readonly ITenantProvider _tenantProvider;
    private readonly IMapper _mapper;

    public ExternalIdentityProviderQueryService(
        IExternalIdentityProviderRepository repository,
        ITenantProvider tenantProvider,
        IMapper mapper)
    {
        _repository = repository;
        _tenantProvider = tenantProvider;
        _mapper = mapper;
    }

    public async Task<ExternalIdentityProviderResponse?> GetAsync(long id, CancellationToken cancellationToken)
    {
        var entity = await _repository.GetByIdAsync(_tenantProvider.GetTenantId(), id, cancellationToken).ConfigureAwait(false);
        return entity is null ? null : _mapper.Map<ExternalIdentityProviderResponse>(entity);
    }

    public async Task<IReadOnlyList<ExternalIdentityProviderListItem>> ListAsync(ConnectorProviderType? type, bool includeDisabled, CancellationToken cancellationToken)
    {
        var entities = await _repository.ListAsync(_tenantProvider.GetTenantId(), type, includeDisabled, cancellationToken).ConfigureAwait(false);
        return _mapper.Map<IReadOnlyList<ExternalIdentityProviderListItem>>(entities);
    }
}
