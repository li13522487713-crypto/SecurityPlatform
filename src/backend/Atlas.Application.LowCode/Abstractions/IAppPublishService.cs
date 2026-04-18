using Atlas.Application.LowCode.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.LowCode.Abstractions;

public interface IAppPublishService
{
    Task<PublishArtifactDto> PublishAsync(TenantId tenantId, long currentUserId, long appId, PublishRequest request, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublishArtifactDto>> ListAsync(TenantId tenantId, long appId, CancellationToken cancellationToken);
    Task RevokeAsync(TenantId tenantId, long currentUserId, long appId, string artifactId, string? reason, CancellationToken cancellationToken);
}
