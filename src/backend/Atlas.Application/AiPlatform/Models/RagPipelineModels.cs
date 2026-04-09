namespace Atlas.Application.AiPlatform.Models;

public enum RagFailureMode
{
    None = 0,
    EmptyQuery = 1,
    RetrievalEmpty = 2,
    VerificationFailed = 3,
    LlmError = 4
}

public sealed record RagEvidenceScore(
    float Relevance,
    float Faithfulness,
    float Freshness,
    string? Summary = null);

public sealed record RagCitation(
    string Label,
    long KnowledgeBaseId,
    long DocumentId,
    long ChunkId,
    string? DocumentName);

public sealed record RagAnswerSynthesis(
    string Answer,
    IReadOnlyList<RagCitation> Citations,
    float Confidence);

public sealed record RagVerificationResult(
    bool IsPassed,
    bool RequiresRetry,
    float SafetyScore,
    string Summary,
    IReadOnlyList<string> Issues);

public sealed record RagPipelineTrace(
    string Stage,
    string Detail,
    string Timestamp);

public sealed record RagPipelineOptions(
    bool EnableQueryRewrite = true,
    bool EnableRerank = true,
    bool EnableEvidenceScoring = true,
    bool EnableVerification = true,
    bool EnableAutoRetry = true,
    int TopK = 6,
    int CandidateTopK = 12,
    int MaxRetries = 1,
    float EvidenceThreshold = 0.08f);

public sealed record RagPipelineResult(
    string Answer,
    float Confidence,
    IReadOnlyList<RagCitation> Citations,
    IReadOnlyList<RagSearchResult> Evidence,
    RagVerificationResult Verification,
    RagFailureMode FailureMode,
    IReadOnlyList<RagPipelineTrace> Traces);
