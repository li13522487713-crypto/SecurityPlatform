using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiDatabaseProvisioner
{
    Task EnsureProvisionedAsync(AiDatabase database, CancellationToken cancellationToken);

    Task EnsureDraftAsync(AiDatabase database, CancellationToken cancellationToken);

    Task EnsureOnlineAsync(AiDatabase database, CancellationToken cancellationToken);

    Task ValidateHostingOptionsAsync(string driverCode, CancellationToken cancellationToken);

    Task DropAsync(AiDatabase database, CancellationToken cancellationToken);
}
