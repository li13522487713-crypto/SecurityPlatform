using Atlas.Core.Abstractions;
using Atlas.Core.Tenancy;

namespace Atlas.Domain.AiPlatform.Entities;

public sealed class AgentKnowledgeLink : TenantEntity
{
    public AgentKnowledgeLink()
        : base(TenantId.Empty)
    {
        InvokeMode = "auto";
        TopK = 5;
        EnabledContentTypesJson = "[\"text\",\"table\",\"image\"]";
        IsEnabled = true;
    }

    public AgentKnowledgeLink(
        TenantId tenantId,
        long agentId,
        long knowledgeBaseId,
        bool isEnabled,
        string? invokeMode,
        int topK,
        double? scoreThreshold,
        string? enabledContentTypesJson,
        string? rewriteQueryTemplate,
        long id)
        : base(tenantId)
    {
        Id = id;
        AgentId = agentId;
        KnowledgeBaseId = knowledgeBaseId;
        IsEnabled = isEnabled;
        InvokeMode = string.IsNullOrWhiteSpace(invokeMode) ? "auto" : invokeMode.Trim();
        TopK = topK <= 0 ? 5 : topK;
        ScoreThreshold = scoreThreshold ?? 0d;
        EnabledContentTypesJson = string.IsNullOrWhiteSpace(enabledContentTypesJson) ? "[\"text\",\"table\",\"image\"]" : enabledContentTypesJson;
        RewriteQueryTemplate = rewriteQueryTemplate;
    }

    public long AgentId { get; private set; }
    public long KnowledgeBaseId { get; private set; }
    public bool IsEnabled { get; private set; }
    public string InvokeMode { get; private set; }
    public int TopK { get; private set; }
    public double ScoreThreshold { get; private set; }
    public string EnabledContentTypesJson { get; private set; }
    public string? RewriteQueryTemplate { get; private set; }

    public void Update(
        bool isEnabled,
        string? invokeMode,
        int topK,
        double? scoreThreshold,
        string? enabledContentTypesJson,
        string? rewriteQueryTemplate)
    {
        IsEnabled = isEnabled;
        InvokeMode = string.IsNullOrWhiteSpace(invokeMode) ? "auto" : invokeMode.Trim();
        TopK = topK <= 0 ? 5 : topK;
        ScoreThreshold = scoreThreshold ?? 0d;
        EnabledContentTypesJson = string.IsNullOrWhiteSpace(enabledContentTypesJson) ? "[\"text\",\"table\",\"image\"]" : enabledContentTypesJson;
        RewriteQueryTemplate = rewriteQueryTemplate;
    }
}
