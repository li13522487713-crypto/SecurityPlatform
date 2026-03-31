using Atlas.Application.AiPlatform.Models;

namespace Atlas.Infrastructure.Services.AiPlatform;

internal sealed class TeamAgentOrchestrationExecutionException : Exception
{
    public TeamAgentOrchestrationExecutionException(
        string message,
        IReadOnlyList<TeamAgentExecutionStep> steps,
        IReadOnlyList<TeamAgentMemberContribution> contributions,
        Exception innerException)
        : base(message, innerException)
    {
        Steps = steps;
        Contributions = contributions;
    }

    public IReadOnlyList<TeamAgentExecutionStep> Steps { get; }

    public IReadOnlyList<TeamAgentMemberContribution> Contributions { get; }
}
