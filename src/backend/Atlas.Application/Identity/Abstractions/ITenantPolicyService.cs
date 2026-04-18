using Atlas.Application.Identity.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.Identity.Abstractions;

public interface ITenantPolicyService
{
    Task<TenantNetworkPolicyDto?> GetNetworkAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<TenantNetworkPolicyDto> UpsertNetworkAsync(TenantId tenantId, long updatedBy, TenantNetworkPolicyUpdateRequest request, CancellationToken cancellationToken);

    Task<TenantDataResidencyPolicyDto?> GetResidencyAsync(TenantId tenantId, CancellationToken cancellationToken);

    Task<TenantDataResidencyPolicyDto> UpsertResidencyAsync(TenantId tenantId, long updatedBy, TenantDataResidencyPolicyUpdateRequest request, CancellationToken cancellationToken);
}
