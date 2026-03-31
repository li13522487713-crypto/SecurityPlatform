using Atlas.Core.Tenancy;
using Microsoft.SemanticKernel;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IKernelFactory
{
    Task<Kernel> CreateAsync(
        TenantId tenantId,
        long? modelConfigId,
        string? modelName,
        CancellationToken cancellationToken);
}
