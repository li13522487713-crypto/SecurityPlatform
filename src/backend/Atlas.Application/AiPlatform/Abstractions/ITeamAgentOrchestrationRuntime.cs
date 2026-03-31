using Atlas.Application.AiPlatform.Models;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ITeamAgentOrchestrationRuntime
{
    Task<TeamAgentOrchestrationRuntimeResult> ExecuteAsync(
        TeamAgentOrchestrationRuntimeRequest request,
        Func<TeamAgentRunEvent, Task> onEventAsync,
        Func<TeamAgentMemberContribution, Task> onContributionAsync,
        CancellationToken cancellationToken);
}
