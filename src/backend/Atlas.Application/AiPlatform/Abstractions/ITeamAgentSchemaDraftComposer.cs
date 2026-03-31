using Atlas.Application.AiPlatform.Models;
using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Abstractions;

public interface ITeamAgentSchemaDraftComposer
{
    SchemaDraftDto Compose(
        TeamAgent teamAgent,
        string requirement,
        IReadOnlyList<TeamAgentMemberContribution> contributions,
        string? appId);
}
