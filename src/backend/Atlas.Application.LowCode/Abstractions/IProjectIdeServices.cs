using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IProjectIdeBootstrapService
{
    Task<ProjectIdeBootstrapDto?> GetBootstrapAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
    Task<ProjectIdeValidationResultDto> ValidateAsync(TenantId tenantId, long appId, string? schemaJsonOverride, CancellationToken cancellationToken);
    Task<ProjectIdePublishPreviewDto?> GetPublishPreviewAsync(TenantId tenantId, long appId, string? schemaJsonOverride, CancellationToken cancellationToken);
}

public interface IProjectIdeDependencyGraphService
{
    Task<ProjectIdeGraphDto?> GetGraphAsync(TenantId tenantId, long appId, string? schemaJsonOverride, CancellationToken cancellationToken);
}

public interface IProjectIdePublishOrchestrator
{
    Task<ProjectIdePublishResultDto> PublishAsync(TenantId tenantId, long currentUserId, long appId, ProjectIdePublishRequest request, CancellationToken cancellationToken);
}
