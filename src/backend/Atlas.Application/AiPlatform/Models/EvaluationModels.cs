using Atlas.Domain.AiPlatform.Entities;

namespace Atlas.Application.AiPlatform.Models;

public sealed record EvaluationDatasetCreateRequest(
    string Name,
    string? Description,
    string? Scene);

public sealed record EvaluationDatasetDto(
    long Id,
    string Name,
    string Description,
    string Scene,
    int CaseCount,
    long CreatedByUserId,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EvaluationCaseCreateRequest(
    string Input,
    string? ExpectedOutput,
    string? ReferenceOutput,
    IReadOnlyList<string>? Tags,
    IReadOnlyList<long>? GroundTruthChunkIds = null,
    IReadOnlyList<string>? GroundTruthCitations = null);

public sealed record EvaluationCaseDto(
    long Id,
    long DatasetId,
    string Input,
    string ExpectedOutput,
    string ReferenceOutput,
    IReadOnlyList<string> Tags,
    IReadOnlyList<long> GroundTruthChunkIds,
    IReadOnlyList<string> GroundTruthCitations,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EvaluationTaskCreateRequest(
    string Name,
    long DatasetId,
    long AgentId,
    bool EnableRag = true);

public sealed record EvaluationTaskDto(
    long Id,
    string Name,
    long DatasetId,
    long AgentId,
    bool EnableRag,
    EvaluationTaskStatus Status,
    int TotalCases,
    int CompletedCases,
    decimal Score,
    IReadOnlyDictionary<string, decimal> AggregateMetrics,
    string ErrorMessage,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    DateTime? StartedAt,
    DateTime? CompletedAt);

public sealed record EvaluationResultDto(
    long Id,
    long TaskId,
    long CaseId,
    string ActualOutput,
    decimal Score,
    decimal FaithfulnessScore,
    decimal ContextPrecisionScore,
    decimal ContextRecallScore,
    decimal AnswerRelevanceScore,
    decimal CitationAccuracyScore,
    decimal HallucinationScore,
    string JudgeReason,
    IReadOnlyDictionary<string, decimal> Metrics,
    EvaluationCaseStatus Status,
    DateTime CreatedAt);

public sealed record EvaluationComparisonResult(
    long LeftTaskId,
    decimal LeftScore,
    long RightTaskId,
    decimal RightScore,
    decimal Delta,
    string Winner);
