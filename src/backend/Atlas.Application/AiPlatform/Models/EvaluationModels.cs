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
    IReadOnlyList<string>? Tags);

public sealed record EvaluationCaseDto(
    long Id,
    long DatasetId,
    string Input,
    string ExpectedOutput,
    string ReferenceOutput,
    IReadOnlyList<string> Tags,
    DateTime CreatedAt,
    DateTime UpdatedAt);

public sealed record EvaluationTaskCreateRequest(
    string Name,
    long DatasetId,
    long AgentId);

public sealed record EvaluationTaskDto(
    long Id,
    string Name,
    long DatasetId,
    long AgentId,
    EvaluationTaskStatus Status,
    int TotalCases,
    int CompletedCases,
    decimal Score,
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
    string JudgeReason,
    EvaluationCaseStatus Status,
    DateTime CreatedAt);

public sealed record EvaluationComparisonResult(
    long LeftTaskId,
    decimal LeftScore,
    long RightTaskId,
    decimal RightScore,
    decimal Delta,
    string Winner);
