namespace Atlas.Application.AiPlatform.Models;

public sealed record AgenticRagQueryRequest(
    string Query,
    IReadOnlyList<long> KnowledgeBaseIds,
    int TopK = 6);

public sealed record AgenticRagCitation(
    long KnowledgeBaseId,
    long DocumentId,
    long ChunkId,
    string? DocumentName,
    float Score);

public sealed record AgenticRagStepTrace(
    string Stage,
    string Detail,
    string At);

public sealed record AgenticRagQueryResponse(
    string Route,
    string Answer,
    float Confidence,
    IReadOnlyList<AgenticRagCitation> Citations,
    IReadOnlyList<AgenticRagStepTrace> Traces);
