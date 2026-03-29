using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface IModelConfigCommandService
{
    Task<long> CreateAsync(TenantId tenantId, ModelConfigCreateRequest request, CancellationToken cancellationToken);

    Task UpdateAsync(TenantId tenantId, long id, ModelConfigUpdateRequest request, CancellationToken cancellationToken);

    Task DeleteAsync(TenantId tenantId, long id, CancellationToken cancellationToken);

    Task<ModelConfigTestResult> TestConnectionAsync(ModelConfigTestRequest request, CancellationToken cancellationToken);

    IAsyncEnumerable<ModelConfigPromptTestStreamEvent> TestPromptStreamAsync(
        ModelConfigPromptTestRequest request,
        CancellationToken cancellationToken);
}
