using Atlas.Core.Tenancy;

namespace Atlas.Core.Abstractions;

public interface IIdGeneratorProvider
{
    long NextId(TenantId tenantId, string appId);
}
