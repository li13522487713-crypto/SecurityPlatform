using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IAiDatabaseProvisioner
{
    Task EnsureProvisionedAsync(AiDatabase database, CancellationToken cancellationToken);

    Task DropAsync(AiDatabase database, CancellationToken cancellationToken);
}
