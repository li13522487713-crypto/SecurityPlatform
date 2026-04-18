namespace Atlas.Application.AiPlatform.Models;

public enum KnowledgeBindingCallerType
{
    Agent = 0,
    App = 1,
    Workflow = 2,
    Chatflow = 3
}

public sealed record KnowledgeBindingDto(
    long Id,
    long KnowledgeBaseId,
    KnowledgeBindingCallerType CallerType,
    string CallerId,
    string CallerName,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    string? RetrievalProfileOverrideJson = null);

public sealed record KnowledgeBindingCreateRequest(
    KnowledgeBindingCallerType CallerType,
    string CallerId,
    string CallerName,
    RetrievalProfile? RetrievalProfileOverride = null);
