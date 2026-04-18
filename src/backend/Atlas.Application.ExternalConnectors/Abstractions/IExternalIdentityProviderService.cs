using Atlas.Application.ExternalConnectors.Models;
using Atlas.Domain.ExternalConnectors.Enums;

namespace Atlas.Application.ExternalConnectors.Abstractions;

public interface IExternalIdentityProviderQueryService
{
    Task<ExternalIdentityProviderResponse?> GetAsync(long id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ExternalIdentityProviderListItem>> ListAsync(ConnectorProviderType? type, bool includeDisabled, CancellationToken cancellationToken);
}

public interface IExternalIdentityProviderCommandService
{
    Task<ExternalIdentityProviderResponse> CreateAsync(ExternalIdentityProviderCreateRequest request, CancellationToken cancellationToken);

    Task<ExternalIdentityProviderResponse> UpdateAsync(long id, ExternalIdentityProviderUpdateRequest request, CancellationToken cancellationToken);

    Task<ExternalIdentityProviderResponse> RotateSecretAsync(long id, ExternalIdentityProviderRotateSecretRequest request, CancellationToken cancellationToken);

    Task<ExternalIdentityProviderResponse> SetEnabledAsync(long id, bool enabled, CancellationToken cancellationToken);

    Task DeleteAsync(long id, CancellationToken cancellationToken);
}
