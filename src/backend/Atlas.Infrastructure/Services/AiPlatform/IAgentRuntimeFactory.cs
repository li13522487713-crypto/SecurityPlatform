using Atlas.Application.AiPlatform.Models;
using Atlas.Core.Tenancy;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Infrastructure.Services.AiPlatform;

public interface IAgentRuntimeFactory
{
    Task<TeamAgentRuntimeDescriptor> ResolveRuntimeAsync(
        TenantId tenantId,
        TeamAgentMode mode,
        IReadOnlyList<TeamAgentMemberItem> members,
        CancellationToken cancellationToken);
}
