using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;

namespace Atlas.Application.AiPlatform.Abstractions.Runtime;

public interface IOpenChatService
{
    Task<AgentChatResponse> CreateAsync(
        TenantId tenantId,
        long projectId,
        AgentChatRequest request,
        CancellationToken cancellationToken);

    IAsyncEnumerable<AgentChatStreamEvent> StreamAsync(
        TenantId tenantId,
        long projectId,
        AgentChatRequest request,
        CancellationToken cancellationToken);

    Task CancelAsync(
        TenantId tenantId,
        long projectId,
        AgentChatCancelRequest request,
        CancellationToken cancellationToken);
}
