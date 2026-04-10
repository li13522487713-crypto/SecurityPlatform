using Atlas.Core.Tenancy;

namespace Atlas.Application.System.Abstractions;

public interface IAppDataSourceProvisioner
{
    Task EnsureProvisionedAsync(
        TenantId tenantId,
        long appInstanceId,
        string appKey,
        long operatorUserId,
        long? preferredDataSourceId = null,
        CancellationToken cancellationToken = default);
}
