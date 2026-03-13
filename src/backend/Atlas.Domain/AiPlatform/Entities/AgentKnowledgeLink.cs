using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AgentKnowledgeLink : TenantEntity
{
    public AgentKnowledgeLink()
        : base(TenantId.Empty)
    {
    }

    public AgentKnowledgeLink(TenantId tenantId, long agentId, long knowledgeBaseId, long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        KnowledgeBaseId = knowledgeBaseId;
    }

    public long AgentId { get; private set; }
    public long KnowledgeBaseId { get; private set; }
}
